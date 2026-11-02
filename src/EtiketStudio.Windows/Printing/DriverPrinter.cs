// SPDX-License-Identifier: GPL-3.0-only
using System.Printing;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Controls;
using EtiketStudio.Core;
using EtiketStudio.Windows.Rendering;

namespace EtiketStudio.Windows.Printing;

public static class DriverPrinter
{
    // Invoke on a dedicated STA worker. WPF/XPS uses the installed Windows driver, never RAW WritePrinter.
    public static void Print(string name, LabelTemplate template, IReadOnlyList<(LabelImage Image, int Copies)> labels)
    {
        using var server = new LocalPrintServer(); using var queue = server.GetPrintQueue(name);
        double width = template.Width * 96 / 25.4, height = template.Height * 96 / 25.4;
        var requested = queue.DefaultPrintTicket.Clone(); requested.PageMediaSize = new PageMediaSize(width, height);
        requested.PageOrientation = PageOrientation.Portrait; requested.PageScalingFactor = 100;
        requested.CopyCount = 1; requested.Duplexing = Duplexing.OneSided;
        var ticket = queue.MergeAndValidatePrintTicket(queue.DefaultPrintTicket, requested).ValidatedPrintTicket;
        var capabilities = queue.GetPrintCapabilities(ticket);
        var actual = ticket.PageMediaSize;
        if (actual?.Width is not double aw || actual.Height is not double ah || Math.Abs(aw - width) > 0.5 || Math.Abs(ah - height) > 0.5)
            throw new DomainException("DriverSize");
        var area = capabilities.PageImageableArea;
        if (area is null || area.OriginWidth > 0.5 || area.OriginHeight > 0.5 || area.ExtentWidth < width - 0.5 || area.ExtentHeight < height - 0.5)
            throw new DomainException("DriverSize");
        var document = new FixedDocument(); document.DocumentPaginator.PageSize = new Size(width, height);
        foreach (var item in labels) for (int copy = 0; copy < item.Copies; copy++)
        {
            var page = new FixedPage { Width = width, Height = height, Background = Brushes.White };
            var image = new Image { Source = item.Image.Bitmap, Width = width, Height = height, Stretch = Stretch.Fill };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
            FixedPage.SetLeft(image, 0); FixedPage.SetTop(image, 0); page.Children.Add(image);
            var content = new PageContent(); ((System.Windows.Markup.IAddChild)content).AddChild(page); document.Pages.Add(content);
        }
        queue.CurrentJobSettings.Description = "Zebra TLP 2844 Label Designer " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var writer = PrintQueue.CreateXpsDocumentWriter(queue); writer.Write(document.DocumentPaginator, ticket);
    }
    public static Task RunSta(Action action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() => { try { action(); completion.SetResult(); } catch (Exception ex) { completion.SetException(ex); } }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); return completion.Task;
    }
}
