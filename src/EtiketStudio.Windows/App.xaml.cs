// SPDX-License-Identifier: GPL-3.0-only
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Markup;
using EtiketStudio.Core;
using EtiketStudio.Windows.Infrastructure;
using EtiketStudio.Windows.ViewModels;

namespace EtiketStudio.Windows;

public partial class App : Application
{
    private Mutex? instanceMutex;
    private bool ownsMutex;
    protected override void OnExit(ExitEventArgs e)
    {
        if (ownsMutex) instanceMutex?.ReleaseMutex();
        instanceMutex?.Dispose(); base.OnExit(e);
    }
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EtiketStudio");
        Directory.CreateDirectory(folder); var ui = new UiServices(folder);
        try
        {
            if (e.Args.Contains("--self-test")) { ShutdownMode = ShutdownMode.OnExplicitShutdown; Shutdown(WindowsSelfTest.Run(e.Args)); return; }
            if (e.Args.Contains("--send-epl")) { Shutdown(CommandLinePrint.Run(e.Args)); return; }
            instanceMutex = new Mutex(false, "Local\\EtiketStudio-" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(folder))));
            try { ownsMutex = instanceMutex.WaitOne(0); } catch (AbandonedMutexException) { ownsMutex = true; }
            if (!ownsMutex) { MessageBox.Show(T.Get("AlreadyRunning"), "TBRK Label Studio"); Shutdown(); return; }
            var store = new Store(Path.Combine(folder, "etiketstudio.db")); var settings = store.Load("settings", () => new AppSettings());
            string language = LanguageCatalog.StartupLanguage(store);
            int languageOption = Array.IndexOf(e.Args, "--language");
            if (languageOption >= 0 && languageOption + 1 < e.Args.Length)
            {
                language = LanguageCatalog.Normalize(e.Args[languageOption + 1]);
                store.Save(LanguageCatalog.PreferenceKey, language);
            }
            settings.Language = language;
            UiLanguage.Apply(language);
            FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement), new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(language)));
            Theme.Apply(settings.DarkTheme);
            var vm = new MainViewModel(store, settings, ui); var window = new MainWindow(vm); MainWindow = window;
            DispatcherUnhandledException += (_, args) => { ui.Error(args.Exception); args.Handled = true; };
            window.Show();
            try { store.AutomaticBackup(Path.Combine(folder, "Backups"), DateOnly.FromDateTime(DateTime.Now)); }
            catch (Exception ex) { ui.Log(ex); MessageBox.Show(window, T.Get("AutoBackupFailed"), T.Get("AppName"), MessageBoxButton.OK, MessageBoxImage.Warning); }
        }
        catch (Exception ex)
        {
            ui.Log(ex); MessageBox.Show(ex is DomainException ? ex.Message : T.Get("UnexpectedError"), T.Get("Error"), MessageBoxButton.OK, MessageBoxImage.Error); Shutdown(1);
        }
    }
}
