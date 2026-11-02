// SPDX-License-Identifier: GPL-3.0-only
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using EtiketStudio.Core;
using EtiketStudio.Windows.Rendering;
using EtiketStudio.Windows.ViewModels;

namespace EtiketStudio.Windows.Views;

public sealed class LabelCanvas : FrameworkElement
{
    private readonly LabelRenderer renderer = new();
    private MainViewModel? vm;
    private bool moving;
    private Point start;
    private double startX, startY;
    public LabelCanvas()
    {
        Focusable = true; AllowDrop = true;
        DataContextChanged += (_, e) =>
        {
            if (vm is not null) { vm.DesignChanged -= Changed; vm.PropertyChanged -= PropertyChanged; }
            vm = e.NewValue as MainViewModel;
            if (vm is not null) { vm.DesignChanged += Changed; vm.PropertyChanged += PropertyChanged; }
            Resize();
        };
        Drop += (_, e) =>
        {
            if (vm is null || !e.Data.GetDataPresent(typeof(ElementKind))) return;
            var p = e.GetPosition(this); vm.Guard(() => vm.Add((ElementKind)e.Data.GetData(typeof(ElementKind)), p.X / Scale, p.Y / Scale)); e.Handled = true;
        };
        DragOver += (_, e) => { e.Effects = e.Data.GetDataPresent(typeof(ElementKind)) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; };
        LostMouseCapture += (_, _) => { if (moving) { moving = false; vm?.EndDrag(); } };
    }
    private double Scale => 96 / 25.4 * (vm?.Zoom ?? 1) * (double.IsFinite(vm?.Settings.DisplayScale ?? 1) ? Math.Clamp(vm?.Settings.DisplayScale ?? 1, 0.5, 2) : 1);
    private void Changed(object? sender, EventArgs e) => Resize();
    private void PropertyChanged(object? sender, PropertyChangedEventArgs e) { if (e.PropertyName == nameof(MainViewModel.SelectedElement)) InvalidateVisual(); }
    private void Resize()
    {
        if (vm is null) return;
        Width = Math.Clamp(double.IsFinite(vm.Template.Width) ? vm.Template.Width : 50, 1, 104) * Scale;
        Height = Math.Clamp(double.IsFinite(vm.Template.Height) ? vm.Template.Height : 30, 1, 279) * Scale;
        InvalidateMeasure(); InvalidateVisual();
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc); dc.DrawRectangle(Brushes.White, null, new Rect(RenderSize));
        if (vm is null) return;
        dc.PushClip(new RectangleGeometry(new Rect(RenderSize)));
        if (vm.Snap)
        {
            var grid = new Pen(new SolidColorBrush(Color.FromRgb(234, 239, 244)), 0.5);
            for (double x = 0; x <= Width / Scale; x += 5) dc.DrawLine(grid, new Point(x * Scale, 0), new Point(x * Scale, Height));
            for (double y = 0; y <= Height / Scale; y += 5) dc.DrawLine(grid, new Point(0, y * Scale), new Point(Width, y * Scale));
        }
        var timestamp = DateTimeOffset.Now;
        foreach (var element in vm.Template.Elements)
        {
            if (!double.IsFinite(element.X + element.Y + element.Width + element.Height) || element.Width <= 0 || element.Height <= 0) continue;
            var rect = new Rect(element.X * Scale, element.Y * Scale, element.Width * Scale, element.Height * Scale);
            try
            {
                var bitmap = renderer.RenderElement(element, vm.PreviewProduct, timestamp);
                dc.DrawImage(bitmap, rect);
            }
            catch (Exception ex)
            {
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(255, 235, 233)), new Pen(Brushes.IndianRed, 1), rect);
                var text = new FormattedText("! " + (ex is DomainException ? ex.Message : T.Get("InvalidFile")), Fields.Turkish, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 10, Brushes.DarkRed, 1) { MaxTextWidth = Math.Max(1, rect.Width), MaxTextHeight = Math.Max(1, rect.Height), Trimming = TextTrimming.CharacterEllipsis };
                dc.DrawText(text, rect.TopLeft);
            }
            if (element == vm.SelectedElement)
                dc.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromRgb(8, 127, 112)), 1.8) { DashStyle = DashStyles.Dash }, rect);
        }
        dc.Pop(); dc.DrawRectangle(null, new Pen(Brushes.SlateGray, 1), new Rect(RenderSize));
    }
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e); if (vm is null) return; Focus();
        var p = e.GetPosition(this);
        vm.SelectedElement = vm.Template.Elements.Reverse().FirstOrDefault(item => item.Width > 0 && item.Height > 0 && double.IsFinite(item.X + item.Y + item.Width + item.Height) && new Rect(item.X * Scale, item.Y * Scale, item.Width * Scale, item.Height * Scale).Contains(p));
        if (vm.SelectedElement is not null)
        {
            start = p; startX = vm.SelectedElement.X; startY = vm.SelectedElement.Y; moving = true; vm.BeginDrag(); CaptureMouse();
        }
        e.Handled = true;
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!moving || vm?.SelectedElement is null) return;
        var p = e.GetPosition(this); vm.Move(vm.SelectedElement, startX + (p.X - start.X) / Scale, startY + (p.Y - start.Y) / Scale);
    }
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e); if (!moving) return;
        moving = false; ReleaseMouseCapture(); vm?.EndDrag(); e.Handled = true;
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e); if (vm?.SelectedElement is null) return;
        if (e.Key == Key.Delete) { vm.DeleteElement.Execute(null); e.Handled = true; return; }
        double dx = e.Key == Key.Left ? -0.5 : e.Key == Key.Right ? 0.5 : 0;
        double dy = e.Key == Key.Up ? -0.5 : e.Key == Key.Down ? 0.5 : 0;
        if (dx != 0 || dy != 0) { vm.BeginDrag(); vm.Move(vm.SelectedElement, vm.SelectedElement.X + dx, vm.SelectedElement.Y + dy); vm.EndDrag(); e.Handled = true; }
    }
}
