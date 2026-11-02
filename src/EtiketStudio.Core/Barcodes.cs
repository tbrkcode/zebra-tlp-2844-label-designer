// SPDX-License-Identifier: GPL-3.0-only
using System.Text;
using ZXing;
using ZXing.Common;

namespace EtiketStudio.Core;

public static class Barcodes
{
    public static BarcodeFormat Format(BarcodeKind kind) => kind switch
    {
        BarcodeKind.Ean13 => BarcodeFormat.EAN_13, BarcodeKind.Ean8 => BarcodeFormat.EAN_8,
        BarcodeKind.UpcA => BarcodeFormat.UPC_A, BarcodeKind.Code39 => BarcodeFormat.CODE_39,
        BarcodeKind.Code128 => BarcodeFormat.CODE_128, BarcodeKind.QrCode => BarcodeFormat.QR_CODE,
        _ => throw new DomainException("InvalidBarcode")
    };
    public static int CheckDigit(string body)
    {
        if (body.Length == 0 || body.Any(c => c is < '0' or > '9')) throw new DomainException("DigitsOnly");
        int sum = 0;
        for (int i = body.Length - 1, weight = 3; i >= 0; i--, weight = 4 - weight) sum += (body[i] - '0') * weight;
        return (10 - sum % 10) % 10;
    }
    // Missing check digits are completed; incorrect supplied digits are rejected, never silently changed.
    public static string Normalize(BarcodeKind kind, string? value)
    {
        if (string.IsNullOrEmpty(value)) throw new DomainException("EmptyBarcode");
        int length = kind switch { BarcodeKind.Ean13 => 13, BarcodeKind.Ean8 => 8, BarcodeKind.UpcA => 12, _ => 0 };
        if (length != 0)
        {
            if (value.Any(c => c is < '0' or > '9')) throw new DomainException("DigitsOnly");
            if (value.Length == length - 1) return value + CheckDigit(value);
            if (value.Length != length) throw new DomainException("BarcodeLength", length - 1, length);
            if (value[^1] - '0' != CheckDigit(value[..^1])) throw new DomainException("Checksum");
        }
        if (kind == BarcodeKind.Code39 && (value.Length > 80 || value.Any(c => !"0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ-. $/+%".Contains(c))))
            throw new DomainException("Code39Characters");
        if (kind == BarcodeKind.Code128 && (value.Length > 80 || value.Any(c => c < 32 || c > 126)))
            throw new DomainException("Code128Characters");
        if (kind == BarcodeKind.QrCode && Encoding.UTF8.GetByteCount(value) > 1200) throw new DomainException("QrTooLong");
        return value;
    }
    public static BitMatrix Matrix(BarcodeKind kind, string value)
    {
        value = Normalize(kind, value);
        var hints = new Dictionary<EncodeHintType, object>
        {
            [EncodeHintType.MARGIN] = kind == BarcodeKind.QrCode ? 4 : 24,
            [EncodeHintType.CHARACTER_SET] = "UTF-8"
        };
        try { return new MultiFormatWriter().encode(value, Format(kind), 1, 1, hints); }
        catch (ArgumentException) { throw new DomainException("InvalidBarcode"); }
    }
    public static int ModuleSize(BarcodeKind kind, BitMatrix matrix, int width, int height)
    {
        int scale = kind == BarcodeKind.QrCode ? Math.Min(width / matrix.Width, height / matrix.Height) : width / matrix.Width;
        int minimum = kind is BarcodeKind.Ean13 or BarcodeKind.Ean8 or BarcodeKind.UpcA ? 3 : 2;
        if (scale < minimum) throw new DomainException("BarcodeTooSmall", Math.Round(matrix.Width * minimum / 8d, 2));
        return scale;
    }
    public static bool DecodesTo(BarcodeKind kind, string expected, byte[] gray8, int width, int height)
    {
        if (gray8.Length != checked(width * height) || width <= 0 || height <= 0) return false;
        try
        {
            var source = new RGBLuminanceSource(gray8, width, height, RGBLuminanceSource.BitmapFormat.Gray8);
            var result = new MultiFormatReader().decode(new BinaryBitmap(new HybridBinarizer(source)),
                new Dictionary<DecodeHintType, object> { [DecodeHintType.POSSIBLE_FORMATS] = new[] { Format(kind) }, [DecodeHintType.TRY_HARDER] = true });
            return result?.Text == Normalize(kind, expected);
        }
        catch (ReaderException) { return false; }
    }
}
