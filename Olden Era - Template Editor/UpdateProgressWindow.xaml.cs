using System.ComponentModel;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace Olden_Era___Template_Editor
{
    public partial class UpdateProgressWindow : Window
    {
        private readonly CancellationTokenSource _cts = new();
        private bool _forceClose = false;

        public CancellationToken CancellationToken => _cts.Token;

        public UpdateProgressWindow()
        {
            InitializeComponent();
        }

        public void SetTitle(string title) => TitleText.Text = title;

        public void SetStatus(string message) => StatusText.Text = message;

        public void SetProgress(double value) => ProgressBar.Value = value;

        public void ForceClose()
        {
            _forceClose = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _cts.Cancel();
            if (sender is Button btn)
            {
                btn.IsEnabled = false;
                btn.Content = "Cancelling…";
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (!_forceClose)
                _cts.Cancel();
        }
    }
}
