// SPDX-License-Identifier: GPL-3.0-only
using System.Globalization;
using System.Text;
using ClosedXML.Excel;

namespace EtiketStudio.Core;

public static class ProductExchange
{
    public static readonly string[] Headers = ["Sku", "Name", "Barcode", "Price", "Description", "Category", "ImageBase64"];
    private const int MaximumRows = 10_000;
    public static List<Product> Read(string path)
    {
        if (new FileInfo(path).Length > 30_000_000) throw new DomainException("FileTooLarge");
        var rows = System.IO.Path.GetExtension(path).Equals(".xlsx", StringComparison.OrdinalIgnoreCase) ? ReadExcel(path) : ReadCsv(File.ReadAllText(path, Encoding.UTF8));
        if (rows.Count < 1) throw new DomainException("ImportHeaders");
        string Key(string s) => s.Trim().TrimStart('\uFEFF').ToLowerInvariant() switch
        {
            "stok kodu" or "ürün kodu" => "sku", "ürün adı" => "name", "barkod" => "barcode",
            "fiyat" => "price", "açıklama" => "description", "kategori" => "category", _ => s.Trim().TrimStart('\uFEFF').ToLowerInvariant()
        };
        var headers = rows[0].Select(Key).ToArray();
        if (headers.Distinct().Count() != headers.Length || !new[] { "sku", "name", "barcode", "price" }.All(headers.Contains)) throw new DomainException("ImportHeaders");
        var result = new List<Product>();
        for (int i = 1; i < rows.Count; i++)
        {
            if (rows[i].All(string.IsNullOrWhiteSpace)) continue;
            if (result.Count >= MaximumRows) throw new DomainException("TooManyRows");
            string Get(string key) { var index = Array.IndexOf(headers, key); return index >= 0 && index < rows[i].Length ? rows[i][index] : ""; }
            try
            {
                if (rows[i].Length != headers.Length) throw new DomainException("ImportColumns");
                var price = Get("price").Trim();
                // Unambiguous decimal input: 12.50 or 12,50; grouping separators are intentionally disallowed.
                if (price.Contains(',') && price.Contains('.')) throw new DomainException("InvalidPrice");
                if (!decimal.TryParse(price.Replace(',', '.'), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out decimal amount)) throw new DomainException("InvalidPrice");
                var image = Get("imagebase64");
                var p = new Product { Sku = Get("sku"), Name = Get("name"), Barcode = Get("barcode"), Price = amount, Description = Get("description"), Category = Get("category"), Image = image.Length == 0 ? null : Convert.FromBase64String(image) };
                Validation.Product(p); result.Add(p);
            }
            catch (Exception ex) when (ex is DomainException or FormatException) { throw new DomainException("ImportRow", i + 1, ex is DomainException ? ex.Message : T.Get("InvalidFile")); }
        }
        return result;
    }
    public static void Write(string path, IReadOnlyList<Product> products)
    {
        foreach (var p in products) Validation.Product(p);
        if (products.Count > MaximumRows) throw new DomainException("TooManyRows");
        string[] Row(Product p) => [p.Sku, p.Name, p.Barcode, p.Price.ToString("0.00", CultureInfo.InvariantCulture), p.Description, p.Category, p.Image is null ? "" : Convert.ToBase64String(p.Image)];
        if (System.IO.Path.GetExtension(path).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            using var workbook = new XLWorkbook(); var sheet = workbook.AddWorksheet("Products");
            for (int col = 0; col < Headers.Length; col++) sheet.Cell(1, col + 1).Value = Headers[col];
            for (int row = 0; row < products.Count; row++)
            {
                var values = Row(products[row]);
                for (int col = 0; col < values.Length; col++)
                {
                    // Excel cells are explicitly text: preserve leading zeros and prevent formula execution.
                    if (values[col].Length > 32767) throw new DomainException("ExcelImageLimit");
                    var cell = sheet.Cell(row + 2, col + 1); cell.Style.NumberFormat.Format = "@"; cell.Value = values[col];
                }
                sheet.Cell(row + 2, 4).Value = products[row].Price; sheet.Cell(row + 2, 4).Style.NumberFormat.Format = "0.00";
            }
            sheet.Row(1).Style.Font.Bold = true; sheet.SheetView.FreezeRows(1);
            sheet.Columns(1, 6).Width = 24; sheet.Column(7).Width = 16;
            var temp = path + "." + Guid.NewGuid().ToString("N") + ".xlsx";
            try { workbook.SaveAs(temp); File.Move(temp, path, true); }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        else
        {
            string Quote(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";
            var lines = new List<string> { string.Join(';', Headers.Select(Quote)) };
            lines.AddRange(products.Select(p => string.Join(';', Row(p).Select(Quote))));
            JsonData.SaveAtomic(path, "\uFEFF" + string.Join("\r\n", lines) + "\r\n");
        }
    }
    private static List<string[]> ReadExcel(string path)
    {
        // Bound decompression before ClosedXML materializes an untrusted workbook.
        using (var archive = System.IO.Compression.ZipFile.OpenRead(path))
            if (archive.Entries.Sum(e => e.Length) > 100_000_000) throw new DomainException("FileTooLarge");
        using var book = new XLWorkbook(path); var sheet = book.Worksheets.First();
        int last = sheet.LastRowUsed()?.RowNumber() ?? 0;
        int columns = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        if (last > MaximumRows + 1 || columns > 30) throw new DomainException("TooManyRows");
        var rows = new List<string[]>();
        for (int r = 1; r <= last; r++)
        {
            var cells = new string[columns];
            for (int c = 1; c <= columns; c++)
            {
                var cell = sheet.Cell(r, c);
                if (cell.HasFormula) throw new DomainException("ImportFormula", r);
                cells[c - 1] = cell.DataType == XLDataType.Number ? cell.GetValue<decimal>().ToString(CultureInfo.InvariantCulture) : cell.GetString();
            }
            rows.Add(cells);
        }
        return rows;
    }
    // RFC4180 quoting, escaped quotes and multiline fields, with Excel-style semicolon support.
    public static List<string[]> ReadCsv(string text)
    {
        var first = text.Split('\n', 2)[0];
        char delimiter = CountOutsideQuotes(first, ';') >= CountOutsideQuotes(first, ',') ? ';' : ',';
        var rows = new List<string[]>(); var row = new List<string>(); var cell = new StringBuilder();
        bool quoted = false, closed = false;
        for (int i = text.StartsWith('\uFEFF') ? 1 : 0; i < text.Length; i++)
        {
            char ch = text[i];
            if (quoted)
            {
                if (ch == '"') { if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; } else { quoted = false; closed = true; } }
                else cell.Append(ch);
            }
            else if (ch == '"' && cell.Length == 0 && !closed) quoted = true;
            else if (ch == delimiter) { row.Add(cell.ToString()); cell.Clear(); closed = false; }
            else if (ch is '\r' or '\n')
            {
                if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                row.Add(cell.ToString()); rows.Add(row.ToArray()); row.Clear(); cell.Clear(); closed = false;
                if (rows.Count > MaximumRows + 1) throw new DomainException("TooManyRows");
            }
            else if (closed || ch == '"') throw new DomainException("InvalidCsv");
            else cell.Append(ch);
        }
        if (quoted) throw new DomainException("InvalidCsv");
        if (cell.Length > 0 || row.Count > 0 || closed) { row.Add(cell.ToString()); rows.Add(row.ToArray()); }
        return rows;
    }
    private static int CountOutsideQuotes(string text, char delimiter)
    {
        bool quoted = false; int count = 0;
        foreach (char c in text) { if (c == '"') quoted = !quoted; if (!quoted && c == delimiter) count++; }
        return count;
    }
}
