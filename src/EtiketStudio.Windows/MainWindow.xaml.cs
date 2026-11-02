// SPDX-License-Identifier: GPL-3.0-only
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EtiketStudio.Core;
using EtiketStudio.Windows.ViewModels;

namespace EtiketStudio.Windows;

public partial class MainWindow : Window
{
    private readonly MainViewModel vm;
    private Point toolboxStart;
    public MainWindow(MainViewModel vm)
    {
        InitializeComponent(); this.vm = vm; DataContext = vm;
        Loaded += async (_, _) => await vm.InitializeAsync();
        Closing += OnClosing;
        SourceInitialized += (_, _) => Infrastructure.WindowSizing.Fit(this);
    }
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (vm.Restored) return;
        if (vm.Busy)
        {
            e.Cancel = true;
            MessageBox.Show(this, T.Get("BusyClose"), T.Get("AppName"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (!vm.ConfirmUnsavedProduct()) { e.Cancel = true; return; }
        try { vm.Persist(); }
        catch (Exception ex) { e.Cancel = !vm.ConfirmDiscard(ex); }
    }
    private void Toolbox_Down(object sender, MouseButtonEventArgs e) => toolboxStart = e.GetPosition(Toolbox);
    private void Toolbox_Move(object sender, MouseEventArgs e)
    {
        var p = e.GetPosition(Toolbox);
        if (e.LeftButton != MouseButtonState.Pressed || Toolbox.SelectedItem is not ElementKind kind || Math.Abs(p.X - toolboxStart.X) + Math.Abs(p.Y - toolboxStart.Y) < 8) return;
        DragDrop.DoDragDrop(Toolbox, kind, DragDropEffects.Copy);
    }
    private void Toolbox_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (Toolbox.SelectedItem is ElementKind kind) vm.AddElement.Execute(kind);
    }
}
