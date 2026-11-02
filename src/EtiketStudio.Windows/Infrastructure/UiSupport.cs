// SPDX-License-Identifier: GPL-3.0-only
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using EtiketStudio.Core;
using Microsoft.Win32;

namespace EtiketStudio.Windows.Infrastructure;

public sealed class TextExtension(string key) : MarkupExtension
{
    public string Key { get; } = key;
    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding("[" + Key + "]") { Source = T.Catalog, Mode = BindingMode.OneWay }.ProvideValue(serviceProvider);
}
public static class UiLanguage
{
    public static LanguageCatalog Source => T.Catalog;
    public static void Apply(string language)
    {
        var culture = CultureInfo.GetCultureInfo(LanguageCatalog.Normalize(language));
        // Set the active UI thread explicitly, not only defaults used by new threads.
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        foreach (Window window in Application.Current.Windows) window.Language = XmlLanguage.GetLanguage(culture.Name);
        T.Catalog.Apply(culture.Name);
    }
}
public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type type, object parameter, CultureInfo culture) => value is not true;
    public object ConvertBack(object value, Type type, object parameter, CultureInfo culture) => value is not true;
}
public sealed class EnumNameConverter : IValueConverter, IMultiValueConverter
{
    public object Convert(object value, Type type, object parameter, CultureInfo culture) => value is null || value == DependencyProperty.UnsetValue ? "" : T.Get(value.ToString() ?? "");
    public object ConvertBack(object value, Type type, object parameter, CultureInfo culture) => Binding.DoNothing;
    public object Convert(object[] values, Type type, object parameter, CultureInfo culture) => Convert(values[0], type, parameter, culture);
    public object[] ConvertBack(object value, Type[] types, object parameter, CultureInfo culture) => types.Select(_ => Binding.DoNothing).ToArray();
}
public sealed class ImageBytesConverter : IValueConverter
{
    public object? Convert(object value, Type type, object parameter, CultureInfo culture)
    {
        if (value is not byte[] bytes) return null;
        try { return Rendering.LabelRenderer.LoadImage(bytes); } catch { return null; }
    }
    public object ConvertBack(object value, Type type, object parameter, CultureInfo culture) => Binding.DoNothing;
}
public sealed class ActionCommand(Action<object?> execute, Func<object?, bool>? canExecute = null) : ICommand
{
    public bool CanExecute(object? parameter) => canExecute?.Invoke(parameter) ?? true;
    public void Execute(object? parameter) => execute(parameter);
    public event EventHandler? CanExecuteChanged { add => CommandManager.RequerySuggested += value; remove => CommandManager.RequerySuggested -= value; }
}
public sealed class AsyncCommand(Func<Task> execute, Func<bool>? canExecute = null) : ICommand
{
    private bool running;
    public bool CanExecute(object? parameter) => !running && (canExecute?.Invoke() ?? true);
    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return;
        running = true; CommandManager.InvalidateRequerySuggested();
        try { await execute(); } finally { running = false; CommandManager.InvalidateRequerySuggested(); }
    }
    public event EventHandler? CanExecuteChanged { add => CommandManager.RequerySuggested += value; remove => CommandManager.RequerySuggested -= value; }
}
public sealed class UiServices
{
    public string DataFolder { get; }
    public UiServices(string folder) => DataFolder = folder;
    public void EnsureValidInput()
    {
        bool HasErrors(DependencyObject node)
        {
            if (System.Windows.Controls.Validation.GetHasError(node))
            {
                if (node is FrameworkElement field) { field.BringIntoView(); field.Focus(); }
                return true;
            }
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) if (HasErrors(VisualTreeHelper.GetChild(node, i))) return true;
            return false;
        }
        if (Application.Current.MainWindow is { } window && HasErrors(window)) throw new DomainException("InputErrors");
    }
    public string? OpenFile(string filter) { var dialog = new OpenFileDialog { Filter = filter }; return dialog.ShowDialog(Application.Current.MainWindow) == true ? dialog.FileName : null; }
    public string? SaveFile(string filter, string fileName) { var dialog = new SaveFileDialog { Filter = filter, FileName = fileName, AddExtension = true }; return dialog.ShowDialog(Application.Current.MainWindow) == true ? dialog.FileName : null; }
    public bool Confirm(string key) => MessageBox.Show(Application.Current.MainWindow, T.Get(key), T.Get("AppName"), MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    public bool ConfirmDiscard(Exception ex)
    {
        Log(ex);
        string detail = ex is DomainException ? ex.Message : T.Get("UnexpectedError");
        return MessageBox.Show(Application.Current.MainWindow, T.Get("DiscardOnClose", detail), T.Get("AppName"), MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes;
    }
    public void Error(Exception ex)
    {
        Log(ex);
        var message = ex is DomainException ? ex.Message : ex is System.Text.Json.JsonException ? T.Get("InvalidFile") : T.Get("UnexpectedError");
        MessageBox.Show(Application.Current.MainWindow, message, T.Get("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
    }
    public void Log(Exception ex)
    {
        try { File.AppendAllText(Path.Combine(DataFolder, "application.log"), $"{DateTimeOffset.Now:O} {ex}\n"); } catch { /* Never hide the original error when logging is unavailable. */ }
    }
    public static void OpenPath(string path) => Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
}
public static class Theme
{
    public static void Apply(bool dark)
    {
        var resources = Application.Current.Resources;
        void Brush(string key, string hex) => resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        Brush("BackgroundBrush", dark ? "#121820" : "#F3F6FA");
        Brush("PanelBrush", dark ? "#1C2633" : "#FFFFFF");
        Brush("ForegroundBrush", dark ? "#E7EDF5" : "#182536");
        Brush("MutedBrush", dark ? "#A5B4C7" : "#5C6E82");
        Brush("BorderBrush", dark ? "#364457" : "#DCE3EC");
        Brush("InputBrush", dark ? "#202E3E" : "#FFFFFF");
        Brush("AccentBrush", dark ? "#54CBB4" : "#087F70");
        Brush("SelectionBrush", dark ? "#284D51" : "#DDF3ED");
    }
}

public static class WindowSizing
{
    public static void Fit(Window window)
    {
        // WorkArea uses WPF logical units, so the window fits at larger Windows display scales.
        var area = SystemParameters.WorkArea;
        window.MinWidth = Math.Min(window.MinWidth, Math.Max(320, area.Width - 24));
        window.MinHeight = Math.Min(window.MinHeight, Math.Max(240, area.Height - 24));
        window.Width = Math.Max(window.MinWidth, Math.Min(window.Width, area.Width - 24));
        window.Height = Math.Max(window.MinHeight, Math.Min(window.Height, area.Height - 24));
    }
}
