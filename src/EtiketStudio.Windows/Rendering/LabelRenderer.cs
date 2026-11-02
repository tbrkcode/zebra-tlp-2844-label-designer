// SPDX-License-Identifier: GPL-3.0-only
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using EtiketStudio.Core;

namespace EtiketStudio.Windows.Rendering;

public sealed record LabelImage(BitmapSource Bitmap, byte[] Packed, int Width, int Height);

public sealed class LabelRenderer
{
    public const double Dpi = 203.2; // Exact 8 dots/mm, not 203/25.4 rounded down.
    public LabelImage Render(LabelTemplate template, Product product, DateTimeOffset timestamp, bool reverse)
    {
        Validation.Template(template);
        if (template.Elements.Count == 0) throw new DomainException("EmptyLabel");
        int width = Validation.Dots(template.Width), height = Validation.Dots(template.Height);
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));
            foreach (var element in template.Elements)
            {
                var image = RenderElement(element, product, timestamp);
                context.DrawImage(image, new Rect(Validation.Dots(element.X), Validation.Dots(element.Y), image.PixelWidth, image.PixelHeight));
            }
        }
        var target = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        target.Render(visual);
        var pixels = new byte[width * height * 4]; target.CopyPixels(pixels, width * 4, 0);
        var output = new byte[pixels.Length];
        // Threshold once, then use exactly these pixels for preview, export and both print transports.
        for (int i = 0; i < width * height; i++)
        {
            int source = (reverse ? width * height - 1 - i : i) * 4;
            byte value = 299 * pixels[source + 2] + 587 * pixels[source + 1] + 114 * pixels[source] < 160000 ? (byte)0 : (byte)255;
            output[i * 4] = output[i * 4 + 1] = output[i * 4 + 2] = value; output[i * 4 + 3] = 255;
        }
        // Decode the final composite, not just the source matrix, to catch occlusion or damaged quiet zones.
        foreach (var element in template.Elements.Where(e => e.Kind is ElementKind.Barcode or ElementKind.QrCode))
        {
            int w = Validation.Dots(element.Width), h = Validation.Dots(element.Height);
            int left = Validation.Dots(element.X), top = Validation.Dots(element.Y);
            if (reverse) { left = width - left - w; top = height - top - h; }
            if (left < 0 || top < 0 || left + w > width || top + h > height) throw new DomainException("OutsideLabel");
            var crop = new byte[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) crop[y * w + x] = output[((top + y) * width + left + x) * 4];
            var kind = element.Kind == ElementKind.QrCode ? BarcodeKind.QrCode : element.BarcodeKind;
            if (!Barcodes.DecodesTo(kind, Fields.Resolve(element, product, timestamp), crop, w, h)) throw new DomainException("BarcodeUnreadable");
        }
        var bitmap = BitmapSource.Create(width, height, Dpi, Dpi, PixelFormats.Bgra32, null, output, width * 4);
        bitmap.Freeze();
        return new(bitmap, Epl2.Pack(width, height, (x, y) => output[(y * width + x) * 4] == 0), width, height);
    }
    public BitmapSource RenderElement(LabelElement element, Product product, DateTimeOffset time)
    {
        int width = Math.Max(1, Validation.Dots(element.Width)), height = Math.Max(1, Validation.Dots(element.Height));
        if (width > 832 || height > 2232 || !double.IsFinite(element.FontSize) || element.FontSize is < 4 or > 144) throw new DomainException("OutsideLabel");
        var visual = new DrawingVisual(); RenderOptions.SetEdgeMode(visual, EdgeMode.Aliased);
        TextOptions.SetTextRenderingMode(visual, TextRenderingMode.Grayscale);
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));
            var value = Fields.Resolve(element, product, time);
            if (element.Kind is ElementKind.Barcode or ElementKind.QrCode)
            {
                var kind = element.Kind == ElementKind.QrCode ? BarcodeKind.QrCode : element.BarcodeKind;
                string normalized = Barcodes.Normalize(kind, value); var matrix = Barcodes.Matrix(kind, normalized);
                bool human = element.HumanReadable && kind != BarcodeKind.QrCode;
                var humanText = human ? Text(normalized, 8, false, "Segoe UI") : null;
                int textHeight = humanText is null ? 0 : (int)Math.Ceiling(humanText.Height) + 2, barsHeight = height - textHeight;
                if (barsHeight < 16) throw new DomainException("BarcodeTooSmall", 8);
                int scale = Barcodes.ModuleSize(kind, matrix, width, barsHeight);
                int left = (width - matrix.Width * scale) / 2;
                if (kind == BarcodeKind.QrCode)
                {
                    int top = (height - matrix.Height * scale) / 2;
                    for (int y = 0; y < matrix.Height; y++) for (int x = 0; x < matrix.Width; x++)
                        if (matrix[x, y]) dc.DrawRectangle(Brushes.Black, null, new Rect(left + x * scale, top + y * scale, scale, scale));
                }
                else
                {
                    for (int x = 0; x < matrix.Width; x++) if (matrix[x, 0]) dc.DrawRectangle(Brushes.Black, null, new Rect(left + x * scale, 0, scale, barsHeight));
                    if (human)
                    {
                        var text = humanText!;
                        if (text.WidthIncludingTrailingWhitespace > width) throw new DomainException("TextOverflow", normalized);
                        dc.DrawText(text, new Point((width - text.WidthIncludingTrailingWhitespace) / 2, barsHeight + 1));
                    }
                }
            }
            else if (element.Kind == ElementKind.Image)
            {
                var image = LoadImage(element.Image ?? product.Image ?? throw new DomainException("NoImage"));
                double scale = Math.Min(width / (double)image.PixelWidth, height / (double)image.PixelHeight);
                double w = image.PixelWidth * scale, h = image.PixelHeight * scale;
                dc.DrawImage(image, new Rect((width - w) / 2, (height - h) / 2, w, h));
            }
            else if (element.Kind is ElementKind.Line or ElementKind.Rectangle)
            {
                double stroke = Math.Max(1, Validation.Dots(element.Stroke)); var pen = new Pen(Brushes.Black, stroke);
                if (element.Kind == ElementKind.Line) dc.DrawLine(pen, new Point(stroke / 2, height / 2d), new Point(width - stroke / 2, height / 2d));
                else dc.DrawRectangle(null, pen, new Rect(stroke / 2, stroke / 2, Math.Max(0, width - stroke), Math.Max(0, height - stroke)));
            }
            else
            {
                var text = Text(value, element.FontSize, element.Bold, element.FontFamily);
                text.MaxTextWidth = width;
                if (text.Height > height + 0.5 || text.WidthIncludingTrailingWhitespace > width + 0.5) throw new DomainException("TextOverflow", value.Length > 30 ? value[..30] + "…" : value);
                dc.DrawText(text, new Point(0, 0));
            }
        }
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual); bitmap.Freeze(); return bitmap;
    }
    private static FormattedText Text(string value, double points, bool bold, string family) => new(value, Fields.Turkish, FlowDirection.LeftToRight,
        new Typeface(new FontFamily(string.IsNullOrWhiteSpace(family) ? "Segoe UI" : family), FontStyles.Normal, bold ? FontWeights.Bold : FontWeights.Normal, FontStretches.Normal), points * Dpi / 72d, Brushes.Black, 1);
    public static BitmapSource LoadImage(byte[] bytes)
    {
        if (bytes.Length > 5_000_000) throw new DomainException("FileTooLarge");
        try
        {
            using var stream = new MemoryStream(bytes);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.OnLoad);
            if (decoder is not PngBitmapDecoder and not JpegBitmapDecoder and not BmpBitmapDecoder) throw new DomainException("ImageFormat");
            var frame = decoder.Frames[0];
            if (frame.PixelWidth > 4096 || frame.PixelHeight > 4096) throw new DomainException("ImageFormat");
            frame.Freeze(); return frame;
        }
        catch (Exception ex) when (ex is not DomainException) { throw new DomainException("ImageFormat"); }
    }
    public static void SavePng(BitmapSource bitmap, string path)
    {
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path); encoder.Save(stream);
    }
}
