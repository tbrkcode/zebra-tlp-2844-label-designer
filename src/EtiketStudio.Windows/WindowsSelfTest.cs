// SPDX-License-Identifier: GPL-3.0-only
using System.IO;
using EtiketStudio.Core;
using EtiketStudio.Windows.Rendering;
using EtiketStudio.Windows.Infrastructure;
using EtiketStudio.Windows.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using EtiketStudio.Windows.Views;

namespace EtiketStudio.Windows;

internal static class WindowsSelfTest
{
    // This entry point never prints. Physical acceptance is an explicit, separate operator procedure.
    public static int Run(string[] arguments)
    {
        int option = Array.IndexOf(arguments, "--output");
        string directory = option >= 0 && option + 1 < arguments.Length ? arguments[option + 1] : Path.Combine(Path.GetTempPath(), "EtiketStudio-self-test");
        Directory.CreateDirectory(directory);
        try
        {
            var renderer = new LabelRenderer(); var product = new Product { Sku = "TEST-001", Name = "Çığ şişe ölçü İı", Barcode = "123456789012", Price = 123.45m };
            var template = LabelTemplate.Default(); var time = new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.FromHours(3));
            var image = renderer.Render(template, product, time, false);
            if (image.Width != 400 || image.Height != 240 || image.Packed.Length != 12000) throw new Exception("Raster dimensions");
            LabelRenderer.SavePng(image.Bitmap, Path.Combine(directory, "turkish-code128.png"));
            File.WriteAllBytes(Path.Combine(directory, "turkish-code128.epl"), Epl2.Raster(template, new(), image.Packed, image.Width, image.Height, 1));
            template.Elements.Clear(); template.Elements.Add(new() { Kind = ElementKind.QrCode, Text = "çğıİöşü / Zebra TLP 2844 Label Designer", X = 3, Y = 3, Width = 24, Height = 24 });
            var qr = renderer.Render(template, product, time, false); LabelRenderer.SavePng(qr.Bitmap, Path.Combine(directory, "turkish-qr.png"));
            var reversed = renderer.Render(template, product, time, true);
            bool Pixel(byte[] bytes, int x, int y) => (bytes[y * 50 + x / 8] & (0x80 >> (x % 8))) == 0;
            for (int y = 0; y < 240; y++) for (int x = 0; x < 400; x++) if (Pixel(qr.Packed, x, y) != Pixel(reversed.Packed, 399 - x, 239 - y)) throw new Exception("Rotation mismatch");
            // Build and lay out every tab without Show()/Loaded, so the test never queries or prints to a device.
            var store = new Store(Path.Combine(directory, "ui-smoke.db")); store.Import([product]);
            var ui = new UiServices(directory); var vm = new MainViewModel(store, new(), ui); var window = new MainWindow(vm);
            TabControl? FindTabs(DependencyObject parent)
            {
                if (parent is TabControl tabs) return tabs;
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) { var found = FindTabs(VisualTreeHelper.GetChild(parent, i)); if (found is not null) return found; }
                return null;
            }
            window.ApplyTemplate(); window.Measure(new Size(1400, 900)); window.Arrange(new Rect(0, 0, 1400, 900));
            var tabs = FindTabs(window) ?? throw new Exception("Navigation tabs missing");
            foreach (var code in new[] { "tr-TR", "en-US" })
            {
                vm.Settings.Language = code;
                var firstTab = (TabItem)tabs.Items[0];
                System.Windows.Data.BindingOperations.GetBindingExpression(firstTab, HeaderedContentControl.HeaderProperty)?.UpdateTarget();
                if (!Equals(firstTab.Header, code == "en-US" ? "Products" : "Ürünler")) throw new Exception("Live navigation translation failed: " + code);
                if (store.Load(LanguageCatalog.PreferenceKey, () => "") != code) throw new Exception("Language persistence failed");
            }
            foreach (bool dark in new[] { false, true })
            {
                Theme.Apply(dark);
                for (int i = 0; i < tabs.Items.Count; i++) { tabs.SelectedIndex = i; window.UpdateLayout(); }
            }
            // Preview regression checks render real pixels but never enumerate or access printers.
            vm.SelectedProduct = null;
            if (!vm.PreviewSingle.CanExecute(null)) throw new Exception("Preview disabled without a product");
            void Complete(Task task)
            {
                if (!task.IsCompleted)
                {
                    var frame = new DispatcherFrame();
                    _ = task.ContinueWith(_ => Application.Current.Dispatcher.BeginInvoke(new Action(() => frame.Continue = false)));
                    Dispatcher.PushFrame(frame);
                }
                task.GetAwaiter().GetResult();
            }
            var staticLabel = new LabelTemplate { Elements = [new() { Text = "Static", Width = 40, Height = 8 }] };
            var staticPreview = new PreviewWindow(staticLabel, new(), ui, [(null, 1)], [], false);
            Complete(staticPreview.RenderAsync());
            if (staticPreview.Images.Count != 1 || ((Button)staticPreview.FindName("PrintButton")).IsEnabled) throw new Exception("Static preview / no-printer guard failed");
            LabelRenderer.SavePng(staticPreview.Images[0].Image.Bitmap, Path.Combine(directory, "static-without-product.png"));
            var missingPreview = new PreviewWindow(LabelTemplate.Default(), new(), ui, [(null, 1)], [], false);
            Complete(missingPreview.RenderAsync());
            if (missingPreview.Images.Count != 0 || ((Button)missingPreview.FindName("EplButton")).IsEnabled || string.IsNullOrEmpty(((TextBlock)missingPreview.FindName("RenderError")).Text)) throw new Exception("Missing product guard failed");
            var actualPreview = new PreviewWindow(LabelTemplate.Default(), new(), ui, [(product, 2)], [product], false);
            Complete(actualPreview.RenderAsync());
            if (actualPreview.Images.Count != 1 || actualPreview.Images[0].Copies != 2) throw new Exception("Product preview failed");
            foreach (var size in new[] { new Size(1093, 614), new Size(1280, 720), new Size(1400, 900) })
            {
                window.Measure(size); window.Arrange(new Rect(new Point(), size)); window.UpdateLayout();
                staticPreview.Measure(size); staticPreview.Arrange(new Rect(new Point(), size)); staticPreview.UpdateLayout();
            }
            staticPreview.Close(); missingPreview.Close(); actualPreview.Close();
            window.Close();
            File.WriteAllText(Path.Combine(directory, "result.txt"), "PASS: WPF Turkish text, Code 128/QR final-raster decode, exact 180-degree rotation, dimensions, EPL2 export six-tab layout in both themes, static/product preview and missing-product export guards.\nPhysical printer test: NOT RUN.\n");
            return 0;
        }
        catch (Exception ex) { File.WriteAllText(Path.Combine(directory, "result.txt"), "FAIL\n" + ex); return 1; }
    }
}
