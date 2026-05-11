namespace OldenEraTemplateEditor.Services.ContentManagement
{
public static class ContentItemGroup
{
    /* ContentIds of mines */
    public static readonly List<SidMapping> Mines = new() { 
        ContentIds.MineWood, 
        ContentIds.MineOre, 
        ContentIds.MineGold,
        ContentIds.MineMercury,
        ContentIds.MineCrystals,
        ContentIds.MineGemstones,
        ContentIds.AlchemyLab
    };
    /* ContentIds of storage structures - weekly reward for visiting */
    public static readonly List<SidMapping> StorageStructures = new()
    {
        ContentIds.StorageWood,
        ContentIds.StorageOre,
        ContentIds.StorageGold,
        ContentIds.StorageMercury,
        ContentIds.StorageCrystals,
        ContentIds.StorageGemstones,
        ContentIds.StorageDust,
        IncludeListIds.StorageBanks
    };
    /* ContentIds of treasures */
    public static readonly List<SidMapping> Treasures = new() {
        ContentIds.MythicScrollBox,
        ContentIds.PandoraBox,
        ContentIds.RandomItemCommon,
        ContentIds.RandomItemEpic,
        ContentIds.RandomItemLegendary
    };
    /* Random hire buildings matching the player faction */
    public static readonly List<SidMapping> HireStructures = new()
    {
        ContentIds.RandomHire1,
        ContentIds.RandomHire2,
        ContentIds.RandomHire3,
        ContentIds.RandomHire4,
        ContentIds.RandomHire5,
        ContentIds.RandomHire6,
        ContentIds.RandomHire7,
        IncludeListIds.RandomHiresLowTier,
        IncludeListIds.RandomHiresHighTier,
        IncludeListIds.RandomHiresAllTier
    };
    /* Resource banks */
    public static readonly List<SidMapping> ResourceBanks = new()
    {
        IncludeListIds.ResourceBanksTier1,
        IncludeListIds.ResourceBanksTier2,
    };
}

}