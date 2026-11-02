// SPDX-License-Identifier: GPL-3.0-only
using EtiketStudio.Core;
using Microsoft.Data.Sqlite;
using Xunit;
namespace EtiketStudio.Tests;
public sealed class BackupTests : IDisposable
{
    private readonly string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "EtiketBackup-" + Guid.NewGuid().ToString("N"));
    private string FilePath(string name) => System.IO.Path.Combine(directory, name);
    public BackupTests() => Directory.CreateDirectory(directory);
    public void Dispose() { SqliteConnection.ClearAllPools(); Directory.Delete(directory, true); }
    [Fact] public void RestoreReplacesAllDataAndPreservesSafetyCopy()
    {
        var source = new Store(FilePath("source.db")); source.SaveProduct(new() { Name = "Restored", Sku = "R" }); source.Save("language", "en-US");
        source.SaveSession(new() { Language = "en-US" }, LabelTemplate.Default());
        source.Record(new("source-job", DateTimeOffset.Now, "Printer", 1, 1, "Sent", "Source"));
        var target = new Store(FilePath("target.db")); target.SaveProduct(new() { Name = "Current", Sku = "C" }); target.Save("language", "tr-TR");
        target.RestoreBackup(source.Path, FilePath("safety.db"));
        Assert.Equal("Restored", Assert.Single(target.Products()).Name); Assert.Equal("en-US", target.Load("language", () => ""));
        Assert.Equal("source-job", Assert.Single(target.History()).Id);
        var safety = new Store(FilePath("safety.db")); Assert.Equal("Current", Assert.Single(safety.Products()).Name); Assert.Equal("tr-TR", safety.Load("language", () => ""));
    }
    [Fact] public void ForeignDatabaseIsRejectedWithoutChangingCurrentData()
    {
        using (var c = new SqliteConnection("Data Source=" + FilePath("foreign.db"))) { c.Open(); using var cmd = c.CreateCommand(); cmd.CommandText = "CREATE TABLE other(value TEXT); PRAGMA user_version=1;"; cmd.ExecuteNonQuery(); }
        var target = new Store(FilePath("target.db")); target.SaveProduct(new() { Name = "Keep", Sku = "K" });
        Assert.Throws<DomainException>(() => target.RestoreBackup(FilePath("foreign.db"), FilePath("safety.db")));
        Assert.Equal("Keep", Assert.Single(target.Products()).Name);
    }
    [Fact] public void InvalidDraftIsRejectedBeforeRestoration()
    {
        var source = new Store(FilePath("source.db")); source.Save("draft", new LabelTemplate { Width = 999 });
        var target = new Store(FilePath("target.db")); target.SaveProduct(new() { Name = "Keep", Sku = "K" });
        Assert.Throws<DomainException>(() => target.RestoreBackup(source.Path, FilePath("safety.db")));
        Assert.Equal("Keep", Assert.Single(target.Products()).Name); Assert.False(File.Exists(FilePath("safety.db")));
    }
    [Fact] public void FailedRestoreTransactionRollsBackAllTables()
    {
        string sourcePath = FilePath("source.db"); var source = new Store(sourcePath);
        source.SaveProduct(new() { Id = "a", Name = "One", Sku = "duplicate" });
        using (var c = new SqliteConnection("Data Source=" + sourcePath))
        {
            c.Open(); using var cmd = c.CreateCommand();
            cmd.CommandText = "ALTER TABLE products RENAME TO old_products; CREATE TABLE products AS SELECT * FROM old_products; INSERT INTO products SELECT 'b',name,barcode,sku,price_minor,description,category,image FROM old_products;";
            cmd.ExecuteNonQuery();
        }
        var target = new Store(FilePath("target.db")); target.SaveProduct(new() { Name = "Keep", Sku = "K" }); target.Save("language", "tr-TR");
        target.Record(new("keep-job", DateTimeOffset.Now, "Printer", 1, 1, "Sent", "Keep"));
        Assert.Throws<DomainException>(() => target.RestoreBackup(sourcePath, FilePath("safety.db")));
        Assert.Equal("Keep", Assert.Single(target.Products()).Name); Assert.Equal("tr-TR", target.Load("language", () => ""));
        Assert.Equal("keep-job", Assert.Single(target.History()).Id);
        Assert.Equal("Keep", Assert.Single(new Store(FilePath("safety.db")).Products()).Name);
    }
    [Fact] public void CorruptBackupIsRejectedWithoutChangingData()
    {
        File.WriteAllText(FilePath("invalid.db"), "not SQLite");
        var target = new Store(FilePath("target.db")); target.SaveProduct(new() { Name = "Keep", Sku = "K" });
        Assert.Throws<DomainException>(() => target.RestoreBackup(FilePath("invalid.db"), FilePath("safety.db")));
        Assert.Equal("Keep", Assert.Single(target.Products()).Name);
    }
    [Fact] public void DailyBackupsRetainSevenAndLeaveManualFilesAlone()
    {
        var store = new Store(FilePath("data.db")); string backups = FilePath("Backups"); Directory.CreateDirectory(backups);
        File.WriteAllText(System.IO.Path.Combine(backups, "manual.db"), "manual"); File.WriteAllText(System.IO.Path.Combine(backups, "auto-not-a-date.db"), "unrelated");
        var day = new DateOnly(2026, 10, 1);
        for (int i = 0; i < 10; i++) store.AutomaticBackup(backups, day.AddDays(i));
        Assert.Equal(9, Directory.GetFiles(backups).Length);
        Assert.False(File.Exists(System.IO.Path.Combine(backups, "auto-2026-10-01.db")));
        Assert.True(File.Exists(System.IO.Path.Combine(backups, "auto-2026-10-10.db")));
        store.SaveProduct(new() { Name = "Later", Sku = "L" }); string existing = store.AutomaticBackup(backups, day.AddDays(9));
        Assert.Empty(new Store(existing).Products());
    }
    [Fact] public void InvalidSessionDoesNotPartiallyPersistSettings()
    {
        var store = new Store(FilePath("data.db")); store.SaveSession(new() { Printer = "Old" }, LabelTemplate.Default());
        Assert.Throws<DomainException>(() => store.SaveSession(new() { Printer = "New" }, new() { Width = 999 }));
        Assert.Equal("Old", store.Load("settings", () => new AppSettings()).Printer);
    }
    [Fact] public void RestoreRejectsOverwritingExistingSafetyBackup()
    {
        var store = new Store(FilePath("data.db")); store.Backup(FilePath("source.db")); File.WriteAllText(FilePath("safety.db"), "keep");
        Assert.Throws<DomainException>(() => store.RestoreBackup(FilePath("source.db"), FilePath("safety.db")));
        Assert.Equal("keep", File.ReadAllText(FilePath("safety.db")));
    }
}
