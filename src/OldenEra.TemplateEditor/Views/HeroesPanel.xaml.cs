using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using OldenEra.Generator.Services;

namespace OldenEra.TemplateEditor.Views;

public partial class HeroesPanel : UserControl
{
    // Per-faction CheckBox map for hero bans, keyed by hero id.
    private readonly Dictionary<string, CheckBox> _heroBanCheckBoxes = new();

    public HeroesPanel()
    {
        InitializeComponent();
        BuildHeroBanTabs();
    }

    private void BuildHeroBanTabs()
    {
        TcHeroBans.Items.Clear();
        _heroBanCheckBoxes.Clear();

        foreach (var faction in CommunityCatalog.Default.Factions)
        {
            var stack = new StackPanel { Margin = new Thickness(6) };
            foreach (var hero in CommunityCatalog.Default.HeroesByFaction(faction.Id)
                                                          .OrderBy(h => h.Name))
            {
                var cb = new CheckBox
                {
                    Content = string.IsNullOrWhiteSpace(hero.Specialty)
                        ? hero.Name
                        : $"{hero.Name} — {hero.Specialty}",
                    Margin = new Thickness(2),
                    ToolTip = hero.SpecialtyDescription,
                    Tag = hero.Id,
                };
                _heroBanCheckBoxes[hero.Id] = cb;
                stack.Children.Add(cb);
            }
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxHeight = 240,
                Content = stack,
            };
            TcHeroBans.Items.Add(new TabItem { Header = faction.Name, Content = scroll });
        }
    }

    /// <summary>Read the UI state into a flat list of banned hero ids.</summary>
    public List<string> GetHeroBans()
    {
        var bans = new List<string>();
        foreach (var (id, cb) in _heroBanCheckBoxes)
        {
            if (cb.IsChecked == true) bans.Add(id);
        }
        return bans;
    }

    /// <summary>Apply persisted hero bans to the UI state.</summary>
    public void ApplyHeroBans(IEnumerable<string>? heroBans)
    {
        var bans = new HashSet<string>(heroBans ?? System.Array.Empty<string>(),
                                       System.StringComparer.OrdinalIgnoreCase);
        foreach (var (id, cb) in _heroBanCheckBoxes)
            cb.IsChecked = bans.Contains(id);
    }
}
