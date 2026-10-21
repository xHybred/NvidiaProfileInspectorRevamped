using nvidiaProfileInspector.Services;
using nvidiaProfileInspector.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace nvidiaProfileInspector.UI.Views.Dialogs
{
    /// <summary>
    /// Interaction logic for InstalledGamesDialog.xaml
    /// </summary>
    public partial class InstalledGamesDialog : Window, INotifyPropertyChanged
    {
        private readonly MainViewModel _viewModel;
        private readonly InstalledGamesService _gamesService;
        private CancellationTokenSource _scanCts;
        private List<InstalledGameInfo> _allGames = new List<InstalledGameInfo>();

        private string _statusText = "Ready.";
        private bool _isScanning;
        private string _filterText = "";
        private bool _suppressOpen;
        private bool _showSteam = true;
        private bool _showEpic = true;
        private bool _showStartMenu = true;

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<GameTile> Tiles { get; } = new ObservableCollection<GameTile>();

        public string StatusText
        {
            get => _statusText;
            private set { _statusText = value; OnPropertyChanged(nameof(StatusText)); }
        }

        public bool IsScanning
        {
            get => _isScanning;
            private set
            {
                _isScanning = value;
                OnPropertyChanged(nameof(IsScanning));
                OnPropertyChanged(nameof(IsNotScanning));
            }
        }

        public bool IsNotScanning => !IsScanning;

        public bool ShowSteam
        {
            get => _showSteam;
            set { _showSteam = value; OnPropertyChanged(nameof(ShowSteam)); SaveDisabledSources(); RefreshTiles(); }
        }

        public bool ShowEpic
        {
            get => _showEpic;
            set { _showEpic = value; OnPropertyChanged(nameof(ShowEpic)); SaveDisabledSources(); RefreshTiles(); }
        }

        public bool ShowStartMenu
        {
            get => _showStartMenu;
            set { _showStartMenu = value; OnPropertyChanged(nameof(ShowStartMenu)); SaveDisabledSources(); RefreshTiles(); }
        }

        private bool _showGog = true;
        private bool _showEa = true;
        private bool _showUbisoft = true;

        public bool ShowGog
        {
            get => _showGog;
            set { _showGog = value; OnPropertyChanged(nameof(ShowGog)); SaveDisabledSources(); RefreshTiles(); }
        }

        public bool ShowEa
        {
            get => _showEa;
            set { _showEa = value; OnPropertyChanged(nameof(ShowEa)); SaveDisabledSources(); RefreshTiles(); }
        }

        public bool ShowUbisoft
        {
            get => _showUbisoft;
            set { _showUbisoft = value; OnPropertyChanged(nameof(ShowUbisoft)); SaveDisabledSources(); RefreshTiles(); }
        }

        private void SetSourceFlag(string source, bool enabled)
        {
            if (string.Equals(source, "Steam", StringComparison.OrdinalIgnoreCase)) _showSteam = enabled;
            else if (string.Equals(source, "Epic", StringComparison.OrdinalIgnoreCase)) _showEpic = enabled;
            else if (string.Equals(source, "Start Menu", StringComparison.OrdinalIgnoreCase)) _showStartMenu = enabled;
            else if (string.Equals(source, "GOG", StringComparison.OrdinalIgnoreCase)) _showGog = enabled;
            else if (string.Equals(source, "EA", StringComparison.OrdinalIgnoreCase)) _showEa = enabled;
            else if (string.Equals(source, "Ubisoft", StringComparison.OrdinalIgnoreCase)) _showUbisoft = enabled;
        }

        private void LoadDisabledSources()
        {
            try
            {
                var disabled = nvidiaProfileInspector.Common.Helper.UserSettings.LoadSettings()?.DisabledGameSources;
                if (disabled == null)
                    return;
                foreach (var source in disabled)
                    SetSourceFlag(source, false);
            }
            catch { }
        }

        private void SaveDisabledSources()
        {
            try
            {
                var disabled = new List<string>();
                if (!ShowSteam) disabled.Add("Steam");
                if (!ShowEpic) disabled.Add("Epic");
                if (!ShowStartMenu) disabled.Add("Start Menu");
                if (!ShowGog) disabled.Add("GOG");
                if (!ShowEa) disabled.Add("EA");
                if (!ShowUbisoft) disabled.Add("Ubisoft");

                var settings = nvidiaProfileInspector.Common.Helper.UserSettings.LoadSettings();
                settings.DisabledGameSources = disabled;
                settings.SaveSettings();
            }
            catch { }
        }

        public bool HasResults => Tiles.Count > 0;

        public int GameCount => Tiles.Count;

        public InstalledGamesDialog(MainViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _gamesService = App.Bootstrapper.Resolve<InstalledGamesService>();
            LoadDisabledSources();
            DataContext = this;

            FilterBox.TextChanged += (_, __) =>
            {
                _filterText = FilterBox.Text ?? "";
                RefreshTiles();
            };
            GamesList.PreviewKeyDown += GamesList_PreviewKeyDown;
            Loaded += InstalledGamesDialog_Loaded;
            Closed += (_, __) =>
            {
                CancelScan();
                SavePlacement();
            };
        }

        private void RestorePlacement()
        {
            try
            {
                var settings = nvidiaProfileInspector.Common.Helper.UserSettings.LoadSettings();
                if (settings.GamesWindowMaximized)
                {
                    WindowState = WindowState.Maximized;
                }
                else if (settings.GamesWindowWidth > 0 && settings.GamesWindowHeight > 0)
                {
                    var desired = new Rect(
                        settings.GamesWindowLeft,
                        settings.GamesWindowTop,
                        settings.GamesWindowWidth,
                        settings.GamesWindowHeight);

                    if (nvidiaProfileInspector.Common.Helper.WindowPlacementHelper.TryNormalizeBounds(
                            desired, new Size(MinWidth, MinHeight), out var bounds))
                    {
                        Left = bounds.Left;
                        Top = bounds.Top;
                        Width = bounds.Width;
                        Height = bounds.Height;
                    }
                }
            }
            catch { }
        }

        private void SavePlacement()
        {
            try
            {
                var settings = nvidiaProfileInspector.Common.Helper.UserSettings.LoadSettings();
                settings.GamesWindowMaximized = WindowState == WindowState.Maximized;

                var bounds = WindowState == WindowState.Normal
                    ? new Rect(Left, Top, Width, Height)
                    : RestoreBounds;

                if (nvidiaProfileInspector.Common.Helper.WindowPlacementHelper.TryNormalizeBounds(
                        bounds, new Size(MinWidth, MinHeight), out var normalizedBounds))
                {
                    bounds = normalizedBounds;
                }

                settings.GamesWindowLeft = bounds.Left;
                settings.GamesWindowTop = bounds.Top;
                settings.GamesWindowWidth = bounds.Width;
                settings.GamesWindowHeight = bounds.Height;
                settings.SaveSettings();
            }
            catch { }
        }

        private async void InstalledGamesDialog_Loaded(object sender, RoutedEventArgs e)
        {
            RestorePlacement();
            FilterBox.Focus();
            if (_gamesService.HasCachedResults)
            {
                _allGames = _gamesService.GetCachedResults();
                ApplyResults();
                StatusText = _allGames.Count == 0
                    ? "No games with driver profiles found. Rescan to try again."
                    : $"Found {_allGames.Count} games with driver profiles (cached).";
            }
            else if (_gamesService.TryRestorePersistedResults())
            {
                _allGames = _gamesService.GetCachedResults();
                ApplyResults();
                StatusText = _allGames.Count == 0
                    ? "No games with driver profiles found. Rescan to try again."
                    : $"Found {_allGames.Count} games with driver profiles (restored).";
            }
            else
            {
                await StartScanAsync();
            }
        }

        private async System.Threading.Tasks.Task StartScanAsync()
        {
            CancelScan();
            _scanCts = new CancellationTokenSource();
            var token = _scanCts.Token;

            IsScanning = true;
            Tiles.Clear();
            RaiseCounts();
            StatusText = "Scanning for installed games...";

            try
            {
                var progress = new Progress<string>(message => StatusText = message);
                _allGames = await _gamesService.ScanAsync(progress, token).ConfigureAwait(true);
                ApplyResults();
                StatusText = _allGames.Count == 0
                    ? "No games with driver profiles found."
                    : $"Found {_allGames.Count} games with driver profiles.";
            }
            catch (OperationCanceledException)
            {
                StatusText = "Scan cancelled.";
            }
            catch (Exception ex)
            {
                StatusText = "Scan failed: " + ex.Message;
            }
            finally
            {
                IsScanning = false;
            }
        }

        private void ApplyResults()
        {
            Tiles.Clear();
            foreach (var tile in FilterGamesCurrent())
                Tiles.Add(tile);
            RaiseCounts();

            if (Tiles.Count == 0 && !IsScanning)
            {
                if (!string.IsNullOrWhiteSpace(_filterText))
                    StatusText = $"No games match \"{_filterText.Trim()}\".";
                else if (_allGames.Count == 0)
                    StatusText = "No games with driver profiles found.";
            }
        }

        private void RefreshTiles()
        {
            if (IsScanning)
                return;
            ApplyResults();
        }

        private IEnumerable<GameTile> FilterGamesCurrent()
        {
            var query = (_filterText ?? "").Trim();
            foreach (var game in _allGames)
            {
                if (!IsSourceEnabled(game.Source))
                    continue;

                if (!string.IsNullOrWhiteSpace(query)
                    && (game.DisplayName ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0
                    && (game.ExeName ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0
                    && (game.ProfileName ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                yield return new GameTile(game);
            }
        }

        private bool IsSourceEnabled(string source)
        {
            if (string.Equals(source, "Steam", StringComparison.OrdinalIgnoreCase))
                return ShowSteam;
            if (string.Equals(source, "Epic", StringComparison.OrdinalIgnoreCase))
                return ShowEpic;
            if (string.Equals(source, "Start Menu", StringComparison.OrdinalIgnoreCase))
                return ShowStartMenu;
            if (string.Equals(source, "GOG", StringComparison.OrdinalIgnoreCase))
                return ShowGog;
            if (string.Equals(source, "EA", StringComparison.OrdinalIgnoreCase))
                return ShowEa;
            if (string.Equals(source, "Ubisoft", StringComparison.OrdinalIgnoreCase))
                return ShowUbisoft;
            return true;
        }

        private void RaiseCounts()
        {
            OnPropertyChanged(nameof(HasResults));
            OnPropertyChanged(nameof(GameCount));
        }

        private void OpenSelected()
        {
            if (GamesList.SelectedItem is GameTile tile)
            {
                _viewModel.SelectProfile(tile.ProfileName);
                Close();
            }
        }

        private void GamesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressOpen)
            {
                _suppressOpen = false;
                GamesList.SelectedItem = null;
                return;
            }

            if (e.AddedItems.Count > 0)
                OpenSelected();
        }

        private void HideTile_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _suppressOpen = true;
        }

        private void HideTile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is GameTile tile)
            {
                _gamesService.HideGame(tile.ExeName);
                _allGames.RemoveAll(g => string.Equals(g.ExeName, tile.ExeName, StringComparison.OrdinalIgnoreCase));
                GamesList.SelectedItem = null;
                RefreshTiles();
            }
        }

        private void GamesList_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                OpenSelected();
                e.Handled = true;
            }
        }

        private async void Rescan_Click(object sender, RoutedEventArgs e)
        {
            _gamesService.ClearCache();
            await StartScanAsync();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            CancelScan();
        }

        private void CancelScan()
        {
            try { _scanCts?.Cancel(); }
            catch { }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public sealed class GameTile
        {
            private readonly InstalledGameInfo _game;

            public GameTile(InstalledGameInfo game)
            {
                _game = game;
                Icon = InstalledGamesService.GetIcon(game.ExePath);
            }

            public string DisplayName => _game.DisplayName;
            public string ProfileName => _game.ProfileName;
            public string ExeName => _game.ExeName;
            public string Source => _game.Source;

            public string Caption => _game.ProfileCount > 1
                ? $"{_game.ExeName} • {_game.Source} • {_game.ProfileCount} profiles"
                : $"{_game.ExeName} • {_game.Source}";

            public ImageSource Icon { get; }
        }
    }
}
