using System.Collections.Generic;
using System.Windows.Media;

namespace OldenEraTemplateEditor.Models
{
    public enum BonusPresetType
    {
        TownPortalFree   = 0,
        Spell            = 1,
        UnitMultiplier   = 2,
        MovementBonus    = 3,
        StartingItem     = 4,
        StartingGold     = 5,
        StartingGems     = 6,
        StartingCrystals = 7,
        StartingMercury  = 8,
    }

    /// <summary>UI view-model for a single configurable game-start bonus.</summary>
    public class BonusEntry
    {
        public BonusPresetType PresetType     { get; set; } = BonusPresetType.TownPortalFree;
        /// <summary>"start_hero" or "all_heroes"</summary>
        public string          ReceiverFilter { get; set; } = "start_hero";
        /// <summary>Spell sid / item sid / numeric value depending on type.</summary>
        public string          Param          { get; set; } = "";
        /// <summary>For Spell: "1" = free, "0" = normal. Unused for other types.</summary>
        public string          Param2         { get; set; } = "0";

        public string ReceiverLabel  => ReceiverFilter == "start_hero" ? "start hero" : "all heroes";

        public string DisplayName => PresetType switch
        {
            BonusPresetType.TownPortalFree                  => "Town Portal (free)",
            BonusPresetType.Spell when Param2 == "1"        => $"Spell (free): {SpellLabel(Param)}",
            BonusPresetType.Spell                           => $"Spell: {SpellLabel(Param)}",
            BonusPresetType.UnitMultiplier                  => $"Unit multiplier ×{Param}",
            BonusPresetType.MovementBonus                   => $"Movement bonus +{Param}",
            BonusPresetType.StartingItem                    => $"Starting item: {Param}",
            BonusPresetType.StartingGold                    => $"Starting gold: {Param}",
            BonusPresetType.StartingGems                    => $"Starting gems: {Param}",
            BonusPresetType.StartingCrystals                => $"Starting crystals: {Param}",
            BonusPresetType.StartingMercury                 => $"Starting mercury: {Param}",
            _                                               => PresetType.ToString(),
        };

        private static string SpellLabel(string sid) => sid switch
        {
            "neutral_magic_town_portal"      => "Town Portal",
            "neutral_magic_dimension_door"   => "Dimension Door",
            "neutral_magic_shadow_form"      => "Shadow Form",
            "neutral_magic_light_gate"       => "Light Gate",
            "neutral_magic_pocket_dimension" => "Pocket Dimension",
            _ => sid,
        };

        public Brush DotBrush => PresetType switch
        {
            BonusPresetType.TownPortalFree or BonusPresetType.Spell
                => new SolidColorBrush(Color.FromRgb(147, 112, 219)), // medium purple (magic)
            BonusPresetType.UnitMultiplier
                => new SolidColorBrush(Color.FromRgb(205,  92,  92)), // indian red (combat)
            BonusPresetType.MovementBonus
                => new SolidColorBrush(Color.FromRgb(100, 149, 237)), // cornflower blue (movement)
            BonusPresetType.StartingItem
                => new SolidColorBrush(Color.FromRgb(186,  85, 211)), // medium orchid (set)
            _ /* resources */
                => new SolidColorBrush(Color.FromRgb(218, 165,  32)), // goldenrod (resources)
        };

        /// <summary>Expands this entry into one or two raw Bonus objects for the template.</summary>
        public List<Bonus> ToBonuses()
        {
            var list = new List<Bonus>();
            switch (PresetType)
            {
                case BonusPresetType.TownPortalFree:
                    list.Add(new Bonus { Sid = "add_bonus_hero_spell", ReceiverSide = -1, ReceiverFilter = ReceiverFilter, Parameters = ["neutral_magic_town_portal"] });
                    list.Add(new Bonus { Sid = "add_bonus_hero_stat",  ReceiverSide = -1, ReceiverFilter = ReceiverFilter, Parameters = ["magicCostSidSet", "neutral_magic_town_portal", "-999", "0"] });
                    break;
                case BonusPresetType.Spell:
                    list.Add(new Bonus { Sid = "add_bonus_hero_spell", ReceiverSide = -1, ReceiverFilter = ReceiverFilter, Parameters = [Param] });
                    if (Param2 == "1")
                        list.Add(new Bonus { Sid = "add_bonus_hero_stat", ReceiverSide = -1, ReceiverFilter = ReceiverFilter, Parameters = ["magicCostSidSet", Param, "-999", "0"] });
                    break;
                case BonusPresetType.UnitMultiplier:
                    list.Add(new Bonus { Sid = "add_bonus_hero_unit_multipler", ReceiverSide = -1, ReceiverFilter = ReceiverFilter, Parameters = [Param] });
                    break;
                case BonusPresetType.MovementBonus:
                    list.Add(new Bonus { Sid = "add_bonus_hero_stat", ReceiverSide = -1, ReceiverFilter = ReceiverFilter, Parameters = ["movementBonus", Param] });
                    break;
                case BonusPresetType.StartingItem:
                    list.Add(new Bonus { Sid = "add_bonus_hero_item", ReceiverSide = -1, ReceiverFilter = ReceiverFilter, Parameters = [Param] });
                    break;
                case BonusPresetType.StartingGold:
                    list.Add(new Bonus { Sid = "add_bonus_res", ReceiverSide = -1, ReceiverFilter = ReceiverFilter, Parameters = ["gold",      Param] });
                    break;
                case BonusPresetType.StartingGems:
                    list.Add(new Bonus { Sid = "add_bonus_res", ReceiverSide = -1, ReceiverFilter = ReceiverFilter, Parameters = ["gemstones", Param] });
                    break;
                case BonusPresetType.StartingCrystals:
                    list.Add(new Bonus { Sid = "add_bonus_res", ReceiverSide = -1, ReceiverFilter = ReceiverFilter, Parameters = ["crystals",  Param] });
                    break;
                case BonusPresetType.StartingMercury:
                    list.Add(new Bonus { Sid = "add_bonus_res", ReceiverSide = -1, ReceiverFilter = ReceiverFilter, Parameters = ["mercury",   Param] });
                    break;
            }
            return list;
        }

        // ── Serialization ─────────────────────────────────────────────────────────

        /// <summary>Serializes to a compact pipe-separated string for storage.</summary>
        public override string ToString() =>
            $"{(int)PresetType}|{ReceiverFilter}|{Param}|{Param2}";

        /// <summary>Deserializes from a pipe-separated string produced by <see cref="ToString"/>.</summary>
        public static BonusEntry? FromString(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            var p = s.Split('|');
            if (p.Length < 4 || !int.TryParse(p[0], out int t)) return null;
            return new BonusEntry
            {
                PresetType     = (BonusPresetType)t,
                ReceiverFilter = p[1],
                Param          = p[2],
                Param2         = p[3],
            };
        }
    }
}
