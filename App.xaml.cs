using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace SportsMax;

public partial class App : Application
{
    public static string LogDir { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SportsMax");

    public static string LogFile => Path.Combine(LogDir, "sportsmax.log");

    public App()
    {
        try { Directory.CreateDirectory(LogDir); } catch { /* ignore */ }

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Log($"[FATAL] {e.ExceptionObject}");

        DispatcherUnhandledException += (_, e) =>
        {
            Log($"[UI] {e.Exception}");
            MessageBox.Show(
                "Ocurrio un error inesperado:\n\n" + e.Exception.Message,
                "SportsMax",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            e.Handled = true;
        };
    }

    public static void Log(string message)
    {
        try
        {
            File.AppendAllText(LogFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch { /* ignore */ }
    }
}
