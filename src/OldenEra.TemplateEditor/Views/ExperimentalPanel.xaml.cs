using System.Linq;
using System.Windows;
using System.Windows.Controls;
using OldenEra.Generator.Models.Unfrozen;
using OldenEra.Generator.Services;

namespace OldenEra.TemplateEditor.Views;

public partial class ExperimentalPanel : UserControl
{
    /// <summary>Item shape for spell ComboBox; flat sorted list "{Tier}. {Name} ({School})".</summary>
    public sealed record SpellOption(string Id, string Display);

    public ExperimentalPanel()
    {
        InitializeComponent();

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
}
