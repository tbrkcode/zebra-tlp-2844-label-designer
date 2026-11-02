// SPDX-License-Identifier: GPL-3.0-only
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EtiketStudio.Core;

public enum ElementKind { Text, Barcode, QrCode, Line, Rectangle, Image, Price, ProductName, ProductCode, DateTime }
public enum BarcodeKind { Ean13, Ean8, Code128, Code39, UpcA, QrCode }

public sealed class Product : ObservableObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    private string name = "", barcode = "", sku = "", description = "", category = "";
    private decimal price;
    private bool selected;
    private int copies = 1;
    private byte[]? image;
    public string Name { get => name; set => Set(ref name, value); }
    public string Barcode { get => barcode; set => Set(ref barcode, value); }
    public string Sku { get => sku; set => Set(ref sku, value); }
    public decimal Price { get => price; set => Set(ref price, value); }
    public string Description { get => description; set => Set(ref description, value); }
    public string Category { get => category; set => Set(ref category, value); }
    public byte[]? Image { get => image; set => Set(ref image, value); }
    [JsonIgnore] public bool Selected { get => selected; set => Set(ref selected, value); }
    [JsonIgnore] public int Copies { get => copies; set => Set(ref copies, value); }
    public Product Clone() => JsonData.Clone(this);
}

public sealed class LabelElement : ObservableObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    private ElementKind kind;
    private BarcodeKind barcodeKind = BarcodeKind.Code128;
    private double x = 2, y = 2, width = 30, height = 5, fontSize = 10, stroke = 0.25;
    private string text = "Etiket", fontFamily = "Segoe UI";
    private bool bold, humanReadable = true;
    private byte[]? image;
    public ElementKind Kind { get => kind; set => Set(ref kind, value); }
    public BarcodeKind BarcodeKind { get => barcodeKind; set => Set(ref barcodeKind, value); }
    public double X { get => x; set => Set(ref x, value); }
    public double Y { get => y; set => Set(ref y, value); }
    public double Width { get => width; set => Set(ref width, value); }
    public double Height { get => height; set => Set(ref height, value); }
    public double FontSize { get => fontSize; set => Set(ref fontSize, value); }
    public double Stroke { get => stroke; set => Set(ref stroke, value); }
    public string Text { get => text; set => Set(ref text, value); }
    public string FontFamily { get => fontFamily; set => Set(ref fontFamily, value); }
    public bool Bold { get => bold; set => Set(ref bold, value); }
    public bool HumanReadable { get => humanReadable; set => Set(ref humanReadable, value); }
    public byte[]? Image { get => image; set => Set(ref image, value); }
    public LabelElement Clone() => JsonData.Clone(this);
}

public sealed class LabelTemplate : ObservableObject
{
    public int SchemaVersion { get; set; } = 1;
    private string name = "50 × 30 mm";
    private double width = 50, height = 30, gap = 3;
    public string Name { get => name; set => Set(ref name, value); }
    public double Width { get => width; set => Set(ref width, value); }
    public double Height { get => height; set => Set(ref height, value); }
    public double Gap { get => gap; set => Set(ref gap, value); }
    public ObservableCollection<LabelElement> Elements { get; set; } = [];
    public LabelTemplate Clone() => JsonData.Clone(this);
    public static LabelTemplate Default() => new()
    {
        Elements = [
            new() { Kind = ElementKind.ProductName, X = 3, Y = 2, Width = 44, Height = 5, Bold = true, FontSize = 10 },
            new() { Kind = ElementKind.Barcode, Text = "{Barcode}", X = 3, Y = 8, Width = 44, Height = 13 },
            new() { Kind = ElementKind.Price, X = 3, Y = 22, Width = 25, Height = 6, Bold = true, FontSize = 12 },
            new() { Kind = ElementKind.ProductCode, X = 29, Y = 24, Width = 18, Height = 4, FontSize = 8 }
        ]
    };
}

public sealed class AppSettings : ObservableObject
{
    private string printer = "", language = "en-US";
    private bool darkTheme, reverse, driverMode, directThermal = true;
    private int darkness = 8, speed = 2;
    private double displayScale = 1;
    public string Printer { get => printer; set => Set(ref printer, value); }
    public string Language { get => language; set => Set(ref language, value); }
    public bool DarkTheme { get => darkTheme; set => Set(ref darkTheme, value); }
    public bool Reverse { get => reverse; set => Set(ref reverse, value); }
    public bool DirectThermal { get => directThermal; set => Set(ref directThermal, value); }
    public bool DriverMode { get => driverMode; set => Set(ref driverMode, value); }
    public int Darkness { get => darkness; set => Set(ref darkness, value); }
    public int Speed { get => speed; set => Set(ref speed, value); }
    public double DisplayScale { get => displayScale; set => Set(ref displayScale, value); }
}

public sealed record PrintRecord(string Id, DateTimeOffset Time, string Printer, int? SpoolId, int Labels, string State, string Detail);

public static class JsonData
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true, PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };
    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);
    public static T Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, Options) ?? throw new DomainException("InvalidFile");
    public static T Clone<T>(T value) => Deserialize<T>(Serialize(value));
    public static LabelTemplate LoadTemplate(string path)
    {
        if (new FileInfo(path).Length > 20_000_000) throw new DomainException("FileTooLarge");
        var template = Deserialize<LabelTemplate>(File.ReadAllText(path));
        Validation.Template(template);
        return template;
    }
    // Write beside the destination and rename atomically; never truncate a user's existing template.
    public static void SaveAtomic(string path, string text)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temporary, text, new System.Text.UTF8Encoding(false)); File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}

public static class Fields
{
    public static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");
    public static string Resolve(LabelElement element, Product product, DateTimeOffset time)
    {
        var text = element.Kind switch
        {
            ElementKind.ProductName => product.Name,
            ElementKind.ProductCode => product.Sku,
            ElementKind.Price => product.Price.ToString("N2", Turkish) + " ₺",
            ElementKind.DateTime => time.ToLocalTime().ToString("dd.MM.yyyy HH:mm", Turkish),
            _ => element.Text ?? ""
        };
        return text.Replace("{Name}", product.Name).Replace("{Barcode}", product.Barcode)
            .Replace("{Sku}", product.Sku).Replace("{Price}", product.Price.ToString("N2", Turkish))
            .Replace("{Category}", product.Category).Replace("{Description}", product.Description)
            .Replace("{Date}", time.ToLocalTime().ToString("dd.MM.yyyy", Turkish))
            .Replace("{Time}", time.ToLocalTime().ToString("HH:mm", Turkish));
    }
}
