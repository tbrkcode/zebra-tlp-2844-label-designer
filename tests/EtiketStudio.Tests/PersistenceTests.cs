// SPDX-License-Identifier: GPL-3.0-only
using EtiketStudio.Core;
using Microsoft.Data.Sqlite;
using ClosedXML.Excel;
using Xunit;

namespace EtiketStudio.Tests;

public sealed class PersistenceTests : IDisposable
{
    private readonly string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "EtiketTests-" + Guid.NewGuid().ToString("N"));
    public PersistenceTests() => Directory.CreateDirectory(directory);
    private string Path(string name) => System.IO.Path.Combine(directory, name);
    private static Product Sample(string sku = "000123") => new() { Sku = sku, Name = "Çay; \"özel\"\nİçim", Barcode = "036000291452", Price = 12.50m, Category = "Gıda", Description = "ğışü", Image = [1, 2, 3] };
    [Fact] public void NewAndUpgradedInstallsStartInEnglishAndRememberLaterChoices()
    {
        var store = new Store(Path("startup.db"));
        Assert.Equal("en-US", LanguageCatalog.StartupLanguage(store));
        store.Save("settings", new AppSettings { Language = "tr-TR" });
        store.Save("language", "tr-TR");
        Assert.Equal("en-US", LanguageCatalog.StartupLanguage(store));
        store.Save(LanguageCatalog.PreferenceKey, "tr-TR");
        Assert.Equal("tr-TR", LanguageCatalog.StartupLanguage(new Store(Path("startup.db"))));
    }
    [Fact] public void SavedLanguageSurvivesReopeningWithoutChangingProductData()
    {
        var store = new Store(Path("language.db")); var product = Sample(); store.SaveProduct(product);
        store.Save("settings", new AppSettings { Language = "tr-TR" }); store.Save("language", "en-US");
        var reopened = new Store(Path("language.db")); var settings = reopened.Load("settings", () => new AppSettings());
        Assert.Equal("en-US", reopened.Load("language", () => settings.Language));
        Assert.Equal(product.Name, Assert.Single(reopened.Products()).Name);
    }
    public void Dispose() { SqliteConnection.ClearAllPools(); Directory.Delete(directory, true); }
    [Fact] public void ProductCrudPersistsTurkishAndMinorUnits()
    {
        var store = new Store(Path("data.db")); var p = Sample(); store.SaveProduct(p);
        var loaded = Assert.Single(new Store(Path("data.db")).Products()); Assert.Equal(p.Name, loaded.Name); Assert.Equal(12.50m, loaded.Price); Assert.Equal(p.Image, loaded.Image);
        p.Price = 0.01m; store.SaveProduct(p); Assert.Equal(0.01m, Assert.Single(store.Products()).Price); store.DeleteProduct(p.Id); Assert.Empty(store.Products());
    }
    [Fact] public void DuplicateSkuDoesNotOverwriteAnotherProduct()
    {
        var store = new Store(Path("data.db")); store.SaveProduct(Sample()); Assert.Throws<DomainException>(() => store.SaveProduct(Sample())); Assert.Single(store.Products());
    }
    [Fact] public void ImportUpsertPreservesExistingProductIdentity()
    {
        var store = new Store(Path("data.db")); var original = Sample(); store.SaveProduct(original); var update = Sample(); update.Name = "New";
        store.Import([update]); var saved = Assert.Single(store.Products()); Assert.Equal(original.Id, saved.Id); Assert.Equal("New", saved.Name);
    }
    [Fact] public void InvalidImportDoesNotPartiallyCommit()
    {
        var store = new Store(Path("data.db")); var invalid = Sample("B"); invalid.Price = -1;
        Assert.Throws<DomainException>(() => store.Import([Sample("A"), invalid])); Assert.Empty(store.Products());
    }
    [Fact] public void SqliteTransactionRollsBackDatabaseFailure()
    {
        var store = new Store(Path("data.db")); var a = Sample("A"); var b = Sample("B"); b.Id = a.Id;
        Assert.Throws<SqliteException>(() => store.Import([a, b])); Assert.Empty(store.Products());
    }
    [Fact] public void ImportRejectsDuplicateSkuWithinFile()
    {
        var store = new Store(Path("data.db")); Assert.Throws<DomainException>(() => store.Import([Sample(), Sample()])); Assert.Empty(store.Products());
    }
    [Fact] public void SettingsAndDraftSurviveRestart()
    {
        var store = new Store(Path("data.db")); store.Save("settings", new AppSettings { Printer = "ZDesigner TLP 2844", DarkTheme = true }); store.Save("draft", LabelTemplate.Default());
        var reopened = new Store(Path("data.db")); Assert.True(reopened.Load("settings", () => new AppSettings()).DarkTheme); Assert.Equal(4, reopened.Load("draft", () => new LabelTemplate()).Elements.Count);
    }
    [Fact] public void PrintHistoryDistinguishesSubmissionFromOutput()
    {
        var store = new Store(Path("data.db")); var item = new PrintRecord("test", DateTimeOffset.Now, "Printer", 42, 3, "Sent", "Test"); store.Record(item);
        Assert.Equal(item, Assert.Single(store.History())); store.Record(item with { State = "Failed" }); Assert.Equal("Failed", Assert.Single(store.History()).State);
    }
    [Fact] public void BackupContainsCommittedWalData()
    {
        var store = new Store(Path("data.db")); store.SaveProduct(Sample()); store.Backup(Path("backup.db")); Assert.Single(new Store(Path("backup.db")).Products());
    }
    [Theory] [InlineData("csv")] [InlineData("xlsx")]
    public void ProductExchangeRoundTripsTurkishLeadingZerosAndImages(string extension)
    {
        var input = Sample(); ProductExchange.Write(Path("products." + extension), [input]); var output = Assert.Single(ProductExchange.Read(Path("products." + extension)));
        Assert.Equal(input.Name, output.Name); Assert.Equal(input.Sku, output.Sku); Assert.Equal(input.Barcode, output.Barcode); Assert.Equal(input.Price, output.Price); Assert.Equal(input.Image, output.Image);
    }
    [Fact] public void ExcelNumericPriceDoesNotAcquireFloatingPointNoise()
    {
        var p = Sample(); p.Price = 0.10m; ProductExchange.Write(Path("price.xlsx"), [p]); Assert.Equal(0.10m, Assert.Single(ProductExchange.Read(Path("price.xlsx"))).Price);
    }
    [Fact] public void ExcelFormulaLikeTextStaysText()
    {
        var p = Sample(); p.Name = "=1+1"; ProductExchange.Write(Path("safe.xlsx"), [p]);
        using var book = new XLWorkbook(Path("safe.xlsx")); Assert.False(book.Worksheet(1).Cell(2, 2).HasFormula); Assert.Equal("=1+1", book.Worksheet(1).Cell(2, 2).GetString());
    }
    [Fact] public void ExcelInputFormulasAreRejected()
    {
        ProductExchange.Write(Path("formula.xlsx"), [Sample()]);
        using (var book = new XLWorkbook(Path("formula.xlsx"))) { book.Worksheet(1).Cell(2, 4).FormulaA1 = "1+1"; book.Save(); }
        Assert.Throws<DomainException>(() => ProductExchange.Read(Path("formula.xlsx")));
    }
    [Fact] public void TurkishCsvHeadersAndDecimalCommaWork()
    {
        File.WriteAllText(Path("turkish.csv"), "Stok kodu;Ürün adı;Barkod;Fiyat\r\n0001;Ürün;123;12,50\r\n");
        var p = Assert.Single(ProductExchange.Read(Path("turkish.csv"))); Assert.Equal("0001", p.Sku); Assert.Equal(12.50m, p.Price);
    }
    [Fact] public void CsvCommaDelimiterAndEscapedMultilineFieldWork()
    {
        var rows = ProductExchange.ReadCsv("a,b\r\n\"hello,\"\"world\"\"\nline\",x\r\n"); Assert.Equal(2, rows.Count); Assert.Equal("hello,\"world\"\nline", rows[1][0]);
    }
    [Theory] [InlineData("a;b\n\"oops;b")] [InlineData("a;b\n\"x\"oops;b")] [InlineData("a;b\nx\"y;b")]
    public void InvalidCsvIsRejected(string data) => Assert.Throws<DomainException>(() => ProductExchange.ReadCsv(data));
    [Fact] public void InvalidRowReportsRowNumber()
    {
        File.WriteAllText(Path("bad.csv"), "Sku;Name;Barcode;Price\nA;N;123;not-a-price\n"); var error = Assert.Throws<DomainException>(() => ProductExchange.Read(Path("bad.csv"))); Assert.Equal("ImportRow", error.Key); Assert.Contains("2", error.Message);
    }
    [Fact] public void AtomicTemplateSaveRoundTripsEmbeddedImage()
    {
        var t = LabelTemplate.Default(); t.Elements[0].Image = [1, 2, 3]; JsonData.SaveAtomic(Path("label.json"), JsonData.Serialize(t)); Assert.Equal(new byte[] { 1, 2, 3 }, JsonData.LoadTemplate(Path("label.json")).Elements[0].Image);
    }
}
