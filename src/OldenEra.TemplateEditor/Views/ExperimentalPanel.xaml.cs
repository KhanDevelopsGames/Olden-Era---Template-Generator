using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using OldenEra.Generator.Models.Unfrozen;
using OldenEra.Generator.Services;

namespace OldenEra.TemplateEditor.Views;

public partial class ExperimentalPanel : UserControl
{
    /// <summary>Item shape for spell ComboBox; flat sorted list "{Tier}. {Name} ({School})".</summary>
    public sealed record SpellOption(string Id, string Display);

    /// <summary>Row in the catalog-driven ban-unit ComboBox.</summary>
    public sealed record BanUnitRow(string Id, string Display, string Faction);

    public ExperimentalPanel()
    {
        InitializeComponent();
        PopulateBanUnitPicker();

        // Populate building-preset combos. Index 0 = "(default)".
        var presets = new System.Collections.Generic.List<string> { "(default)" };
        presets.AddRange(KnownValues.BuildingsConstructionSids);
        CmbPlayerPreset.ItemsSource = presets;
        CmbNeutralPreset.ItemsSource = presets;
        CmbLowTierPreset.ItemsSource = presets;
        CmbMediumTierPreset.ItemsSource = presets;
        CmbHighTierPreset.ItemsSource = presets;
        CmbPlayerPreset.SelectedIndex = 0;
        CmbNeutralPreset.SelectedIndex = 0;
        CmbLowTierPreset.SelectedIndex = 0;
        CmbMediumTierPreset.SelectedIndex = 0;
        CmbHighTierPreset.SelectedIndex = 0;

        // Populate the bonus-spell combo from the community catalog.
        // Index 0 = sentinel (no bonus spell, value="").
        var spells = new System.Collections.Generic.List<SpellOption>
        {
            new("", "(no bonus spell)"),
        };
        spells.AddRange(
            CommunityCatalog.Default.Spells
                .OrderBy(s => s.School, System.StringComparer.OrdinalIgnoreCase)
                .ThenBy(s => s.Tier)
                .ThenBy(s => s.Name, System.StringComparer.OrdinalIgnoreCase)
                .Select(s => new SpellOption(s.Id, $"T{s.Tier}. {s.Name} ({s.School})")));
        CmbBonusSpell.ItemsSource = spells;
        CmbBonusSpell.SelectedIndex = 0;

        // Live label updates for sliders.
        SldBonusGold.ValueChanged       += (_, _) => TxtBonusGold.Text       = ((int)SldBonusGold.Value).ToString();
        SldBonusWood.ValueChanged       += (_, _) => TxtBonusWood.Text       = ((int)SldBonusWood.Value).ToString();
        SldBonusOre.ValueChanged        += (_, _) => TxtBonusOre.Text        = ((int)SldBonusOre.Value).ToString();
        SldBonusMercury.ValueChanged    += (_, _) => TxtBonusMercury.Text    = ((int)SldBonusMercury.Value).ToString();
        SldBonusCrystals.ValueChanged   += (_, _) => TxtBonusCrystals.Text   = ((int)SldBonusCrystals.Value).ToString();
        SldBonusGemstones.ValueChanged  += (_, _) => TxtBonusGemstones.Text  = ((int)SldBonusGemstones.Value).ToString();
        SldBonusAttack.ValueChanged     += (_, _) => TxtBonusAttack.Text     = ((int)SldBonusAttack.Value).ToString();
        SldBonusDefense.ValueChanged    += (_, _) => TxtBonusDefense.Text    = ((int)SldBonusDefense.Value).ToString();
        SldBonusSpellpower.ValueChanged += (_, _) => TxtBonusSpellpower.Text = ((int)SldBonusSpellpower.Value).ToString();
        SldBonusKnowledge.ValueChanged  += (_, _) => TxtBonusKnowledge.Text  = ((int)SldBonusKnowledge.Value).ToString();
        SldBonusUnitMultiplier.ValueChanged += (_, _) => TxtBonusUnitMultiplier.Text = ((int)SldBonusUnitMultiplier.Value).ToString();

        SldTerrainObstacles.ValueChanged    += (_, _) => TxtTerrainObstacles.Text    = ((int)SldTerrainObstacles.Value).ToString();
        SldTerrainLakes.ValueChanged        += (_, _) => TxtTerrainLakes.Text        = ((int)SldTerrainLakes.Value).ToString();
        SldZoneGuardWeekly.ValueChanged     += (_, _) => TxtZoneGuardWeekly.Text     = ((int)SldZoneGuardWeekly.Value).ToString();
        SldConnectionGuardWeekly.ValueChanged += (_, _) => TxtConnectionGuardWeekly.Text = ((int)SldConnectionGuardWeekly.Value).ToString();
        SldNeutralGuardChance.ValueChanged  += (_, _) => TxtNeutralGuardChance.Text  = ((int)SldNeutralGuardChance.Value).ToString();
        SldNeutralGuardValue.ValueChanged   += (_, _) => TxtNeutralGuardValue.Text   = ((int)SldNeutralGuardValue.Value).ToString();
        SldLowTierGuardWeekly.ValueChanged    += (_, _) => TxtLowTierGuardWeekly.Text    = ((int)SldLowTierGuardWeekly.Value).ToString();
        SldMediumTierGuardWeekly.ValueChanged += (_, _) => TxtMediumTierGuardWeekly.Text = ((int)SldMediumTierGuardWeekly.Value).ToString();
        SldHighTierGuardWeekly.ValueChanged   += (_, _) => TxtHighTierGuardWeekly.Text   = ((int)SldHighTierGuardWeekly.Value).ToString();
    }

    /// <summary>
    /// Single-Hero mode is mutually exclusive with hero-hire-ban: enabling
    /// SingleHero clears and disables the hire-ban checkbox.
    /// </summary>
    private void ChkSingleHero_Changed(object sender, RoutedEventArgs e)
    {
        bool on = ChkSingleHero.IsChecked == true;
        ChkHeroHireBan.IsEnabled = !on;
        if (on) ChkHeroHireBan.IsChecked = false;
    }

    /// <summary>
    /// Populates the unit-ban picker with rows grouped by faction, sorted by
    /// (tier, name). Selecting a row appends its unit id to TxtGlobalBans.
    /// </summary>
    private void PopulateBanUnitPicker()
    {
        var catalog = CommunityCatalog.Default;
        var factionNames = catalog.Factions.ToDictionary(f => f.Id, f => f.Name,
            System.StringComparer.OrdinalIgnoreCase);

        var rows = catalog.Units
            .OrderBy(u => u.Faction)
            .ThenBy(u => u.Tier)
            .ThenBy(u => u.Name, System.StringComparer.OrdinalIgnoreCase)
            .Select(u => new BanUnitRow(
                u.Id,
                $"T{u.Tier}. {u.Name}" + (string.IsNullOrEmpty(u.Variant) ? "" : $" ({u.Variant})"),
                factionNames.TryGetValue(u.Faction, out var n) ? n : u.Faction))
            .ToList();

        var view = CollectionViewSource.GetDefaultView(rows);
        view.GroupDescriptions.Clear();
        view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(BanUnitRow.Faction)));
        CmbBanUnitPicker.ItemsSource = view;
    }

    private void CmbBanUnitPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbBanUnitPicker.SelectedItem is not BanUnitRow row) return;
        var current = (TxtGlobalBans.Text ?? string.Empty).Trim();
        var existing = current
            .Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();
        if (!existing.Contains(row.Id, System.StringComparer.OrdinalIgnoreCase))
        {
            existing.Add(row.Id);
            TxtGlobalBans.Text = string.Join(", ", existing);
        }
        // Reset selection so the same item can be re-picked next time the user
        // clears it from the textbox.
        CmbBanUnitPicker.SelectedIndex = -1;
    }
}
