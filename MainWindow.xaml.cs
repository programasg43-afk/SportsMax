using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using LibVLCSharp.Shared;
using Microsoft.Web.WebView2.Core;
using SportsMax.Models;
using SportsMax.Services;

namespace SportsMax;

public partial class MainWindow : Window
{
    private enum Engine { Auto, Vlc, Web }

    private readonly EventAggregator _aggregator = new();
    private readonly List<SportEvent> _allEvents = new();
    private SportEvent? _currentEvent;
    private CancellationTokenSource? _fetchCts;

    private LibVLC? _libVLC;
    private LibVLCSharp.Shared.MediaPlayer? _player;
    private bool _isMuted;
    private int _lastVolume = 80;
    private bool _webReady;
    private Engine _engine = Engine.Auto;
    private string? _lastPlayedUrl;
    private Button? _activeChannelBtn;
    private Border? _activeEventCard;

    private PipWindow? _pip;

    // Fullscreen state
    private bool _isFullscreen;
    private WindowStyle _savedWindowStyle;
    private WindowState _savedWindowState;
    private ResizeMode _savedResizeMode;
    private GridLength _savedSidebarWidth;
    private GridLength _savedSplitterWidth;
    private double _savedSidebarMinWidth;
    private double _savedPlayerMinWidth;
    private Thickness _savedMainGridMargin;
    private Thickness _savedPlayerInnerMargin;
    private CornerRadius _savedPlayerOuterCorner;
    private CornerRadius _savedVideoSurfaceCorner;

    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _statusTimer = new() { Interval = TimeSpan.FromSeconds(60) };

    // Auto-hide barra de fullscreen
    private readonly DispatcherTimer _fsPollTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly DispatcherTimer _fsHideTimer = new() { Interval = TimeSpan.FromSeconds(4) };
    private POINT _lastFsCursor;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out POINT lpPoint);

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
        Closed += MainWindow_Closed;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // ---- VLC ----
        try
        {
            Core.Initialize();
            _libVLC = new LibVLC("--no-video-title-show", "--network-caching=1500");
            _player = new LibVLCSharp.Shared.MediaPlayer(_libVLC);
            VideoView.MediaPlayer = _player;

            _player.Playing += (_, __) => Dispatcher.Invoke(() =>
            {
                PlayerStateLabel.Text = "Reproduciendo (VLC)";
                PlayerStateLabel.Foreground = (Brush)FindResource("OkGreen");
                PosterImage.Visibility = Visibility.Collapsed;
            });
            _player.Paused += (_, __) => Dispatcher.Invoke(() =>
                PlayerStateLabel.Text = "Pausado");
            _player.Stopped += (_, __) => Dispatcher.Invoke(() =>
            {
                PlayerStateLabel.Text = "Detenido";
                PlayerStateLabel.Foreground = (Brush)FindResource("FgMuted");
                if (WebPlayer.Visibility != Visibility.Visible)
                    PosterImage.Visibility = Visibility.Visible;
            });
            _player.EncounteredError += (_, __) => Dispatcher.Invoke(async () =>
            {
                PlayerStateLabel.Text = "VLC fallo, probando navegador...";
                PlayerStateLabel.Foreground = (Brush)FindResource("LiveRed");
                if (_engine == Engine.Auto && !string.IsNullOrEmpty(_lastPlayedUrl))
                    await PlayInWebViewAsync(_lastPlayedUrl!);
            });

            App.Log("[Init] LibVLC inicializado");
        }
        catch (Exception ex)
        {
            App.Log($"[Init] Error LibVLC: {ex.Message}");
        }

        // ---- WebView2 + AdBlocker ----
        try
        {
            var userData = Path.Combine(App.LogDir, "WebView2");
            Directory.CreateDirectory(userData);
            // Opciones de browser con autoplay habilitado
            var env = await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: userData,
                options: AdBlocker.CreateEnvironmentOptions());
            await WebPlayer.EnsureCoreWebView2Async(env);
            _webReady = true;

            await AdBlocker.ApplyAsync(WebPlayer);

            // Si la pagina pide fullscreen propio (boton fullscreen del player web), entramos en FS
            WebPlayer.CoreWebView2.ContainsFullScreenElementChanged += (_, __) =>
            {
                if (WebPlayer.CoreWebView2.ContainsFullScreenElement && !_isFullscreen)
                    ToggleFullscreen();
                else if (!WebPlayer.CoreWebView2.ContainsFullScreenElement && _isFullscreen)
                    ToggleFullscreen();
            };

            App.Log("[Init] WebView2 + AdBlocker activos");
        }
        catch (Exception ex)
        {
            App.Log($"[Init] Error WebView2: {ex.Message}");
        }

        InitFilters();
        InitEngineCombo();
        StartClock();
        StartStatusTimer();
        await ReloadEventsAsync();
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        try { _player?.Stop(); } catch { /* ignore */ }
        _player?.Dispose();
        _libVLC?.Dispose();
        try { WebPlayer?.Dispose(); } catch { /* ignore */ }
        try { _pip?.Close(); } catch { /* ignore */ }
        _clock.Stop();
        _statusTimer.Stop();
    }

    private void StartClock()
    {
        _clock.Tick += (_, __) => ClockLabel.Text = DateTime.Now.ToString("HH:mm:ss");
        _clock.Start();
        ClockLabel.Text = DateTime.Now.ToString("HH:mm:ss");
    }

    private void StartStatusTimer()
    {
        _statusTimer.Tick += (_, __) => ApplyFilters();
        _statusTimer.Start();
    }

    private void InitFilters()
    {
        CategoryCombo.Items.Clear();
        CategoryCombo.Items.Add(new ComboBoxItem { Content = "Todas las categorias", Tag = "*" });
        CategoryCombo.SelectedIndex = 0;

        StatusCombo.Items.Clear();
        StatusCombo.Items.Add(new ComboBoxItem { Content = "Todos los estados", Tag = "*" });
        StatusCombo.Items.Add(new ComboBoxItem { Content = "EN VIVO",          Tag = "live" });
        StatusCombo.Items.Add(new ComboBoxItem { Content = "Proximamente",     Tag = "soon" });
        StatusCombo.Items.Add(new ComboBoxItem { Content = "Finalizados",      Tag = "finished" });
        StatusCombo.SelectedIndex = 0;
    }

    private void InitEngineCombo()
    {
        EngineCombo.Items.Clear();
        EngineCombo.Items.Add(new ComboBoxItem { Content = "Automatico", Tag = "auto" });
        EngineCombo.Items.Add(new ComboBoxItem { Content = "Navegador",  Tag = "web" });
        EngineCombo.Items.Add(new ComboBoxItem { Content = "VLC",        Tag = "vlc" });
        EngineCombo.SelectedIndex = 0;
    }

    // =================== CARGA DE EVENTOS ===================

    private async void RefreshBtn_Click(object sender, RoutedEventArgs e) =>
        await ReloadEventsAsync();

    private async Task ReloadEventsAsync()
    {
        _fetchCts?.Cancel();
        _fetchCts = new CancellationTokenSource();
        var ct = _fetchCts.Token;

        StatusBar.Text = "Cargando eventos...";
        RefreshBtn.IsEnabled = false;

        var progress = new Progress<(string source, int count, bool error)>(_ =>
        {
            StatusBar.Text = "Cargando agenda de eventos...";
        });

        try
        {
            var list = await _aggregator.FetchAllAsync(progress, ct);
            _allEvents.Clear();
            _allEvents.AddRange(list);
            RebuildCategoryFilter();
            ApplyFilters();
            var visible = _allEvents.Count(e => !e.ShouldHide);
            StatusBar.Text = $"Cargados {visible} eventos vigentes ({_allEvents.Count - visible} antiguos ocultos) · TZ local";
        }
        catch (OperationCanceledException)
        {
            StatusBar.Text = "Carga cancelada";
        }
        catch (Exception ex)
        {
            App.Log($"[Reload] {ex.Message}");
            StatusBar.Text = "Error al cargar: " + ex.Message;
        }
        finally
        {
            RefreshBtn.IsEnabled = true;
        }
    }

    private void RebuildCategoryFilter()
    {
        var selectedTag = (CategoryCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "*";
        CategoryCombo.Items.Clear();
        CategoryCombo.Items.Add(new ComboBoxItem { Content = "Todas las categorias", Tag = "*" });
        var cats = _allEvents
            .Where(e => !e.ShouldHide)
            .Select(e => e.DisplayCategory)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s);
        foreach (var c in cats)
            CategoryCombo.Items.Add(new ComboBoxItem { Content = c, Tag = c });

        for (int i = 0; i < CategoryCombo.Items.Count; i++)
        {
            if ((CategoryCombo.Items[i] as ComboBoxItem)?.Tag?.ToString() == selectedTag)
            {
                CategoryCombo.SelectedIndex = i;
                return;
            }
        }
        CategoryCombo.SelectedIndex = 0;
    }

    // =================== FILTROS Y RENDER ===================

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilters();
    private void Filter_Changed(object sender, SelectionChangedEventArgs e) => ApplyFilters();

    private void ApplyFilters()
    {
        var query = (SearchBox.Text ?? string.Empty).Trim();
        var cat = (CategoryCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "*";
        var st = (StatusCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "*";

        IEnumerable<SportEvent> view = _allEvents.Where(e => !e.ShouldHide);

        if (cat != "*")
            view = view.Where(e => string.Equals(e.DisplayCategory, cat, StringComparison.OrdinalIgnoreCase));

        if (st != "*")
        {
            view = st switch
            {
                "live"     => view.Where(e => e.Status == EventStatus.Live),
                "soon"     => view.Where(e => e.Status == EventStatus.Soon),
                "finished" => view.Where(e => e.Status == EventStatus.Finished),
                _ => view
            };
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            view = view.Where(e =>
                e.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                e.DisplayCategory.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                e.Links.Any(l => l.Name.Contains(query, StringComparison.OrdinalIgnoreCase)));
        }

        var list = view.ToList();
        RenderEvents(list);

        TotalCount.Text = list.Count.ToString();
        LiveCount.Text = list.Count(e => e.Status == EventStatus.Live).ToString();
        SoonCount.Text = list.Count(e => e.Status == EventStatus.Soon).ToString();
    }

    private void RenderEvents(List<SportEvent> events)
    {
        EventsPanel.Children.Clear();
        _activeEventCard = null;   // se reasigna en BuildEventCard si el evento activo sigue visible

        if (events.Count == 0)
        {
            EventsPanel.Children.Add(new TextBlock
            {
                Text = "Sin eventos para los filtros actuales.",
                Foreground = (Brush)FindResource("FgMuted"),
                Margin = new Thickness(8, 16, 8, 16),
                HorizontalAlignment = HorizontalAlignment.Center,
                FontSize = 12
            });
            return;
        }

        foreach (var ev in events)
            EventsPanel.Children.Add(BuildEventCard(ev));
    }

    private Border BuildEventCard(SportEvent ev)
    {
        var card = new Border
        {
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 0, 0, 6),
            BorderThickness = new Thickness(2),
            Cursor = Cursors.Hand,
            Tag = ev
        };

        // Resalta la tarjeta si corresponde al evento seleccionado
        var isActive = _currentEvent != null && ReferenceEquals(ev, _currentEvent);
        ApplyEventCardStyle(card, isActive);
        if (isActive) _activeEventCard = card;

        card.MouseLeftButtonUp += (_, __) => { SetActiveEventCard(card); SelectEvent(ev); };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(54) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var timePanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        timePanel.Children.Add(new TextBlock
        {
            Text = ev.DisplayTime,
            FontSize = 16, FontWeight = FontWeights.Bold,
            Foreground = (Brush)FindResource("AccentSoft"),
            HorizontalAlignment = HorizontalAlignment.Center
        });
        timePanel.Children.Add(new TextBlock
        {
            Text = ev.DisplayCategory,
            FontSize = 9, Foreground = (Brush)FindResource("FgMuted"),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 50
        });
        Grid.SetColumn(timePanel, 0);
        grid.Children.Add(timePanel);

        var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 10, 0) };
        info.Children.Add(new TextBlock
        {
            Text = ev.Title,
            FontSize = 13, FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("FgPrimary"),
            TextWrapping = TextWrapping.Wrap,
            MaxHeight = 38, TextTrimming = TextTrimming.CharacterEllipsis
        });
        var meta = string.IsNullOrEmpty(ev.Language)
            ? $"{ev.Links.Count} canal(es)"
            : $"{ev.Links.Count} canal(es) · {ev.Language}";
        info.Children.Add(new TextBlock
        {
            Text = meta,
            FontSize = 10, Foreground = (Brush)FindResource("FgMuted"),
            Margin = new Thickness(0, 2, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        Grid.SetColumn(info, 1);
        grid.Children.Add(info);

        var badgeBrush = ev.Status switch
        {
            EventStatus.Live     => (Brush)FindResource("LiveRed"),
            EventStatus.Soon     => (Brush)FindResource("SoonOrange"),
            EventStatus.Finished => (Brush)FindResource("FgMuted"),
            _ => (Brush)FindResource("AccentDark")
        };
        var badgeText = ev.Status switch
        {
            EventStatus.Live     => "● EN VIVO",
            EventStatus.Soon     => "PRONTO",
            EventStatus.Finished => "FIN",
            _ => "PROG"
        };
        var badge = new Border
        {
            Background = badgeBrush,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 2, 6, 2),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = badgeText,
                FontSize = 9, FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            }
        };
        Grid.SetColumn(badge, 2);
        grid.Children.Add(badge);

        card.Child = grid;
        return card;
    }

    private void ApplyEventCardStyle(Border card, bool active)
    {
        // Grosor de borde constante (2) para que no salte el layout; solo cambia el color
        card.BorderThickness = new Thickness(2);
        if (active)
        {
            card.Background = (Brush)FindResource("BgCardAlt");
            card.BorderBrush = (Brush)FindResource("Accent");
        }
        else
        {
            card.Background = (Brush)FindResource("BgCard");
            card.BorderBrush = System.Windows.Media.Brushes.Transparent;
        }
    }

    private void SetActiveEventCard(Border card)
    {
        if (_activeEventCard != null && !ReferenceEquals(_activeEventCard, card))
            ApplyEventCardStyle(_activeEventCard, false);
        ApplyEventCardStyle(card, true);
        _activeEventCard = card;
    }

    // =================== SELECCION / PLAYER ===================

    private void SelectEvent(SportEvent ev)
    {
        _currentEvent = ev;
        PlayerTitle.Text = ev.Title;
        PlayerMeta.Text = $"{ev.DisplayCategory} · {ev.DisplayTime}";

        BuildChannelButtons(ev);

        StatusBar.Text = $"Seleccionado: {ev.Title}";
    }

    private void BuildChannelButtons(SportEvent ev)
    {
        ChannelButtonsPanel.Children.Clear();
        _activeChannelBtn = null;

        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < ev.Links.Count; i++)
        {
            var link = ev.Links[i];
            var name = string.IsNullOrWhiteSpace(link.Name) ? $"Canal {i + 1}" : link.Name.Trim();
            // Evita botones con nombre identico (antes se distinguian por la fuente)
            if (seen.TryGetValue(name, out var c)) { seen[name] = c + 1; name = $"{name} {c + 1}"; }
            else seen[name] = 1;

            var btn = new Button
            {
                Content = name,
                Style = (Style)FindResource("ChannelBtn"),
                Margin = new Thickness(0, 0, 6, 6),
                CommandParameter = link.Url,   // URL aqui (Tag se usa para marcar el activo)
                ToolTip = "Reproducir " + name
            };
            btn.Click += ChannelButton_Click;
            ChannelButtonsPanel.Children.Add(btn);
        }

        // Se posiciona en el primer canal (lo carga) pero SIN forzar autoplay.
        // El usuario pulsa play en el reproductor cuando quiera.
        if (ChannelButtonsPanel.Children.Count > 0 &&
            ChannelButtonsPanel.Children[0] is Button first)
        {
            SetActiveChannel(first);
            PlayUrl((string)first.CommandParameter);
        }
        else
        {
            StopAllPlayback();
            _lastPlayedUrl = null;
            PosterImage.Visibility = Visibility.Visible;
            PlayerStateLabel.Text = "Sin canales disponibles";
            PlayerStateLabel.Foreground = (Brush)FindResource("FgMuted");
        }
    }

    private void ChannelButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        SetActiveChannel(btn);
        var url = btn.CommandParameter as string;
        if (!string.IsNullOrWhiteSpace(url))
            PlayUrl(url);
    }

    private void SetActiveChannel(Button btn)
    {
        if (_activeChannelBtn != null)
            _activeChannelBtn.Tag = null;
        btn.Tag = "ACTIVE";
        _activeChannelBtn = btn;
    }

    private void EngineCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var tag = (EngineCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "auto";
        _engine = tag switch
        {
            "vlc" => Engine.Vlc,
            "web" => Engine.Web,
            _ => Engine.Auto
        };
        if (!string.IsNullOrEmpty(_lastPlayedUrl))
            PlayUrl(_lastPlayedUrl!);
    }

    private async void PlayUrl(string url)
    {
        _lastPlayedUrl = url;
        StopAllPlayback();

        var useWeb = _engine switch
        {
            Engine.Web => true,
            Engine.Vlc => false,
            _ => IsWrapperPage(url)
        };

        if (useWeb)
            await PlayInWebViewAsync(url);
        else
            PlayInVlc(url);
    }

    private static bool IsWrapperPage(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        var lower = url.ToLowerInvariant();
        if (lower.Contains(".m3u8") || lower.Contains(".ts?") ||
            lower.EndsWith(".mp4") || lower.EndsWith(".mkv") ||
            lower.StartsWith("rtmp://") || lower.StartsWith("rtsp://"))
            return false;
        return true;
    }

    private void PlayInVlc(string url)
    {
        if (_player == null || _libVLC == null) return;
        try
        {
            VideoView.Visibility = Visibility.Visible;
            WebPlayer.Visibility = Visibility.Collapsed;

            using var media = new Media(_libVLC, new Uri(url));
            media.AddOption(":http-user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 SportsMax/1.0");
            media.AddOption(":network-caching=1500");
            _player.Play(media);
            _player.Volume = (int)VolumeSlider.Value;
            PlayerStateLabel.Text = "Conectando VLC...";
            PlayerStateLabel.Foreground = (Brush)FindResource("AccentSoft");
            App.Log($"[Play VLC] {url}");
        }
        catch (Exception ex)
        {
            App.Log($"[Play VLC:error] {ex.Message}");
        }
    }

    private async Task PlayInWebViewAsync(string url)
    {
        if (!_webReady || WebPlayer.CoreWebView2 == null) return;

        try
        {
            WebPlayer.Visibility = Visibility.Visible;
            VideoView.Visibility = Visibility.Collapsed;
            PosterImage.Visibility = Visibility.Collapsed;
            PlayerStateLabel.Text = "Cargando pagina...";
            PlayerStateLabel.Foreground = (Brush)FindResource("AccentSoft");

            WebPlayer.CoreWebView2.Navigate(url);
            WebPlayer.CoreWebView2.NavigationCompleted -= OnWebNavigationCompleted;
            WebPlayer.CoreWebView2.NavigationCompleted += OnWebNavigationCompleted;
            await Task.Yield();
            App.Log($"[Play Web] {url}");
        }
        catch (Exception ex)
        {
            App.Log($"[Play Web:error] {ex.Message}");
        }
    }

    private void OnWebNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        // Sin autoplay forzado: la pagina queda lista; el usuario pulsa play si no inicia sola
        PlayerStateLabel.Text = "Canal cargado · pulsa ▶ si no inicia";
        PlayerStateLabel.Foreground = (Brush)FindResource("AccentSoft");
        // Sincroniza el volumen actual del slider con el video recien cargado
        ApplyWebVolume((int)VolumeSlider.Value);
    }

    private void StopAllPlayback()
    {
        try { _player?.Stop(); } catch { /* ignore */ }
        try
        {
            if (_webReady && WebPlayer.CoreWebView2 != null)
                WebPlayer.CoreWebView2.Navigate("about:blank");
        }
        catch { /* ignore */ }
    }

    private void StopBtn_Click(object sender, RoutedEventArgs e)
    {
        StopAllPlayback();
        PlayerStateLabel.Text = "Detenido";
        PlayerStateLabel.Foreground = (Brush)FindResource("FgMuted");
        VideoView.Visibility = Visibility.Visible;
        WebPlayer.Visibility = Visibility.Collapsed;
        PosterImage.Visibility = Visibility.Visible;
    }

    // =================== FULLSCREEN ===================

    private void FullscreenBtn_Click(object sender, RoutedEventArgs e) => ToggleFullscreen();

    private void ToggleFullscreen()
    {
        if (!_isFullscreen)
            EnterFullscreen();
        else
            ExitFullscreen();
    }

    private void EnterFullscreen()
    {
        // Guarda estado original
        _savedWindowStyle = WindowStyle;
        _savedWindowState = WindowState;
        _savedResizeMode = ResizeMode;
        _savedSidebarWidth = SidebarCol.Width;
        _savedSplitterWidth = SplitterCol.Width;
        _savedSidebarMinWidth = SidebarCol.MinWidth;
        _savedPlayerMinWidth = PlayerCol.MinWidth;
        _savedMainGridMargin = MainGrid.Margin;
        _savedPlayerInnerMargin = PlayerInnerDock.Margin;
        _savedPlayerOuterCorner = PlayerOuterBorder.CornerRadius;
        _savedVideoSurfaceCorner = VideoSurfaceBorder.CornerRadius;

        // Oculta chrome
        HeaderBorder.Visibility = Visibility.Collapsed;
        StatusBarBorder.Visibility = Visibility.Collapsed;
        SidebarBorder.Visibility = Visibility.Collapsed;
        SplitterCtl.Visibility = Visibility.Collapsed;
        PlayerHeader.Visibility = Visibility.Collapsed;
        ChannelBar.Visibility = Visibility.Collapsed;
        ControlBar.Visibility = Visibility.Collapsed;

        // ZERO de MinWidth y Width para que la sidebar realmente colapse
        SidebarCol.MinWidth = 0;
        SidebarCol.Width = new GridLength(0);
        SplitterCol.Width = new GridLength(0);
        PlayerCol.MinWidth = 0;

        // Quita margenes y radios para que el video llene la pantalla
        MainGrid.Margin = new Thickness(0);
        PlayerInnerDock.Margin = new Thickness(0);
        PlayerOuterBorder.CornerRadius = new CornerRadius(0);
        VideoSurfaceBorder.CornerRadius = new CornerRadius(0);

        // Muestra la barra de salida en la parte superior (fuera del area de video)
        FsExitBar.Visibility = Visibility.Visible;

        // Ventana sin marco, fullscreen real (cubre taskbar)
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        if (WindowState == WindowState.Maximized) WindowState = WindowState.Normal;
        WindowState = WindowState.Maximized;

        _isFullscreen = true;
        StartFsAutoHide();
    }

    private void ExitFullscreen()
    {
        StopFsAutoHide();

        WindowStyle = _savedWindowStyle;
        ResizeMode = _savedResizeMode;
        WindowState = _savedWindowState;

        HeaderBorder.Visibility = Visibility.Visible;
        StatusBarBorder.Visibility = Visibility.Visible;
        SidebarBorder.Visibility = Visibility.Visible;
        SplitterCtl.Visibility = Visibility.Visible;
        PlayerHeader.Visibility = Visibility.Visible;
        ChannelBar.Visibility = Visibility.Visible;
        ControlBar.Visibility = Visibility.Visible;
        FsExitBar.Visibility = Visibility.Collapsed;

        SidebarCol.MinWidth = _savedSidebarMinWidth;
        SidebarCol.Width = _savedSidebarWidth;
        SplitterCol.Width = _savedSplitterWidth;
        PlayerCol.MinWidth = _savedPlayerMinWidth;

        MainGrid.Margin = _savedMainGridMargin;
        PlayerInnerDock.Margin = _savedPlayerInnerMargin;
        PlayerOuterBorder.CornerRadius = _savedPlayerOuterCorner;
        VideoSurfaceBorder.CornerRadius = _savedVideoSurfaceCorner;

        _isFullscreen = false;
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _isFullscreen)
        {
            ExitFullscreen();
            e.Handled = true;
        }
        else if (e.Key == Key.F11)
        {
            ToggleFullscreen();
            e.Handled = true;
        }
    }

    // -- Auto-hide de la barra superior en fullscreen --

    private void StartFsAutoHide()
    {
        GetCursorPos(out _lastFsCursor);
        FsExitBar.Visibility = Visibility.Visible;
        Mouse.OverrideCursor = null;

        _fsPollTimer.Tick -= FsPoll_Tick;
        _fsHideTimer.Tick -= FsHide_Tick;
        _fsPollTimer.Tick += FsPoll_Tick;
        _fsHideTimer.Tick += FsHide_Tick;
        _fsPollTimer.Start();
        _fsHideTimer.Stop();
        _fsHideTimer.Start();
    }

    private void StopFsAutoHide()
    {
        _fsPollTimer.Stop();
        _fsHideTimer.Stop();
        _fsPollTimer.Tick -= FsPoll_Tick;
        _fsHideTimer.Tick -= FsHide_Tick;
        FsExitBar.Visibility = Visibility.Collapsed;
        Mouse.OverrideCursor = null;
    }

    private void FsPoll_Tick(object? sender, EventArgs e)
    {
        if (!GetCursorPos(out var p)) return;
        if (p.X == _lastFsCursor.X && p.Y == _lastFsCursor.Y) return;
        _lastFsCursor = p;

        if (FsExitBar.Visibility != Visibility.Visible)
            FsExitBar.Visibility = Visibility.Visible;
        Mouse.OverrideCursor = null;
        _fsHideTimer.Stop();
        _fsHideTimer.Start();
    }

    private void FsHide_Tick(object? sender, EventArgs e)
    {
        _fsHideTimer.Stop();
        // Si el mouse esta sobre la barra, no la ocultes
        if (FsExitBar.IsMouseOver)
        {
            _fsHideTimer.Start();
            return;
        }
        FsExitBar.Visibility = Visibility.Collapsed;
        Mouse.OverrideCursor = Cursors.None;
    }

    // =================== PiP ===================

    private void PipBtn_Click(object sender, RoutedEventArgs e)
    {
        var url = _lastPlayedUrl
            ?? _activeChannelBtn?.CommandParameter as string;
        if (string.IsNullOrWhiteSpace(url))
        {
            MessageBox.Show("Selecciona un evento primero.", "SportsMax",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (_pip != null)
        {
            _pip.Activate();
            return;
        }

        var useWeb = _engine switch
        {
            Engine.Web => true,
            Engine.Vlc => false,
            _ => IsWrapperPage(url)
        };

        StopAllPlayback();
        PlayerStateLabel.Text = "Reproduciendo en ventana flotante";
        PlayerStateLabel.Foreground = (Brush)FindResource("AccentSoft");

        _pip = new PipWindow(url, _currentEvent?.Title ?? "", useWeb);
        _pip.Closed += (_, __) =>
        {
            _pip = null;
            PlayerStateLabel.Text = "Detenido";
            PlayerStateLabel.Foreground = (Brush)FindResource("FgMuted");
        };
        _pip.ReturnRequested += retUrl =>
        {
            PlayUrl(retUrl);
        };
        _pip.Show();
    }

    // =================== VOLUMEN / MUTE ===================

    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        var vol = (int)e.NewValue;
        if (_player != null) _player.Volume = vol;   // VLC
        ApplyWebVolume(vol);                          // WebView2
        if (!_isMuted) _lastVolume = vol;
        MuteIcon.Text = (vol == 0 || _isMuted) ? "🔇" : "🔊";
    }

    private async void ApplyWebVolume(int vol)
    {
        if (!_webReady || WebPlayer.CoreWebView2 == null) return;
        try
        {
            var v = Math.Clamp(vol, 0, 100) / 100.0;
            var js = "window.__smSetVolume && window.__smSetVolume(" +
                     v.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")";
            await WebPlayer.CoreWebView2.ExecuteScriptAsync(js);
        }
        catch { /* ignore */ }
    }

    private void MuteIcon_Click(object sender, MouseButtonEventArgs e)
    {
        _isMuted = !_isMuted;
        if (_isMuted)
        {
            _lastVolume = (int)VolumeSlider.Value;
            VolumeSlider.Value = 0;
            MuteIcon.Text = "🔇";
        }
        else
        {
            VolumeSlider.Value = _lastVolume == 0 ? 80 : _lastVolume;
            MuteIcon.Text = "🔊";
        }
    }
}
