using System;
using System.Windows;
using System.Windows.Controls;

namespace OldenEra.TemplateEditor.Views;

public partial class MapPanel : UserControl
{
    public MapPanel()
    {
        InitializeComponent();
    }

    private void BtnRandomizeSeed_Click(object sender, RoutedEventArgs e)
    {
        TxtSeed.Text = Random.Shared.Next(0, int.MaxValue).ToString();
    }
}
