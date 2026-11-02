// SPDX-License-Identifier: GPL-3.0-only
using System.Text;
using EtiketStudio.Core;

if (args.Length != 1) { Console.Error.WriteLine("Usage: EtiketStudio.Fixtures <output-directory> (creates files; never prints)"); return 2; }
string root = Path.GetFullPath(args[0]);
string templates = Path.Combine(root, "templates"), epl = Path.Combine(root, "epl2");
Directory.CreateDirectory(templates); Directory.CreateDirectory(epl);
File.WriteAllBytes(Path.Combine(epl, "01-native-code128-50x30.epl"), Epl2.NativeDiagnostic(new()));
File.WriteAllBytes(Path.Combine(epl, "calibrate-gap.epl"), Epl2.Calibration);
File.WriteAllBytes(Path.Combine(epl, "print-configuration.epl"), Epl2.Configuration);
JsonData.SaveAtomic(Path.Combine(templates, "retail-50x30.etiket.json"), JsonData.Serialize(LabelTemplate.Default()));
var retail = LabelTemplate.Default(); retail.Name = "EAN-13 · 58 × 35 mm"; retail.Width = 58; retail.Height = 35;
retail.Elements[1].BarcodeKind = BarcodeKind.Ean13; retail.Elements[1].Width = 52; retail.Elements[1].Height = 16;
retail.Elements[2].Y = 26; retail.Elements[3].Y = 28;
JsonData.SaveAtomic(Path.Combine(templates, "ean13-58x35.etiket.json"), JsonData.Serialize(retail));
var qr = new LabelTemplate { Name = "QR · 60 × 40 mm", Width = 60, Height = 40, Elements = [
    new() { Kind = ElementKind.ProductName, X = 3, Y = 2, Width = 54, Height = 5, Bold = true },
    new() { Kind = ElementKind.QrCode, BarcodeKind = BarcodeKind.QrCode, X = 3, Y = 10, Width = 26, Height = 26, Text = "{Name} | {Sku} | {Barcode}" },
    new() { Kind = ElementKind.Price, X = 32, Y = 12, Width = 25, Height = 6, FontSize = 12, Bold = true },
    new() { Kind = ElementKind.ProductCode, X = 32, Y = 22, Width = 25, Height = 5, FontSize = 9 }
] };
JsonData.SaveAtomic(Path.Combine(templates, "qr-60x40.etiket.json"), JsonData.Serialize(qr));
var products = new List<Product> {
    new() { Name = "Çay – Özel İçim", Sku = "CAY-001", Barcode = "8691234567890", Price = 49.90m, Category = "İçecek", Description = "Türkçe karakter örneği: ç ğ ı İ ö ş ü" },
    new() { Name = "Şişe Süt", Sku = "SUT-002", Barcode = "4006381333931", Price = 35.50m, Category = "Gıda" },
    new() { Name = "Öğütülmüş Kahve", Sku = "KHV-003", Barcode = "5901234123457", Price = 129.00m, Category = "İçecek" }
};
foreach (var product in products) product.Barcode = Barcodes.Normalize(BarcodeKind.Ean13, product.Barcode[..12]);
ProductExchange.Write(Path.Combine(root, "products.csv"), products); ProductExchange.Write(Path.Combine(root, "products.xlsx"), products);
var testValues = new Dictionary<BarcodeKind, string> { [BarcodeKind.Ean13] = "4006381333931", [BarcodeKind.Ean8] = "96385074", [BarcodeKind.UpcA] = "036000291452", [BarcodeKind.Code128] = "ABC-123456", [BarcodeKind.Code39] = "ABC-123", [BarcodeKind.QrCode] = "çğıİöşü / Zebra TLP 2844 Label Designer" };
foreach (var pair in testValues)
{
    var t = new LabelTemplate { Width = 64, Height = 40, Gap = 3 }; var m = Barcodes.Matrix(pair.Key, pair.Value);
    int width = 512, height = 320, scale = pair.Key == BarcodeKind.QrCode ? 6 : 3;
    if (m.Width * scale > 464) scale = 2;
    bool Black(int x, int y)
    {
        int a = x - 24, b = y - 32;
        if (a < 0 || b < 0 || a >= m.Width * scale) return false;
        return pair.Key == BarcodeKind.QrCode ? b < m.Height * scale && m[a / scale, b / scale] : b < 128 && m[a / scale, 0];
    }
    var packed = Epl2.Pack(width, height, Black); var file = Path.Combine(epl, "raster-" + pair.Key.ToString().ToLowerInvariant() + "-64x40");
    File.WriteAllBytes(file + ".epl", Epl2.Raster(t, new(), packed, width, height, 1));
    // PBM has opposite polarity to EPL GW. These portable previews let tests inspect the same bytes.
    using var stream = File.Create(file + ".pbm"); stream.Write(Encoding.ASCII.GetBytes($"P4\n{width} {height}\n")); stream.Write(packed.Select(b => (byte)~b).ToArray());
}
Console.WriteLine("Generated templates, CSV/XLSX samples, native EPL2 and six binary barcode fixtures. No printer accessed.");
return 0;
