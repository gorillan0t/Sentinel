using BepInEx.Configuration;

namespace Sentinel;

public static class Cfg
{
    public static ConfigEntry<int> Theme;

    public static ConfigEntry<int> CloseBtn;

    public static ConfigEntry<int> OpenBtn;

    public static ConfigEntry<int> TagFont;

    public static ConfigEntry<int> TagSize;

    public static ConfigEntry<int> TagColor;

    public static ConfigEntry<int> MenuSfx;

    public static ConfigEntry<int> MenuLayout;

    public static ConfigEntry<bool> TagBold;

    public static ConfigEntry<bool> KeepMenuOpen;

    public static ConfigEntry<bool> PlayerRay;

    public static ConfigEntry<string> NotifMode;

    public static ConfigEntry<string> SpotifyId;

    public static ConfigEntry<string> YtChannel;

    public static ConfigEntry<bool> Tags;

    public static ConfigEntry<bool> TagFps;

    public static ConfigEntry<bool> TagPlat;

    public static ConfigEntry<bool> TagMenu;

    public static ConfigEntry<bool> BoardColors;

    public static ConfigEntry<bool> Gesture;

    public static ConfigEntry<bool> Palm;

    public static ConfigEntry<bool> Broadcast;

    public static ConfigEntry<bool> AutoJoin;

    public static ConfigEntry<bool> Sounds;

    public static ConfigEntry<bool> Tooltips;

    public static ConfigEntry<bool> SwapHands;

    public static ConfigEntry<bool> Touch;

    public static ConfigEntry<bool> HandMenu;

    public static ConfigEntry<bool> OneHand;

    public static int Revision { get; private set; }

    public static void Load(ConfigFile config)
    {
        Theme     = config.Bind("menu",   "theme", 0,     "0 hamburbur, 1 blue, 2 violet, 3 ember, 4 mint, 5 gold, 6 sentinel");
        NotifMode = config.Bind("notifs", "mode",  "all", "all, cheats, off");

        Tags        = config.Bind("tags",    "enabled",   true);
        TagFps      = config.Bind("tags",    "fps",       true);
        TagPlat     = config.Bind("tags",    "platform",  true);
        TagMenu     = config.Bind("tags",    "sentinel",  true, "show the menu icon over other sentinel users");
        TagFont     = config.Bind("tags",    "font",      0,    "0 default, 1 sans, 2 serif, 3 mono");
        TagSize     = config.Bind("tags",    "size",      1,    "0 small, 1 normal, 2 large");
        TagColor    = config.Bind("tags",    "color",     0,    "0 player, 1 theme, 2 white");
        TagBold     = config.Bind("tags",    "bold",      false);
        BoardColors = config.Bind("board",   "colors",    true);
        Gesture     = config.Bind("gesture", "enabled",   true);
        Palm        = config.Bind("disc",    "palm",      true);
        Broadcast   = config.Bind("net",     "broadcast", true,  "announce yourself to other sentinel users, off means receive only");
        AutoJoin    = config.Bind("net",     "autojoin",  false, "join a random public room whenever you are not in one");

        CloseBtn     = config.Bind("menu",     "close",          0,     "double tap to close, 0 X, 1 Y, 2 A, 3 B");
        OpenBtn      = config.Bind("menu",     "open",           3,     "press to open, 0 X, 1 Y, 2 A, 3 B");
        OneHand      = config.Bind("menu",     "onehand",        false, "menu drops in front of you and one hand runs it with the laser");
        Sounds       = config.Bind("menu",     "sounds",         true);
        MenuSfx      = config.Bind("menu",     "sfx_pack",       0, "0 classic, 1 soft, 2 arcade, 3 crystal");
        MenuLayout   = config.Bind("menu",     "layout",         0, "0 disc, 1 rectangle");
        Tooltips     = config.Bind("menu",     "tooltips",       true);
        KeepMenuOpen = config.Bind("menu",     "keep_open",      false, "keep the hand menu open when looking away");
        PlayerRay    = config.Bind("controls", "player_raycast", false, "select a player with a VR controller ray or desktop mouse ray");
        Touch        = config.Bind("menu",     "touch",          false, "poke the menu with your hands instead of the laser");
        HandMenu     = config.Bind("disc",     "handmenu",       false, "tap the disc to open the menu on your palm instead of throwing it");
        SwapHands    = config.Bind("disc",     "swap",           false, "disc sits in the right palm and you grab with the left");

        SpotifyId = config.Bind("spotify", "client_id", "", "your spotify app client id");
        YtChannel = config.Bind("youtube", "channel",   "", "your handle, channel, or stream URL");

        config.SettingChanged += OnSettingChanged;
    }

    private static void OnSettingChanged(object sender, SettingChangedEventArgs args)
    {
        Revision++;

        if (args.ChangedSetting == SpotifyId)
        {
            SpotifyPlayer.Initialize();

            return;
        }

        if (args.ChangedSetting == YtChannel)
        {
            YouTubeLiveChat.Initialize();
        }
    }
}