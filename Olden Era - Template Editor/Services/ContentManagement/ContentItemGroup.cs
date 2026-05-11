namespace OldenEraTemplateEditor.Services.ContentManagement
{
public static class ContentItemGroup
{
    /* ContentIds of mines */
    public static readonly List<SidMapping> Mines = new() { 
        ContentIds.MineWood, 
        ContentIds.MineOre, 
        ContentIds.MineGold,
        IncludeListIds.RandomRareMines,
        IncludeListIds.RandomRareMinesBiomeRestricted,
        ContentIds.MineMercury,
        ContentIds.MineCrystals,
        ContentIds.MineGemstones,
        ContentIds.AlchemyLab,
    };
    /* ContentIds of utility structures */
    public static readonly List<SidMapping> UtilityStructures = new()
    {
        ContentIds.Watchtower,
        ContentIds.ManaWell,    
        ContentIds.WindRose,
        ContentIds.Market,
        ContentIds.Tavern,
        ContentIds.Forge,
        ContentIds.Stables,
        ContentIds.TearOfTruth,
        ContentIds.Fountain,
        ContentIds.Fountain2,
        ContentIds.BeerFountain,
        ContentIds.QuixsPath,
        ContentIds.PileOfBooks,
        ContentIds.MysteriousStone,
        ContentIds.CrystalTrail,
        ContentIds.SacrificialShrine,
        ContentIds.Chimerologist,
    };
    /* ContentIds of treasures */
    public static readonly List<SidMapping> Treasures = new() {
        ContentIds.PandoraBox,
        ContentIds.RandomItemCommon,
        ContentIds.RandomItemRare,
        ContentIds.RandomItemEpic,
        ContentIds.RandomItemLegendary,
        ContentIds.ScrollBox,
        ContentIds.EnchantedScrollBox,
        ContentIds.MythicScrollBox,
        ContentIds.Prison,
        ContentIds.Mirage
    };
    /* Unit recruitment buildings  */
    public static readonly List<SidMapping> UnitRecruitment = new()
    {
        /* Random hires - matching the player faction */
        IncludeListIds.RandomHiresLowTier,
        ContentIds.RandomHire1,
        ContentIds.RandomHire2,
        ContentIds.RandomHire3,
        ContentIds.RandomHire4,
        IncludeListIds.RandomHiresHighTier,
        ContentIds.RandomHire5,
        ContentIds.RandomHire6,
        ContentIds.RandomHire7,
        IncludeListIds.RandomHiresAllTier,
        ContentIds.MercenaryGuild,
        /* Guarded unit banks */
        IncludeListIds.RandomGuardedUnitBank,
        ContentIds.JoustingRange,
        ContentIds.UnforgottenGrave,
        ContentIds.PetrifiedMemorial,
        ContentIds.RitualPyre,
        ContentIds.BorealCall,
        ContentIds.TheGorge,
        ContentIds.PointOfBalance
    };
    /* Resource banks */
    public static readonly List<SidMapping> ResourceBanks = new()
    {
        IncludeListIds.BasicStorageBanks,
        ContentIds.StorageWood,
        ContentIds.StorageOre,
        ContentIds.StorageGold,
        ContentIds.StorageMercury,
        ContentIds.StorageCrystals,
        ContentIds.StorageGemstones,
        ContentIds.StorageDust,
        IncludeListIds.ResourceBanksTier1,
        ContentIds.Gardener,
        ContentIds.Windmill,
        ContentIds.Village,
        ContentIds.GingerbreadHouse,
        ContentIds.PeasantCart,
        ContentIds.AbandonedCorpse,
        ContentIds.CrowNest,
        ContentIds.GoblinCache,
        IncludeListIds.ResourceBanksTier2,
        ContentIds.MontyHall,
        ContentIds.HerosCrypt,
        IncludeListIds.GuardedBanksTier1,
        ContentIds.BlackTower,
        ContentIds.AbandonedMansion,
        ContentIds.MereasShrine,
        ContentIds.ShadyDen,
        IncludeListIds.GuardedBanksTier2,
        ContentIds.RaidersCamp,
        ContentIds.OvergrownGrave,
        ContentIds.LegionsMemorial,
        ContentIds.AlvarsEye,
        ContentIds.CursedOldHouse,
        ContentIds.AbnormalStructure,
        ContentIds.PrismaticLair,
        ContentIds.UncannyRite,
        ContentIds.CircleOfLife,
        ContentIds.IridescentAbbey,
        IncludeListIds.GuardedBanksTier3,
        ContentIds.TroglodyteThrone,
        ContentIds.TwilightBloom,
        ContentIds.UnstableRuins,
        ContentIds.DragonUtopia,
        ContentIds.ResearchLaboratory
    };
    /* Hero improvement structures */
    public static readonly List<SidMapping> HeroImprovementStructures = new()
    {
        IncludeListIds.HeroStatsAndSkillsTier1,
        ContentIds.StingingSword,
        ContentIds.ArmoryAutomaton,
        ContentIds.MagicWheel,
        ContentIds.KnowledgeGarden,
        ContentIds.WiseOwl,
        IncludeListIds.HeroStatsAndSkillsTier2,
        ContentIds.Fort,
        ContentIds.OrbObservatory,
        ContentIds.University,
        ContentIds.Circus,
        ContentIds.InfernalCirque,
        IncludeListIds.HeroStatsAndSkillsTier3,
        ContentIds.Maze,
        ContentIds.TrialScales,
        ContentIds.CollegeOfWonder,
        ContentIds.LearningStone,
        IncludeListIds.HeroExpTier2,
        ContentIds.LostLibrary,
        ContentIds.TreeOfKnowledge,
        IncludeListIds.MagicBuildingsTier1,
        ContentIds.MysticalTower,
        ContentIds.CelestialSphere,
        ContentIds.AltarOfMagic1,
        ContentIds.AltarOfMagic2,
        ContentIds.AltarOfMagic3,
        ContentIds.AltarOfMagic4,
        IncludeListIds.MagicBuildingsTier2,
        ContentIds.MagicAmplifier1,
        ContentIds.MagicAmplifier2,
        ContentIds.MagicAmplifier3,
        ContentIds.MagicAmplifier4,
    };
}

}