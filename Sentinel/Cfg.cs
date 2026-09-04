using BepInEx.Configuration;

namespace Sentinel.Sentinel;

public static class Cfg
{
    public static ConfigEntry<int> Theme;

    public static ConfigEntry<int> CloseBtn;

    public static ConfigEntry<int> OpenBtn;

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

    public static ConfigEntry<bool> Sounds;

    public static ConfigEntry<bool> AutoJoin;

    public static ConfigEntry<bool> SwapHands;

    public static ConfigEntry<bool> Touch;

    public static ConfigEntry<bool> HandMenu;

    public static ConfigEntry<bool> OneHand;

    public static void Load(ConfigFile config)
    {
        Theme       = config.Bind("menu",    "theme",     0,     "0 hamburbur, 1 blue, 2 violet, 3 ember, 4 mint, 5 gold, 6 sentinel");
        NotifMode   = config.Bind("notifs",  "mode",      "all", "all, cheats, off");
        Tags        = config.Bind("tags",    "enabled",   true);
        TagFps      = config.Bind("tags",    "fps",       true);
        TagPlat     = config.Bind("tags",    "platform",  true);
        TagMenu     = config.Bind("tags",    "sentinel",  true, "show the menu icon over other sentinel users");
        BoardColors = config.Bind("board",   "colors",    true);
        Gesture     = config.Bind("gesture", "enabled",   true);
        Palm        = config.Bind("disc",    "palm",      true);
        Broadcast   = config.Bind("net",     "broadcast", true,  "announce yourself to other sentinel users, off = receive only");
        CloseBtn    = config.Bind("menu",    "close",     0,     "double tap to close: 0 X, 1 Y, 2 A, 3 B");
        OpenBtn     = config.Bind("menu",    "open",      3,     "press to open: 0 X, 1 Y, 2 A, 3 B");
        OneHand     = config.Bind("menu",    "onehand",   false, "menu drops in front of you and one hand runs it with the laser");
        Sounds      = config.Bind("menu",    "sounds",    true);
        Touch       = config.Bind("menu",    "touch",     false, "poke the menu with your hands instead of the laser");
        AutoJoin    = config.Bind("net",     "autojoin",  false, "join a random public room whenever you are not in one");
        HandMenu    = config.Bind("disc",    "handmenu",  false, "tap the disc to open the menu on your palm instead of throwing it");
        SwapHands   = config.Bind("disc",    "swap",      false, "disc sits in the right palm and you grab with the left");
        SpotifyId   = config.Bind("spotify", "client_id", "",    "your spotify app client id");
        YtChannel   = config.Bind("youtube", "channel",   "",    "your handle like @name, or a channel or stream url");
    }
}