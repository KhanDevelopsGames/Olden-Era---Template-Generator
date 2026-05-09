using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace OldenEraTemplateEditor.Services.ContentManagement
{
    public class SidMapping
    {
        public string Sid { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public static class ContentIds
    {
        public static IReadOnlyList<SidMapping> GetAll() => SidReflection.GetSidMappings(typeof(ContentIds));
        public static readonly SidMapping AlchemyLab = new() { Sid = "alchemy_lab", Name = "Alchemy Lab" };
        public static readonly SidMapping Arena = new() { Sid = "arena", Name = "Arena" };
        public static readonly SidMapping BeerFountain = new() { Sid = "beer_fountain", Name = "Beer Fountain" };
        public static readonly SidMapping BorealCall = new() { Sid = "boreal_call", Name = "Boreal Call" };
        public static readonly SidMapping CelestialSphere = new() { Sid = "celestial_sphere", Name = "Celestial Sphere" };
        public static readonly SidMapping Chimerologist = new() { Sid = "chimerologist", Name = "Chimerologist" };
        public static readonly SidMapping Circus = new() { Sid = "circus", Name = "Circus" };
        public static readonly SidMapping CollegeOfWonder = new() { Sid = "college_of_wonder", Name = "College Of Wonder" };
        public static readonly SidMapping CrystalTrail = new() { Sid = "crystal_trail", Name = "Crystal Trail" };
        public static readonly SidMapping DragonUtopia = new() { Sid = "dragon_utopia", Name = "Dragon Utopia" };
        public static readonly SidMapping EternalDragon = new() { Sid = "eternal_dragon", Name = "Eternal Dragon" };
        public static readonly SidMapping FickleShrine = new() { Sid = "fickle_shrine", Name = "Fickle Shrine" };
        public static readonly SidMapping FlatteringMirror = new() { Sid = "flattering_mirror", Name = "Flattering Mirror" };
        public static readonly SidMapping Forge = new() { Sid = "forge", Name = "Forge" };
        public static readonly SidMapping Fort = new() { Sid = "fort", Name = "Fort" };
        public static readonly SidMapping Fountain = new() { Sid = "fountain", Name = "Fountain" };
        public static readonly SidMapping Fountain2 = new() { Sid = "fountain_2", Name = "Fountain 2" };
        public static readonly SidMapping HuntsmansCamp = new() { Sid = "huntsmans_camp", Name = "Huntsman's Camp" };
        public static readonly SidMapping InfernalCirque = new() { Sid = "infernal_cirque", Name = "Infernal Cirque" };
        public static readonly SidMapping InsarasEye = new() { Sid = "insaras_eye", Name = "Insara's Eye" };
        public static readonly SidMapping JoustingRange = new() { Sid = "jousting_range", Name = "Jousting Range" };
        public static readonly SidMapping ManaWell = new() { Sid = "mana_well", Name = "Mana Well" };
        public static readonly SidMapping Market = new() { Sid = "market", Name = "Market" };
        public static readonly SidMapping MineCrystals = new() { Sid = "mine_crystals", Name = "Mine Crystals" };
        public static readonly SidMapping MineGemstones = new() { Sid = "mine_gemstones", Name = "Mine Gemstones" };
        public static readonly SidMapping MineGold = new() { Sid = "mine_gold", Name = "Mine Gold" };
        public static readonly SidMapping MineMercury = new() { Sid = "mine_mercury", Name = "Mine Mercury" };
        public static readonly SidMapping MineOre = new() { Sid = "mine_ore", Name = "Mine Ore" };
        public static readonly SidMapping MineWood = new() { Sid = "mine_wood", Name = "Mine Wood" };
        public static readonly SidMapping Mirage = new() { Sid = "mirage", Name = "Mirage" };
        public static readonly SidMapping MontyHall = new() { Sid = "monty_hall", Name = "Monty Hall" };
        public static readonly SidMapping MysteriousStone = new() { Sid = "mysterious_stone", Name = "Mysterious Stone" };
        public static readonly SidMapping MysticalTower = new() { Sid = "mystical_tower", Name = "Mystical Tower" };
        public static readonly SidMapping MythicScrollBox = new() { Sid = "mythic_scroll_box", Name = "Mythic Scroll Box" };
        public static readonly SidMapping OrbObservatory = new() { Sid = "orb_observatory", Name = "Orb Observatory" };
        public static readonly SidMapping PandoraBox = new() { Sid = "pandora_box", Name = "Pandora Box" };
        public static readonly SidMapping PetrifiedMemorial = new() { Sid = "petrified_memorial", Name = "Petrified Memorial" };
        public static readonly SidMapping PileOfBooks = new() { Sid = "pile_of_books", Name = "Pile Of Books" };
        public static readonly SidMapping PointOfBalance = new() { Sid = "point_of_balance", Name = "Point Of Balance" };
        public static readonly SidMapping Prison = new() { Sid = "prison", Name = "Prison" };
        public static readonly SidMapping QuixsPath = new() { Sid = "quixs_path", Name = "Quix's Path" };
        public static readonly SidMapping RandomHire1 = new() { Sid = "random_hire_1", Name = "Random Hire 1" };
        public static readonly SidMapping RandomHire2 = new() { Sid = "random_hire_2", Name = "Random Hire 2" };
        public static readonly SidMapping RandomHire3 = new() { Sid = "random_hire_3", Name = "Random Hire 3" };
        public static readonly SidMapping RandomHire4 = new() { Sid = "random_hire_4", Name = "Random Hire 4" };
        public static readonly SidMapping RandomHire5 = new() { Sid = "random_hire_5", Name = "Random Hire 5" };
        public static readonly SidMapping RandomHire6 = new() { Sid = "random_hire_6", Name = "Random Hire 6" };
        public static readonly SidMapping RandomHire7 = new() { Sid = "random_hire_7", Name = "Random Hire 7" };
        public static readonly SidMapping RandomItemCommon = new() { Sid = "random_item_common", Name = "Random Item Common" };
        public static readonly SidMapping RandomItemEpic = new() { Sid = "random_item_epic", Name = "Random Item Epic" };
        public static readonly SidMapping RandomItemLegendary = new() { Sid = "random_item_legendary", Name = "Random Item Legendary" };
        public static readonly SidMapping RandomItemRare = new() { Sid = "random_item_rare", Name = "Random Item Rare" };
        public static readonly SidMapping RemoteFoothold = new() { Sid = "remote_foothold", Name = "Remote Foothold" };
        public static readonly SidMapping ResearchLaboratory = new() { Sid = "research_laboratory", Name = "Research Laboratory" };
        public static readonly SidMapping RitualPyre = new() { Sid = "ritual_pyre", Name = "Ritual Pyre" };
        public static readonly SidMapping SacrificialShrine = new() { Sid = "sacrificial_shrine", Name = "Sacrificial Shrine" };
        public static readonly SidMapping ShadyDen = new() { Sid = "shady_den", Name = "Shady Den" };
        public static readonly SidMapping Stables = new() { Sid = "stables", Name = "Stables" };
        public static readonly SidMapping Tavern = new() { Sid = "tavern", Name = "Tavern" };
        public static readonly SidMapping TearOfTruth = new() { Sid = "tear_of_truth", Name = "Tear Of Truth" };
        public static readonly SidMapping TheGorge = new() { Sid = "the_gorge", Name = "The Gorge" };
        public static readonly SidMapping TownGate = new() { Sid = "town_gate", Name = "Town Gate" };
        public static readonly SidMapping TreeOfAbundance = new() { Sid = "tree_of_abundance", Name = "Tree Of Abundance" };
        public static readonly SidMapping TroglodyteThrone = new() { Sid = "troglodyte_throne", Name = "Troglodyte Throne" };
        public static readonly SidMapping UnforgottenGrave = new() { Sid = "unforgotten_grave", Name = "Unforgotten Grave" };
        public static readonly SidMapping University = new() { Sid = "university", Name = "University" };
        public static readonly SidMapping UnstableRuins = new() { Sid = "unstable_ruins", Name = "Unstable Ruins" };
        public static readonly SidMapping Watchtower = new() { Sid = "watchtower", Name = "Watchtower" };
        public static readonly SidMapping WindRose = new() { Sid = "wind_rose", Name = "Wind Rose" };
        public static readonly SidMapping WiseOwl = new() { Sid = "wise_owl", Name = "Wise Owl" };
    }

    public static class IncludeListIds
    {
        public static IReadOnlyList<SidMapping> GetAll() => SidReflection.GetSidMappings(typeof(IncludeListIds));

        public static readonly SidMapping RandomHiresLowTier = new() { Sid = "content_list_building_random_hires_low_tier", Name = "Random Hires Low Tier" };
        public static readonly SidMapping RandomHiresHighTier = new() { Sid = "content_list_building_random_hires_high_tier", Name = "Random Hires High Tier" };
        public static readonly SidMapping RandomHiresAllTier = new() { Sid = "basic_content_list_building_random_hires", Name = "Random Hires All Tier" };
        public static readonly SidMapping ResourceBanksTier1 = new() { Sid = "basic_content_list_building_guarded_resource_banks_tier_1", Name = "Resource Banks T1" };
        public static readonly SidMapping ResourceBanksTier2 = new() { Sid = "basic_content_list_building_guarded_resource_banks_tier_2", Name = "Resource Banks T2" };
    }

    public static class GlobalContent
    {
        public static readonly IReadOnlyList<SidMapping> GlobalContentList =
            ContentIds.GetAll().Concat(IncludeListIds.GetAll()).ToList().AsReadOnly();

        public static SidMapping? GetBySid(string sid)
        {
            if (string.IsNullOrWhiteSpace(sid))
            {
                return null;
            }

            return GlobalContentList.FirstOrDefault(item =>
                string.Equals(item.Sid, sid));
        }
        public static SidMapping? GetByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            return GlobalContentList.FirstOrDefault(item =>
                string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
        }


    }
    internal static class SidReflection
    {
        internal static IReadOnlyList<SidMapping> GetSidMappings(Type sourceType)
        {
            return sourceType
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.FieldType == typeof(SidMapping))
                .Select(field => (SidMapping?)field.GetValue(null))
                .Where(mapping => mapping is not null)
                .Cast<SidMapping>()
                .ToList()
                .AsReadOnly();
        }
    }
}
