using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using LibVLCSharp.Shared;
using Microsoft.Web.WebView2.Core;
using SportsMax.Services;

namespace SportsMax;

public partial class PipWindow : Window
{
    private LibVLC? _libVLC;
    private LibVLCSharp.Shared.MediaPlayer? _player;
    private readonly bool _useWeb;
    private readonly string _url;

    // Auto-hide del header de la PiP
    private readonly DispatcherTimer _pollTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly DispatcherTimer _hideTimer = new() { Interval = TimeSpan.FromSeconds(4) };
    private POINT _lastCursor;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out POINT lpPoint);

    public event Action<string>? ReturnRequested;

    public PipWindow(string url, string title, bool useWeb)
    {
        InitializeComponent();
        _url = url;
        _useWeb = useWeb;
        TitleLabel.Text = string.IsNullOrWhiteSpace(title) ? "SportsMax PiP" : "SportsMax · " + title;
        Loaded += PipWindow_Loaded;
        Closed += PipWindow_Closed;
        PositionBottomRight();
    }

    private async void PipWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_useWeb)
        {
            VideoView.Visibility = Visibility.Collapsed;
            WebPlayer.Visibility = Visibility.Visible;
            try
            {
                var userData = Path.Combine(App.LogDir, "WebView2Pip");
                Directory.CreateDirectory(userData);
                var env = await CoreWebView2Environment.CreateAsync(
                    null, userData, AdBlocker.CreateEnvironmentOptions());
                await WebPlayer.EnsureCoreWebView2Async(env);

                await AdBlocker.ApplyAsync(WebPlayer);

                WebPlayer.CoreWebView2.Navigate(_url);
            }
            catch (Exception ex)
            {
                App.Log($"[Pip web] {ex.Message}");
            }
        }
        else
        {
            VideoView.Visibility = Visibility.Visible;
            WebPlayer.Visibility = Visibility.Collapsed;
            try
            {
                Core.Initialize();
                _libVLC = new LibVLC("--no-video-title-show", "--network-caching=1500");
                _player = new LibVLCSharp.Shared.MediaPlayer(_libVLC);
                VideoView.MediaPlayer = _player;
                using var media = new Media(_libVLC, new Uri(_url));
                media.AddOption(":http-user-agent=Mozilla/5.0 SportsMax/1.0");
                media.AddOption(":network-caching=1500");
                _player.Play(media);
                _player.Volume = 80;
            }
            catch (Exception ex)
            {
                App.Log($"[Pip vlc] {ex.Message}");
            }
        }

        StartAutoHide();
    }

    // -- Auto-hide --

    private void StartAutoHide()
    {
        GetCursorPos(out _lastCursor);
        HeaderBar.Visibility = Visibility.Visible;
        _pollTimer.Tick -= Poll_Tick;
        _hideTimer.Tick -= Hide_Tick;
        _pollTimer.Tick += Poll_Tick;
        _hideTimer.Tick += Hide_Tick;
        _pollTimer.Start();
        _hideTimer.Stop();
        _hideTimer.Start();
    }

    private void StopAutoHide()
    {
        _pollTimer.Stop();
        _hideTimer.Stop();
        _pollTimer.Tick -= Poll_Tick;
        _hideTimer.Tick -= Hide_Tick;
    }

    private void Poll_Tick(object? sender, EventArgs e)
    {
        if (!GetCursorPos(out var p)) return;
        if (p.X == _lastCursor.X && p.Y == _lastCursor.Y) return;
        _lastCursor = p;

        // Solo mostrar el header si el mouse esta dentro de la ventana PiP
        if (!IsCursorInsideWindow(p)) return;

        if (HeaderBar.Visibility != Visibility.Visible)
            HeaderBar.Visibility = Visibility.Visible;
        _hideTimer.Stop();
        _hideTimer.Start();
    }

    private void Hide_Tick(object? sender, EventArgs e)
    {
        _hideTimer.Stop();
        if (HeaderBar.IsMouseOver)
        {
            _hideTimer.Start();
            return;
        }
        HeaderBar.Visibility = Visibility.Collapsed;
    }

    private bool IsCursorInsideWindow(POINT p)
    {
        // Coordenadas de la ventana en pixeles de pantalla
        var left = (int)Left;
        var top = (int)Top;
        var right = left + (int)ActualWidth;
        var bottom = top + (int)ActualHeight;
        return p.X >= left && p.X < right && p.Y >= top && p.Y < bottom;
    }

    // -- Botones / drag --

    private void Drag_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            try { DragMove(); } catch { /* ignore */ }
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Return_Click(object sender, RoutedEventArgs e)
    {
        ReturnRequested?.Invoke(_url);
        Close();
    }

    private void PipWindow_Closed(object? sender, EventArgs e)
    {
        StopAutoHide();
        try { _player?.Stop(); } catch { /* ignore */ }
        _player?.Dispose();
        _libVLC?.Dispose();
        try { WebPlayer?.Dispose(); } catch { /* ignore */ }
    }

    private void PositionBottomRight()
    {
        var area = SystemParameters.WorkArea;
        Left = area.Right - Width - 20;
        Top = area.Bottom - Height - 20;
    }
}
