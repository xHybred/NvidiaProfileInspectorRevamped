using nvidiaProfileInspector.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace nvidiaProfileInspector.UI.Views.Dialogs
{
    /// <summary>
    /// Interaction logic for CommandPaletteDialog.xaml (Ctrl+K quick launcher).
    /// </summary>
    public partial class CommandPaletteDialog : Window
    {
        private readonly MainViewModel _viewModel;
        private readonly ObservableCollection<PaletteEntry> _results = new ObservableCollection<PaletteEntry>();
        private readonly List<PaletteEntry> _actions = new List<PaletteEntry>();

        public CommandPaletteDialog(MainViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;

            BuildActions();
            ResultsList.ItemsSource = _results;
            SearchBox.TextChanged += (_, __) => RefreshResults();
            SearchBox.PreviewKeyDown += SearchBox_PreviewKeyDown;
            Loaded += (_, __) =>
            {
                SearchBox.Focus();
                RefreshResults();
            };
        }

        private sealed class PaletteEntry
        {
            public string Kind { get; set; }
            public string Title { get; set; }
            public string Detail { get; set; }
            public string Keywords { get; set; }
            public Action Run { get; set; }
        }

        private void AddAction(string title, string detail, string keywords, Action run)
        {
            _actions.Add(new PaletteEntry { Kind = "Action", Title = title, Detail = detail, Keywords = keywords, Run = run });
        }

        private void BuildActions()
        {
            AddAction("Apply Changes", "Write all pending changes to the driver", "apply save write driver", () => _viewModel.ApplyCommand.Execute(null));
            AddAction("Reload all profiles", "Reload every profile from the driver", "reload refresh all", () => _viewModel.RefreshCommand.Execute(null));
            AddAction("Discard changes", "Discard unsaved changes to the current profile", "discard revert reset undo", () => _viewModel.RefreshCurrentProfileCommand.Execute(null));
            AddAction("Export profiles...", "Export profiles to a file", "export backup nip save file", () => _viewModel.RequestExport());
            AddAction("Import profiles...", "Import profiles from a file", "import restore nip open file", () => _viewModel.RequestImport());
            AddAction("Installed games...", "Browse installed games and open their profiles", "games installed steam epic library browse play", () => _viewModel.ShowInstalledGamesCommand.Execute(null));
            AddAction("New profile...", "Create a new driver profile", "new create profile", () => _viewModel.CreateProfileCommand.Execute(null));
            if (_viewModel.SelectedSetting != null)
                AddAction("Open Bit Editor", "Edit the selected setting as bits", "bit editor flags hex", () => _viewModel.OpenBitEditorCommand.Execute(null));
            AddAction("Check for updates", "Check GitHub releases for a newer version", "update check version", () => _viewModel.CheckUpdateCommand.Execute(null));
            AddAction("About", "Version, contributors and update channel", "about version contributors", () => _viewModel.ShowAboutCommand.Execute(null));
        }

        private void RefreshResults()
        {
            var query = (SearchBox.Text ?? "").Trim();
            _results.Clear();

            foreach (var a in _actions.Where(a => Matches(query, a.Title + " " + a.Detail + " " + a.Keywords)))
                _results.Add(a);

            if (_viewModel.ProfileNames != null)
            {
                foreach (var p in _viewModel.ProfileNames
                    .Where(p => Matches(query, p.ProfileName))
                    .Take(15))
                {
                    var name = p.ProfileName;
                    _results.Add(new PaletteEntry
                    {
                        Kind = "Profile",
                        Title = name,
                        Detail = p.ProfileTypeLabel,
                        Run = () => _viewModel.SelectProfile(name)
                    });
                }
            }

            if (_viewModel.Settings != null)
            {
                foreach (var s in _viewModel.Settings
                    .Where(s => s != null && Matches(query, (s.DisplayName ?? "") + " " + (s.SettingIdHex ?? "")))
                    .Take(30))
                {
                    var setting = s;
                    _results.Add(new PaletteEntry
                    {
                        Kind = "Setting",
                        Title = setting.DisplayName,
                        Detail = setting.SettingIdHex,
                        Run = () =>
                        {
                            _viewModel.FilterText = setting.DisplayName;
                            _viewModel.SelectedSetting = setting;
                        }
                    });
                }
            }

            if (_results.Count > 0)
                ResultsList.SelectedIndex = 0;
        }

        private static bool Matches(string query, string text)
        {
            if (string.IsNullOrWhiteSpace(query))
                return true;
            return (text ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void RunSelected()
        {
            if (ResultsList.SelectedItem is PaletteEntry entry)
            {
                Close();
                entry.Run();
            }
        }

        private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down && _results.Count > 0)
            {
                ResultsList.Focus();
                ResultsList.SelectedIndex = 0;
                if (ResultsList.ItemContainerGenerator.ContainerFromIndex(0) is ListBoxItem item)
                    item.Focus();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                RunSelected();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
        }

        private void ResultsList_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            RunSelected();
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !(e.OriginalSource is TextBox))
            {
                RunSelected();
                e.Handled = true;
            }
            base.OnPreviewKeyDown(e);
        }
    }
}
