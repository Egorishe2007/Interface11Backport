using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace Interface10
{
    // Interface 1.1 Backport: the interface of EFT 1.0-1.2 on SPT's 0.16 client, screen by screen, each one switched on
    // and off here. The screens are rebuilt from the later clients' own UI scenes (layouts, sizes, colours, sprites and
    // texts of their MenuUIScene etc.). Name and GUID as SPT Forge wants them ("<user>-<mod>", com.<user>.<mod>).
    [BepInPlugin(Guid, "Grifm0le-Interface11Backport", "1.0.1")]
    // Soft dependency, loaded first when it is there: VisitAPI (it builds the character's tasks tab itself, so ours stays
    // out of it). The tutorial mod (Tutorial Backport) depends on this one instead: see Tutorial.Present.
    [BepInDependency("com.sora.visitapi", BepInDependency.DependencyFlags.SoftDependency)]
    public class Interface10Plugin : BaseUnityPlugin
    {
        internal const string Guid = "com.grifm0le.interface11backport";

        internal static ManualLogSource Log;
        internal static string Dir;
        internal static ConfigEntry<bool> LoadingScreen;
        internal static ConfigEntry<bool> LocationScreen;
        internal static ConfigEntry<bool> Buttons;
        internal static ConfigEntry<bool> HideBetaBanner;
        internal static ConfigEntry<float> ButtonsLift;
        internal static ConfigEntry<bool> HideOfflineVoipWarning;
        internal static ConfigEntry<bool> Handbook;
        internal static ConfigEntry<bool> MenuColumn;
        internal static ConfigEntry<MenuBackground> Background;
        internal static ConfigEntry<bool> Logo10;
        internal static ConfigEntry<bool> Settings;
        internal static ConfigEntry<bool> QuestGroups;
        internal static ConfigEntry<bool> CharacterTab;
        internal static ConfigEntry<bool> TasksTab;
        internal static ConfigEntry<bool> ClothingSlots;
        internal static ConfigEntry<bool> NotForFlea;
        internal static ConfigEntry<bool> Windows;
        internal static ConfigEntry<bool> ItemCells;
        internal static ConfigEntry<bool> TaskView;
        internal static ConfigEntry<bool> SkillsList;
        internal static ConfigEntry<bool> MessengerTabs;
        internal static ConfigEntry<bool> RaidPrep;
        internal static ConfigEntry<bool> RaidEnd;
        internal static ConfigEntry<bool> Icons12;
        internal static ConfigEntry<bool> InventoryWindows;
        internal static ConfigEntry<bool> Market;
        internal static ConfigEntry<bool> Splash;
        internal static ConfigEntry<bool> RaidHud;

        private void Awake()
        {
            Log = Logger;
            Dir = Path.GetDirectoryName(Info.Location);
            LoadingScreen = Config.Bind("Screens", "Raid loading screen", true,
                "1.0's raid loading screen: the loading stage, a progress bar in segments and the loading time under it");
            LocationScreen = Config.Bind("Screens", "Location selection", true,
                "1.0's location selection: the location names on the map as 1.0's toggles, the buttons placed as in 1.0. " +
                "Takes effect the next time the screen opens.");
            Buttons = Config.Bind("Look", "1.0 buttons", true,
                "1.0's buttons everywhere: the plate under the text on hover is a worn stroke of light paint. Needs a game restart.");
            HideBetaBanner = Config.Bind("Menu", "Hide beta banner", true,
                "Hide the orange 'This is a Beta version' banner in the main menu. The raid reconnect notice is not affected.");
            ButtonsLift = Config.Bind("Menu", "Lift buttons without banner", 110f,
                new ConfigDescription("How far the main menu buttons move up into the space the hidden banner leaves, in 1080p UI units. 0 keeps them in place.",
                    new AcceptableValueRange<float>(0f, 300f)));
            HideOfflineVoipWarning = Config.Bind("Raid", "Keep VoIP in offline raids", true,
                "Offline raids mark VoIP as unavailable and show a warning in the sound settings. Keeps VoIP as in online raids.");
            Handbook = Config.Bind("Screens", "Handbook notes and tapes", true,
                "As in 1.0: the tutorial mod's notes and tapes are in the Handbook once found, a note can be read there and a tape " +
                "played (with the recorder in a special slot or the Intelligence Center built), its transcript under the description.");
            MenuColumn = Config.Bind("Menu", "1.0 button column", true,
                "The main menu's buttons placed as in 1.0: a column around the middle of the screen, EXIT apart under it.");
            Background = Config.Bind("Menu", "Background", MenuBackground.Game,
                "The main menu's background scene. Game keeps the choice in the game's settings (Game > Main menu background); Factory has the lamp over the logo.");
            Settings = Config.Bind("Screens", "1.0 settings", true,
                "1.0's settings screen: one column of rows on a dark panel, the names on the left, drop-downs, sliders and check boxes " +
                "at the right end, the nickname's bar grey. Takes effect when the settings open; switching it off needs a game restart.");
            QuestGroups = Config.Bind("Screens", "Trader task groups", true,
                "1.2's trader tasks: the list in groups (loyalty levels, key tasks, operational tasks) that open and close with a " +
                "click, and buttons to switch between that and the plain list. Takes effect the next time a trader's tasks open.");
            CharacterTab = Config.Bind("Screens", "Character tab", true,
                "1.2's CHARACTER tab on the character screen: the faces of your side to choose from, the face large, the voice " +
                "with a button to hear it (the voice leaves the sound settings, as in 1.2). Changing the face needs Interface10's " +
                "server part. Takes effect the next time the character screen opens.");
            TasksTab = Config.Bind("Screens", "Character tasks tab", true,
                "1.2's TASKS tab on the character screen: the task type toggles in a bar right over the list, the list and the " +
                "quest items as high as the screen. Not with VisitAPI, which builds this tab its own way. Takes effect after a restart.");
            ClothingSlots = Config.Bind("Screens", "Clothing cells", true,
                "1.2's clothing on the character screen: a cell with the picture of the top and of the bottom, the name under it, " +
                "the other suites in a column over it (the arrow over the cell). Takes effect the next time the character screen opens.");
            NotForFlea = Config.Bind("Items", "Not for flea market line", true,
                "1.2's line on the description of an item that can't be sold on the flea market (inspect window and Handbook): " +
                "the flea market's arrows and NOT FOR SALE ON THE FLEA MARKET among the item's characteristics. " +
                "Takes effect the next time an item's description opens.");
            Windows = Config.Bind("Look", "1.2 windows", true,
                "1.2's windows: captions 20 high with the title in the middle, the inspect window wider (800) with its " +
                "caption over the contents, container windows in a grey frame, the confirmation window's margins, the error screens' captions. Needs a game restart.");
            ItemCells = Config.Bind("Look", "1.2 item cells", true,
                "1.2's item cells: a thinner frame, the texts cut at the cell's edges, 1.2's redrawn icons (secured " +
                "container, lock, favourites, insurance, mod types). Needs a game restart.");
            TaskView = Config.Bind("Screens", "1.2 task view", true,
                "1.2's task view at the traders: every reward in its own cell (experience, skills and standing with 1.2's " +
                "pictures), titled Rewards, the rewards of the tasks this one opens under them, the task's picture in a grey frame. " +
                "Takes effect the next time a task is shown.");
            SkillsList = Config.Bind("Screens", "1.2 skills list", true,
                "1.2's skills on the character screen: without the skills that do nothing (first aid, field medicine, night " +
                "and silent operations, auctions, clean operations, shadow connections, task performance). Needs a game restart.");
            MessengerTabs = Config.Bind("Screens", "1.2 messenger", true,
                "1.2's messenger: the dialogues in two tabs over their list (PMC frequency: players; Special communications: " +
                "traders and the system), NO ACTIVE CHATS in an empty tab, the friends button at the foot of the list. Needs a game restart.");
            RaidPrep = Config.Bind("Screens", "1.2 raid preparation", true,
                "1.2's screens before a raid: the offline raid settings with 1.2's drop-downs and check marks, the insurance " +
                "with numbered steps, the insurer's figures in a box and the money under the list, the ready screen's name " +
                "row; the buttons at the foot a little higher (also on the side selection, entry points and key access screens). Takes effect the next time a screen opens; switching it off needs a game restart.");
            RaidEnd = Config.Bind("Screens", "1.2 raid end", true,
                "1.2's screens after a raid: 1.2's clock by the raid time, the kill list wider with 1.2's columns and NO KILLS " +
                "over stripes, the location's banner over the statistics, the experience figures at the right end, the " +
                "treatment's condition icons in one column; the buttons at the foot a little higher. Switching it off needs a game restart.");
            Icons12 = Config.Bind("Look", "1.2 icons", true,
                "1.2's redrawn icons wherever the game shows them: the stash filters, health and condition icons, character " +
                "screen tabs (at 24 from the left, 24 x 24), item context menu, money, builds, hideout areas. Needs a game restart.");
            InventoryWindows = Config.Bind("Screens", "1.2 inventory windows", true,
                "1.2's inventory windows (character, Scav, item transfer, builds, weapon modding, hideout handovers): the weight and health row " +
                "under the character, the stash's buttons and money, key labels over the quick slots that grow with the key, " +
                "1.2's check boxes and lights on the weapon in the builds and modding screens. Switching it off needs a game restart.");
            Market = Config.Bind("Screens", "1.2 flea market", true,
                "1.2's flea market filter window (caption, drop-downs and check marks) and the trading screen's TRADERS / " +
                "FLEA MARKET toggles. Takes effect the next time they open; switching it off needs a game restart.");
            RaidHud = Config.Bind("Screens", "1.2 raid interface", true,
                "1.2's raid interface: the exits list (O) wider with 1.2's sizes, the extraction, transit and event messages " +
                "centred on a wider plate, the raid's PostFX screen at the top left. Takes effect in the next raid.");
            Splash = Config.Bind("Look", "1.2 loading screen", true,
                "1.2's loading screen at the game's start: its four new pictures among the game's, each shown as often, and " +
                "1.2's copyright line (2026, its (c) icon, no Quixel logo). Takes effect at the next start.");
            Logo10 = Config.Bind("Menu", "1.0 logo", true,
                "The main menu's logo as in 1.0, without BETA TESTING over it. Takes effect when the background comes up again.");
            MenuColumn.SettingChanged += (_, _) => MenuLayout.Refresh();
            HideBetaBanner.SettingChanged += (_, _) => MenuLayout.Refresh();
            ButtonsLift.SettingChanged += (_, _) => MenuLayout.Refresh();

            gameObject.AddComponent<LogoSwap>();
            var harmony = new Harmony(Guid);
            harmony.PatchAll(typeof(Interface10Plugin).Assembly);
            Splash10.Late();
        }
    }
}
