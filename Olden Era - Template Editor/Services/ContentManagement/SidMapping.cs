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
        public static readonly SidMapping AltarOfMagic1 = new() { Sid = "altar_of_magic_1", Name = "Altar Of Magic 1" };
        public static readonly SidMapping AltarOfMagic2 = new() { Sid = "altar_of_magic_2", Name = "Altar Of Magic 2" };
        public static readonly SidMapping AltarOfMagic3 = new() { Sid = "altar_of_magic_3", Name = "Altar Of Magic 3" };
        public static readonly SidMapping AltarOfMagic4 = new() { Sid = "altar_of_magic_4", Name = "Altar Of Magic 4" };
        public static readonly SidMapping MagicAmplifier1 = new() { Sid = "magic_amplifier_1", Name = "Magic Amplifier 1" };
        public static readonly SidMapping MagicAmplifier2 = new() { Sid = "magic_amplifier_2", Name = "Magic Amplifier 2" };
        public static readonly SidMapping MagicAmplifier3 = new() { Sid = "magic_amplifier_3", Name = "Magic Amplifier 3" };
        public static readonly SidMapping MagicAmplifier4 = new() { Sid = "magic_amplifier_4", Name = "Magic Amplifier 4" };
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
        public static readonly SidMapping MineCrystals = new() { Sid = "mine_crystals", Name = "Crystal Vein" };
        public static readonly SidMapping MineGemstones = new() { Sid = "mine_gemstones", Name = "Gem Mound" };
        public static readonly SidMapping MineGold = new() { Sid = "mine_gold", Name = "Gold Mine" };
        public static readonly SidMapping MineMercury = new() { Sid = "mine_mercury", Name = "Mercury Fissure" };
        public static readonly SidMapping MineOre = new() { Sid = "mine_ore", Name = "Ore Mine" };
        public static readonly SidMapping MineWood = new() { Sid = "mine_wood", Name = "Sawmill" };
        public static readonly SidMapping Mirage = new() { Sid = "mirage", Name = "Mirage" };
        public static readonly SidMapping MysteriousStone = new() { Sid = "mysterious_stone", Name = "Mysterious Stone" };
        public static readonly SidMapping MysticalTower = new() { Sid = "mystical_tower", Name = "Mystical Tower" };
        public static readonly SidMapping ScrollBox = new() { Sid = "scroll_box", Name = "Magic Scroll" };
        public static readonly SidMapping EnchantedScrollBox = new() { Sid = "enchanted_scroll_box", Name = "Enchanted Scroll" };
        public static readonly SidMapping MythicScrollBox = new() { Sid = "mythic_scroll_box", Name = "Mythic Scroll" };
        public static readonly SidMapping OrbObservatory = new() { Sid = "orb_observatory", Name = "Orb Observatory" };
        public static readonly SidMapping PandoraBox = new() { Sid = "pandora_box", Name = "Pandora Box" };
        public static readonly SidMapping PetrifiedMemorial = new() { Sid = "petrified_memorial", Name = "Petrified Memorial" };
        public static readonly SidMapping PileOfBooks = new() { Sid = "pile_of_books", Name = "Pile Of Books" };
        public static readonly SidMapping PointOfBalance = new() { Sid = "point_of_balance", Name = "Point Of Balance" };
        public static readonly SidMapping Prison = new() { Sid = "prison", Name = "Prison" };
        public static readonly SidMapping QuixsPath = new() { Sid = "quixs_path", Name = "Quix's Path" };
        public static readonly SidMapping RandomHire1 = new() { Sid = "random_hire_1", Name = "Random Hire Tier 1" };
        public static readonly SidMapping RandomHire2 = new() { Sid = "random_hire_2", Name = "Random Hire Tier 2" };
        public static readonly SidMapping RandomHire3 = new() { Sid = "random_hire_3", Name = "Random Hire Tier 3" };
        public static readonly SidMapping RandomHire4 = new() { Sid = "random_hire_4", Name = "Random Hire Tier 4" };
        public static readonly SidMapping RandomHire5 = new() { Sid = "random_hire_5", Name = "Random Hire Tier 5" };
        public static readonly SidMapping RandomHire6 = new() { Sid = "random_hire_6", Name = "Random Hire Tier 6" };
        public static readonly SidMapping RandomHire7 = new() { Sid = "random_hire_7", Name = "Random Hire Tier 7" };
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
        public static readonly SidMapping TheGorge = new() { Sid = "the_gorge", Name = "Carrion Pile" }; // Mismatch from SID, but that's the name shown in-game.
        public static readonly SidMapping TownGate = new() { Sid = "town_gate", Name = "Town Gate" };
        public static readonly SidMapping TreeOfAbundance = new() { Sid = "tree_of_abundance", Name = "Tree Of Abundance" };
        public static readonly SidMapping TroglodyteThrone = new() { Sid = "troglodyte_throne", Name = "Troglodyte Throne" };
        public static readonly SidMapping TwilightBloom = new() { Sid = "twilight_bloom", Name = "Twilight Bloom" };
        public static readonly SidMapping UnforgottenGrave = new() { Sid = "unforgotten_grave", Name = "Unforgotten Grave" };
        public static readonly SidMapping University = new() { Sid = "university", Name = "University" };
        public static readonly SidMapping UnstableRuins = new() { Sid = "unstable_ruins", Name = "Unstable Ruins" };
        public static readonly SidMapping Watchtower = new() { Sid = "watchtower", Name = "Watchtower" };
        public static readonly SidMapping WindRose = new() { Sid = "wind_rose", Name = "Wind Rose" };
        public static readonly SidMapping WiseOwl = new() { Sid = "wise_owl", Name = "Wise Owl" };
        public static readonly SidMapping StorageWood = new() { Sid = "storage_wood", Name = "Wood Storage" };
        public static readonly SidMapping StorageOre = new() { Sid = "storage_ore", Name = "Ore Storage" };
        public static readonly SidMapping StorageGold = new() { Sid = "storage_gold", Name = "Gold Storage" };
        public static readonly SidMapping StorageMercury = new() { Sid = "storage_mercury", Name = "Mercury Storage" };
        public static readonly SidMapping StorageCrystals = new() { Sid = "storage_crystals", Name = "Crystals Storage" };
        public static readonly SidMapping StorageGemstones = new() { Sid = "storage_gemstones", Name = "Gemstones Storage" };
        public static readonly SidMapping StorageDust = new() { Sid = "storage_dust", Name = "Dust Storage" };
        public static readonly SidMapping Gardener = new() { Sid = "gardener", Name = "Gardener" };
        public static readonly SidMapping Windmill = new() { Sid = "windmill", Name = "Windmill" };
        public static readonly SidMapping Village = new() { Sid = "village", Name = "Village" };
        public static readonly SidMapping GingerbreadHouse = new() { Sid = "gingerbread_house", Name = "Gingerbread House" };
        public static readonly SidMapping PeasantCart = new() { Sid = "peasant_cart", Name = "Peasant Cart" };
        public static readonly SidMapping AbandonedCorpse = new() { Sid = "abandoned_corpse", Name = "Abandoned Corpse" };
        public static readonly SidMapping AbandonedMansion = new() { Sid = "abandoned_mansion", Name = "Abandoned Mansion" };
        public static readonly SidMapping AbnormalStructure = new() { Sid = "abnormal_structure", Name = "Abnormal Structure" };
        public static readonly SidMapping AlvarsEye = new() { Sid = "alvars_eye", Name = "Alvar's Eye" };
        public static readonly SidMapping BlackTower = new() { Sid = "black_tower", Name = "Black Tower" };
        public static readonly SidMapping CircleOfLife = new() { Sid = "circle_of_life", Name = "Circle Of Life" };
        public static readonly SidMapping CursedOldHouse = new() { Sid = "cursed_old_house", Name = "Cursed Old House" };
        public static readonly SidMapping CrowNest = new() { Sid = "crow_nest", Name = "Crow Nest" };
        public static readonly SidMapping GoblinCache = new() { Sid = "goblin_cache", Name = "Goblin Cache" };
        public static readonly SidMapping IridescentAbbey = new() { Sid = "iridescent_abbey", Name = "Iridescent Abbey" };
        public static readonly SidMapping LegionsMemorial = new() { Sid = "legions_memorial", Name = "Legions Memorial" };
        public static readonly SidMapping MereasShrine = new() { Sid = "mereas_shrine", Name = "Merea's Shrine" };
        public static readonly SidMapping MontyHall = new() { Sid = "monty_hall", Name = "Monty Hall" };
        public static readonly SidMapping OvergrownGrave = new() { Sid = "overgrown_grave", Name = "Overgrown Grave" };
        public static readonly SidMapping PrismaticLair = new() { Sid = "prismatic_lair", Name = "Prismatic Lair" };
        public static readonly SidMapping RaidersCamp = new() { Sid = "raiders_camp", Name = "Raiders Camp" };
        public static readonly SidMapping HerosCrypt = new() { Sid = "heros_crypt", Name = "Hero's Crypt" };
        public static readonly SidMapping UncannyRite = new() { Sid = "uncanny_rite", Name = "Uncanny Rite" };
        public static readonly SidMapping LearningStone = new() { Sid = "learning_stone", Name = "Learning Stone" };
        public static readonly SidMapping LostLibrary = new() { Sid = "lost_library", Name = "Lost Library" };
        public static readonly SidMapping TreeOfKnowledge = new() { Sid = "tree_of_knowledge", Name = "Tree Of Knowledge" };
        public static readonly SidMapping StingingSword = new() { Sid = "stinging_sword", Name = "Stinging Sword" };
        public static readonly SidMapping ArmoryAutomaton = new() { Sid = "armory_automaton", Name = "Armory Automaton" };
        public static readonly SidMapping MagicWheel = new() { Sid = "magic_wheel", Name = "Magic Wheel" };
        public static readonly SidMapping KnowledgeGarden = new() { Sid = "knowledge_garden", Name = "Knowledge Garden" };
        public static readonly SidMapping Maze = new() { Sid = "maze", Name = "Maze" };
        public static readonly SidMapping TrialScales = new() { Sid = "trial_scales", Name = "Trial Scales" };
        public static readonly SidMapping MercenaryGuild = new() { Sid = "mercenary_guild", Name = "Mercenary Guild" };
        
    }

    public static class IncludeListIds
    {
        /* string identifier for include lists, to properly handle content item creation. (These are not real SID values of content items, but names of their include lists) */
        public static readonly string Identifier = "content";
        public static IReadOnlyList<SidMapping> GetAll() => SidReflection.GetSidMappings(typeof(IncludeListIds));

        public static readonly SidMapping RandomHiresLowTier = new() { Sid = "content_list_building_random_hires_low_tier", Name = "Random Hires Low Tier" };
        public static readonly SidMapping RandomHiresHighTier = new() { Sid = "content_list_building_random_hires_high_tier", Name = "Random Hires High Tier" };
        public static readonly SidMapping RandomHiresAllTier = new() { Sid = "basic_content_list_building_random_hires", Name = "Random Hires Any Tier" };
        public static readonly SidMapping ResourceBanksTier1 = new() { Sid = "basic_content_list_building_guarded_resource_banks_tier_1", Name = "Resource Banks T1" };
        public static readonly SidMapping ResourceBanksTier2 = new() { Sid = "basic_content_list_building_guarded_resource_banks_tier_2", Name = "Resource Banks T2" };
        public static readonly SidMapping GuardedBanksTier1 = new() { Sid = "basic_content_list_building_guarded_resource_banks_tier_1", Name = "Guarded Banks T1" };
        public static readonly SidMapping GuardedBanksTier2 = new() { Sid = "basic_content_list_building_guarded_resource_banks_tier_2", Name = "Guarded Banks T2" };
        public static readonly SidMapping GuardedBanksTier3 = new() { Sid = "basic_content_list_building_guarded_resource_banks_tier_3", Name = "Guarded Banks T3" };
        public static readonly SidMapping BasicStorageBanks = new() { Sid = "basic_content_list_basic_storage", Name = "Random Basic Storage" };

        public static readonly SidMapping RandomRareMines = new() { Sid = "basic_content_list_rare_mines", Name = "Random Rare Mine" };
        public static readonly SidMapping RandomRareMinesBiomeRestricted = new() { Sid = "basic_content_list_rare_mines_by_biome", Name = "Random Rare Mine (Biome Restricted)" };
        public static readonly SidMapping RandomGuardedUnitBank = new() { Sid = "basic_content_list_building_guarded_units_banks", Name = "Random Guarded Unit Bank" };
        public static readonly SidMapping HeroBuffTier1 = new() { Sid = "basic_content_list_building_hero_buff_tier_1", Name = "Random Hero Buff Tier 1" };
        public static readonly SidMapping HeroExpTier2 = new() { Sid = "basic_content_list_building_hero_exp_tier_2", Name = "Random Hero Exp Tier 2" };
        public static readonly SidMapping HeroStatsAndSkillsTier1 = new() { Sid = "basic_content_list_building_hero_stats_and_skills_tier_1", Name = "Random Hero Stat/Skill Tier 1" };
        public static readonly SidMapping HeroStatsAndSkillsTier2 = new() { Sid = "basic_content_list_building_hero_stats_and_skills_tier_2", Name = "Random Hero Stat/Skill Tier 2" };
        public static readonly SidMapping HeroStatsAndSkillsTier3 = new() { Sid = "basic_content_list_building_hero_stats_and_skills_tier_3", Name = "Random Hero Stat/Skill Tier 3" };
        public static readonly SidMapping MagicBuildingsTier1 = new() { Sid = "basic_content_list_building_magic_tier_1", Name = "Random Magic Building Tier 1" };
        public static readonly SidMapping MagicBuildingsTier2 = new() { Sid = "basic_content_list_building_magic_tier_2", Name = "Random Magic Building Tier 2" };


        
        
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
