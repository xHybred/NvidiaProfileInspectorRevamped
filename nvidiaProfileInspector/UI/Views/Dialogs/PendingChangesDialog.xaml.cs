using nvidiaProfileInspector.UI.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace nvidiaProfileInspector.UI.Views.Dialogs
{
    /// <summary>
    /// Interaction logic for PendingChangesDialog.xaml
    /// </summary>
    public partial class PendingChangesDialog : Window
    {
        private readonly MainViewModel _viewModel;

        public PendingChangesDialog(MainViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = viewModel;
        }

        private void ResetRow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is SettingItemViewModel setting)
            {
                _viewModel.ResetValueCommand.Execute(setting);
                _viewModel.RefreshPendingSnapshot();
                if (_viewModel.PendingChangesCount == 0)
                    Close();
            }
        }

        private void DiscardAll_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.RefreshCurrentProfileCommand.Execute(null);
            Close();
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ApplyCommand.Execute(null);
            Close();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
