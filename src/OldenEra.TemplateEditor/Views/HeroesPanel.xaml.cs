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

    // Per-faction ComboBox for pinned starting hero, keyed by faction id.
    private readonly Dictionary<string, ComboBox> _fixedHeroCombos = new();

    // Per-school CheckBox map for spell bans, keyed by spell id.
    private readonly Dictionary<string, CheckBox> _spellBanCheckBoxes = new();

    public HeroesPanel()
    {
        InitializeComponent();
        BuildHeroBanTabs();
        BuildFixedHeroRows();
        BuildSpellBanTabs();
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

    private void BuildFixedHeroRows()
    {
        var rows = new StackPanel();
        _fixedHeroCombos.Clear();

        foreach (var faction in CommunityCatalog.Default.Factions)
        {
            var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var label = new Label { Content = faction.Name, Padding = new Thickness(5, 2, 5, 2) };
            Grid.SetColumn(label, 0);
            grid.Children.Add(label);

            var combo = new ComboBox { Margin = new Thickness(2), Tag = faction.Id };
            combo.Items.Add(new ComboBoxItem { Content = "(Random)", Tag = "" });
            foreach (var hero in CommunityCatalog.Default.HeroesByFaction(faction.Id).OrderBy(h => h.Name))
            {
                var label2 = string.IsNullOrWhiteSpace(hero.Specialty)
                    ? hero.Name
                    : $"{hero.Name} — {hero.Specialty}";
                combo.Items.Add(new ComboBoxItem { Content = label2, Tag = hero.Id });
            }
            combo.SelectedIndex = 0;
            _fixedHeroCombos[faction.Id] = combo;

            Grid.SetColumn(combo, 1);
            grid.Children.Add(combo);
            rows.Children.Add(grid);
        }
        IcFixedHeroes.ItemsSource = null;
        IcFixedHeroes.Items.Clear();
        IcFixedHeroes.Items.Add(rows);
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

    /// <summary>Read the UI state into a faction-keyed dictionary of pinned hero ids.</summary>
    public Dictionary<string, string?> GetFixedStartingHeroByFaction()
    {
        var result = new Dictionary<string, string?>();
        foreach (var (factionId, combo) in _fixedHeroCombos)
        {
            var item = combo.SelectedItem as ComboBoxItem;
            var heroId = item?.Tag as string;
            result[factionId] = string.IsNullOrEmpty(heroId) ? null : heroId;
        }
        return result;
    }

    /// <summary>Apply persisted hero bans to the UI state.</summary>
    public void ApplyHeroBans(IEnumerable<string>? heroBans)
    {
        var bans = new HashSet<string>(heroBans ?? System.Array.Empty<string>(),
                                       System.StringComparer.OrdinalIgnoreCase);
        foreach (var (id, cb) in _heroBanCheckBoxes)
            cb.IsChecked = bans.Contains(id);
    }

    /// <summary>Apply persisted hero bans + pinned heroes to the UI state.</summary>
    public void ApplyHeroSelection(IEnumerable<string>? heroBans,
                                   IDictionary<string, string?>? fixedHeroes)
    {
        ApplyHeroBans(heroBans);

        if (fixedHeroes is null) return;
        foreach (var (factionId, combo) in _fixedHeroCombos)
        {
            fixedHeroes.TryGetValue(factionId, out var pinned);
            int idx = 0;
            for (int i = 0; i < combo.Items.Count; i++)
            {
                if (combo.Items[i] is ComboBoxItem cbi
                    && string.Equals(cbi.Tag as string ?? "", pinned ?? "", System.StringComparison.OrdinalIgnoreCase))
                {
                    idx = i;
                    break;
                }
            }
            combo.SelectedIndex = idx;
        }
    }

    private void BuildSpellBanTabs()
    {
        TcSpellBans.Items.Clear();
        _spellBanCheckBoxes.Clear();

        foreach (var school in CommunityCatalog.Default.SpellSchools)
        {
            var stack = new StackPanel { Margin = new Thickness(6) };
            var spells = CommunityCatalog.Default.SpellsBySchool(school)
                .OrderBy(s => s.Tier)
                .ThenBy(s => s.Name, System.StringComparer.OrdinalIgnoreCase);

            foreach (var spell in spells)
            {
                var cb = new CheckBox
                {
                    Content = $"T{spell.Tier}. {spell.Name}",
                    Margin = new Thickness(2),
                    ToolTip = string.IsNullOrWhiteSpace(spell.Description)
                        ? $"T{spell.Tier} · {CommunityCatalog.FriendlySpellSchool(spell.School)}"
                        : spell.Description,
                    Tag = spell.Id,
                };
                _spellBanCheckBoxes[spell.Id] = cb;
                stack.Children.Add(cb);
            }
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxHeight = 240,
                Content = stack,
            };
            TcSpellBans.Items.Add(new TabItem { Header = CommunityCatalog.FriendlySpellSchool(school), Content = scroll });
        }
    }

    /// <summary>Read the UI state into a flat list of banned spell ids.</summary>
    public List<string> GetBannedSpells()
    {
        var bans = new List<string>();
        foreach (var (id, cb) in _spellBanCheckBoxes)
            if (cb.IsChecked == true) bans.Add(id);
        return bans;
    }

    /// <summary>Apply persisted spell bans to the UI state.</summary>
    public void ApplyBannedSpells(IEnumerable<string>? bannedSpells)
    {
        var bans = new HashSet<string>(bannedSpells ?? System.Array.Empty<string>(),
                                       System.StringComparer.OrdinalIgnoreCase);
        foreach (var (id, cb) in _spellBanCheckBoxes)
            cb.IsChecked = bans.Contains(id);
    }
}
