// SPDX-License-Identifier: GPL-3.0-only
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Validation = EtiketStudio.Core.Validation;
using EtiketStudio.Core;
using EtiketStudio.Windows.Infrastructure;
using EtiketStudio.Windows.Rendering;
using EtiketStudio.Windows.Printing;

namespace EtiketStudio.Windows.Views;

public partial class PreviewWindow : Window
{
    private readonly List<(LabelImage Image, int Copies, string Name)> images = [];
    private readonly LabelTemplate template;
    private readonly AppSettings settings;
    private readonly UiServices ui;
    private readonly IReadOnlyList<(Product? Product, int Copies)> jobs;
    private readonly bool batch;
    private readonly WindowsSpooler spooler = new();
    private readonly DateTimeOffset timestamp = DateTimeOffset.Now;
    private bool initialized, rendering, refreshing, checking, closed;
    private int index;
    private double zoom = 2;
    public IReadOnlyList<(LabelImage Image, int Copies, string Name)> Images => images;
    public PrinterInfo? ChosenPrinter => PrinterPicker.SelectedItem as PrinterInfo;
    public Product? ChosenProduct => ProductPicker.SelectedItem as Product;
    public DateTimeOffset Timestamp => timestamp;

    public PreviewWindow(LabelTemplate template, AppSettings settings, UiServices ui,
        IReadOnlyList<(Product? Product, int Copies)> jobs, IReadOnlyList<Product> products, bool batch)
    {
        this.template = template; this.settings = settings; this.ui = ui; this.jobs = jobs; this.batch = batch;
        InitializeComponent();
        ProductPanel.Visibility = batch ? Visibility.Collapsed : Visibility.Visible;
        ProductPicker.ItemsSource = products;
        ProductPicker.SelectedItem = products.FirstOrDefault(p => p.Id == jobs.FirstOrDefault().Product?.Id);
        DriverModeCheck.IsChecked = settings.DriverMode;
        initialized = true; UpdateControls();
        Loaded += async (_, _) => { await RenderAsync(); await RefreshPrintersAsync(); };
        Closing += (_, _) => closed = true;
        SourceInitialized += (_, _) => WindowSizing.Fit(this);
    }
    internal async Task RenderAsync()
    {
        if (!initialized || rendering || closed) return;
        rendering = true; images.Clear(); index = 0; RenderError.Text = ""; PreviewImage.Source = null; ItemName.Text = ""; UpdateControls();
        try
        {
            Validation.Template(template);
            var requests = batch ? jobs : new List<(Product? Product, int Copies)> { (ChosenProduct, jobs[0].Copies) };
            if (requests.Count == 0) throw new DomainException("NoProductsSelected");
            foreach (var request in requests) Validation.Copies(request.Copies);
            if (requests.Sum(p => (long)p.Copies) > 1000) throw new DomainException("BatchLimit");
            if ((long)Validation.Dots(template.Width) * Validation.Dots(template.Height) * 5 * requests.Count > 128_000_000) throw new DomainException("BatchMemory");
            var renderer = new LabelRenderer();
            foreach (var request in requests)
            {
                if (closed) { images.Clear(); return; }
                var product = PrintPreparation.ResolveProduct(template, request.Product);
                images.Add((renderer.Render(template, product, timestamp, settings.Reverse), request.Copies, product.Name));
                await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Background);
            }
            UpdateImage();
        }
        catch (Exception ex) { images.Clear(); ShowError(ex, RenderError); }
        finally { rendering = false; UpdateControls(); }
    }
    private async Task RefreshPrintersAsync()
    {
        if (refreshing || checking || closed) return;
        refreshing = true; UpdateControls();
        try
        {
            string remembered = (PrinterPicker.SelectedItem as PrinterInfo)?.Name ?? settings.Printer;
            var printers = await Task.Run(spooler.Printers);
            if (closed) return;
            PrinterPicker.ItemsSource = printers;
            PrinterPicker.SelectedItem = printers.FirstOrDefault(p => p.Name == remembered)
                ?? (string.IsNullOrEmpty(remembered) ? printers.FirstOrDefault(p => p.IsEpl2844) : null);
            PrinterHint.Text = printers.Count == 0 ? T.Get("PreviewNoPrinter") : PrinterPicker.SelectedItem is null ? T.Get("PreviewSelectPrinter") : "";
        }
        catch (Exception ex) { PrinterPicker.ItemsSource = null; ShowError(ex, PrinterHint); }
        finally { refreshing = false; UpdateControls(); }
    }
    private void UpdateControls()
    {
        if (!initialized || closed) return;
        bool available = !rendering && !refreshing && !checking;
        bool valid = images.Count > 0 && !rendering;
        ProductPicker.IsEnabled = available; PrinterPicker.IsEnabled = available;
        RefreshButton.IsEnabled = available; DriverModeCheck.IsEnabled = available;
        EplButton.IsEnabled = valid && available; PngButton.IsEnabled = valid && available;
        PrintButton.IsEnabled = valid && available && PrinterPicker.SelectedItem is PrinterInfo;
        PreviousButton.IsEnabled = valid && available && index > 0; NextButton.IsEnabled = valid && available && index < images.Count - 1;
        DriverHelp.Visibility = settings.DriverMode ? Visibility.Visible : Visibility.Collapsed;
        Summary.Text = T.Get("PrintSummary", jobs.Sum(j => j.Copies), jobs.Count, (PrinterPicker.SelectedItem as PrinterInfo)?.Name ?? T.Get("PreviewPrinterUnset"));
    }
    private void UpdateImage()
    {
        if (images.Count == 0) return;
        PreviewImage.Source = images[index].Image.Bitmap;
        double scale = 96 / 25.4 * zoom * settings.DisplayScale;
        PreviewImage.Width = template.Width * scale; PreviewImage.Height = template.Height * scale; Ruler.Width = 50 * scale;
        ItemName.Text = $"{index + 1} / {images.Count}  ·  {images[index].Name}  ·  {images[index].Copies} {T.Get("Copies")}";
        UpdateControls();
    }
    private void ShowError(Exception ex, TextBlock target) { ui.Log(ex); if (closed) return; target.Text = ex is DomainException ? ex.Message : T.Get("UnexpectedError"); }
    private async void Product_Changed(object sender, SelectionChangedEventArgs e) { if (initialized) await RenderAsync(); }
    private void Printer_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!initialized || closed) return;
        PrinterHint.Text = PrinterPicker.SelectedItem is null ? T.Get("PreviewSelectPrinter") : ""; UpdateControls();
    }
    private void Mode_Changed(object sender, RoutedEventArgs e) { if (initialized) { settings.DriverMode = DriverModeCheck.IsChecked == true; UpdateControls(); } }
    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshPrintersAsync();
    private void Previous_Click(object sender, RoutedEventArgs e) { index--; UpdateImage(); }
    private void Next_Click(object sender, RoutedEventArgs e) { index++; UpdateImage(); }
    private void Actual_Click(object sender, RoutedEventArgs e) { zoom = 1; UpdateImage(); }
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    private async void Print_Click(object sender, RoutedEventArgs e)
    {
        if (PrinterPicker.SelectedItem is not PrinterInfo printer || images.Count == 0 || checking) return;
        checking = true; UpdateControls();
        try
        {
            settings.Printer = printer.Name; Validation.Settings(settings);
            await Task.Run(() => spooler.Require(settings.Printer, !settings.DriverMode));
            if (closed) return;
            checking = false; DialogResult = true;
        }
        catch (Exception ex) { ShowError(ex, PrinterHint); }
        finally { checking = false; UpdateControls(); }
    }
    private void Epl_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Validation.Settings(settings);
            var path = ui.SaveFile("EPL2 (*.epl)|*.epl", "labels.epl"); if (path is null) return;
            // Build all payloads first; a validation error must not leave a partially exported batch.
            using var buffer = new MemoryStream();
            foreach (var item in images) buffer.Write(Epl2.Raster(template, settings, item.Image.Packed, item.Image.Width, item.Image.Height, item.Copies));
            File.WriteAllBytes(path, buffer.ToArray());
        }
        catch (Exception ex) { ui.Error(ex); }
    }
    private void Png_Click(object sender, RoutedEventArgs e)
    {
        try { var path = ui.SaveFile("PNG (*.png)|*.png", "preview.png"); if (path is not null) LabelRenderer.SavePng(images[index].Image.Bitmap, path); }
        catch (Exception ex) { ui.Error(ex); }
    }
}
