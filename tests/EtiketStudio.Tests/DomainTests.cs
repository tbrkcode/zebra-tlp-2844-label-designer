// SPDX-License-Identifier: GPL-3.0-only
using System.Text;
using EtiketStudio.Core;
using Xunit;
using ZXing;
using ZXing.Common;

namespace EtiketStudio.Tests;

public sealed class BarcodeTests
{
    [Theory]
    [InlineData(BarcodeKind.Ean13, "400638133393", "4006381333931")]
    [InlineData(BarcodeKind.Ean8, "9638507", "96385074")]
    [InlineData(BarcodeKind.UpcA, "03600029145", "036000291452")]
    public void CompletesAndValidatesRetailCheckDigits(BarcodeKind kind, string body, string full)
    {
        Assert.Equal(full, Barcodes.Normalize(kind, body)); Assert.Equal(full, Barcodes.Normalize(kind, full));
        Assert.Throws<DomainException>(() => Barcodes.Normalize(kind, full[..^1] + (char)('0' + (full[^1] - '0' + 1) % 10)));
    }
    [Theory]
    [InlineData(BarcodeKind.Ean13, "123")]
    [InlineData(BarcodeKind.Ean8, "abcdefgh")]
    [InlineData(BarcodeKind.UpcA, "１２３４５６７８９０１２")]
    [InlineData(BarcodeKind.Code39, "a")]
    [InlineData(BarcodeKind.Code39, "ABC*DEF")]
    [InlineData(BarcodeKind.Code128, "Türkçe")]
    [InlineData(BarcodeKind.Code128, "A\nP50")]
    [InlineData(BarcodeKind.Code128, "")]
    public void RejectsInvalidBarcodeData(BarcodeKind kind, string data) => Assert.Throws<DomainException>(() => Barcodes.Normalize(kind, data));

    [Theory]
    [InlineData(BarcodeKind.Ean13, "4006381333931")]
    [InlineData(BarcodeKind.Ean8, "96385074")]
    [InlineData(BarcodeKind.UpcA, "036000291452")]
    [InlineData(BarcodeKind.Code128, "ABC-123456")]
    [InlineData(BarcodeKind.Code39, "ABC-123")]
    [InlineData(BarcodeKind.QrCode, "ç ğ ı İ ö ş ü / 你好")]
    public void EncodedPixelsDecodeToOriginalData(BarcodeKind kind, string data)
    {
        var matrix = Barcodes.Matrix(kind, data); int scale = 4;
        int width = matrix.Width * scale, height = kind == BarcodeKind.QrCode ? matrix.Height * scale : 100;
        var luminance = new byte[width * height];
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) luminance[y * width + x] = matrix[x / scale, kind == BarcodeKind.QrCode ? y / scale : 0] ? (byte)0 : (byte)255;
        var source = new RGBLuminanceSource(luminance, width, height, RGBLuminanceSource.BitmapFormat.Gray8);
        var result = new MultiFormatReader().decode(new BinaryBitmap(new HybridBinarizer(source)), new Dictionary<DecodeHintType, object> { [DecodeHintType.POSSIBLE_FORMATS] = new[] { Barcodes.Format(kind) }, [DecodeHintType.TRY_HARDER] = true });
        Assert.NotNull(result); Assert.Equal(data, result.Text);
        Assert.True(Barcodes.DecodesTo(kind, data, luminance, width, height));
        Assert.False(Barcodes.DecodesTo(kind, data, Enumerable.Repeat((byte)255, width * height).ToArray(), width, height));
    }
    [Fact] public void DoesNotStretchBarcodeModules()
    {
        var matrix = Barcodes.Matrix(BarcodeKind.Ean13, "4006381333931");
        Assert.Equal(3, Barcodes.ModuleSize(BarcodeKind.Ean13, matrix, matrix.Width * 3 + 2, 100));
        Assert.Throws<DomainException>(() => Barcodes.ModuleSize(BarcodeKind.Ean13, matrix, matrix.Width * 2, 100));
    }
    [Fact] public void QrHasFourModuleQuietZone()
    {
        var m = Barcodes.Matrix(BarcodeKind.QrCode, "TEST");
        for (int x = 0; x < m.Width; x++) for (int y = 0; y < 4; y++) { Assert.False(m[x, y]); Assert.False(m[x, m.Height - 1 - y]); }
    }
    [Fact] public void TurkishTokenValuesSurviveResolution()
    {
        var p = new Product { Name = "Çığ şişe İı", Sku = "ÖZEL", Price = 12.50m };
        var e = new LabelElement { Text = "{Name} | {Sku} | {Price}" };
        Assert.Equal("Çığ şişe İı | ÖZEL | 12,50", Fields.Resolve(e, p, DateTimeOffset.Now));
    }
}

public sealed class EplTests
{
    [Fact] public void PacksMsbFirstWithWhitePaddingAndCorrectPolarity()
    {
        var packed = Epl2.Pack(9, 2, (x, y) => y == 0 && x is 0 or 7 or 8);
        Assert.Equal(new byte[] { 0x7e, 0x7f, 0xff, 0xff }, packed);
    }
    [Fact] public void GeneratesBinaryGwWithExactLengthAndNoZpl()
    {
        var label = new LabelTemplate(); var packed = Epl2.Pack(400, 240, (_, _) => false);
        packed[0] = 10; packed[1] = 0; packed[2] = 255; // Includes LF, NUL and 0xff inside the binary payload.
        var output = Epl2.Raster(label, new(), packed, 400, 240, 3);
        var header = Encoding.ASCII.GetBytes("\nR0,0\nq400\nQ240,24\nS2\nD8\nZT\nOD\nN\nGW0,0,50,240,");
        Assert.Equal(header, output[..header.Length]);
        Assert.Equal(packed, output[header.Length..(header.Length + packed.Length)]);
        Assert.Equal("\nP3\n", Encoding.ASCII.GetString(output[(header.Length + packed.Length)..]));
        Assert.DoesNotContain("^XA", Encoding.ASCII.GetString(output));
    }
    [Fact] public void NativeDiagnosticUsesEplCode128()
    {
        var text = Encoding.ASCII.GetString(Epl2.NativeDiagnostic(new()));
        Assert.Contains("B24,64,0,1,2,2,88,B,\"123456789012\"\n", text);
        Assert.EndsWith("P1\n", text); Assert.DoesNotContain("^", text);
    }
    [Fact] public void UsesDocumentedCalibrationAndConfigurationCommands()
    {
        Assert.Equal("\nxa\n", Encoding.ASCII.GetString(Epl2.Calibration)); Assert.Equal("\nU\n", Encoding.ASCII.GetString(Epl2.Configuration));
    }
    [Theory] [InlineData(0)] [InlineData(1001)] [InlineData(-1)]
    public void RejectsInvalidCopies(int copies) => Assert.Throws<DomainException>(() => Epl2.Raster(new(), new(), new byte[12000], 400, 240, copies));
    [Fact] public void RejectsMismatchedRaster() => Assert.Throws<DomainException>(() => Epl2.Raster(new(), new(), new byte[11999], 400, 240, 1));
    [Theory] [InlineData(-1)] [InlineData(16)]
    public void RejectsInvalidDarkness(int value) => Assert.Throws<DomainException>(() => Epl2.NativeDiagnostic(new() { Darkness = value }));
    [Theory] [InlineData(0)] [InlineData(5)]
    public void RejectsInvalidSpeed(int value) => Assert.Throws<DomainException>(() => Epl2.NativeDiagnostic(new() { Speed = value }));
    [Fact] public void ThermalTransferOmitsDirectThermalFlag() => Assert.Contains("\nO\n", Encoding.ASCII.GetString(Epl2.NativeDiagnostic(new() { DirectThermal = false })));
}

public sealed class GeometryTests
{
    [Theory] [InlineData(50, 400)] [InlineData(104, 832)] [InlineData(30, 240)] [InlineData(0.125, 1)]
    public void UsesExactEightDotsPerMillimetre(double mm, int dots) => Assert.Equal(dots, Validation.Dots(mm));
    [Theory] [InlineData(104.1)] [InlineData(0)] [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)]
    public void RejectsInvalidWidth(double width) => Assert.Throws<DomainException>(() => Validation.Template(new() { Width = width }));
    [Fact] public void RejectsOutOfBoundsElements()
    {
        var label = LabelTemplate.Default(); label.Elements[0].X = 45; Assert.Throws<DomainException>(() => Validation.Template(label));
    }
    [Fact] public void RejectsUnsupportedSchema() => Assert.Throws<DomainException>(() => Validation.Template(new() { SchemaVersion = 2 }));
    [Fact] public void GapMinimumMatchesEpl203Dpi()
    {
        Assert.Throws<DomainException>(() => Validation.Template(new() { Gap = 1 })); Validation.Template(new() { Gap = 0 }); Validation.Template(new() { Gap = 2 });
    }
    [Fact] public void TemplateCloneDoesNotShareMutableObjects()
    {
        var t = LabelTemplate.Default(); var clone = t.Clone(); clone.Elements[0].X = 10; Assert.NotEqual(t.Elements[0].X, clone.Elements[0].X);
    }
}
