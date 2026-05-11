using System.Windows;

namespace Olden_Era___Template_Editor
{
    public partial class UpdateProgressWindow : Window
    {
        public UpdateProgressWindow()
        {
            InitializeComponent();
        }

        public void SetTitle(string title) => TitleText.Text = title;

        public void SetStatus(string message) => StatusText.Text = message;

        public void SetProgress(double value) => ProgressBar.Value = value;
    }
}
