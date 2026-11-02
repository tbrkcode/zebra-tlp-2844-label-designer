// SPDX-License-Identifier: GPL-3.0-only
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using EtiketStudio.Core;
using EtiketStudio.Windows.Infrastructure;
using EtiketStudio.Windows.Printing;
using EtiketStudio.Windows.Rendering;
using EtiketStudio.Windows.Views;

namespace EtiketStudio.Windows.ViewModels;

public sealed record LanguageOption(string Code, string Name);

public sealed class MainViewModel : ObservableObject
{
    private readonly Store store;
    private readonly UiServices ui;
    private readonly WindowsSpooler spooler = new();
        private readonly Stack<string> undo = new(), redo = new();
    private string previousDesign = "";
    private bool trackingSuppressed, dragging;
    private CancellationTokenSource? cancellation;
    private LabelTemplate template;
    private LabelElement? selectedElement;
    private Product? selectedProduct;
    private Product editProduct = new();
    private PrinterInfo? selectedPrinter;
    private bool busy, snap = true;
    private string status = T.Get("Ready"), search = "", barcodeInput = "869123456789", barcodeResult = "";
    private double zoom = 2;
    private int copies = 1;
    private BarcodeKind toolBarcode = BarcodeKind.Ean13;
    public ObservableCollection<Product> Products { get; } = [];
    public ICollectionView ProductView { get; }
    public ObservableCollection<PrinterInfo> Printers { get; } = [];
    public ObservableCollection<QueueJob> Queue { get; } = [];
    public ObservableCollection<PrintRecord> History { get; } = [];
    public Array ElementKinds { get; } = Enum.GetValues<ElementKind>();
    public Array BarcodeKinds { get; } = Enum.GetValues<BarcodeKind>();
    public LanguageOption[] Languages { get; } = [new("tr-TR", "Türkçe"), new("en-US", "English")];
    public int[] Speeds { get; } = [1, 2, 3, 4];
    public AppSettings Settings { get; }
    public LabelTemplate Template => template;
    public LabelElement? SelectedElement { get => selectedElement; set { Set(ref selectedElement, value); CommandManager.InvalidateRequerySuggested(); } }
    public Product? SelectedProduct
    {
        get => selectedProduct;
        set { if (Set(ref selectedProduct, value)) { EditProduct = value?.Clone() ?? new(); DesignChanged?.Invoke(this, EventArgs.Empty); } }
    }
    public Product EditProduct { get => editProduct; set => Set(ref editProduct, value); }
    public Product PreviewProduct => SelectedProduct ?? new Product { Name = T.Get("SampleProduct"), Sku = "DEMO-001", Barcode = "123456789012", Price = 49.90m };
    public PrinterInfo? SelectedPrinter
    {
        get => selectedPrinter;
        set { if (Set(ref selectedPrinter, value) && value is not null) { Settings.Printer = value.Name; store.Save("settings", Settings); Queue.Clear(); } }
    }
    public QueueJob? SelectedJob { get; set; }
    public bool Busy { get => busy; private set { Set(ref busy, value); CommandManager.InvalidateRequerySuggested(); } }
    public bool Snap { get => snap; set => Set(ref snap, value); }
    public double Zoom { get => zoom; set { if (Set(ref zoom, Math.Clamp(value, 0.5, 5))) DesignChanged?.Invoke(this, EventArgs.Empty); } }
    public int Copies { get => copies; set => Set(ref copies, value); }
    public string Status { get => status; private set => Set(ref status, value); }
    public string Search { get => search; set { if (Set(ref search, value)) ProductView.Refresh(); } }
    public string BarcodeInput { get => barcodeInput; set => Set(ref barcodeInput, value); }
    public string BarcodeResult { get => barcodeResult; set => Set(ref barcodeResult, value); }
    public BarcodeKind ToolBarcode { get => toolBarcode; set => Set(ref toolBarcode, value); }
    public event EventHandler? DesignChanged;

    public ICommand NewProduct { get; }
    public ICommand SaveProduct { get; }
    public ICommand DeleteProduct { get; }
    public ICommand ImportProducts { get; }
    public ICommand ExportProducts { get; }
    public ICommand ProductImage { get; }
    public ICommand RemoveProductImage { get; }
    public ICommand SelectAll { get; }
    public ICommand ClearSelection { get; }
    public ICommand NewTemplate { get; }
    public ICommand OpenTemplate { get; }
    public ICommand SaveTemplate { get; }
    public ICommand AddElement { get; }
    public ICommand DeleteElement { get; }
    public ICommand DuplicateElement { get; }
    public ICommand AttachImage { get; }
    public ICommand BringFront { get; }
    public ICommand SendBack { get; }
    public ICommand Undo { get; }
    public ICommand Redo { get; }
    public ICommand ActualSize { get; }
    public ICommand PreviewSingle { get; }
    public ICommand PreviewBatch { get; }
    public ICommand RefreshPrinters { get; }
    public ICommand RefreshQueue { get; }
    public ICommand CancelJob { get; }
    public ICommand CancelSubmission { get; }
    public ICommand Calibrate { get; }
    public ICommand NativeTest { get; }
    public ICommand ConfigurationTest { get; }
    public ICommand OpenWindowsQueue { get; }
    public ICommand ValidateBarcode { get; }
    public ICommand ApplyBarcode { get; }
    public ICommand SaveSettings { get; }
    public ICommand Backup { get; }
    public ICommand RestoreBackup { get; }
    public bool Restored { get; private set; }
    public ICommand OpenDataFolder { get; }
    public ICommand Help { get; }

    public MainViewModel(Store store, AppSettings settings, UiServices ui)
    {
        this.store = store; this.ui = ui; Settings = settings;
        template = store.Load("draft", LabelTemplate.Default); TrackTemplate();
        ProductView = CollectionViewSource.GetDefaultView(Products);
        ProductView.Filter = o => o is Product p && (string.IsNullOrWhiteSpace(Search) || new[] { p.Name, p.Sku, p.Barcode, p.Category }.Any(v => v.Contains(Search, StringComparison.CurrentCultureIgnoreCase)));
        ReloadProducts(); ReloadHistory();
        ActionCommand Command(Action action, Func<bool>? can = null) => new(_ => Guard(action), _ => !Busy && (can?.Invoke() ?? true));
        AsyncCommand Async(Func<Task> action, Func<bool>? can = null) => new(() => GuardAsync(action), () => !Busy && (can?.Invoke() ?? true));
        NewProduct = Command(() => { SelectedProduct = null; EditProduct = new(); });
        SaveProduct = Command(() => { ui.EnsureValidInput(); store.SaveProduct(EditProduct); var id = EditProduct.Id; ReloadProducts(id); Status = T.Get("Saved"); });
        DeleteProduct = Command(() => { if (ui.Confirm("DeleteConfirm")) { store.DeleteProduct(SelectedProduct!.Id); ReloadProducts(); } }, () => SelectedProduct is not null);
        ImportProducts = Async(ImportAsync);
        ExportProducts = Async(async () => { var path = ui.SaveFile("Excel (*.xlsx)|*.xlsx|CSV (*.csv)|*.csv", "products.xlsx"); if (path is null) return; var snapshot = Products.Select(p => p.Clone()).ToList(); await Task.Run(() => ProductExchange.Write(path, snapshot)); Status = T.Get("Exported"); });
        ProductImage = Command(() => { var bytes = PickImage(); if (bytes is not null) EditProduct.Image = bytes; });
        RemoveProductImage = Command(() => EditProduct.Image = null, () => EditProduct.Image is not null);
        SelectAll = Command(() => { foreach (var p in Products) p.Selected = true; });
        ClearSelection = Command(() => { foreach (var p in Products) p.Selected = false; });
        NewTemplate = Command(() => { if (ui.Confirm("NewTemplateConfirm")) ReplaceTemplate(LabelTemplate.Default(), true); });
        OpenTemplate = Command(() => { var path = ui.OpenFile("TBRK Label Studio (*.etiket.json)|*.etiket.json|JSON (*.json)|*.json"); if (path is not null && ui.Confirm("NewTemplateConfirm")) ReplaceTemplate(JsonData.LoadTemplate(path), true); });
        SaveTemplate = Command(() => { ui.EnsureValidInput(); Validation.Template(Template); var path = ui.SaveFile("TBRK Label Studio (*.etiket.json)|*.etiket.json", "label.etiket.json"); if (path is not null) { JsonData.SaveAtomic(path, JsonData.Serialize(Template)); store.Save("draft", Template); Status = T.Get("Saved"); } });
        AddElement = new ActionCommand(parameter => Guard(() => Add((ElementKind)parameter!)), _ => !Busy);
        DeleteElement = Command(() => { Template.Elements.Remove(SelectedElement!); SelectedElement = null; }, () => SelectedElement is not null);
        DuplicateElement = Command(() => { var item = SelectedElement!.Clone(); item.Id = Guid.NewGuid().ToString("N"); item.X = Math.Min(Template.Width - item.Width, item.X + 1); item.Y = Math.Min(Template.Height - item.Height, item.Y + 1); Template.Elements.Add(item); SelectedElement = item; }, () => SelectedElement is not null);
        AttachImage = Command(() => { var bytes = PickImage(); if (bytes is not null) SelectedElement!.Image = bytes; }, () => SelectedElement?.Kind == ElementKind.Image);
        BringFront = Command(() => Template.Elements.Move(Template.Elements.IndexOf(SelectedElement!), Template.Elements.Count - 1), () => SelectedElement is not null);
        SendBack = Command(() => Template.Elements.Move(Template.Elements.IndexOf(SelectedElement!), 0), () => SelectedElement is not null);
        Undo = Command(() => Restore(undo, redo), () => undo.Count > 0); Redo = Command(() => Restore(redo, undo), () => redo.Count > 0);
        ActualSize = Command(() => Zoom = 1);
        PreviewSingle = Async(() => PreviewAndPrint(false));
        PreviewBatch = Async(() => PreviewAndPrint(true), () => Products.Any(p => p.Selected));
        RefreshPrinters = Async(RefreshPrintersAsync);
        RefreshQueue = Async(RefreshQueueAsync, () => SelectedPrinter is not null);
        CancelJob = Async(async () => { await Task.Run(() => spooler.Cancel(Settings.Printer, SelectedJob!.Id)); Status = T.Get("Cancelled"); await RefreshQueueAsync(); }, () => SelectedJob?.Owned == true);
        CancelSubmission = new ActionCommand(_ => cancellation?.Cancel(), _ => Busy && cancellation is not null);
        Calibrate = Async(async () => { if (ui.Confirm("CalibrationConfirm")) await SendDiagnostic(Epl2.Calibration, 0); });
        NativeTest = Async(async () => { if (ui.Confirm("TestConfirm")) await SendDiagnostic(Epl2.NativeDiagnostic(Settings), 1); });
        ConfigurationTest = Async(() => SendDiagnostic(Epl2.Configuration, 1));
        OpenWindowsQueue = new ActionCommand(_ => Guard(() => { var psi = new System.Diagnostics.ProcessStartInfo("rundll32.exe") { UseShellExecute = false }; psi.ArgumentList.Add("printui.dll,PrintUIEntry"); psi.ArgumentList.Add("/o"); psi.ArgumentList.Add("/n"); psi.ArgumentList.Add(Settings.Printer); System.Diagnostics.Process.Start(psi); }), _ => !string.IsNullOrEmpty(Settings.Printer));
        ValidateBarcode = Command(() => { BarcodeInput = Barcodes.Normalize(ToolBarcode, BarcodeInput); BarcodeResult = T.Get("BarcodeValid", BarcodeInput); });
        ApplyBarcode = Command(() => { if (SelectedElement?.Kind is not ElementKind.Barcode and not ElementKind.QrCode) throw new DomainException("NoBarcodeSelected"); SelectedElement.Text = Barcodes.Normalize(ToolBarcode, BarcodeInput); SelectedElement.Kind = ToolBarcode == BarcodeKind.QrCode ? ElementKind.QrCode : ElementKind.Barcode; SelectedElement.BarcodeKind = ToolBarcode; });
        SaveSettings = Command(() => { ui.EnsureValidInput(); Validation.Settings(Settings); store.Save("settings", Settings); Status = T.Get("Saved"); });
        Backup = Async(async () => { var path = ui.SaveFile("SQLite (*.db)|*.db", "EtiketStudio-backup.db"); if (path is not null) { await Task.Run(() => store.Backup(path)); Status = T.Get("Saved"); } });
        RestoreBackup = Async(async () =>
        {
            var path = ui.OpenFile("SQLite (*.db)|*.db");
            if (path is null || !ui.Confirm("RestoreConfirm")) return;
            string safety = Path.Combine(ui.DataFolder, "Backups", "before-restore-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N") + ".db");
            await Task.Run(() => store.RestoreBackup(path, safety));
            Restored = true; // Never overwrite restored settings/draft during shutdown.
            MessageBox.Show(T.Get("RestoreComplete", safety), T.Get("AppName"), MessageBoxButton.OK, MessageBoxImage.Information);
            Application.Current.Shutdown();
        });
        OpenDataFolder = Command(() => UiServices.OpenPath(ui.DataFolder));
        Help = Command(() => { var guide = Path.Combine(AppContext.BaseDirectory, "docs", Settings.Language.StartsWith("tr") ? "USER_GUIDE_TR.html" : "USER_GUIDE_EN.html"); UiServices.OpenPath(guide); });
        Settings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppSettings.DarkTheme)) Theme.Apply(Settings.DarkTheme);
            if (e.PropertyName == nameof(AppSettings.Language)) Guard(() =>
            {
                // Persist independently of unrelated numeric fields or validation errors in other tabs.
                store.Save(LanguageCatalog.PreferenceKey, LanguageCatalog.Normalize(Settings.Language));
                UiLanguage.Apply(Settings.Language);
                ReloadHistory(); Status = T.Get("Saved"); BarcodeResult = "";
            });
            DesignChanged?.Invoke(this, EventArgs.Empty);
        };
    }
    public void Guard(Action action) { try { action(); } catch (Exception ex) { ui.Error(ex); } }
    private async Task GuardAsync(Func<Task> action)
    {
        Busy = true;
        try { await action(); }
        catch (OperationCanceledException) { Status = T.Get("PartialSubmission"); }
        catch (Exception ex) { Status = T.Get("Error"); ui.Error(ex); }
        finally { Busy = false; cancellation?.Dispose(); cancellation = null; }
    }
    public async Task InitializeAsync() => await GuardAsync(RefreshPrintersAsync);
    public void Persist()
    {
        ui.EnsureValidInput(); Validation.Settings(Settings); Validation.Template(Template);
        store.SaveSession(Settings, Template);
    }
    public bool ConfirmDiscard(Exception error) => ui.ConfirmDiscard(error);
    public bool ConfirmUnsavedProduct()
    {
        bool changed = SelectedProduct is not null
            ? JsonData.Serialize(EditProduct) != JsonData.Serialize(SelectedProduct)
            : !string.IsNullOrEmpty(EditProduct.Name + EditProduct.Sku + EditProduct.Barcode + EditProduct.Description + EditProduct.Category) || EditProduct.Price != 0 || EditProduct.Image is not null;
        return !changed || ui.Confirm("DiscardProduct");
    }
    private void ReloadProducts(string? id = null)
    {
        Products.Clear(); foreach (var p in store.Products()) Products.Add(p);
        SelectedProduct = Products.FirstOrDefault(p => p.Id == id) ?? Products.FirstOrDefault();
    }
    private void ReloadHistory() { History.Clear(); foreach (var r in store.History()) History.Add(r with { State = T.Get(r.State) }); }
    public async Task RefreshPrintersAsync()
    {
        string remembered = Settings.Printer;
        var printers = await Task.Run(spooler.Printers); Printers.Clear(); foreach (var p in printers) Printers.Add(p);
        SelectedPrinter = Printers.FirstOrDefault(p => p.Name == remembered);
        if (SelectedPrinter is null && string.IsNullOrEmpty(remembered)) SelectedPrinter = Printers.FirstOrDefault(p => p.IsEpl2844);
        Status = SelectedPrinter is null ? T.Get("NoPrinter") : T.Get("Ready");
    }
    private async Task RefreshQueueAsync()
    {
        var items = await Task.Run(() => spooler.Jobs(Settings.Printer)); Queue.Clear(); foreach (var job in items) Queue.Add(job); ReloadHistory();
    }
    private async Task ImportAsync()
    {
        var path = ui.OpenFile("Excel / CSV (*.xlsx;*.csv)|*.xlsx;*.csv"); if (path is null) return;
        var products = await Task.Run(() => ProductExchange.Read(path));
        if (!ui.Confirm("ImportConfirm")) return;
        await Task.Run(() => store.Import(products)); ReloadProducts(); Status = T.Get("Imported", products.Count);
    }
    private byte[]? PickImage()
    {
        var path = ui.OpenFile("PNG / JPEG / BMP|*.png;*.jpg;*.jpeg;*.bmp"); if (path is null) return null;
        if (new FileInfo(path).Length > 5_000_000) throw new DomainException("FileTooLarge");
        var bytes = File.ReadAllBytes(path); LabelRenderer.LoadImage(bytes); return bytes;
    }
    public void Add(ElementKind kind, double? x = null, double? y = null)
    {
        var e = new LabelElement { Kind = kind, Text = kind is ElementKind.Barcode or ElementKind.QrCode ? "{Barcode}" : T.Get("Text"), Width = Math.Min(40, Template.Width - 4), Height = kind is ElementKind.Barcode or ElementKind.QrCode ? 16 : 6 };
        if (kind == ElementKind.QrCode) { e.Width = e.Height = 20; e.BarcodeKind = BarcodeKind.QrCode; }
        if (kind == ElementKind.Image) { e.Width = e.Height = 12; var bytes = PickImage(); if (bytes is null) return; e.Image = bytes; }
        if (kind == ElementKind.Rectangle) e.Height = 10;
        if (kind == ElementKind.Line) e.Height = 0.5;
        e.Width = Math.Min(e.Width, Template.Width); e.Height = Math.Min(e.Height, Template.Height);
        e.X = Math.Clamp(x ?? 2, 0, Math.Max(0, Template.Width - e.Width)); e.Y = Math.Clamp(y ?? 2, 0, Math.Max(0, Template.Height - e.Height));
        Template.Elements.Add(e); SelectedElement = e;
    }
    public void BeginDrag() => dragging = true;
    public void EndDrag() { dragging = false; CaptureChange(); }
    public void Move(LabelElement e, double x, double y)
    {
        double SnapValue(double value) => Snap ? Math.Round(value * 2) / 2 : Math.Round(value, 3);
        e.X = Math.Clamp(SnapValue(x), 0, Math.Max(0, Template.Width - e.Width));
        e.Y = Math.Clamp(SnapValue(y), 0, Math.Max(0, Template.Height - e.Height));
    }
    private void TrackTemplate()
    {
        template.PropertyChanged += OnDesignProperty; template.Elements.CollectionChanged += OnCollection;
        foreach (var e in template.Elements) e.PropertyChanged += OnDesignProperty;
        previousDesign = JsonData.Serialize(template);
    }
    private void OnCollection(object? sender, NotifyCollectionChangedEventArgs args)
    {
        if (args.OldItems is not null) foreach (LabelElement e in args.OldItems) e.PropertyChanged -= OnDesignProperty;
        if (args.NewItems is not null) foreach (LabelElement e in args.NewItems) e.PropertyChanged += OnDesignProperty;
        CaptureChange();
    }
    private void OnDesignProperty(object? sender, PropertyChangedEventArgs args) => CaptureChange();
    private void CaptureChange()
    {
        if (!trackingSuppressed && !dragging)
        {
            var current = JsonData.Serialize(template);
            if (current != previousDesign) { if (undo.Count >= 100) undo.Clear(); undo.Push(previousDesign); previousDesign = current; redo.Clear(); }
        }
        DesignChanged?.Invoke(this, EventArgs.Empty); CommandManager.InvalidateRequerySuggested();
    }
    private void ReplaceTemplate(LabelTemplate value, bool remember)
    {
        if (remember) { undo.Push(JsonData.Serialize(template)); redo.Clear(); }
        template.PropertyChanged -= OnDesignProperty; template.Elements.CollectionChanged -= OnCollection;
        foreach (var e in template.Elements) e.PropertyChanged -= OnDesignProperty;
        template = value; TrackTemplate(); SelectedElement = null; Notify(nameof(Template)); DesignChanged?.Invoke(this, EventArgs.Empty);
    }
    private void Restore(Stack<string> from, Stack<string> to)
    {
        to.Push(JsonData.Serialize(template)); trackingSuppressed = true;
        try { ReplaceTemplate(JsonData.Deserialize<LabelTemplate>(from.Pop()), false); } finally { trackingSuppressed = false; }
    }
    private async Task SendDiagnostic(byte[] bytes, int labels)
    {
        Validation.Settings(Settings);
        var id = Guid.NewGuid().ToString("N"); var record = new PrintRecord(id, DateTimeOffset.Now, Settings.Printer, null, labels, "Pending", "EPL2 diagnostic");
        store.Record(record);
        int? startedId = null;
        try { int job = await Task.Run(() => spooler.Send(Settings.Printer, bytes, onJobStarted: id => { startedId = id; store.Record(record with { SpoolId = id }); })); store.Record(record with { SpoolId = job, State = "Sent" }); Status = T.Get("Submitted", job); }
        catch { store.Record(record with { SpoolId = startedId, State = "Failed" }); throw; }
        finally { ReloadHistory(); }
    }
    private async Task PreviewAndPrint(bool batch)
    {
        ui.EnsureValidInput();
        Validation.Range(Settings.DisplayScale, 0.5, 2, "DisplayScale");
        var label = Template.Clone(); var settings = JsonData.Clone(Settings);
        IReadOnlyList<(Product? Product, int Copies)> products = batch
            ? Products.Where(p => p.Selected).Select(p => ((Product?)p.Clone(), p.Copies)).ToList()
            : new List<(Product? Product, int Copies)> { (SelectedProduct?.Clone(), Copies) };
        if (products.Count == 0) throw new DomainException("NoProductsSelected");
        var preview = new PreviewWindow(label, settings, ui, products, Products.Select(p => p.Clone()).ToList(), batch) { Owner = Application.Current.MainWindow };
        if (preview.ShowDialog() != true) return;
        var images = preview.Images; var timestamp = preview.Timestamp;
        if (!batch && preview.ChosenProduct is { } chosen) SelectedProduct = Products.FirstOrDefault(p => p.Id == chosen.Id);
        await Task.Run(() => spooler.Require(settings.Printer, !settings.DriverMode));
        Settings.Printer = settings.Printer; Settings.DriverMode = settings.DriverMode;
        if (preview.ChosenPrinter is { } chosenPrinter && !Printers.Any(p => p.Name == chosenPrinter.Name)) Printers.Add(chosenPrinter);
        SelectedPrinter = Printers.FirstOrDefault(p => p.Name == settings.Printer);
        store.Save("settings", settings);
        // RAW submissions can stop between WritePrinter chunks. The synchronous driver transport
        // has no cancellable operation; its jobs are cancelled in the Windows queue instead.
        if (!settings.DriverMode) cancellation = new CancellationTokenSource();
        CommandManager.InvalidateRequerySuggested();
        if (settings.DriverMode)
        {
            var record = new PrintRecord(Guid.NewGuid().ToString("N"), timestamp, settings.Printer, null, images.Sum(p => p.Copies), "Pending", "Windows driver");
            store.Record(record);
            Status = T.Get("DriverSubmitting");
            try { await DriverPrinter.RunSta(() => DriverPrinter.Print(settings.Printer, label, images.Select(x => (x.Image, x.Copies)).ToList())); store.Record(record with { State = "Sent" }); Status = T.Get("SubmittedDriver"); }
            catch { store.Record(record with { State = "Failed" }); throw; }
            finally { ReloadHistory(); }
        }
        else
        {
            foreach (var item in images)
            {
                cancellation!.Token.ThrowIfCancellationRequested();
                var payload = Epl2.Raster(label, settings, item.Image.Packed, item.Image.Width, item.Image.Height, item.Copies);
                var record = new PrintRecord(Guid.NewGuid().ToString("N"), timestamp, settings.Printer, null, item.Copies, "Pending", item.Name);
                store.Record(record);
                int? startedId = null;
                try { int job = await Task.Run(() => spooler.Send(settings.Printer, payload, cancellation!.Token, id => { startedId = id; store.Record(record with { SpoolId = id }); })); store.Record(record with { SpoolId = job, State = "Sent" }); Status = T.Get("Submitted", job); }
                catch { store.Record(record with { SpoolId = startedId, State = "Failed" }); Status = T.Get("PartialSubmission"); throw; }
                finally { ReloadHistory(); }
            }
        }
    }
}
