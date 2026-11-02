// SPDX-License-Identifier: GPL-3.0-only
using Microsoft.Data.Sqlite;

namespace EtiketStudio.Core;

public sealed class Store
{
    private readonly string connectionString;
    public string Path { get; }
    public Store(string path) : this(path, false) { }
    private Store(string path, bool readOnly)
    {
        Path = System.IO.Path.GetFullPath(path);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        connectionString = new SqliteConnectionStringBuilder { DataSource = Path, Pooling = !readOnly, Mode = readOnly ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWriteCreate }.ToString();
        if (readOnly) return;
        using var c = Open();
        using var version = c.CreateCommand(); version.CommandText = "PRAGMA user_version";
        if (Convert.ToInt32(version.ExecuteScalar()) > 1) throw new DomainException("DatabaseVersion");
        using var command = c.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode=WAL;
            PRAGMA foreign_keys=ON;
            CREATE TABLE IF NOT EXISTS products(
              id TEXT PRIMARY KEY, name TEXT NOT NULL, barcode TEXT NOT NULL, sku TEXT NOT NULL UNIQUE,
              price_minor INTEGER NOT NULL CHECK(price_minor>=0), description TEXT NOT NULL,
              category TEXT NOT NULL, image BLOB);
            CREATE INDEX IF NOT EXISTS ix_products_barcode ON products(barcode);
            CREATE TABLE IF NOT EXISTS state(key TEXT PRIMARY KEY, json TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS print_history(id TEXT PRIMARY KEY, time TEXT NOT NULL,
              printer TEXT NOT NULL, spool_id INTEGER, labels INTEGER NOT NULL, state TEXT NOT NULL, detail TEXT NOT NULL);
            PRAGMA user_version=1;
            """;
        command.ExecuteNonQuery();
    }
    private SqliteConnection Open() { var c = new SqliteConnection(connectionString); c.Open(); return c; }
    public List<Product> Products()
    {
        using var c = Open(); using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT id,name,barcode,sku,price_minor,description,category,image FROM products ORDER BY name";
        using var reader = cmd.ExecuteReader(); var products = new List<Product>();
        while (reader.Read()) products.Add(new Product { Id = reader.GetString(0), Name = reader.GetString(1), Barcode = reader.GetString(2), Sku = reader.GetString(3), Price = reader.GetInt64(4) / 100m, Description = reader.GetString(5), Category = reader.GetString(6), Image = reader.IsDBNull(7) ? null : (byte[])reader[7] });
        return products;
    }
    public void SaveProduct(Product p)
    {
        Validation.Product(p);
        using var c = Open(); using var tx = c.BeginTransaction();
        try { Upsert(c, tx, p, false); tx.Commit(); }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19) { throw new DomainException("DuplicateSku"); }
    }
    public void Import(IReadOnlyList<Product> products)
    {
        foreach (var p in products) Validation.Product(p);
        if (products.Select(p => p.Sku).Distinct(StringComparer.Ordinal).Count() != products.Count) throw new DomainException("DuplicateSku");
        using var c = Open(); using var tx = c.BeginTransaction();
        foreach (var p in products) Upsert(c, tx, p, true);
        tx.Commit();
    }
    private static void Upsert(SqliteConnection c, SqliteTransaction tx, Product p, bool bySku)
    {
        using var cmd = c.CreateCommand(); cmd.Transaction = tx;
        cmd.CommandText = $"""
            INSERT INTO products(id,name,barcode,sku,price_minor,description,category,image)
            VALUES($id,$name,$barcode,$sku,$price,$description,$category,$image)
            ON CONFLICT({(bySku ? "sku" : "id")}) DO UPDATE SET name=excluded.name,
            barcode=excluded.barcode,sku=excluded.sku,price_minor=excluded.price_minor,
            description=excluded.description,category=excluded.category,image=excluded.image
            """;
        cmd.Parameters.AddWithValue("$id", p.Id); cmd.Parameters.AddWithValue("$name", p.Name);
        cmd.Parameters.AddWithValue("$barcode", p.Barcode); cmd.Parameters.AddWithValue("$sku", p.Sku);
        cmd.Parameters.AddWithValue("$price", checked((long)(p.Price * 100m)));
        cmd.Parameters.AddWithValue("$description", p.Description); cmd.Parameters.AddWithValue("$category", p.Category);
        cmd.Parameters.AddWithValue("$image", (object?)p.Image ?? DBNull.Value); cmd.ExecuteNonQuery();
    }
    public void DeleteProduct(string id)
    {
        using var c = Open(); using var cmd = c.CreateCommand();
        cmd.CommandText = "DELETE FROM products WHERE id=$id"; cmd.Parameters.AddWithValue("$id", id); cmd.ExecuteNonQuery();
    }
    public TItem Load<TItem>(string key, Func<TItem> fallback)
    {
        using var c = Open(); using var cmd = c.CreateCommand(); cmd.CommandText = "SELECT json FROM state WHERE key=$key";
        cmd.Parameters.AddWithValue("$key", key); var json = cmd.ExecuteScalar() as string;
        return json is null ? fallback() : JsonData.Deserialize<TItem>(json);
    }
    public void Save<TItem>(string key, TItem value)
    {
        using var c = Open(); using var cmd = c.CreateCommand();
        cmd.CommandText = "INSERT INTO state VALUES($key,$json) ON CONFLICT(key) DO UPDATE SET json=excluded.json";
        cmd.Parameters.AddWithValue("$key", key); cmd.Parameters.AddWithValue("$json", JsonData.Serialize(value)); cmd.ExecuteNonQuery();
    }
    public void Record(PrintRecord record)
    {
        using var c = Open(); using var cmd = c.CreateCommand();
        cmd.CommandText = "INSERT OR REPLACE INTO print_history VALUES($id,$time,$printer,$spool,$labels,$state,$detail)";
        cmd.Parameters.AddWithValue("$id", record.Id); cmd.Parameters.AddWithValue("$time", record.Time.ToString("O"));
        cmd.Parameters.AddWithValue("$printer", record.Printer); cmd.Parameters.AddWithValue("$spool", (object?)record.SpoolId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$labels", record.Labels); cmd.Parameters.AddWithValue("$state", record.State);
        cmd.Parameters.AddWithValue("$detail", record.Detail); cmd.ExecuteNonQuery();
    }
    public List<PrintRecord> History()
    {
        using var c = Open(); using var cmd = c.CreateCommand(); cmd.CommandText = "SELECT * FROM print_history ORDER BY time DESC LIMIT 500";
        using var r = cmd.ExecuteReader(); var result = new List<PrintRecord>();
        while (r.Read()) result.Add(new(r.GetString(0), DateTimeOffset.Parse(r.GetString(1)), r.GetString(2), r.IsDBNull(3) ? null : r.GetInt32(3), r.GetInt32(4), r.GetString(5), r.GetString(6)));
        return result;
    }
    public void SaveSession(AppSettings settings, LabelTemplate template)
    {
        Validation.Settings(settings); Validation.Template(template);
        using var c = Open(); using var tx = c.BeginTransaction();
        foreach (var item in new[] { ("settings", JsonData.Serialize(settings)), ("draft", JsonData.Serialize(template)) })
        {
            using var cmd = c.CreateCommand(); cmd.Transaction = tx;
            cmd.CommandText = "INSERT INTO state VALUES($key,$json) ON CONFLICT(key) DO UPDATE SET json=excluded.json";
            cmd.Parameters.AddWithValue("$key", item.Item1); cmd.Parameters.AddWithValue("$json", item.Item2); cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }
    public string AutomaticBackup(string directory, DateOnly day, int keep = 7)
    {
        if (keep < 1) throw new ArgumentOutOfRangeException(nameof(keep));
        Directory.CreateDirectory(directory);
        string target = System.IO.Path.Combine(directory, "auto-" + day.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) + ".db");
        if (!File.Exists(target)) Backup(target);
        // Only prune files generated by this policy; manual and pre-restore backups are retained.
        var owned = Directory.GetFiles(directory, "auto-*.db").Where(p => DateOnly.TryParseExact(System.IO.Path.GetFileNameWithoutExtension(p)[5..], "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _)).OrderByDescending(p => p, StringComparer.Ordinal).ToList();
        foreach (var old in owned.Skip(keep)) File.Delete(old);
        return target;
    }
    public void Backup(string destination)
    {
        destination = System.IO.Path.GetFullPath(destination);
        if (destination.Equals(Path, StringComparison.OrdinalIgnoreCase)) throw new DomainException("InvalidFile");
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destination)!);
        string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var source = Open())
            using (var target = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = temporary, Pooling = false }.ToString()))
            {
                target.Open(); source.BackupDatabase(target);
            }
            File.Move(temporary, destination, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public void RestoreBackup(string sourcePath, string safetyBackup)
    {
        sourcePath = System.IO.Path.GetFullPath(sourcePath);
        safetyBackup = System.IO.Path.GetFullPath(safetyBackup);
        if (sourcePath.Equals(Path, StringComparison.OrdinalIgnoreCase) || safetyBackup.Equals(sourcePath, StringComparison.OrdinalIgnoreCase) || safetyBackup.Equals(Path, StringComparison.OrdinalIgnoreCase) || File.Exists(safetyBackup)) throw new DomainException("InvalidFile");
        if (!File.Exists(sourcePath) || new FileInfo(sourcePath).Length > 512_000_000) throw new DomainException("InvalidBackup");
        string stage = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path)!, "restore-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            // Snapshot the selected database before validation so subsequent edits to the file
            // cannot change what is restored. Opening read-only never upgrades a foreign database.
            new Store(sourcePath, true).Backup(stage);
            var incoming = new Store(stage, true);
            using (var check = incoming.Open())
            {
                using var cmd = check.CreateCommand(); cmd.CommandText = "PRAGMA quick_check";
                if (!Equals(cmd.ExecuteScalar(), "ok")) throw new DomainException("InvalidBackup");
                cmd.CommandText = "PRAGMA user_version";
                if (Convert.ToInt32(cmd.ExecuteScalar()) != 1) throw new DomainException("DatabaseVersion");
                cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('products','state','print_history')";
                if (Convert.ToInt32(cmd.ExecuteScalar()) != 3) throw new DomainException("InvalidBackup");
            }
            foreach (var product in incoming.Products()) Validation.Product(product);
            Validation.Settings(incoming.Load("settings", () => new AppSettings()));
            Validation.Template(incoming.Load("draft", LabelTemplate.Default));
            _ = LanguageCatalog.StartupLanguage(incoming);
            _ = incoming.History();
            Backup(safetyBackup);
            using var c = Open();
            using (var attach = c.CreateCommand()) { attach.CommandText = "ATTACH DATABASE $path AS incoming"; attach.Parameters.AddWithValue("$path", stage); attach.ExecuteNonQuery(); }
            bool committed = false;
            try
            {
                using var tx = c.BeginTransaction(); using var cmd = c.CreateCommand(); cmd.Transaction = tx;
                cmd.CommandText = """
                    DELETE FROM products; DELETE FROM state; DELETE FROM print_history;
                    INSERT INTO products SELECT id,name,barcode,sku,price_minor,description,category,image FROM incoming.products;
                    INSERT INTO state SELECT key,json FROM incoming.state;
                    INSERT INTO print_history SELECT id,time,printer,spool_id,labels,state,detail FROM incoming.print_history;
                    """;
                cmd.ExecuteNonQuery(); tx.Commit(); committed = true;
            }
            finally
            {
                try { using var detach = c.CreateCommand(); detach.CommandText = "DETACH DATABASE incoming"; detach.ExecuteNonQuery(); }
                catch (SqliteException) when (committed) { /* Connection disposal releases the attachment after a successful commit. */ }
            }
        }
        catch (SqliteException) { throw new DomainException("InvalidBackup"); }
        finally
        {
            // Cleanup cannot turn a successfully committed restore into a reported failure.
            foreach (string file in new[] { stage, stage + "-wal", stage + "-shm" })
            {
                try { if (File.Exists(file)) File.Delete(file); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }
}
