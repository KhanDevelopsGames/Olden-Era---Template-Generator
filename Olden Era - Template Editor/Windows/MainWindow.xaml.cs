using Microsoft.Win32;
using Olden_Era___Template_Editor.Models;
using Olden_Era___Template_Editor.Services;
using OldenEraTemplateEditor.Models;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using OldenEraTemplateEditor.Services.ContentManagement;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Olden_Era___Template_Editor
{
    public partial class MainWindow : Window
    {
        private const string GitHubPage = "https://github.com/KhanDevelopsGames/Olden-Era---Template-Generator";
        private const string GitHubApiLatestRelease = "https://api.github.com/repos/KhanDevelopsGames/Olden-Era---Template-Generator/releases/latest";
        private const string GitHubReleasesPage     = "https://github.com/KhanDevelopsGames/Olden-Era---Template-Generator/releases";
        private const string DiscordServer = "https://discord.gg/UqT8KshsxW";
        private const int SimpleModeMaxZones = 32;
        private const int AdvancedModeMaxZones = 32;

        private static readonly HttpClient Http = new();

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        // Currently open settings file path (null = unsaved / untitled)
        private string? _currentSettingsPath = null;
        private bool _isDirty = false;

        // Ban lists
        private readonly ObservableCollection<BanEntry>   _bannedItems  = [];
        private readonly ObservableCollection<BanEntry>   _bannedMagics = [];
        private readonly ObservableCollection<BonusEntry> _bonuses      = [];
        private bool _isRefreshingMapSizes = false;
        private string _baseTitle = string.Empty;

        private ZoneMandatoryContent _playerZoneMandatoryContent = new();
        private ZoneMandatoryContent _lowNeutralMandatoryContent = new();
        private ZoneMandatoryContent _mediumNeutralMandatoryContent = new();
        private ZoneMandatoryContent _highNeutralMandatoryContent = new();
        private ZoneMandatoryContent _hubZoneMandatoryContent = new();

        private static readonly (MapTopology Topology, string Label, string Description)[] TopologyOptions =
        [
            (MapTopology.Balanced,    "Balanced",      "Zones are placed on concentric rings by quality tier. Players are on the outer ring; neutral zones form inner rings. Each zone connects to neighbouring zones across adjacent rings."),
            (MapTopology.Random,      "Random",        "Zones are placed at random positions. Each zone connects to all zones that border it — no fixed structure."),
            (MapTopology.Default,     "Ring",          "All zones are arranged in a circle. Each zone connects to the two zones next to it."),
            (MapTopology.HubAndSpoke, "Hub",   "All zones connect to a shared central hub. Players never border each other directly."),
            (MapTopology.Chain,       "Chain",         "Zones are connected in a straight line from one end to the other, with no wrap-around.")
            ];

        public MainWindow()
        {
            InitializeComponent();

            ConnectionEditorControl.ConnectionsModified += ConnectionEditorControl_ConnectionsModified;
            ConnectionEditorControl.ErrorsChanged += ConnectionEditorControl_ErrorsChanged;
            _connectionPreviewRefreshDebounce.Tick += ConnectionPreviewRefreshDebounce_Tick;

            // Clamp startup size to the available work area so the window never
            // overflows the screen at high-DPI scaling (e.g. 125 %, 150 %, 200 %).
            var area = SystemParameters.WorkArea;
            if (Height > area.Height) { Height = area.Height; MinHeight = area.Height; }
            if (Width  > area.Width)  { Width  = area.Width;  MinWidth  = area.Width;  }

            // Stamp version from assembly metadata into all visible locations.
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            string versionLabel = version != null ? FormatVersion(version) : "v?";
            _baseTitle = $"Olden Era - Simple Template Generator {versionLabel}";
            TxtAppTitle.Text = $"Olden Era - Simple Template Generator  {versionLabel}";
            TxtWipWarning.Text = $"⚠️ Work in progress — Some generated templates may contain game-breaking bugs or issues.";

            CmbGameMode.ItemsSource = KnownValues.GameModes;
            CmbGameMode.SelectedIndex = 0;
            RefreshMapSizeOptions(160);
            CmbVictory.ItemsSource = KnownValues.VictoryConditionLabels;
            CmbVictory.SelectedIndex = 0; // Classic (win_condition_1)
            CmbTopology.ItemsSource = TopologyOptions.Select(t => t.Label).ToList();
            CmbTopology.SelectedIndex = 0; // Random is first
            UpdateValueLabels();
            UpdateAdvancedZoneSettingsVisibility();
            UpdatePlayerCastleFactionVisibility();

            // Wire ban-list ObservableCollections to the ListBoxes.
            LbBannedItems.ItemsSource  = _bannedItems;
            LbBannedMagics.ItemsSource = _bannedMagics;
            LbBonuses.ItemsSource      = _bonuses;

            InitializeZoneContentPresets();
            InitializeDefaultPlayerZoneContents();
            InitializeDefaultLowNeutralContents();
            InitializeDefaultMediumNeutralContents();
            InitializeDefaultHighNeutralContents();
            InitializeDefaultHubZoneContents();
            DataContext = new
            {
                MineContentItems = _playerZoneMandatoryContent.mines,
                TreasureContentItems = _playerZoneMandatoryContent.treasures,
                UnitRecruitmentContentItems = _playerZoneMandatoryContent.unitRecruitment,
                ResourceBankContentItems = _playerZoneMandatoryContent.resourceBanks,
                UtilityStructureContentItems = _playerZoneMandatoryContent.utilityStructures,
                HeroImprovementStructureContentItems = _playerZoneMandatoryContent.heroImprovementStructures,
                LowNeutralMineContentItems = _lowNeutralMandatoryContent.mines,
                LowNeutralTreasureContentItems = _lowNeutralMandatoryContent.treasures,
                LowNeutralUnitRecruitmentContentItems = _lowNeutralMandatoryContent.unitRecruitment,
                LowNeutralResourceBankContentItems = _lowNeutralMandatoryContent.resourceBanks,
                LowNeutralUtilityStructureContentItems = _lowNeutralMandatoryContent.utilityStructures,
                LowNeutralHeroImprovementStructureContentItems = _lowNeutralMandatoryContent.heroImprovementStructures,
                MediumNeutralMineContentItems = _mediumNeutralMandatoryContent.mines,
                MediumNeutralTreasureContentItems = _mediumNeutralMandatoryContent.treasures,
                MediumNeutralUnitRecruitmentContentItems = _mediumNeutralMandatoryContent.unitRecruitment,
                MediumNeutralResourceBankContentItems = _mediumNeutralMandatoryContent.resourceBanks,
                MediumNeutralUtilityStructureContentItems = _mediumNeutralMandatoryContent.utilityStructures,
                MediumNeutralHeroImprovementStructureContentItems = _mediumNeutralMandatoryContent.heroImprovementStructures,
                HighNeutralMineContentItems = _highNeutralMandatoryContent.mines,
                HighNeutralTreasureContentItems = _highNeutralMandatoryContent.treasures,
                HighNeutralUnitRecruitmentContentItems = _highNeutralMandatoryContent.unitRecruitment,
                HighNeutralResourceBankContentItems = _highNeutralMandatoryContent.resourceBanks,
                HighNeutralUtilityStructureContentItems = _highNeutralMandatoryContent.utilityStructures,
                HighNeutralHeroImprovementStructureContentItems = _highNeutralMandatoryContent.heroImprovementStructures,
                HubZoneMineContentItems = _hubZoneMandatoryContent.mines,
                HubZoneTreasureContentItems = _hubZoneMandatoryContent.treasures,
                HubZoneUnitRecruitmentContentItems = _hubZoneMandatoryContent.unitRecruitment,
                HubZoneResourceBankContentItems = _hubZoneMandatoryContent.resourceBanks,
                HubZoneUtilityStructureContentItems = _hubZoneMandatoryContent.utilityStructures,
                HubZoneHeroImprovementStructureContentItems = _hubZoneMandatoryContent.heroImprovementStructures,
            };
            // Fire-and-forget background update check — never blocks the UI.
            _ = CheckForUpdateAsync(version);

            TxtTemplateName.TextChanged += (_, _) => { MarkDirtyNameOnly(); Validate(); };
            UpdateTitle();
            TxtWindowTitle.Text = Title;
        }
        private void InitializeDefaultPlayerZoneContents()
        {
            // ── Basic mines — guarded, anchored near the player castle (every template). ──
            _playerZoneMandatoryContent.mines.Add(CreateZoneContentItem(ContentIds.MineWood, 
                contentRules: new List<IContentRule> {
                    new RuleDistanceToTown(DistancePresets.Near),
                    new RuleDistanceToRoad(DistancePresets.Near),
                    new RuleGuarded(true)
                }));
            _playerZoneMandatoryContent.mines.Add(CreateZoneContentItem(ContentIds.MineOre, 
                contentRules: new List<IContentRule> {
                    new RuleDistanceToTown(DistancePresets.Near),
                    new RuleDistanceToRoad(DistancePresets.Near),
                    new RuleGuarded(true)
                }));
            // ── Gold mine (Exodus/Staircase/Yin Yang pattern). ──
            _playerZoneMandatoryContent.mines.Add(CreateZoneContentItem(ContentIds.MineGold, 
                contentRules: new List<IContentRule> {
                    new RuleDistanceToRoad(DistancePresets.Near),
                    new RuleGuarded(true)
                }));
            // ── Rare mines spread along roads (Exodus/Staircase/Yin Yang pattern). ──
            _playerZoneMandatoryContent.mines.Add(CreateZoneContentItem(ContentIds.MineCrystals,
                contentRules: new List<IContentRule> {
                    new RuleDistanceToRoad(DistancePresets.NextTo),
                    new RuleGuarded(true)
                }));
            _playerZoneMandatoryContent.mines.Add(CreateZoneContentItem(ContentIds.MineMercury, 
                contentRules: new List<IContentRule> {
                    new RuleDistanceToRoad(DistancePresets.NextTo),
                    new RuleGuarded(true)
                }));
            _playerZoneMandatoryContent.mines.Add(CreateZoneContentItem(ContentIds.MineGemstones, 
                contentRules: new List<IContentRule> {
                    new RuleDistanceToRoad(DistancePresets.NextTo),
                    new RuleGuarded(true)
                }));
            _playerZoneMandatoryContent.mines.Add(CreateZoneContentItem(ContentIds.AlchemyLab, 
                contentRules: new List<IContentRule> {
                    new RuleDistanceToRoad(DistancePresets.NextTo),
                    new RuleGuarded(true)
                }));
            // ── Loot — epic items + army pandora (Exodus/Blitz pattern). ──
            _playerZoneMandatoryContent.treasures.Add(CreateZoneContentItem(IncludeListIds.PandoraBoxArmyLowTier));
            _playerZoneMandatoryContent.treasures.Add(CreateZoneContentItem(ContentIds.RandomItemEpic));

            // ── Hiring — low-tier × 2 + high-tier × 1 + full pool × 1 (Kerberos + Universe blend). ──
            _playerZoneMandatoryContent.unitRecruitment.Add(CreateZoneContentItem(IncludeListIds.RandomHiresLowTier, count: 2));
            _playerZoneMandatoryContent.unitRecruitment.Add(CreateZoneContentItem(IncludeListIds.RandomHiresHighTier));
            _playerZoneMandatoryContent.unitRecruitment.Add(CreateZoneContentItem(IncludeListIds.RandomHiresAllTier));

            // ── Guarded resource banks — tier 1 × 2 + tier 2 × 1 (Exodus pattern). ──
            _playerZoneMandatoryContent.resourceBanks.Add(CreateZoneContentItem(IncludeListIds.ResourceBanksTier1, count: 2));
            _playerZoneMandatoryContent.resourceBanks.Add(CreateZoneContentItem(IncludeListIds.ResourceBanksTier2));

            // ── Utility buildings (Blitz/Kerberos/Exodus pattern). ──
            _playerZoneMandatoryContent.utilityStructures.Add(CreateZoneContentItem(ContentIds.Watchtower));
            _playerZoneMandatoryContent.utilityStructures.Add(CreateZoneContentItem(ContentIds.Market, 
                contentRule: new RuleDistanceToRoad(DistancePresets.Near)));
            _playerZoneMandatoryContent.utilityStructures.Add(CreateZoneContentItem(ContentIds.ManaWell,
                contentRule: new RuleDistanceToRoad(DistancePresets.Near)));
            
            // ── Hero training — tier-2 stat building (fort/university/orb_observatory) ──
            //    + uncommon hero bank (university/wise_owl/tree_of_knowledge) (Blitz/Exodus pattern).
            _playerZoneMandatoryContent.heroImprovementStructures.Add(CreateZoneContentItem(IncludeListIds.HeroStatsAndSkillsTier2));
            _playerZoneMandatoryContent.heroImprovementStructures.Add(CreateZoneContentItem(IncludeListIds.HeroImprovementUncommon));

        }

        private void InitializeDefaultLowNeutralContents()
        {
            // Mines — biome rare mine + one random rare mine
            _lowNeutralMandatoryContent.mines.Add(CreateZoneContentItem(IncludeListIds.RandomRareMinesBiomeRestricted));
            _lowNeutralMandatoryContent.mines.Add(CreateZoneContentItem(IncludeListIds.RandomRareMines));
            // Utility — guarded market + vision building
            _lowNeutralMandatoryContent.utilityStructures.Add(CreateZoneContentItem(ContentIds.Market));
            _lowNeutralMandatoryContent.utilityStructures.Add(CreateZoneContentItem(IncludeListIds.VisionBuildingsTier1));
            // Buff buildings — two hero buff tier-1 picks
            _lowNeutralMandatoryContent.heroImprovementStructures.Add(CreateZoneContentItem(IncludeListIds.HeroBuffTier1));
            _lowNeutralMandatoryContent.heroImprovementStructures.Add(CreateZoneContentItem(IncludeListIds.HeroBuffTier1));
            // Hero stat building — tier-1
            _lowNeutralMandatoryContent.heroImprovementStructures.Add(CreateZoneContentItem(IncludeListIds.HeroStatsAndSkillsTier1));
            // Hiring — two low-tier random hires
            _lowNeutralMandatoryContent.unitRecruitment.Add(CreateZoneContentItem(IncludeListIds.RandomHiresLowTier, count: 2));
            // Loot — pandora box + random pickup item
            _lowNeutralMandatoryContent.treasures.Add(CreateZoneContentItem(ContentIds.PandoraBox));
            _lowNeutralMandatoryContent.treasures.Add(CreateZoneContentItem(IncludeListIds.RandomPickupItems));
            // Magic buildings — tier 1
            _lowNeutralMandatoryContent.heroImprovementStructures.Add(CreateZoneContentItem(IncludeListIds.MagicBuildingsTier1));
        }

        private void InitializeDefaultMediumNeutralContents()
        {
            // Mines — full rare set + gold + alchemy lab
            _mediumNeutralMandatoryContent.mines.Add(CreateZoneContentItem(ContentIds.MineCrystals, 
                contentRule: new RuleDistanceToRoad(DistancePresets.NextTo)));
            _mediumNeutralMandatoryContent.mines.Add(CreateZoneContentItem(ContentIds.MineMercury, 
                contentRule: new RuleDistanceToRoad(DistancePresets.NextTo)));
            _mediumNeutralMandatoryContent.mines.Add(CreateZoneContentItem(ContentIds.MineGemstones, 
                contentRule: new RuleDistanceToRoad(DistancePresets.NextTo)));
            _mediumNeutralMandatoryContent.mines.Add(CreateZoneContentItem(ContentIds.AlchemyLab, 
                contentRule: new RuleDistanceToRoad(DistancePresets.NextTo)));
            _mediumNeutralMandatoryContent.mines.Add(CreateZoneContentItem(ContentIds.MineGold, 
                contentRule: new RuleDistanceToRoad(DistancePresets.Near)));
            // Utility — guarded watchtower + vision building
            _mediumNeutralMandatoryContent.utilityStructures.Add(CreateZoneContentItem(ContentIds.Watchtower));
            _mediumNeutralMandatoryContent.utilityStructures.Add(CreateZoneContentItem(IncludeListIds.VisionBuildingsTier1));
            // Buff buildings
            _mediumNeutralMandatoryContent.heroImprovementStructures.Add(CreateZoneContentItem(IncludeListIds.HeroBuffTier1));
            // Hero stats — tier 1 + tier 2
            _mediumNeutralMandatoryContent.heroImprovementStructures.Add(CreateZoneContentItem(IncludeListIds.HeroStatsAndSkillsTier1));
            _mediumNeutralMandatoryContent.heroImprovementStructures.Add(CreateZoneContentItem(IncludeListIds.HeroStatsAndSkillsTier2));
            // Magic buildings — tier 1 + tier 2
            _mediumNeutralMandatoryContent.heroImprovementStructures.Add(CreateZoneContentItem(IncludeListIds.MagicBuildingsTier1));
            _mediumNeutralMandatoryContent.heroImprovementStructures.Add(CreateZoneContentItem(IncludeListIds.MagicBuildingsTier2));
            // Hiring — low + high tier
            _mediumNeutralMandatoryContent.unitRecruitment.Add(CreateZoneContentItem(IncludeListIds.RandomHiresLowTier));
            _mediumNeutralMandatoryContent.unitRecruitment.Add(CreateZoneContentItem(IncludeListIds.RandomHiresHighTier));
            // Unit banks — biome-restricted
            _mediumNeutralMandatoryContent.resourceBanks.Add(CreateZoneContentItem(IncludeListIds.GuardedUnitBanksBiomeRestricted));
            // Guarded resource banks — tier 2
            _mediumNeutralMandatoryContent.resourceBanks.Add(CreateZoneContentItem(IncludeListIds.ResourceBanksTier2));
            // Loot — epic items + pandora boxes
            _mediumNeutralMandatoryContent.treasures.Add(CreateZoneContentItem(ContentIds.RandomItemEpic));
            _mediumNeutralMandatoryContent.treasures.Add(CreateZoneContentItem(ContentIds.RandomItemEpic));
            _mediumNeutralMandatoryContent.treasures.Add(CreateZoneContentItem(ContentIds.PandoraBox));
            _mediumNeutralMandatoryContent.treasures.Add(CreateZoneContentItem(ContentIds.PandoraBox));
            _mediumNeutralMandatoryContent.treasures.Add(CreateZoneContentItem(IncludeListIds.PandoraBoxArmyLowTier));
        }

        private void InitializeDefaultHighNeutralContents()
        {
            // Epic encounters — utopias + epic resource banks
            _highNeutralMandatoryContent.resourceBanks.Add(CreateZoneContentItem(IncludeListIds.UtopiaBuildings, count: 2));
            _highNeutralMandatoryContent.resourceBanks.Add(CreateZoneContentItem(IncludeListIds.EpicGuardedResourceBanks, count: 2));
            // Utility — vision + buff buildings
            _highNeutralMandatoryContent.utilityStructures.Add(CreateZoneContentItem(IncludeListIds.VisionBuildingsTier1));
            _highNeutralMandatoryContent.heroImprovementStructures.Add(CreateZoneContentItem(IncludeListIds.HeroBuffTier1));
            // Hero stats — tier 2 + tier 3 × 2
            _highNeutralMandatoryContent.heroImprovementStructures.Add(CreateZoneContentItem(IncludeListIds.HeroStatsAndSkillsTier2));
            _highNeutralMandatoryContent.heroImprovementStructures.Add(CreateZoneContentItem(IncludeListIds.HeroStatsAndSkillsTier3, count: 2));
            // Magic buildings — tier 2 × 2
            _highNeutralMandatoryContent.heroImprovementStructures.Add(CreateZoneContentItem(IncludeListIds.MagicBuildingsTier2, count: 2));
            // Hiring — high-tier × 2 + all-tier
            _highNeutralMandatoryContent.unitRecruitment.Add(CreateZoneContentItem(IncludeListIds.RandomHiresHighTier, count: 2));
            _highNeutralMandatoryContent.unitRecruitment.Add(CreateZoneContentItem(IncludeListIds.RandomHiresAllTier));
            // Unit banks — biome-restricted + no-restriction × 2
            _highNeutralMandatoryContent.resourceBanks.Add(CreateZoneContentItem(IncludeListIds.GuardedUnitBanksBiomeRestricted));
            _highNeutralMandatoryContent.resourceBanks.Add(CreateZoneContentItem(IncludeListIds.GuardedUnitBanksNoBiome, count: 2));
            // Guarded resource banks — tier 2 + tier 3
            _highNeutralMandatoryContent.resourceBanks.Add(CreateZoneContentItem(IncludeListIds.ResourceBanksTier2));
            _highNeutralMandatoryContent.resourceBanks.Add(CreateZoneContentItem(IncludeListIds.GuardedBanksTier3));
            // Loot — mythic scrolls × 2, legendary × 2, epic, pandoras + high-tier army × 2
            _highNeutralMandatoryContent.treasures.Add(CreateZoneContentItem(IncludeListIds.MythicScrollBoxPickup, count: 2));
            _highNeutralMandatoryContent.treasures.Add(CreateZoneContentItem(ContentIds.RandomItemLegendary));
            _highNeutralMandatoryContent.treasures.Add(CreateZoneContentItem(ContentIds.RandomItemLegendary));
            _highNeutralMandatoryContent.treasures.Add(CreateZoneContentItem(ContentIds.RandomItemEpic));
            _highNeutralMandatoryContent.treasures.Add(CreateZoneContentItem(ContentIds.PandoraBox, count: 2));
            _highNeutralMandatoryContent.treasures.Add(CreateZoneContentItem(IncludeListIds.PandoraBoxArmyHighTier, count: 2));
            // Mines — gold-heavy with full rare set
            _highNeutralMandatoryContent.mines.Add(CreateZoneContentItem(ContentIds.MineGold, count: 3));
            _highNeutralMandatoryContent.mines.Add(CreateZoneContentItem(ContentIds.MineCrystals));
            _highNeutralMandatoryContent.mines.Add(CreateZoneContentItem(ContentIds.MineMercury));
            _highNeutralMandatoryContent.mines.Add(CreateZoneContentItem(ContentIds.MineGemstones));
            _highNeutralMandatoryContent.mines.Add(CreateZoneContentItem(ContentIds.AlchemyLab, count: 2));
        }

        private void InitializeDefaultHubZoneContents()
        {
            // Hub zones are connector/transit zones; give them medium-quality defaults
            // as a sensible starting point (user can customize from here)
            _hubZoneMandatoryContent.mines.Add(CreateZoneContentItem(ContentIds.MineGold));
            _hubZoneMandatoryContent.mines.Add(CreateZoneContentItem(ContentIds.MineCrystals));
            _hubZoneMandatoryContent.mines.Add(CreateZoneContentItem(ContentIds.MineMercury));
            _hubZoneMandatoryContent.mines.Add(CreateZoneContentItem(ContentIds.MineGemstones));
            _hubZoneMandatoryContent.mines.Add(CreateZoneContentItem(ContentIds.AlchemyLab));
            _hubZoneMandatoryContent.utilityStructures.Add(CreateZoneContentItem(ContentIds.Watchtower));
            _hubZoneMandatoryContent.utilityStructures.Add(CreateZoneContentItem(IncludeListIds.VisionBuildingsTier1));
            _hubZoneMandatoryContent.unitRecruitment.Add(CreateZoneContentItem(IncludeListIds.RandomHiresLowTier));
            _hubZoneMandatoryContent.unitRecruitment.Add(CreateZoneContentItem(IncludeListIds.RandomHiresHighTier));
            _hubZoneMandatoryContent.resourceBanks.Add(CreateZoneContentItem(IncludeListIds.ResourceBanksTier2));
            _hubZoneMandatoryContent.heroImprovementStructures.Add(CreateZoneContentItem(IncludeListIds.HeroStatsAndSkillsTier1));
            _hubZoneMandatoryContent.heroImprovementStructures.Add(CreateZoneContentItem(IncludeListIds.MagicBuildingsTier1));
            _hubZoneMandatoryContent.treasures.Add(CreateZoneContentItem(ContentIds.RandomItemEpic));
            _hubZoneMandatoryContent.treasures.Add(CreateZoneContentItem(ContentIds.PandoraBox));
        }

        private void PopulateZoneContentMenu(ComboBox comboBox, ComboBox? comboBoxSticky, List<SidMapping> contentGroup)
        {
            var names = new List<string>();
            foreach (SidMapping sidMapping in contentGroup)
            {
                names.Add(sidMapping.Name);
            }
            comboBox.ItemsSource = names;
            comboBox.SelectedIndex = 0;
            if (comboBoxSticky != null)
            {
                comboBoxSticky.ItemsSource = names;
                comboBoxSticky.SelectedIndex = 0;
                comboBox.SelectionChanged       += (_, _) => comboBoxSticky.SelectedIndex = comboBox.SelectedIndex;
                comboBoxSticky.SelectionChanged += (_, _) => comboBox.SelectedIndex       = comboBoxSticky.SelectedIndex;
            }
        }

        private void InitializeZoneContentPresets()
        {
            /* Populate the Mines dropdown menu */
            PopulateZoneContentMenu(CmbZoneContentPreset, CmbZoneContentPresetSticky, ContentItemGroup.Mines);
            /* Populate the Treasures dropdown menu */
            PopulateZoneContentMenu(CmbTreasureContentPreset, CmbTreasureContentPresetSticky, ContentItemGroup.Treasures);
            /* Populate the Unit Recruitment dropdown menu */
            PopulateZoneContentMenu(CmbUnitRecruitmentContentPreset, CmbUnitRecruitmentContentPresetSticky, ContentItemGroup.UnitRecruitment);
             /* Populate the Resource Banks dropdown menu */
            PopulateZoneContentMenu(CmbResourceBankContentPreset, CmbResourceBankContentPresetSticky, ContentItemGroup.ResourceBanks);
            /* Populate the Utility Structures dropdown menu */
            PopulateZoneContentMenu(CmbUtilityStructureContentPreset, CmbUtilityStructureContentPresetSticky, ContentItemGroup.UtilityStructures);
            /* Populate the Hero Improvement Structures dropdown menu */
            PopulateZoneContentMenu(CmbHeroImprovementContentPreset, CmbHeroImprovementContentPresetSticky, ContentItemGroup.HeroImprovementStructures);
            /* Populate Low Neutral dropdowns */
            PopulateZoneContentMenu(CmbLowNeutralMineContentPreset,               null, ContentItemGroup.Mines);
            PopulateZoneContentMenu(CmbLowNeutralTreasureContentPreset,           null, ContentItemGroup.Treasures);
            PopulateZoneContentMenu(CmbLowNeutralUnitRecruitmentContentPreset,    null, ContentItemGroup.UnitRecruitment);
            PopulateZoneContentMenu(CmbLowNeutralResourceBankContentPreset,       null, ContentItemGroup.ResourceBanks);
            PopulateZoneContentMenu(CmbLowNeutralUtilityStructureContentPreset,   null, ContentItemGroup.UtilityStructures);
            PopulateZoneContentMenu(CmbLowNeutralHeroImprovementContentPreset,    null, ContentItemGroup.HeroImprovementStructures);
            /* Populate Medium Neutral dropdowns */
            PopulateZoneContentMenu(CmbMediumNeutralMineContentPreset,            null, ContentItemGroup.Mines);
            PopulateZoneContentMenu(CmbMediumNeutralTreasureContentPreset,        null, ContentItemGroup.Treasures);
            PopulateZoneContentMenu(CmbMediumNeutralUnitRecruitmentContentPreset, null, ContentItemGroup.UnitRecruitment);
            PopulateZoneContentMenu(CmbMediumNeutralResourceBankContentPreset,    null, ContentItemGroup.ResourceBanks);
            PopulateZoneContentMenu(CmbMediumNeutralUtilityStructureContentPreset,null, ContentItemGroup.UtilityStructures);
            PopulateZoneContentMenu(CmbMediumNeutralHeroImprovementContentPreset, null, ContentItemGroup.HeroImprovementStructures);
            /* Populate High Neutral dropdowns */
            PopulateZoneContentMenu(CmbHighNeutralMineContentPreset,              null, ContentItemGroup.Mines);
            PopulateZoneContentMenu(CmbHighNeutralTreasureContentPreset,          null, ContentItemGroup.Treasures);
            PopulateZoneContentMenu(CmbHighNeutralUnitRecruitmentContentPreset,   null, ContentItemGroup.UnitRecruitment);
            PopulateZoneContentMenu(CmbHighNeutralResourceBankContentPreset,      null, ContentItemGroup.ResourceBanks);
            PopulateZoneContentMenu(CmbHighNeutralUtilityStructureContentPreset,  null, ContentItemGroup.UtilityStructures);
            PopulateZoneContentMenu(CmbHighNeutralHeroImprovementContentPreset,   null, ContentItemGroup.HeroImprovementStructures);
            /* Populate Hub Zone dropdowns */
            PopulateZoneContentMenu(CmbHubZoneMineContentPreset,                  null, ContentItemGroup.Mines);
            PopulateZoneContentMenu(CmbHubZoneTreasureContentPreset,              null, ContentItemGroup.Treasures);
            PopulateZoneContentMenu(CmbHubZoneUnitRecruitmentContentPreset,       null, ContentItemGroup.UnitRecruitment);
            PopulateZoneContentMenu(CmbHubZoneResourceBankContentPreset,          null, ContentItemGroup.ResourceBanks);
            PopulateZoneContentMenu(CmbHubZoneUtilityStructureContentPreset,      null, ContentItemGroup.UtilityStructures);
            PopulateZoneContentMenu(CmbHubZoneHeroImprovementContentPreset,       null, ContentItemGroup.HeroImprovementStructures);
        }

        private async Task CheckForUpdateAsync(Version? currentVersion)
        {
            try
            {
                Http.DefaultRequestHeaders.UserAgent.Clear();
                Http.DefaultRequestHeaders.UserAgent.Add(
                    new ProductInfoHeaderValue("OldenEraTemplateGenerator", currentVersion?.ToString() ?? "0"));

                using var response = await Http.GetAsync(GitHubApiLatestRelease);
                if (!response.IsSuccessStatusCode) return;

                using var stream = await response.Content.ReadAsStreamAsync();
                var release = await JsonSerializer.DeserializeAsync<GitHubRelease>(stream);
                if (release?.TagName == null) return;

                // Tag expected format: "v1.2", "1.2", or "v1.2.3" — parse major.minor[.build].
                string tag = release.TagName.TrimStart('v');
                if (!Version.TryParse(tag, out Version? latestVersion)) return;
                if (currentVersion == null || latestVersion <= currentVersion) return;

                // Prefer an .exe installer asset, then a .zip, then fall back to browser.
                var asset = release.Assets?.FirstOrDefault(a =>
                    a.BrowserDownloadUrl != null &&
                    a.Name?.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) == true)
                    ?? release.Assets?.FirstOrDefault(a =>
                    a.BrowserDownloadUrl != null &&
                    a.Name?.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) == true);

                // A newer version exists — prompt on the UI thread.
                bool userAccepted = false;
                Dispatcher.Invoke(() =>
                {
                    string downloadNote = asset != null
                        ? "The update will be downloaded and launched automatically."
                        : "No installer asset was found. The releases page will be opened instead.";

                    var result = MessageBox.Show(
                        $"A new version is available: {FormatVersion(latestVersion)}\n" +
                        $"You are running: {FormatVersion(currentVersion)}\n\n" +
                        downloadNote + "\n\nUpdate now?",
                        "Update Available",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    userAccepted = result == MessageBoxResult.Yes;
                });

                if (!userAccepted) return;

                if (asset?.BrowserDownloadUrl == null)
                {
                    // No downloadable asset — open browser as fallback.
                    Process.Start(new ProcessStartInfo(GitHubReleasesPage) { UseShellExecute = true });
                    return;
                }

                await DownloadAndLaunchUpdateAsync(asset, latestVersion);
            }
            catch { /* Network unavailable or API error — silently ignore. */ }
        }

        private async Task DownloadAndLaunchUpdateAsync(GitHubReleaseAsset asset, Version latestVersion)
        {
            string ext        = Path.GetExtension(asset.Name ?? ".exe");
            string tempPath   = Path.Combine(Path.GetTempPath(), $"OldenEraUpdate_{latestVersion}{ext}");
            string versionStr = FormatVersion(latestVersion);

            UpdateProgressWindow? progressWindow = null;
            CancellationToken ct = default;
            Dispatcher.Invoke(() =>
            {
                progressWindow = new UpdateProgressWindow { Owner = this };
                ct = progressWindow.CancellationToken;
                progressWindow.SetTitle($"Downloading update {versionStr}…");
                progressWindow.SetStatus("Connecting…");
                progressWindow.Show();
            });

            try
            {
                using var download = await Http.GetAsync(asset.BrowserDownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
                download.EnsureSuccessStatusCode();

                long? total = download.Content.Headers.ContentLength;
                await using var src = await download.Content.ReadAsStreamAsync(ct);
                await using var dst = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);

                byte[] buffer     = new byte[81920];
                long   downloaded = 0;
                int    read;
                int    lastPct    = -1;
                while ((read = await src.ReadAsync(buffer, ct)) > 0)
                {
                    await dst.WriteAsync(buffer.AsMemory(0, read), ct);
                    downloaded += read;
                    if (total > 0)
                    {
                        int pct = (int)(downloaded * 100 / total.Value);
                        if (pct != lastPct)
                        {
                            lastPct = pct;
                            Dispatcher.Invoke(() =>
                            {
                                progressWindow?.SetProgress(pct);
                                progressWindow?.SetStatus($"{pct}%  ({downloaded / 1024:N0} KB / {total.Value / 1024:N0} KB)");
                            });
                        }
                    }
                    else
                    {
                        Dispatcher.Invoke(() => progressWindow?.SetStatus($"{downloaded / 1024:N0} KB downloaded…"));
                    }
                }
            }
            catch (OperationCanceledException)
            {
                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { /* best effort */ }
                Dispatcher.Invoke(() => progressWindow?.ForceClose());
                return;
            }
            catch
            {
                Dispatcher.Invoke(() =>
                {
                    progressWindow?.ForceClose();
                    MessageBox.Show(
                        "Download failed. The releases page will be opened instead.",
                        "Update Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    Process.Start(new ProcessStartInfo(GitHubReleasesPage) { UseShellExecute = true });
                });
                return;
            }
            // Replace the running exe with the downloaded file using a batch script
            // (the running exe cannot be overwritten directly while the process holds it).
            bool isExeReplacement = ext.Equals(".exe", StringComparison.OrdinalIgnoreCase)
                                    && asset.Name?.Contains("setup", StringComparison.OrdinalIgnoreCase) == false
                                    && asset.Name?.Contains("install", StringComparison.OrdinalIgnoreCase) == false;

            string currentExe = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;


            if (isExeReplacement && !string.IsNullOrEmpty(currentExe))
            {
                // Write a small batch script that waits for this process to exit,
                // copies the downloaded exe over the original, then restarts it.
                string batPath = Path.Combine(Path.GetTempPath(), "OldenEraUpdater.bat");
                int    pid     = Environment.ProcessId;
                string batContent =
                    $"@echo off\r\n" +
                    $":WAIT\r\n" +
                    $"tasklist /FI \"PID eq {pid}\" 2>NUL | find \"{pid}\" >NUL\r\n" +
                    $"if not errorlevel 1 ( timeout /t 1 /nobreak >NUL & goto WAIT )\r\n" +
                    $"copy /Y \"{tempPath}\" \"{currentExe}\"\r\n" +
                    $"start \"\" \"{currentExe}\"\r\n" +
                    $"del \"{tempPath}\"\r\n" +
                    $"del \"%~f0\"\r\n";

                await File.WriteAllTextAsync(batPath, batContent);

                Dispatcher.Invoke(() =>
                {
                    progressWindow?.SetStatus("Installing…");
                    progressWindow?.SetProgress(100);
                });

                Process.Start(new ProcessStartInfo("cmd.exe", $"/C \"{batPath}\"")
                {
                    CreateNoWindow  = true,
                    UseShellExecute = false,
                });

                Dispatcher.Invoke(() =>
                {
                    progressWindow?.ForceClose();
                    Application.Current.Shutdown();
                });
            }
            else
            {
                // It's an installer or a zip — just launch it and exit.
                Dispatcher.Invoke(() =>
                {
                    progressWindow?.ForceClose();
                    Process.Start(new ProcessStartInfo(tempPath) { UseShellExecute = true });
                    Application.Current.Shutdown();
                });
            }
        }

        // Formats a Version as "vMajor.Minor" or "vMajor.Minor.Build" when build > 0.
        private static string FormatVersion(Version v)
            => v.Build > 0 ? $"v{v.Major}.{v.Minor}.{v.Build}" : $"v{v.Major}.{v.Minor}";

        // Minimal model for GitHub releases API response.
        private sealed class GitHubRelease
        {
            [JsonPropertyName("tag_name")]
            public string? TagName { get; set; }

            [JsonPropertyName("assets")]
            public List<GitHubReleaseAsset>? Assets { get; set; }
        }

        private sealed class GitHubReleaseAsset
        {
            [JsonPropertyName("name")]
            public string? Name { get; set; }

            [JsonPropertyName("browser_download_url")]
            public string? BrowserDownloadUrl { get; set; }
        }

        private void MarkDirty()
        {
            if (!IsInitialized) return;
            _isDirty = true;
            if (_generatedTemplate is not null)
                _templateOutdated = true;
            UpdateOutdatedWarning();
            UpdateTitle();
        }

        private void MarkDirtyNameOnly()
        {
            if (!IsInitialized) return;
            _isDirty = true;
            if (_generatedTemplate is not null)
                _generatedTemplate.Name = TxtTemplateName.Text.Trim();
            UpdateTitle();
        }

        private void UpdateOutdatedWarning()
        {
            if (TxtOutdatedWarning == null) return;
            bool outdated = _templateOutdated && _generatedTemplate is not null;
            TxtOutdatedWarning.Visibility = outdated ? Visibility.Visible : Visibility.Hidden;
            if (BtnSaveGenerated != null)
                BtnSaveGenerated.IsEnabled = _generatedTemplate is not null && !outdated;
        }

        private void UpdateTitle()
        {
            string file = _currentSettingsPath is not null
                ? System.IO.Path.GetFileName(_currentSettingsPath)
                : "Untitled";
            string full = _isDirty
                ? $"{_baseTitle}  —  {file}*"
                : $"{_baseTitle}  —  {file}";
            Title = full;
            if (IsInitialized) TxtWindowTitle.Text = full;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
                ToggleMaximize();
            else if (e.ClickCount == 1)
                DragMove();
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e) =>
            WindowState = WindowState.Minimized;

        private void BtnMaximize_Click(object sender, RoutedEventArgs e) =>
            ToggleMaximize();

        private void ToggleMaximize()
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (BtnMaximize == null) return;
            if (WindowState == WindowState.Maximized)
            {
                BtnMaximize.Content = "🗗";
                BtnMaximize.ToolTip = "Restore";
            }
            else
            {
                BtnMaximize.Content = "🗖";
                BtnMaximize.ToolTip = "Maximize";
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) =>
            Close();

        // Keep value labels in sync with slider positions.
        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsInitialized) return;

            UpdateValueLabels();
            UpdatePlayerCastleFactionVisibility();
            UpdateAdvancedZoneSettingsVisibility();
            MarkDirty();
            Validate();
        }

        private void UpdateValueLabels()
        {
            TxtPlayers.Text = ((int)SldPlayers.Value).ToString();
            TxtHeroMin.Text = ((int)SldHeroMin.Value).ToString();
            TxtHeroMax.Text = ((int)SldHeroMax.Value).ToString();
            TxtHeroIncrement.Text = ((int)SldHeroIncrement.Value).ToString();
            TxtNeutral.Text = ((int)SldNeutral.Value).ToString();
            TxtPlayerCastles.Text = ((int)SldPlayerCastles.Value).ToString();
            TxtNeutralCastles.Text = ((int)SldNeutralCastles.Value).ToString();
            TxtResourceDensity.Text = $"{(int)SldResourceDensity.Value}%";
            TxtStructureDensity.Text = $"{(int)SldStructureDensity.Value}%";
            TxtNeutralStackStrength.Text = $"{(int)SldNeutralStackStrength.Value}%";
            TxtBorderGuardStrength.Text = $"{(int)SldBorderGuardStrength.Value}%";
            TxtFactionLawsExp.Text = $"{(int)SldFactionLawsExp.Value}%";
            TxtAstrologyExp.Text = $"{(int)SldAstrologyExp.Value}%";
            TxtNeutralLowNoCastle.Text = ((int)SldNeutralLowNoCastle.Value).ToString();
            TxtNeutralLowCastle.Text = ((int)SldNeutralLowCastle.Value).ToString();
            TxtNeutralMediumNoCastle.Text = ((int)SldNeutralMediumNoCastle.Value).ToString();
            TxtNeutralMediumCastle.Text = ((int)SldNeutralMediumCastle.Value).ToString();
            TxtNeutralHighNoCastle.Text = ((int)SldNeutralHighNoCastle.Value).ToString();
            TxtNeutralHighCastle.Text = ((int)SldNeutralHighCastle.Value).ToString();
            TxtMinNeutralBetweenPlayers.Text = ((int)SldMinNeutralBetweenPlayers.Value).ToString();
            TxtPlayerZoneSize.Text = $"{SldPlayerZoneSize.Value:F2}x";
            TxtNeutralZoneSize.Text = $"{SldNeutralZoneSize.Value:F2}x";
            TxtHubZoneSize.Text = $"{SldHubZoneSize.Value:F2}x";
            TxtHubCastles.Text = ((int)SldHubCastles.Value).ToString();
            TxtGuardRandomization.Text = $"{(int)SldGuardRandomization.Value}%";
            TxtLostStartCityDay.Text = ((int)SldLostStartCityDay.Value).ToString();
            TxtCityHoldDays.Text = ((int)SldCityHoldDays.Value).ToString();
            TxtGladiatorDelay.Text = ((int)SldGladiatorDelay.Value).ToString();
            TxtGladiatorCountDay.Text = ((int)SldGladiatorCountDay.Value).ToString();
            TxtTournamentPointsToWin.Text = ((int)SldTournamentPointsToWin.Value).ToString();
            TxtTournamentFirstTournamentDay.Text = ((int)SldTournamentFirstTournamentDay.Value).ToString();
            TxtTournamentInterval.Text = ((int)SldTournamentInterval.Value).ToString();
        }

        private record ValidationMessage(string Text, System.Windows.Media.Brush Foreground);

        private void SetValidationMessages(IEnumerable<ValidationMessage> messages)
        {
            var list = messages.ToList();
            LstValidation.ItemsSource = list;
            PnlValidation.Visibility = list.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SetValidationError(string text)
        {
            SetValidationMessages([new ValidationMessage(text, (System.Windows.Media.Brush)FindResource("BrushError"))]);
        }

        private bool Validate()
        {
            int heroMin = (int)SldHeroMin.Value;
            int heroMax = (int)SldHeroMax.Value;
            int players = (int)SldPlayers.Value;
            int neutral = TotalNeutralZonesFromUi();

            if (heroMin > heroMax)
            {
                SetValidationError("Min Heroes cannot be greater than Max Heroes.");
                BtnPreview.IsEnabled = false;
                return false;
            }

            int maxZones = _advancedZoneSettings ? AdvancedModeMaxZones : SimpleModeMaxZones;
            if (players + neutral > maxZones)
            {
                SetValidationError($"Total zones (players + neutral) cannot exceed {maxZones}.");
                BtnPreview.IsEnabled = false;
                return false;
            }

            if (string.IsNullOrWhiteSpace(TxtTemplateName.Text))
            {
                SetValidationError("Template name cannot be empty.");
                BtnPreview.IsEnabled = false;
                return false;
            }

            var warnBrush = (System.Windows.Media.Brush)FindResource("BrushWarnText");
            var warnings = new System.Collections.Generic.List<ValidationMessage>();

            if (TxtTemplateName.Text.Trim().Equals("Custom Template", StringComparison.OrdinalIgnoreCase))
                warnings.Add(new ValidationMessage("The template is still using the default name \"Custom Template\". Consider renaming it before saving.", warnBrush));

            int selectedMapSize = SelectedMapSize();
            int totalZones = players + neutral;
            var selectedTopology = CmbTopology.SelectedIndex >= 0 ? TopologyOptions[CmbTopology.SelectedIndex].Topology : MapTopology.Default;
            // Hub layout has an extra central zone that also occupies map area.
            int totalZonesIncludingHub = selectedTopology == MapTopology.HubAndSpoke ? totalZones + 1 : totalZones;
            if (totalZonesIncludingHub > 0 && (selectedMapSize * selectedMapSize) / totalZonesIncludingHub < 1024)
                warnings.Add(new ValidationMessage($"Estimated zone size is too small. The game may freeze when loading the map. Increase the map size or reduce the number of zones.", warnBrush));

            if (selectedMapSize > KnownValues.MaxOfficialMapSize)
                warnings.Add(new ValidationMessage("Experimental map sizes above 240x240 are not confirmed by official templates; generated maps may fail, freeze, or behave unpredictably in game.", warnBrush));

            if (totalZones > 10)
            {
                int playerCastles = (int)SldPlayerCastles.Value;
                int neutralCastles = (int)SldNeutralCastles.Value;
                if (playerCastles > 1 || neutralCastles > 1)
                    warnings.Add(new ValidationMessage("Using more than 1 castle per zone with more than 10 total zones may cause the game to freeze when generating the map. Consider reducing the number of castles.", warnBrush));
            }

            int minNeutralBetweenPlayers = (int)SldMinNeutralBetweenPlayers.Value;
            if (minNeutralBetweenPlayers > 0)
            {
                var separationSettings = new GeneratorSettings
                {
                    PlayerCount = players,
                    Topology = selectedTopology,
                    RandomPortals = ChkRandomPortals.IsChecked == true,
                    MinNeutralZonesBetweenPlayers = minNeutralBetweenPlayers
                };

                if (!TemplateGenerator.CanHonorNeutralSeparation(separationSettings, neutral))
                        warnings.Add(new ValidationMessage("Minimum neutral separation cannot be guaranteed with the current layout, neutral zone total, or portal setting; generation will ignore that option.", warnBrush));
            }

            bool cityHoldActive = ChkCityHold.IsChecked == true;
            if (cityHoldActive)
            {
                if (selectedTopology != MapTopology.HubAndSpoke && neutral == 0)
                {
                    SetValidationError("City Hold requires at least one neutral zone to place the hold city. Add a neutral zone or switch to the Hub layout.");
                    BtnPreview.IsEnabled = false;
                    return false;
                }
            }

            if (ChkNoDirectPlayerConn.IsChecked == true && neutral == 0)
            {
                SetValidationError("\"Connect via neutral zones only\" requires at least one neutral zone. Add a neutral zone or disable this option.");
                BtnPreview.IsEnabled = false;
                return false;
            }

            string selectedVictoryCondition = CmbVictory.SelectedIndex >= 0 && CmbVictory.SelectedIndex < KnownValues.VictoryConditionIds.Length
                ? KnownValues.VictoryConditionIds[CmbVictory.SelectedIndex]
                : "win_condition_1";
            if (selectedVictoryCondition == "win_condition_6" && players != 2)
            {
                SetValidationError("Tournament mode only supports exactly 2 players.");
                BtnPreview.IsEnabled = false;
                return false;
            }

            if (selectedVictoryCondition == "win_condition_6")
            {
                // Each neutral zone tier must be divisible by 2 so both players get an identical cluster.
                var oddTiers = new System.Collections.Generic.List<string>();
                if ((int)SldNeutralLowNoCastle.Value    % 2 != 0) oddTiers.Add("Low (no castle)");
                if ((int)SldNeutralLowCastle.Value      % 2 != 0) oddTiers.Add("Low (castle)");
                if ((int)SldNeutralMediumNoCastle.Value % 2 != 0) oddTiers.Add("Medium (no castle)");
                if ((int)SldNeutralMediumCastle.Value   % 2 != 0) oddTiers.Add("Medium (castle)");
                if ((int)SldNeutralHighNoCastle.Value   % 2 != 0) oddTiers.Add("High (no castle)");
                if ((int)SldNeutralHighCastle.Value     % 2 != 0) oddTiers.Add("High (castle)");
                if (oddTiers.Count > 0)
                {
                    SetValidationError($"Tournament mode requires each neutral zone type to be divisible by 2 for a fair layout. Odd count: {string.Join(", ", oddTiers)}.");
                    BtnPreview.IsEnabled = false;
                    return false;
                }
            }

            if ((int)SldBorderGuardStrength.Value > 100)
                warnings.Add(new ValidationMessage("Border/portal guard strength above 100% may cause issues for easy and medium AI enemies — guards can become too strong for them to progress through.", warnBrush));

            SetValidationMessages(warnings);

            BtnPreview.IsEnabled = true;
            return true;
        }

        private int TotalNeutralZonesFromUi()
        {
            return (int)SldNeutralLowNoCastle.Value
                + (int)SldNeutralLowCastle.Value
                + (int)SldNeutralMediumNoCastle.Value
                + (int)SldNeutralMediumCastle.Value
                + (int)SldNeutralHighNoCastle.Value
                + (int)SldNeutralHighCastle.Value;
        }

        private int SelectedMapSize() =>
            CmbMapSize.SelectedItem is string sizeStr && int.TryParse(sizeStr.Split('x')[0], out int parsedSize)
                ? parsedSize
                : 160;

        private static string FormatMapSize(int size) =>
            KnownValues.IsExperimentalMapSize(size)
                ? $"{size}x{size} ({KnownValues.MapSizeLabel(size)}) (Experimental)"
                : $"{size}x{size} ({KnownValues.MapSizeLabel(size)})";

        private static double GuardRandomizationPercent(double guardRandomization)
        {
            if (double.IsNaN(guardRandomization) || double.IsInfinity(guardRandomization))
                return 5.0;

            return Math.Clamp(guardRandomization * 100.0, 0.0, 50.0);
        }

        private void RefreshMapSizeOptions(int? requestedSize = null)
        {
            if (CmbMapSize == null) return;

            int selectedSize = requestedSize ?? SelectedMapSize();
            bool includeExperimental = ChkExperimentalMapSizes?.IsChecked == true;
            int[] sizes = includeExperimental ? KnownValues.AllMapSizes : KnownValues.MapSizes;

            if (!includeExperimental && KnownValues.IsExperimentalMapSize(selectedSize))
                selectedSize = KnownValues.MaxOfficialMapSize;
            else if (!sizes.Contains(selectedSize))
                selectedSize = KnownValues.MapSizes.Contains(selectedSize) ? selectedSize : 160;

            _isRefreshingMapSizes = true;
            try
            {
                CmbMapSize.ItemsSource = sizes.Select(FormatMapSize).ToList();
                CmbMapSize.SelectedItem = FormatMapSize(selectedSize);
                if (CmbMapSize.SelectedIndex < 0)
                    CmbMapSize.SelectedItem = FormatMapSize(160);
            }
            finally
            {
                _isRefreshingMapSizes = false;
            }

            UpdateExperimentalMapSizeWarningVisibility();
        }

        private void CmbTopology_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsInitialized) return;
            int idx = CmbTopology.SelectedIndex;
            if (idx >= 0 && idx < TopologyOptions.Length)
                TxtTopologyDesc.Text = TopologyOptions[idx].Description;

            // Isolate option is only meaningful for Random and Chain topologies.
            var topo = idx >= 0 && idx < TopologyOptions.Length ? TopologyOptions[idx].Topology : MapTopology.Default;
            bool isolateApplicable = topo is MapTopology.Random;
            ChkNoDirectPlayerConn.Visibility = isolateApplicable ? Visibility.Visible : Visibility.Collapsed;
            if (!isolateApplicable) ChkNoDirectPlayerConn.IsChecked = false;
            UpdateIsolateDescVisibility();
            UpdateAdvancedZoneSettingsVisibility();
            PnlHubZoneSize.Visibility = topo == MapTopology.HubAndSpoke ? Visibility.Visible : Visibility.Collapsed;
            PnlHubCastles.Visibility  = topo == MapTopology.HubAndSpoke ? Visibility.Visible : Visibility.Collapsed;

            MarkDirty();
            Validate();
        }

        private void BansOverrides_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsInitialized) return;
            MarkDirty();
        }

        private void BtnPickValueOverride_Click(object sender, RoutedEventArgs e)
        {
            // Collect SIDs already in the text box so the picker hides them
            var existing = TxtValueOverrides.Text
                .Split('\n')
                .Select(l => { var eq = l.IndexOf('='); return eq > 0 ? l[..eq].Trim() : ""; })
                .Where(s => s.Length > 0)
                .ToHashSet();

            var picker = new ValueOverridePickerWindow(existing) { Owner = this };
            if (picker.ShowDialog() == true && picker.ResultLines.Count > 0)
            {
                var current = TxtValueOverrides.Text.TrimEnd('\r', '\n');
                var appended = string.Join("\n", picker.ResultLines);
                TxtValueOverrides.Text = string.IsNullOrEmpty(current)
                    ? appended
                    : current + "\n" + appended;
                MarkDirty();
            }
        }

        // ── Ban list picker helpers ───────────────────────────────────────────────

        /// <summary>Builds a BanEntry from an artifact ID using the catalog, or a plain fallback entry.</summary>
        private static BanEntry ItemEntryFromId(string id)
        {
            var known = System.Array.Find(KnownValues.BannableItems, b => b.Id == id);
            if (known != null)
                return new BanEntry { Id = id, DisplayName = known.DisplayName, Category = known.Category };
            return new BanEntry { Id = id, DisplayName = KnownValues.SidToDisplayName(id), Category = "Misc" };
        }

        /// <summary>Builds a BanEntry from a spell ID using the catalog, or a plain fallback entry.</summary>
        private static BanEntry MagicEntryFromId(string id)
        {
            var known = System.Array.Find(KnownValues.KnownSpells, s => s.Id == id);
            if (known != null)
                return new BanEntry { Id = id, DisplayName = known.Name, Category = "Spell" };
            return new BanEntry { Id = id, DisplayName = KnownValues.SidToDisplayName(id), Category = "Spell" };
        }

        /// <summary>Reloads an ObservableCollection from a newline-separated string of IDs.</summary>
        private static void LoadBanList(System.Collections.ObjectModel.ObservableCollection<BanEntry> col,
                                        string raw, bool isMagics)
        {
            col.Clear();
            if (string.IsNullOrWhiteSpace(raw)) return;
            foreach (var id in raw.Split('\n'))
            {
                var trimmed = id.Trim();
                if (trimmed.Length == 0) continue;
                col.Add(isMagics ? MagicEntryFromId(trimmed) : ItemEntryFromId(trimmed));
            }
        }

        private void BtnAddBannedItem_Click(object sender, RoutedEventArgs e)
        {
            var entries = KnownValues.BannableItems
                .Select(b => new BanEntry { Id = b.Id, DisplayName = b.DisplayName, Category = b.Category });
            var picker = new ItemPickerWindow(entries, _bannedItems.Select(b => b.Id), "Add Banned Item") { Owner = this };
            if (picker.ShowDialog() == true)
            {
                foreach (var id in picker.SelectedIds)
                    if (!_bannedItems.Any(e => e.Id == id))
                        _bannedItems.Add(ItemEntryFromId(id));
                MarkDirty();
            }
        }

        private void BtnAddBannedMagic_Click(object sender, RoutedEventArgs e)
        {
            var picker = new SpellPickerWindow(_bannedMagics.Select(b => b.Id), showMakeFree: false) { Owner = this };
            if (picker.ShowDialog() == true)
            {
                foreach (var id in picker.SelectedIds)
                    if (!_bannedMagics.Any(e => e.Id == id))
                        _bannedMagics.Add(MagicEntryFromId(id));
                MarkDirty();
            }
        }

        private void RemoveBannedItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { Tag: string id })
            {
                var entry = _bannedItems.FirstOrDefault(b => b.Id == id);
                if (entry != null) { _bannedItems.Remove(entry); MarkDirty(); }
            }
        }

        private void RemoveBannedMagic_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { Tag: string id })
            {
                var entry = _bannedMagics.FirstOrDefault(b => b.Id == id);
                if (entry != null) { _bannedMagics.Remove(entry); MarkDirty(); }
            }
        }

        // ── Bonus list handlers ───────────────────────────────────────────────────

        private void BtnAddBonus_Click(object sender, RoutedEventArgs e)
        {
            var picker = new BonusPickerWindow(_bonuses) { Owner = this };
            if (picker.ShowDialog() == true)
            {
                foreach (var entry in picker.Results)
                    _bonuses.Add(entry);
                MarkDirty();
            }
        }

        private void RemoveBonus_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { Tag: BonusEntry entry })
            {
                _bonuses.Remove(entry);
                MarkDirty();
            }
        }

        private void LoadBonusList(string raw)
        {
            _bonuses.Clear();
            if (string.IsNullOrWhiteSpace(raw)) return;
            foreach (var line in raw.Split('\n'))
            {
                var entry = BonusEntry.FromString(line.Trim());
                if (entry != null) _bonuses.Add(entry);
            }
        }

        private void CmbMapSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsInitialized || _isRefreshingMapSizes) return;
            UpdateExperimentalMapSizeWarningVisibility();
            MarkDirty();
            Validate();
        }

        private void ChkSpawnAbandonedOutposts_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            UpdateSpawnAbandonedOutpostsDescVisibility();
            MarkDirty();
            Validate();
        }

        private void ChkOption_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            UpdateIsolateDescVisibility();
            UpdatePlayerCastleFactionVisibility();
            UpdateWinConditionDetailVisibility();
            MarkDirty();
            Validate();
        }

        private void ChkRandomPortals_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            PnlMaxPortals.Visibility = ChkRandomPortals.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            MarkDirty();
            Validate();
        }

        private void SldMaxPortals_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsInitialized) return;
            LblMaxPortals.Text = ((int)SldMaxPortals.Value).ToString();
            MarkDirty();
        }

        private void WinConditionOption_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            UpdateWinConditionDetailVisibility();
            MarkDirty();
            Validate();
        }

        private void ChkSingleHeroMode_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            bool single = ChkSingleHeroMode.IsChecked == true;
            SldHeroMin.IsEnabled = !single;
            SldHeroMax.IsEnabled = !single;
            SldHeroIncrement.IsEnabled = !single;
            if (single)
            {
                TxtHeroMin.Text = "1";
                TxtHeroMax.Text = "1";
                TxtHeroIncrement.Text = "1";
                ChkLostStartHero.IsChecked = true;
            }
            else
            {
                UpdateValueLabels();
                // Restore the checkbox to unchecked unless a win condition forces it
                ChkLostStartHero.IsChecked = false;
                UpdateWinConditionDetailVisibility();
            }
            MarkDirty();
            Validate();
        }
        private bool _advancedZoneSettings = false;

        private void BtnAdvancedZoneSettings_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            _advancedZoneSettings = ChkAdvancedZoneSettings.IsChecked == true;
            UpdateAdvancedZoneSettingsVisibility();
            UpdateValueLabels();
            MarkDirty();
            Validate();
        }

        private void ChkExperimentalMapSizes_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            RefreshMapSizeOptions();
            MarkDirty();
            Validate();
        }

        private int TotalAdvancedNeutralZonesFromSliders() =>
            (int)SldNeutralLowNoCastle.Value
            + (int)SldNeutralLowCastle.Value
            + (int)SldNeutralMediumNoCastle.Value
            + (int)SldNeutralMediumCastle.Value
            + (int)SldNeutralHighNoCastle.Value
            + (int)SldNeutralHighCastle.Value;

        private void UpdateAdvancedZoneSettingsVisibility()
        {
            if (PnlAdvancedNeutralZones == null) return;
            bool advanced = _advancedZoneSettings;
            // Neutral zone quality panels are always visible — advanced mode is always used for zone generation.
            PnlAdvancedNeutralZones.Visibility = Visibility.Visible;
            PnlSimpleNeutralCountLabel.Visibility = Visibility.Collapsed;
            SldNeutral.Visibility = Visibility.Collapsed;
            // Zone size / guard tuning remain gated by the Advanced settings checkbox.
            PnlAdvancedZoneSizes.Visibility = advanced ? Visibility.Visible : Visibility.Collapsed;
            if (ChkAdvancedZoneSettings != null)
                ChkAdvancedZoneSettings.IsChecked = advanced;
        }

        private void CmbVictory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsInitialized) return;

            int idx = CmbVictory.SelectedIndex;
            if (idx >= 0 && idx < KnownValues.VictoryConditionIds.Length)
                ApplyVictoryPreset(KnownValues.VictoryConditionIds[idx]);

            UpdateWinConditionDetailVisibility();
            MarkDirty();
            Validate();
        }

        private void ApplyVictoryPreset(string victoryCondition)
        {
            ChkLostStartCity.IsChecked = false;
            if (ChkSingleHeroMode.IsChecked != true) ChkLostStartHero.IsChecked = false;            ChkCityHold.IsChecked = false;
            ChkGladiatorArena.IsChecked = false;
            ChkTournament.IsChecked = false;

            SldLostStartCityDay.Value = 3;
            SldCityHoldDays.Value = 6;
            SldGladiatorDelay.Value = 30;
            SldGladiatorCountDay.Value = 3;

            SldTournamentPointsToWin.Value = 2;
            SldTournamentInterval.Value = 7;
            SldTournamentFirstTournamentDay.Value = 14;
            ChkTournamentSaveArmy.IsChecked = true;

            switch (victoryCondition)
            {
                case "win_condition_3":
                    ChkLostStartCity.IsChecked = true;
                    break;
                case "win_condition_4":
                    ChkLostStartHero.IsChecked = true;
                    ChkGladiatorArena.IsChecked = true;
                    break;
                case "win_condition_5":
                    ChkCityHold.IsChecked = true;
                    break;
                case "win_condition_6":
                    ChkLostStartHero.IsChecked = true;
                    ChkTournament.IsChecked = true;
                    SldGladiatorDelay.Value = 21;
                    SldGladiatorCountDay.Value = 8;
                    break;
            }
        }

        private void UpdateWinConditionDetailVisibility()
        {
            if (PnlLostStartCityDetails == null) return;

            string selectedVictoryCondition = CmbVictory.SelectedIndex >= 0 && CmbVictory.SelectedIndex < KnownValues.VictoryConditionIds.Length
                ? KnownValues.VictoryConditionIds[CmbVictory.SelectedIndex]
                : "win_condition_1";

            bool isTournament = selectedVictoryCondition == "win_condition_6";

            if (isTournament)
            {
                // Tournament is exclusive — force it on and disable all other conditions.
                ChkTournament.IsChecked = true;
                ChkLostStartCity.IsChecked = false;
                ChkCityHold.IsChecked = false;
                ChkGladiatorArena.IsChecked = false;
            }
            else
            {
                // Tournament is unavailable outside of the Tournament win condition.
                ChkTournament.IsChecked = false;
                if (selectedVictoryCondition == "win_condition_3")
                    ChkLostStartCity.IsChecked = true;
                if (selectedVictoryCondition == "win_condition_4")
                {
                    ChkLostStartHero.IsChecked = true;
                    ChkGladiatorArena.IsChecked = true;
                }
                if (selectedVictoryCondition == "win_condition_5")
                    ChkCityHold.IsChecked = true;
            }

            ChkLostStartCity.IsEnabled = !isTournament && selectedVictoryCondition != "win_condition_3";
            ChkLostStartHero.IsEnabled = ChkSingleHeroMode.IsChecked != true && selectedVictoryCondition != "win_condition_4";
            if (ChkSingleHeroMode.IsChecked == true) ChkLostStartHero.IsChecked = true;
            ChkCityHold.IsEnabled = !isTournament && selectedVictoryCondition != "win_condition_5";
            ChkGladiatorArena.IsEnabled = !isTournament && selectedVictoryCondition != "win_condition_4";
            ChkTournament.IsChecked = isTournament;
            ChkTournament.IsEnabled = isTournament;

            PnlLostStartCityDetails.Visibility = ChkLostStartCity.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PnlCityHoldDetails.Visibility = ChkCityHold.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PnlGladiatorDetails.Visibility = ChkGladiatorArena.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PnlTournamentDetails.Visibility = isTournament ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateExperimentalMapSizeWarningVisibility()
        {
            if (TxtExperimentalMapSizeWarning == null) return;
            bool includeExperimental = ChkExperimentalMapSizes?.IsChecked == true;
            TxtExperimentalMapSizeWarning.Visibility = includeExperimental ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdatePlayerCastleFactionVisibility()
        {
            if (PnlPlayerCastleFactionOption == null || SldPlayerCastles == null) return;
            bool hasExtraCastles = (int)SldPlayerCastles.Value > 1;
            PnlPlayerCastleFactionOption.Visibility = hasExtraCastles ? Visibility.Visible : Visibility.Collapsed;
            if (!hasExtraCastles)
            {
                ChkMatchPlayerCastleFactions.IsChecked = false;
                ChkPlayerStartsWithCastles.IsChecked = false;
            }
        }

        private void UpdateIsolateDescVisibility()
        {
            if (TxtIsolateDesc == null || ChkNoDirectPlayerConn == null) return;
            TxtIsolateDesc.Visibility = ChkNoDirectPlayerConn.IsChecked == true && ChkNoDirectPlayerConn.Visibility == Visibility.Visible
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void UpdateSpawnAbandonedOutpostsDescVisibility()
        {
            if (TxtSpawnAbandonedOutpostsDesc == null || ChkSpawnAbandonedOutposts == null) return;
            TxtSpawnAbandonedOutpostsDesc.Visibility = ChkSpawnAbandonedOutposts.IsChecked == true
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void AddZoneContentItemFromName(ObservableCollection<ZoneContentItemUI> collection, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return;

            SidMapping? mapping = GlobalContent.GetByName(name);
            if (mapping == null)
                return;
            
            collection.Add(CreateZoneContentItem(mapping));
            MarkDirty();
        }

        private void BtnAddMineContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;

            string name = CmbZoneContentPreset.SelectedItem as string ?? string.Empty;
            AddZoneContentItemFromName(_playerZoneMandatoryContent.mines, name);
        }

        private void BtnAddTreasureContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;

            string name = CmbTreasureContentPreset.SelectedItem as string ?? string.Empty;
            AddZoneContentItemFromName(_playerZoneMandatoryContent.treasures, name);
        }

        private void BtnAddUnitRecruitmentContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;

            string name = (FindName("CmbUnitRecruitmentContentPreset") as ComboBox)?.SelectedItem as string ?? string.Empty;
            AddZoneContentItemFromName(_playerZoneMandatoryContent.unitRecruitment, name);
        }

        private void BtnAddResourceBankContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;

            string name = (FindName("CmbResourceBankContentPreset") as ComboBox)?.SelectedItem as string ?? string.Empty; 
            AddZoneContentItemFromName(_playerZoneMandatoryContent.resourceBanks, name);   
        }

        private void BtnAddUtilityStructureContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;

            string name = (FindName("CmbUtilityStructureContentPreset") as ComboBox)?.SelectedItem as string ?? string.Empty;
            AddZoneContentItemFromName(_playerZoneMandatoryContent.utilityStructures, name);
        }

        private void BtnAddHeroImprovementContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;

            string name = (FindName("CmbHeroImprovementContentPreset") as ComboBox)?.SelectedItem as string ?? string.Empty;
            AddZoneContentItemFromName(_playerZoneMandatoryContent.heroImprovementStructures, name);
        }

        private void BtnRemoveZoneContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;

            if (sender is not Button button || button.DataContext is not ZoneContentItemUI item)
                return;


            if(_playerZoneMandatoryContent.Remove(item))
                MarkDirty();
            else if (_lowNeutralMandatoryContent.Remove(item))
                MarkDirty();
            else if (_mediumNeutralMandatoryContent.Remove(item))
                MarkDirty();
            else if (_highNeutralMandatoryContent.Remove(item))
                MarkDirty();
            else if (_hubZoneMandatoryContent.Remove(item))
                MarkDirty();
        }

        private void BtnManageZoneContentRules_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.DataContext is not ZoneContentItemUI item || item.SidMapping == null)
                return;

            var window = new ManageZoneContentRulesWindow(item.SidMapping, item.Rules)
            {
                Owner = this
            };

            window.ShowDialog();
            item.NotifyRulesChanged();
        }

        private void BtnResetPlayerZoneContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;

            _playerZoneMandatoryContent.Clear();

            InitializeDefaultPlayerZoneContents();
            MarkDirty();
        }

        private void BtnResetLowNeutralContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            _lowNeutralMandatoryContent.Clear();
            InitializeDefaultLowNeutralContents();
            MarkDirty();
        }

        private void BtnResetMediumNeutralContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            _mediumNeutralMandatoryContent.Clear();
            InitializeDefaultMediumNeutralContents();
            MarkDirty();
        }

        private void BtnResetHighNeutralContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            _highNeutralMandatoryContent.Clear();
            InitializeDefaultHighNeutralContents();
            MarkDirty();
        }

        private void BtnResetHubZoneContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            _hubZoneMandatoryContent.Clear();
            InitializeDefaultHubZoneContents();
            MarkDirty();
        }

        // -- Low Neutral add handlers --
        private void BtnAddLowNeutralMineContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            string name = (FindName("CmbLowNeutralMineContentPreset") as ComboBox)?.SelectedItem as string ?? string.Empty;
            AddZoneContentItemFromName(_lowNeutralMandatoryContent.mines, name);
        }
        private void BtnAddLowNeutralTreasureContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            string name = (FindName("CmbLowNeutralTreasureContentPreset") as ComboBox)?.SelectedItem as string ?? string.Empty;
            AddZoneContentItemFromName(_lowNeutralMandatoryContent.treasures, name);
        }
        private void BtnAddLowNeutralUnitRecruitmentContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            string name = (FindName("CmbLowNeutralUnitRecruitmentContentPreset") as ComboBox)?.SelectedItem as string ?? string.Empty;
            AddZoneContentItemFromName(_lowNeutralMandatoryContent.unitRecruitment, name);
        }
        private void BtnAddLowNeutralResourceBankContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            string name = (FindName("CmbLowNeutralResourceBankContentPreset") as ComboBox)?.SelectedItem as string ?? string.Empty;
            AddZoneContentItemFromName(_lowNeutralMandatoryContent.resourceBanks, name);
        }
        private void BtnAddLowNeutralUtilityStructureContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            string name = (FindName("CmbLowNeutralUtilityStructureContentPreset") as ComboBox)?.SelectedItem as string ?? string.Empty;
            AddZoneContentItemFromName(_lowNeutralMandatoryContent.utilityStructures, name);
        }
        private void BtnAddLowNeutralHeroImprovementContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            string name = (FindName("CmbLowNeutralHeroImprovementContentPreset") as ComboBox)?.SelectedItem as string ?? string.Empty;
            AddZoneContentItemFromName(_lowNeutralMandatoryContent.heroImprovementStructures, name);
        }

        // -- Medium Neutral add handlers --
        private void BtnAddMediumNeutralMineContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            string name = (FindName("CmbMediumNeutralMineContentPreset") as ComboBox)?.SelectedItem as string ?? string.Empty;
            AddZoneContentItemFromName(_mediumNeutralMandatoryContent.mines, name);
        }
        private void BtnAddMediumNeutralTreasureContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            string name = (FindName("CmbMediumNeutralTreasureContentPreset") as ComboBox)?.SelectedItem as string ?? string.Empty;
            AddZoneContentItemFromName(_mediumNeutralMandatoryContent.treasures, name);
        }
        private void BtnAddMediumNeutralUnitRecruitmentContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            string name = (FindName("CmbMediumNeutralUnitRecruitmentContentPreset") as ComboBox)?.SelectedItem as string ?? string.Empty;
            AddZoneContentItemFromName(_mediumNeutralMandatoryContent.unitRecruitment, name);
        }
        private void BtnAddMediumNeutralResourceBankContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            string name = (FindName("CmbMediumNeutralResourceBankContentPreset") as ComboBox)?.SelectedItem as string ?? string.Empty;
            AddZoneContentItemFromName(_mediumNeutralMandatoryContent.resourceBanks, name);
        }
        private void BtnAddMediumNeutralUtilityStructureContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            string name = (FindName("CmbMediumNeutralUtilityStructureContentPreset") as ComboBox)?.SelectedItem as string ?? string.Empty;
            AddZoneContentItemFromName(_mediumNeutralMandatoryContent.utilityStructures, name);
        }
        private void BtnAddMediumNeutralHeroImprovementContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            string name = (FindName("CmbMediumNeutralHeroImprovementContentPreset") as ComboBox)?.SelectedItem as string ?? string.Empty;
            AddZoneContentItemFromName(_mediumNeutralMandatoryContent.heroImprovementStructures, name);
        }

        // -- High Neutral add handlers --
        private void BtnAddHighNeutralMineContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            string name = (FindName("CmbHighNeutralMineContentPreset") as ComboBox)?.SelectedItem as string ?? string.Empty;
            AddZoneContentItemFromName(_highNeutralMandatoryContent.mines, name);
        }
        private void BtnAddHighNeutralTreasureContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            string name = (FindName("CmbHighNeutralTreasureContentPreset") as ComboBox)?.SelectedItem as string ?? string.Empty;
            AddZoneContentItemFromName(_highNeutralMandatoryContent.treasures, name);
        }
        private void BtnAddHighNeutralUnitRecruitmentContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            string name = (FindName("CmbHighNeutralUnitRecruitmentContentPreset") as ComboBox)?.SelectedItem as string ?? string.Empty;
            AddZoneContentItemFromName(_highNeutralMandatoryContent.unitRecruitment, name);
        }
        private void BtnAddHighNeutralResourceBankContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            string name = (FindName("CmbHighNeutralResourceBankContentPreset") as ComboBox)?.SelectedItem as string ?? string.Empty;
            AddZoneContentItemFromName(_highNeutralMandatoryContent.resourceBanks, name);
        }
        private void BtnAddHighNeutralUtilityStructureContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            string name = (FindName("CmbHighNeutralUtilityStructureContentPreset") as ComboBox)?.SelectedItem as string ?? string.Empty;
            AddZoneContentItemFromName(_highNeutralMandatoryContent.utilityStructures, name);
        }
        private void BtnAddHighNeutralHeroImprovementContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            string name = (FindName("CmbHighNeutralHeroImprovementContentPreset") as ComboBox)?.SelectedItem as string ?? string.Empty;
            AddZoneContentItemFromName(_highNeutralMandatoryContent.heroImprovementStructures, name);
        }

        // -- Hub Zone add handlers --
        private void BtnAddHubZoneMineContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            string name = (FindName("CmbHubZoneMineContentPreset") as ComboBox)?.SelectedItem as string ?? string.Empty;
            AddZoneContentItemFromName(_hubZoneMandatoryContent.mines, name);
        }
        private void BtnAddHubZoneTreasureContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            string name = (FindName("CmbHubZoneTreasureContentPreset") as ComboBox)?.SelectedItem as string ?? string.Empty;
            AddZoneContentItemFromName(_hubZoneMandatoryContent.treasures, name);
        }
        private void BtnAddHubZoneUnitRecruitmentContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            string name = (FindName("CmbHubZoneUnitRecruitmentContentPreset") as ComboBox)?.SelectedItem as string ?? string.Empty;
            AddZoneContentItemFromName(_hubZoneMandatoryContent.unitRecruitment, name);
        }
        private void BtnAddHubZoneResourceBankContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            string name = (FindName("CmbHubZoneResourceBankContentPreset") as ComboBox)?.SelectedItem as string ?? string.Empty;
            AddZoneContentItemFromName(_hubZoneMandatoryContent.resourceBanks, name);
        }
        private void BtnAddHubZoneUtilityStructureContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            string name = (FindName("CmbHubZoneUtilityStructureContentPreset") as ComboBox)?.SelectedItem as string ?? string.Empty;
            AddZoneContentItemFromName(_hubZoneMandatoryContent.utilityStructures, name);
        }
        private void BtnAddHubZoneHeroImprovementContent_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInitialized) return;
            string name = (FindName("CmbHubZoneHeroImprovementContentPreset") as ComboBox)?.SelectedItem as string ?? string.Empty;
            AddZoneContentItemFromName(_hubZoneMandatoryContent.heroImprovementStructures, name);
        }

        
        private static ZoneContentItemUI CreateZoneContentItem(SidMapping preset, IContentRule contentRule, int count = 1)
        {
            return CreateZoneContentItem(preset, new List<IContentRule> { contentRule }, count);
        }

        private static ZoneContentItemUI CreateZoneContentItem(SidMapping preset, List<IContentRule>? contentRules = null, int count = 1)
        {
            bool isGroup = false;
            if(preset.Sid.ToLower().Contains(IncludeListIds.Identifier))
            {
                // Mark the content item as an include list group for proper generation.
                isGroup = true;
            }
            return new ZoneContentItemUI
            {
                SidMapping = preset,
                Count = count,
                Rules = contentRules ?? new List<IContentRule>(),
                IsGroup = isGroup
            };
        }

        // -- Settings persistence -----------------------------------------------

        /// <summary>
        /// Restores a <see cref="ZoneMandatoryContent"/> from the new row-based save format.
        /// Each <see cref="ZoneContentRowSave"/> maps to exactly one UI row, preserving Count
        /// and row identity.  Falls back to <paramref name="defaultInit"/> when the list is empty.
        /// </summary>
        private void ApplyZoneContentRows(ZoneMandatoryContent target, List<ZoneContentRowSave>? rows, Action defaultInit)
        {
            target.Clear();

            if (rows is null || rows.Count == 0)
            {
                defaultInit();
                return;
            }

            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.Sid)) continue;

                SidMapping? sidMapping = GlobalContent.GetBySid(row.Sid);
                if (sidMapping is null) continue;

                List<IContentRule> rowRules = RestoreContentRulesFromRow(row, sidMapping);
                var item = CreateZoneContentItem(
                    sidMapping,
                    contentRules: rowRules,
                    count:        row.Count);

                if (row.IsMine)
                    target.mines.Add(item);
                else if (IsContentItemGroupSid(row.Sid, ContentItemGroup.UnitRecruitment))
                    target.unitRecruitment.Add(item);
                else if (IsContentItemGroupSid(row.Sid, ContentItemGroup.ResourceBanks))
                    target.resourceBanks.Add(item);
                else if (IsContentItemGroupSid(row.Sid, ContentItemGroup.UtilityStructures))
                    target.utilityStructures.Add(item);
                else if (IsContentItemGroupSid(row.Sid, ContentItemGroup.HeroImprovementStructures))
                    target.heroImprovementStructures.Add(item);
                else if (IsContentItemGroupSid(row.Sid, ContentItemGroup.Treasures))
                    target.treasures.Add(item);
                else
                    target.utilityStructures.Add(item); // safe fallback
            }
        }

        private SettingsFile GatherSettings() => new()
        {
            TemplateName          = TxtTemplateName.Text.Trim(),
            MapSize               = SelectedMapSize(),
            PlayerCount           = (int)SldPlayers.Value,
            NeutralZoneCount      = (int)SldNeutral.Value,
            PlayerZoneCastles     = (int)SldPlayerCastles.Value,
            NeutralZoneCastles    = (int)SldNeutralCastles.Value,
            SpawnAbandonedOutposts = ChkSpawnAbandonedOutposts.IsChecked == true,
            AdvancedMode          = _advancedZoneSettings,
            NeutralLowNoCastleCount = (int)SldNeutralLowNoCastle.Value,
            NeutralLowCastleCount = (int)SldNeutralLowCastle.Value,
            NeutralMediumNoCastleCount = (int)SldNeutralMediumNoCastle.Value,
            NeutralMediumCastleCount = (int)SldNeutralMediumCastle.Value,
            NeutralHighNoCastleCount = (int)SldNeutralHighNoCastle.Value,
            NeutralHighCastleCount = (int)SldNeutralHighCastle.Value,
            MatchPlayerCastleFactions   = ChkMatchPlayerCastleFactions.IsChecked == true,
            PlayerStartsWithCastles     = ChkPlayerStartsWithCastles.IsChecked == true,
            MinNeutralZonesBetweenPlayers = (int)SldMinNeutralBetweenPlayers.Value,
            ExperimentalMapSizes  = ChkExperimentalMapSizes.IsChecked == true,
            PlayerZoneSize        = _advancedZoneSettings ? SldPlayerZoneSize.Value : 1.0,
            NeutralZoneSize       = _advancedZoneSettings ? SldNeutralZoneSize.Value : 1.0,
            HubZoneSize           = SldHubZoneSize.Value,
            HubZoneCastles        = (int)SldHubCastles.Value,
            GuardRandomization    = SldGuardRandomization.Value / 100.0,
            HeroCountMin          = (int)SldHeroMin.Value,
            HeroCountMax          = (int)SldHeroMax.Value,
            HeroCountIncrement    = (int)SldHeroIncrement.Value,
            SingleHeroMode        = ChkSingleHeroMode.IsChecked == true,
            Topology              = TopologyOptions[CmbTopology.SelectedIndex].Topology,
            RandomPortals         = ChkRandomPortals.IsChecked == true,
            MaxPortalConnections  = (int)SldMaxPortals.Value,
            SpawnRemoteFootholds  = ChkSpawnFootholds.IsChecked == true,
            GenerateRoads         = ChkGenerateRoads.IsChecked == true,
            NoDirectPlayerConn    = ChkNoDirectPlayerConn.IsChecked == true,
            ResourceDensityPercent = (int)SldResourceDensity.Value,
            StructureDensityPercent = (int)SldStructureDensity.Value,
            NeutralStackStrengthPercent = (int)SldNeutralStackStrength.Value,
            BorderGuardStrengthPercent = (int)SldBorderGuardStrength.Value,
            VictoryCondition = CmbVictory.SelectedIndex >= 0 && CmbVictory.SelectedIndex < KnownValues.VictoryConditionIds.Length
                ? KnownValues.VictoryConditionIds[CmbVictory.SelectedIndex]
                : "win_condition_1",
            FactionLawsExpPercent = (int)SldFactionLawsExp.Value,
            AstrologyExpPercent   = (int)SldAstrologyExp.Value,
            LostStartCity         = ChkLostStartCity.IsChecked == true,
            LostStartCityDay = (int)SldLostStartCityDay.Value,
            LostStartHero         = ChkLostStartHero.IsChecked == true,
            CityHold              = ChkCityHold.IsChecked == true,
            CityHoldDays = (int)SldCityHoldDays.Value,
            GladiatorArena               = ChkGladiatorArena.IsChecked == true,
            GladiatorArenaDaysDelayStart = (int)SldGladiatorDelay.Value,
            GladiatorArenaCountDay       = (int)SldGladiatorCountDay.Value,
            Tournament                   = ChkTournament.IsChecked == true,
            TournamentFirstTournamentDay = (int)SldTournamentFirstTournamentDay.Value,
            TournamentInterval = (int)SldTournamentInterval.Value,
            TournamentPointsToWin = (int)SldTournamentPointsToWin.Value,
            TournamentSaveArmy = ChkTournamentSaveArmy.IsChecked == true,
            BannedItems        = string.Join("\n", _bannedItems.Select(e => e.Id)),
            BannedMagics       = string.Join("\n", _bannedMagics.Select(e => e.Id)),
            ValueOverridesText = TxtValueOverrides.Text,
            BonusesJson        = string.Join("\n", _bonuses.Select(b => b.ToString())),
            // New format: save UI rows verbatim so Count and row identity are preserved.
            PlayerZoneContentRows      = BuildZoneContentRows(_playerZoneMandatoryContent),
            LowNeutralContentRows      = BuildZoneContentRows(_lowNeutralMandatoryContent),
            MediumNeutralContentRows   = BuildZoneContentRows(_mediumNeutralMandatoryContent),
            HighNeutralContentRows     = BuildZoneContentRows(_highNeutralMandatoryContent),
            HubZoneContentRows         = BuildZoneContentRows(_hubZoneMandatoryContent),
        };

        private void ApplySettings(SettingsFile s)
        {
            TxtTemplateName.Text    = s.TemplateName;
            bool hasCustomZoneSizes = Math.Abs(s.PlayerZoneSize - 1.0) > 0.0001 || Math.Abs(s.NeutralZoneSize - 1.0) > 0.0001;
            bool needsExperimentalMapSizes = s.ExperimentalMapSizes || KnownValues.IsExperimentalMapSize(s.MapSize);
            _advancedZoneSettings = s.AdvancedMode || needsExperimentalMapSizes || hasCustomZoneSizes;
            ChkExperimentalMapSizes.IsChecked = needsExperimentalMapSizes;
            RefreshMapSizeOptions(s.MapSize);
            SldPlayers.Value        = s.PlayerCount;
            SldNeutral.Value        = s.NeutralZoneCount;
            SldPlayerCastles.Value  = s.PlayerZoneCastles;
            SldNeutralCastles.Value = s.NeutralZoneCastles;
            ChkSpawnAbandonedOutposts.IsChecked = s.SpawnAbandonedOutposts;
            UpdateSpawnAbandonedOutpostsDescVisibility();

            // Legacy compatibility: if the settings were saved with Advanced mode disabled,
            // the neutral zones were all stored as a single count in NeutralZoneCount.
            // Convert them to medium-quality neutrals with castles (or without, if NeutralZoneCastles was 0).
            int legacyLowNoCastle    = s.NeutralLowNoCastleCount;
            int legacyLowCastle      = s.NeutralLowCastleCount;
            int legacyMedNoCastle    = s.NeutralMediumNoCastleCount;
            int legacyMedCastle      = s.NeutralMediumCastleCount;
            int legacyHighNoCastle   = s.NeutralHighNoCastleCount;
            int legacyHighCastle     = s.NeutralHighCastleCount;
            if (!s.AdvancedMode && s.NeutralZoneCount > 0
                && legacyLowNoCastle == 0 && legacyLowCastle == 0
                && legacyMedNoCastle == 0 && legacyMedCastle == 0
                && legacyHighNoCastle == 0 && legacyHighCastle == 0)
            {
                if (s.NeutralZoneCastles > 0)
                    legacyMedCastle = s.NeutralZoneCount;
                else
                    legacyMedNoCastle = s.NeutralZoneCount;
            }

            SldNeutralLowNoCastle.Value    = legacyLowNoCastle;
            SldNeutralLowCastle.Value      = legacyLowCastle;
            SldNeutralMediumNoCastle.Value = legacyMedNoCastle;
            SldNeutralMediumCastle.Value   = legacyMedCastle;
            SldNeutralHighNoCastle.Value   = legacyHighNoCastle;
            SldNeutralHighCastle.Value     = legacyHighCastle;
            ChkMatchPlayerCastleFactions.IsChecked = s.MatchPlayerCastleFactions;
            ChkPlayerStartsWithCastles.IsChecked   = s.PlayerStartsWithCastles;
            SldMinNeutralBetweenPlayers.Value = s.MinNeutralZonesBetweenPlayers;
            SldPlayerZoneSize.Value = Math.Clamp(s.PlayerZoneSize, 0.1, 2.0);
            SldNeutralZoneSize.Value = Math.Clamp(s.NeutralZoneSize, 0.1, 2.0);
            SldHubZoneSize.Value = Math.Clamp(s.HubZoneSize, 0.25, 3.0);
            SldHubCastles.Value = Math.Clamp(s.HubZoneCastles, 0, 4);
            SldGuardRandomization.Value = GuardRandomizationPercent(s.GuardRandomization);
            SldHeroMin.Value        = s.HeroCountMin;
            SldHeroMax.Value        = s.HeroCountMax;
            SldHeroIncrement.Value  = s.HeroCountIncrement;
            ChkSingleHeroMode.IsChecked = s.SingleHeroMode;
            int topoIdx = Array.FindIndex(TopologyOptions, t => t.Topology == s.Topology);
            if (topoIdx >= 0) CmbTopology.SelectedIndex = topoIdx;
            ChkRandomPortals.IsChecked        = s.RandomPortals;
            SldMaxPortals.Value               = Math.Clamp(s.MaxPortalConnections, 1, 32);
            PnlMaxPortals.Visibility          = s.RandomPortals ? Visibility.Visible : Visibility.Collapsed;
            ChkSpawnFootholds.IsChecked       = s.SpawnRemoteFootholds;
            ChkGenerateRoads.IsChecked        = s.GenerateRoads;
            ChkNoDirectPlayerConn.IsChecked   = s.NoDirectPlayerConn;
            SldResourceDensity.Value          = s.EffectiveResourceDensityPercent;
            SldStructureDensity.Value         = s.EffectiveStructureDensityPercent;
            SldNeutralStackStrength.Value     = s.NeutralStackStrengthPercent;
            SldBorderGuardStrength.Value      = s.BorderGuardStrengthPercent;
            int victoryIdx = Array.IndexOf(KnownValues.VictoryConditionIds, s.VictoryCondition);
            CmbVictory.SelectedIndex = victoryIdx >= 0 ? victoryIdx : 0;
            SldFactionLawsExp.Value = Math.Clamp(s.FactionLawsExpPercent, 25, 200);
            SldAstrologyExp.Value = Math.Clamp(s.AstrologyExpPercent, 25, 200);
            ChkLostStartCity.IsChecked = s.LostStartCity;
            SldLostStartCityDay.Value = Math.Clamp(s.LostStartCityDay, 1, 30);
            ChkLostStartHero.IsChecked = s.LostStartHero || s.SingleHeroMode;
            ChkCityHold.IsChecked = s.CityHold;
            SldCityHoldDays.Value = Math.Clamp(s.CityHoldDays, 1, 30);
            ChkGladiatorArena.IsChecked = s.GladiatorArena;
            SldGladiatorDelay.Value = Math.Clamp(s.GladiatorArenaDaysDelayStart, 1, 60);
            SldGladiatorCountDay.Value = Math.Clamp(s.GladiatorArenaCountDay, 1, 30);
            ChkTournament.IsChecked = s.Tournament;
            SldTournamentFirstTournamentDay.Value = Math.Clamp(s.TournamentFirstTournamentDay, 1, 60);
            SldTournamentInterval.Value = Math.Clamp(s.TournamentInterval, 1, 30);
            SldTournamentPointsToWin.Value = Math.Clamp(s.TournamentPointsToWin, 1, 10);
            ChkTournamentSaveArmy.IsChecked = s.TournamentSaveArmy;
            LoadBanList(_bannedItems,  s.BannedItems,  isMagics: false);
            LoadBanList(_bannedMagics, s.BannedMagics, isMagics: true);
            LoadBonusList(s.BonusesJson);
            TxtValueOverrides.Text = s.ValueOverridesText;
            ApplyZoneContentRows(_playerZoneMandatoryContent,  s.PlayerZoneContentRows,    InitializeDefaultPlayerZoneContents);
            ApplyZoneContentRows(_lowNeutralMandatoryContent,  s.LowNeutralContentRows,    InitializeDefaultLowNeutralContents);
            ApplyZoneContentRows(_mediumNeutralMandatoryContent, s.MediumNeutralContentRows, InitializeDefaultMediumNeutralContents);
            ApplyZoneContentRows(_highNeutralMandatoryContent, s.HighNeutralContentRows,   InitializeDefaultHighNeutralContents);
            ApplyZoneContentRows(_hubZoneMandatoryContent,     s.HubZoneContentRows,       InitializeDefaultHubZoneContents);
            UpdateValueLabels();
            UpdateAdvancedZoneSettingsVisibility();
            UpdatePlayerCastleFactionVisibility();
            UpdateWinConditionDetailVisibility();
        }

        private bool SaveToPath(string path)
        {
            try
            {
                var json = JsonSerializer.Serialize(GatherSettings(), JsonOptions);
                File.WriteAllText(path, json);
                _currentSettingsPath = path;
                _isDirty = false;
                UpdateTitle();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save settings:\n{ex.Message}", "Save Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private void BtnNew_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Reset all settings to defaults?", "New Settings",
                                         MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;
            ApplySettings(new SettingsFile());
            _currentSettingsPath = null;
            _isDirty = false;
            UpdateTitle();
        }

        private void BtnOpen_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title  = "Open Settings Or Template File",
                Filter = "Template Settings (*.oetgs)|*.oetgs|RMG Template (*.rmg.json)|*.rmg.json|All files (*.*)|*.*",
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                var json = File.ReadAllText(dlg.FileName);

                bool isSettingsFile = dlg.FileName.EndsWith(".oetgs", StringComparison.OrdinalIgnoreCase);
                if (isSettingsFile)
                {
                    var s = JsonSerializer.Deserialize<SettingsFile>(json, JsonOptions);
                    if (s is null) throw new InvalidDataException("File is empty or invalid.");
                    ApplySettings(s);
                    _currentSettingsPath = dlg.FileName;
                }
                else
                {
                    var template = JsonSerializer.Deserialize<RmgTemplate>(json, JsonOptions);
                    if (template is null) throw new InvalidDataException("Template file is empty or invalid.");
                    var imported = TemplateImportService.ImportToSettings(template);
                    ApplySettings(imported);
                    _currentSettingsPath = null;

                    // Show preview from the imported template so users can edit from this state directly.
                    _generatedTemplate = template;
                    _generatedTopology = imported.Topology;
                    _templateOutdated = false;
                    ImgPreview.Source = TemplatePreviewPngWriter.Render(template, imported.Topology);
                    lblNoPreview.Content = "?";
                    BtnSaveGenerated.Visibility = Visibility.Visible;
                    UpdateOutdatedWarning();
                }

                _isDirty = false;
                UpdateTitle();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open file:\n{ex.Message}", "Open Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSettingsPath is not null)
                SaveToPath(_currentSettingsPath);
            else
                BtnSaveAs_Click(sender, e);
        }

        private void BtnSaveAs_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Title      = "Save Settings As",
                Filter     = "Template Settings (*.oetgs)|*.oetgs|All files (*.*)|*.*",
                FileName   = TxtTemplateName.Text.Trim().Length > 0 ? TxtTemplateName.Text.Trim() : "My Settings",
                DefaultExt = ".oetgs",
            };
            if (dlg.ShowDialog() == true)
                SaveToPath(dlg.FileName);
        }

        // -- Generate ----------------------------------------------------------

        // The most recently generated template — used by BtnSaveGenerated_Click
        private RmgTemplate? _generatedTemplate;
        private MapTopology  _generatedTopology;
        private bool _templateOutdated = false;

        // ── Zone Connection Editor state ──────────────────────────────────────
        private bool _connectionsEditedByUser = false;
        // Snapshot for "Reset to Generated" in the editor.
        private List<Connection>? _originalGeneratedConnections;
        private HashSet<string> _playerZoneNames = new(StringComparer.Ordinal);
        // True when the editor reported unresolved zone-reference errors; blocks export.
        private bool _connectionsHaveErrors = false;
        private readonly DispatcherTimer _connectionPreviewRefreshDebounce = new() { Interval = TimeSpan.FromMilliseconds(120) };
        private bool _connectionPreviewRefreshPending = false;

        private void BtnPreview_Click(object sender, RoutedEventArgs e)
        {
            if (!Validate()) return;

            if (_connectionsEditedByUser)
            {
                var proceed = MessageBox.Show(
                    "Re-generating will discard all of your custom connections. Proceed?",
                    "Discard Custom Connections",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (proceed != MessageBoxResult.Yes) return;

                _connectionsEditedByUser = false;
                _connectionsHaveErrors   = false;
            }

            var settings = BuildSettings();
            _generatedTemplate = TemplateGenerator.Generate(settings);
            _generatedTopology = settings.Topology;

            var variant = _generatedTemplate.Variants?.FirstOrDefault();

            // Snapshot the freshly-generated connections for "Reset to Generated"
            _originalGeneratedConnections = variant?.Connections?
                .Select(c => ZoneConnectionEditorWindow.CloneConnection(c))
                .ToList() ?? [];

            // Collect player zone names for the editor colour coding
            _playerZoneNames = variant?.Zones?
                .Where(z => z.Name.StartsWith("Spawn-", StringComparison.Ordinal))
                .Select(z => z.Name)
                .ToHashSet(StringComparer.Ordinal) ?? new(StringComparer.Ordinal);

            _templateOutdated = false;
            ImgPreview.Source = TemplatePreviewPngWriter.Render(_generatedTemplate, _generatedTopology);
            lblNoPreview.Content = "?";
            BtnSaveGenerated.Visibility   = Visibility.Visible;
            InitializeConnectionEditorForGeneratedTemplate();
            UpdateOutdatedWarning();
            Validate(); // refresh warnings now that template is up to date
        }

        private void InitializeConnectionEditorForGeneratedTemplate()
        {
            var variant = _generatedTemplate?.Variants?.FirstOrDefault();
            if (variant is null) return;

            variant.Connections ??= [];

            ConnectionEditorControl.InitializeEditor(
                variant.Zones ?? [],
                variant.Connections,
                _originalGeneratedConnections ?? [],
                _generatedTopology,
                _playerZoneNames);

            ConnectionEditorControl.Visibility = Visibility.Visible;
            PnlConnectionEditorHint.Visibility = Visibility.Collapsed;
            _connectionsHaveErrors = ConnectionEditorControl.HasUnresolvedErrors;
        }

        private void ConnectionEditorControl_ConnectionsModified(object? sender, EventArgs e)
        {
            _connectionsEditedByUser = true;
            _connectionPreviewRefreshPending = true;
            _connectionPreviewRefreshDebounce.Stop();
            _connectionPreviewRefreshDebounce.Start();
            _connectionsHaveErrors = ConnectionEditorControl.HasUnresolvedErrors;
        }

        private void ConnectionPreviewRefreshDebounce_Tick(object? sender, EventArgs e)
        {
            _connectionPreviewRefreshDebounce.Stop();
            FlushPendingConnectionPreviewRefresh();
        }

        private void FlushPendingConnectionPreviewRefresh()
        {
            if (!_connectionPreviewRefreshPending)
                return;

            _connectionPreviewRefreshPending = false;

            if (_generatedTemplate?.Variants?.FirstOrDefault() is not Variant variant)
                return;

            TemplateGenerator.RegenerateZoneRoads(variant.Zones ?? [], variant.Connections ?? []);
            ImgPreview.Source = TemplatePreviewPngWriter.Render(_generatedTemplate, _generatedTopology);
        }

        private void ConnectionEditorControl_ErrorsChanged(object? sender, EventArgs e)
        {
            _connectionsHaveErrors = ConnectionEditorControl.HasUnresolvedErrors;
        }

        private void BtnSaveGenerated_Click(object sender, RoutedEventArgs e)
        {
            if (_generatedTemplate is null) return;

            FlushPendingConnectionPreviewRefresh();

            if (_connectionsHaveErrors)
            {
                MessageBox.Show(
                    "Connection editor contains invalid zone references. Open the editor to resolve them before saving.",
                    "Export Blocked",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            string? gameTemplatesPath = FindOldenEraTemplatesPath();

            string currentTemplateName = TxtTemplateName.Text.Trim();

            var dlg = new SaveFileDialog
            {
                Title = "Save Template",
                Filter = "RMG Template (*.rmg.json)|*.rmg.json",
                FileName = $"{(currentTemplateName.Length > 0 ? currentTemplateName : "Custom Template")}.rmg.json",
                DefaultExt = ".rmg.json"
            };

            if (gameTemplatesPath != null)
                dlg.InitialDirectory = gameTemplatesPath;

            if (dlg.ShowDialog() != true) return;

            if (!IsInsideGameTemplatesFolder(dlg.FileName, gameTemplatesPath))
            {
                string expectedDesc = gameTemplatesPath != null
                    ? $"Expected:\n{gameTemplatesPath}\n\n"
                    : $"Expected folder structure:\n...\\HeroesOldenEra_Data\\StreamingAssets\\map_templates\n\n";
                var wrongFolderResult = MessageBox.Show(
                    $"The file is being saved outside the expected templates folder.\n\n{expectedDesc}Chosen:\n{Path.GetDirectoryName(dlg.FileName)}\n\nTemplates saved elsewhere will not appear in-game. Save here anyway?",
                    "Wrong Folder",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (wrongFolderResult != MessageBoxResult.Yes) return;
            }

            string chosenBaseName = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(dlg.FileName));
            if (!chosenBaseName.Equals(currentTemplateName, StringComparison.Ordinal))
            {
                var mismatchResult = MessageBox.Show(
                    $"The file will be saved as \"{Path.GetFileName(dlg.FileName)}\", but the template will appear in-game as \"{currentTemplateName}\".\n\nClick Yes to save anyway, or No to go back and rename the template first.",
                    "Template Name Mismatch",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (mismatchResult != MessageBoxResult.Yes) return;
            }

            string json = JsonSerializer.Serialize(_generatedTemplate, JsonOptions);
            File.WriteAllText(dlg.FileName, json);

            string previewPath = TemplatePreviewPngWriter.GetSidecarPath(dlg.FileName);
            string? previewError = null;
            if (ChkSavePreviewImage.IsChecked == true)
            {
                try
                {
                    TemplatePreviewPngWriter.Save(_generatedTemplate, previewPath, _generatedTopology);
                }
                catch (Exception ex)
                {
                    previewError = ex.Message;
                }
            }

            string savedMsg = $"Template successfully saved to:\n\n{dlg.FileName}";
            if (ChkSavePreviewImage.IsChecked == true)
            {
                if (previewError == null)
                    savedMsg += $"\n\nPreview PNG saved to:\n\n{previewPath}";
                else
                    savedMsg += $"\n\nTemplate saved, but the preview PNG could not be written:\n{previewError}";
            }
            if (gameTemplatesPath == null)
                savedMsg += "\n\n\n💡 Tip: Templates must be placed in:\n<Olden Era install folder>\\HeroesOldenEra_Data\\StreamingAssets\\map_templates";

            MessageBox.Show(savedMsg, "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private GeneratorSettings BuildSettings() => new()
        {
            TemplateName = TxtTemplateName.Text.Trim(),
            GameMode = CmbGameMode.SelectedItem as string ?? "Classic",
            SingleHeroMode = ChkSingleHeroMode.IsChecked == true,
            PlayerCount = (int)SldPlayers.Value,
            HeroSettings = new HeroSettings
            {
                HeroCountMin = (int)SldHeroMin.Value,
                HeroCountMax = (int)SldHeroMax.Value,
                HeroCountIncrement = (int)SldHeroIncrement.Value
            },
            MapSize = SelectedMapSize(),
            GameEndConditions = new GameEndConditions
            {
                VictoryCondition = CmbVictory.SelectedIndex >= 0 && CmbVictory.SelectedIndex < KnownValues.VictoryConditionIds.Length
                    ? KnownValues.VictoryConditionIds[CmbVictory.SelectedIndex]
                    : "win_condition_1",
                LostStartCity = ChkLostStartCity.IsChecked == true,
                LostStartCityDay = (int)SldLostStartCityDay.Value,
                LostStartHero = ChkLostStartHero.IsChecked == true,
                CityHold = ChkCityHold.IsChecked == true,
                CityHoldDays = (int)SldCityHoldDays.Value,
            },
            ZoneCfg = new ZoneConfiguration
            {
                NeutralZoneCount = (int)SldNeutral.Value,
                PlayerZoneCastles = (int)SldPlayerCastles.Value,
                NeutralZoneCastles = (int)SldNeutralCastles.Value,
                SpawnAbandonedOutposts = ChkSpawnAbandonedOutposts.IsChecked == true,
                ResourceDensityPercent = (int)SldResourceDensity.Value,
                StructureDensityPercent = (int)SldStructureDensity.Value,
                NeutralStackStrengthPercent = (int)SldNeutralStackStrength.Value,
                BorderGuardStrengthPercent = (int)SldBorderGuardStrength.Value,
                HubZoneSize = SldHubZoneSize.Value,
                HubZoneCastles = (int)SldHubCastles.Value,
                Advanced = new AdvancedSettings
                {
                    Enabled = _advancedZoneSettings,
                    NeutralLowNoCastleCount = (int)SldNeutralLowNoCastle.Value,
                    NeutralLowCastleCount = (int)SldNeutralLowCastle.Value,
                    NeutralMediumNoCastleCount = (int)SldNeutralMediumNoCastle.Value,
                    NeutralMediumCastleCount = (int)SldNeutralMediumCastle.Value,
                    NeutralHighNoCastleCount = (int)SldNeutralHighNoCastle.Value,
                    NeutralHighCastleCount = (int)SldNeutralHighCastle.Value,
                    PlayerZoneSize = _advancedZoneSettings ? SldPlayerZoneSize.Value : 1.0,
                    NeutralZoneSize = _advancedZoneSettings ? SldNeutralZoneSize.Value : 1.0,
                    GuardRandomization = _advancedZoneSettings ? SldGuardRandomization.Value / 100.0 : 0.05,
                }
            },
            ZoneContent = new ZoneContent
            {
                PlayerZoneMandatoryContent = BuildZoneMandatoryContentFromUi(_playerZoneMandatoryContent),
                LowNeutralMandatoryContent = BuildZoneMandatoryContentFromUi(_lowNeutralMandatoryContent),
                MediumNeutralMandatoryContent = BuildZoneMandatoryContentFromUi(_mediumNeutralMandatoryContent),
                HighNeutralMandatoryContent = BuildZoneMandatoryContentFromUi(_highNeutralMandatoryContent),
                HubZoneMandatoryContent = BuildZoneMandatoryContentFromUi(_hubZoneMandatoryContent),
            },
            // Neutral zones between players can be influenced by advanced zone settings, but is functionally independent.
            MinNeutralZonesBetweenPlayers = _advancedZoneSettings ? (int)SldMinNeutralBetweenPlayers.Value : 0,
            MatchPlayerCastleFactions = ChkMatchPlayerCastleFactions.IsChecked == true,
            PlayerStartsWithCastles   = ChkPlayerStartsWithCastles.IsChecked == true,
            NoDirectPlayerConnections = ChkNoDirectPlayerConn.IsChecked == true,
            RandomPortals = ChkRandomPortals.IsChecked == true,
            MaxPortalConnections = (int)SldMaxPortals.Value,
            SpawnRemoteFootholds = ChkSpawnFootholds.IsChecked == true,
            GenerateRoads = ChkGenerateRoads.IsChecked == true,
            Topology = CmbTopology.SelectedIndex >= 0 ? TopologyOptions[CmbTopology.SelectedIndex].Topology : MapTopology.Default,
            FactionLawsExpPercent = (int)SldFactionLawsExp.Value,
            AstrologyExpPercent = (int)SldAstrologyExp.Value,
            GladiatorArenaRules = new GladiatorArenaRules
            {
                Enabled = ChkGladiatorArena.IsChecked == true,
                DaysDelayStart = (int)SldGladiatorDelay.Value,
                CountDay = (int)SldGladiatorCountDay.Value
            },
            TournamentRules = new TournamentRules
            {
                Enabled = ChkTournament.IsChecked == true,
                FirstTournamentDay = (int)SldTournamentFirstTournamentDay.Value,
                Interval = (int)SldTournamentInterval.Value,
                PointsToWin = (int)SldTournamentPointsToWin.Value,
                SaveArmy = ChkTournamentSaveArmy.IsChecked == true
            },
            BannedItems        = string.Join("\n", _bannedItems.Select(e => e.Id)),
            BannedMagics       = string.Join("\n", _bannedMagics.Select(e => e.Id)),
            ValueOverridesText = TxtValueOverrides.Text,
            Bonuses            = [.. _bonuses],
        };
        
        /* Creates list of ContentItems for the zone mandatory content, according to the UI settings. */
        private List<ContentItem> BuildZoneMandatoryContentFromUi(ZoneMandatoryContent content)
        {
            var result = new List<ContentItem>();
            foreach (var item in content.AllItems)
            {
                /* Some initial sanity checks*/
                if (item.Count <= 0) continue;
                if(item.SidMapping == null) continue;

                
                for (int i = 0; i < item.Count; i++)
                {
                    if (item.IsGroup)
                    {
                        var groupItem = new ContentItem
                        {
                            IncludeLists = new List<string> { item.SidMapping.Sid }
                        };
                        ContentRuleManager.ApplyRulesToFinalContentItem(groupItem, item);

                        result.Add(groupItem);
                        continue;
                    }
                    else
                    {
                        var builder = ContentItemBuilder
                            .Create(item.SidMapping.Sid);
                        
                        if(content.mines.Contains(item))
                            builder.Mine();
                        
                        ContentItem finalItem = builder.Build();
                        ContentRuleManager.ApplyRulesToFinalContentItem(finalItem, item);
                        
                        result.Add(finalItem);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Serializes a zone's UI rows directly as <see cref="ZoneContentRowSave"/> records,
        /// preserving Count and row identity so that two separate sawmill rows remain two rows
        /// after a round-trip (unlike the legacy ContentItem expansion path).
        /// </summary>
        private static List<ZoneContentRowSave> BuildZoneContentRows(ZoneMandatoryContent content)
        {
            var rows = new List<ZoneContentRowSave>();
            foreach (var item in content.AllItems)
            {
                if (item.SidMapping == null) continue;
                rows.Add(new ZoneContentRowSave
                {
                    Sid          = item.SidMapping.Sid,
                    Count        = item.Count,
                    IsGroup      = item.IsGroup,
                    Rules        = BuildRuleRows(item.Rules),
                    IsMine       = content.mines.Contains(item),
                });
            }
            return rows;
        }

        private static List<ContentRuleRowSave>? BuildRuleRows(List<IContentRule> rules)
        {
            if (rules.Count == 0)
                return null;

            var result = new List<ContentRuleRowSave>();
            foreach (IContentRule rule in rules)
            {
                result.Add(rule.SerializeToRowSave());
            }

            return result;
        }

        private static List<IContentRule> RestoreContentRulesFromRow(ZoneContentRowSave row, SidMapping contentItem)
        {
            var result = new List<IContentRule>();

            /* New format: explicit serialized rules. */
            if (row.Rules is { Count: > 0 })
            {
                foreach (ContentRuleRowSave savedRule in row.Rules)
                {
                    var rule = ContentRuleManager.CreateRuleFromSavedRule(savedRule, contentItem);
                    if (rule != null)
                    {
                        result.Add(rule);
                    }
                }

                return result;
            }

            /* Legacy fallback for old saves. (0.7.1 and earlier only) */
            if (row.IsGuarded)
            {
                result.Add(new RuleGuarded(true));
            }
            else
            {
                /* People had to manually uncheck this, so create unguarded rule to perserve their intent */
                result.Add(new RuleGuarded(false));
            }
            if (!string.IsNullOrWhiteSpace(row.RoadDistance) && !string.Equals(row.RoadDistance, "Any", StringComparison.OrdinalIgnoreCase))
            {
                result.Add(new RuleDistanceToRoad(DistancePresets.GetDistanceVariationByName(row.RoadDistance)));
            }
            if (row.NearCastle)
            {
                result.Add(new RuleDistanceToTown(DistancePresets.Near));
            }

            return result;
        }

        /* Helper function for checking if a SID belongs to a content item group */
        private static bool IsContentItemGroupSid(string sid, List<SidMapping> groupItems)
            => groupItems.Any(item => string.Equals(item.Sid, sid, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Returns true when
        /// (including any sub-folders, since the game supports those).
        /// If <paramref name="gameTemplatesPath"/> was resolved, a prefix match is used.
        /// Otherwise the chosen directory is checked against the known folder-structure tail
        /// <c>HeroesOldenEra_Data\StreamingAssets\map_templates</c>.
        /// </summary>
        private static bool IsInsideGameTemplatesFolder(string filePath, string? gameTemplatesPath)
        {
            string chosenDir = Path.GetDirectoryName(filePath) ?? string.Empty;

            if (gameTemplatesPath != null)
            {
                // Normalise both paths to ensure consistent separator and casing comparison.
                string normalised = Path.GetFullPath(chosenDir);
                string expected   = Path.GetFullPath(gameTemplatesPath);
                // Accept the folder itself or any sub-folder inside it.
                return normalised.Equals(expected, StringComparison.OrdinalIgnoreCase)
                    || normalised.StartsWith(expected + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
            }

            // Game not found via registry/fallback paths — match on the known folder-structure tail.
            const string expectedTail = @"HeroesOldenEra_Data\StreamingAssets\map_templates";
            return chosenDir.EndsWith(expectedTail, StringComparison.OrdinalIgnoreCase)
                || chosenDir.Contains(expectedTail + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Tries to locate the Olden Era map_templates folder via the Steam registry.
        /// Returns null if the game installation cannot be found.
        /// </summary>
        private static string? FindOldenEraTemplatesPath()
        {
            // Olden Era Steam App ID
            const string appId = "3105440";

            // Steam stores per-app install paths under this key.
            string[] registryRoots =
            [
                $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App {appId}",
                $@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Steam App {appId}"
            ];

            foreach (var keyPath in registryRoots)
            {
                try
                {
                    using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(keyPath);
                    if (key?.GetValue("InstallLocation") is string installDir && Directory.Exists(installDir))
                    {
                        string templatesDir = Path.Combine(installDir, "HeroesOldenEra_Data", "StreamingAssets", "map_templates");
                        if (Directory.Exists(templatesDir))
                            return templatesDir;
                    }
                }
                catch { /* registry access denied — skip */ }
            }

            // Fallback: check common Steam library locations manually.
            string[] steamLibraryRoots =
            [
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "Heroes of Might and Magic Olden Era"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),    "Steam", "steamapps", "common", "Heroes of Might and Magic Olden Era"),
            ];

            foreach (var candidate in steamLibraryRoots)
            {
                string templatesDir = Path.Combine(candidate, "HeroesOldenEra_Data", "StreamingAssets", "map_templates");
                if (Directory.Exists(templatesDir))
                    return templatesDir;
            }
            return null;
        }
        private void ChkSavePreviewImage_Click(object sender, RoutedEventArgs e)
        {
            if (ChkSavePreviewImage.IsChecked == true)
            {
                ImgPreview.Visibility = Visibility.Visible;
                lblNoPreview.Visibility = Visibility.Collapsed;

            }
            else
            {
                ImgPreview.Visibility = Visibility.Collapsed;
                lblNoPreview.Visibility = Visibility.Visible;
            }
        }

        private void PlayerZonesScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // Walk headers from last to first; show the sticky row for the last one
            // whose top edge has scrolled above the viewport top.
            var headers = new[]
            {
                (Element: TxtHeaderHeroImprovementStructures, Sticky: StickyHeroImprovementStructures),
                (Element: TxtHeaderUtilityStructures, Sticky: StickyUtilityStructures),
                (Element: TxtHeaderResourceBanks, Sticky: StickyResourceBanks),
                (Element: TxtHeaderUnitRecruitment, Sticky: StickyUnitRecruitment),
                (Element: TxtHeaderTreasures,     Sticky: StickyTreasures),
                (Element: TxtHeaderMines,         Sticky: StickyMines),
            };

            System.Windows.Controls.DockPanel? active = null;
            foreach (var (element, sticky) in headers)
            {
                var pos = element.TranslatePoint(new System.Windows.Point(0, 0), PlayerZonesScrollViewer);
                if (pos.Y < 0)
                {
                    active = sticky;
                    break;
                }
            }

            // If nothing has scrolled out of view, hide the sticky panel entirely (no duplication).
            if (active == null)
            {
                StickyHeaderPanel.Visibility = Visibility.Collapsed;
                return;
            }

            StickyHeaderPanel.Visibility   = Visibility.Visible;
            StickyMines.Visibility         = Visibility.Collapsed;
            StickyTreasures.Visibility     = Visibility.Collapsed;
            StickyUnitRecruitment.Visibility = Visibility.Collapsed;
            StickyResourceBanks.Visibility = Visibility.Collapsed;
            StickyUtilityStructures.Visibility = Visibility.Collapsed;
            StickyHeroImprovementStructures.Visibility = Visibility.Collapsed;
            active.Visibility              = Visibility.Visible;
        }



        private void BtnDiscord_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = DiscordServer,
                UseShellExecute = true
            });
        }

        private void BtnPatchNotes_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = GitHubReleasesPage,
                UseShellExecute = true
            });
        }

        private void BtnGithub_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = GitHubPage,
                UseShellExecute = true
            });
        }
    }
}
