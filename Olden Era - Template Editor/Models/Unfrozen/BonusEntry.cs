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
        StartingWood     = 9,
        StartingOre      = 10,
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

        public bool ShowReceiverLabel => PresetType is not (
            BonusPresetType.StartingGold or
            BonusPresetType.StartingGems or
            BonusPresetType.StartingCrystals or
            BonusPresetType.StartingMercury or
            BonusPresetType.StartingWood or
            BonusPresetType.StartingOre);

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
            BonusPresetType.StartingWood                    => $"Starting wood: {Param}",
            BonusPresetType.StartingOre                     => $"Starting ore: {Param}",
            _                                               => PresetType.ToString(),
        };

        private static string SpellLabel(string sid)
        {
            var known = System.Array.Find(KnownValues.KnownSpells, s => s.Id == sid);
            return known?.Name ?? sid;
        }

        private static readonly Brush MagicDotBrush    = CreateFrozenBrush(Color.FromRgb(147, 112, 219));
        private static readonly Brush CombatDotBrush   = CreateFrozenBrush(Color.FromRgb(205,  92,  92));
        private static readonly Brush MovementDotBrush = CreateFrozenBrush(Color.FromRgb(100, 149, 237));
        private static readonly Brush SetDotBrush      = CreateFrozenBrush(Color.FromRgb(186,  85, 211));
        private static readonly Brush ResourceDotBrush = CreateFrozenBrush(Color.FromRgb(218, 165,  32));

        private static Brush CreateFrozenBrush(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        public Brush DotBrush => PresetType switch
        {
            BonusPresetType.TownPortalFree or BonusPresetType.Spell
                => MagicDotBrush,    // medium purple (magic)
            BonusPresetType.UnitMultiplier
                => CombatDotBrush,   // indian red (combat)
            BonusPresetType.MovementBonus
                => MovementDotBrush, // cornflower blue (movement)
            BonusPresetType.StartingItem
                => SetDotBrush,      // medium orchid (set)
            _ /* resources */
                => ResourceDotBrush, // goldenrod (resources)
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
                case BonusPresetType.StartingWood:
                    list.Add(new Bonus { Sid = "add_bonus_res", ReceiverSide = -1, ReceiverFilter = ReceiverFilter, Parameters = ["wood",      Param] });
                    break;
                case BonusPresetType.StartingOre:
                    list.Add(new Bonus { Sid = "add_bonus_res", ReceiverSide = -1, ReceiverFilter = ReceiverFilter, Parameters = ["ore",       Param] });
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
