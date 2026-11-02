// SPDX-License-Identifier: GPL-3.0-only
namespace EtiketStudio.Core;

public static class Validation
{
    public const int DotsPerMm = 8;
    public static int Dots(double mm) => checked((int)Math.Round(mm * DotsPerMm, MidpointRounding.AwayFromZero));
    public static void Range(double value, double minimum, double maximum, string field)
    {
        if (!double.IsFinite(value) || value < minimum || value > maximum)
            throw new DomainException("Range", T.Get(field), minimum, maximum);
    }
    public static void Product(Product p)
    {
        if (string.IsNullOrWhiteSpace(p.Name) || string.IsNullOrWhiteSpace(p.Sku)) throw new DomainException("ProductRequired");
        if (p.Name.Length > 500 || p.Sku.Length > 100 || p.Barcode.Length > 2000 || p.Description.Length > 5000 || p.Category.Length > 200) throw new DomainException("TextTooLong");
        if (p.Price < 0 || p.Price > 999999999m || decimal.Round(p.Price, 2) != p.Price) throw new DomainException("InvalidPrice");
        if (p.Image?.Length > 5_000_000) throw new DomainException("FileTooLarge");
    }
    public static void Copies(int copies) { if (copies is < 1 or > 1000) throw new DomainException("CopiesRange"); }
    public static void Settings(AppSettings s)
    {
        Range(s.Darkness, 0, 15, "Darkness"); Range(s.Speed, 1, 4, "Speed");
        Range(s.DisplayScale, 0.5, 2, "DisplayScale");
    }
    public static void Template(LabelTemplate t)
    {
        if (t.SchemaVersion != 1 || t.Elements is null) throw new DomainException("TemplateVersion");
        Range(t.Width, 25.4, 104, "Width"); Range(t.Height, 9.7, 279, "Height"); Range(t.Gap, 0, 10, "Gap");
        if (t.Gap > 0 && Dots(t.Gap) < 16) throw new DomainException("GapMinimum");
        if (t.Elements.Count > 200) throw new DomainException("TooManyObjects");
        foreach (var e in t.Elements)
        {
            if (!Enum.IsDefined(e.Kind) || !Enum.IsDefined(e.BarcodeKind)) throw new DomainException("InvalidFile");
            Range(e.X, 0, t.Width, "X"); Range(e.Y, 0, t.Height, "Y");
            Range(e.Width, 0.125, t.Width, "Width"); Range(e.Height, 0.125, t.Height, "Height");
            if (e.X + e.Width > t.Width + 0.001 || e.Y + e.Height > t.Height + 0.001) throw new DomainException("OutsideLabel");
            Range(e.FontSize, 4, 144, "FontSize"); Range(e.Stroke, 0.125, 5, "Stroke");
            if (e.Image?.Length > 5_000_000 || e.Text?.Length > 5000) throw new DomainException("FileTooLarge");
        }
    }
}
