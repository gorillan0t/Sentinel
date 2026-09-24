using System.Collections.Generic;

namespace Sentinel;

public static class Manifest
{
    public static readonly HashSet<string> Features = new()
    {
            "board_colors",
            "custom_tags",
            "custom_theme",
            "disc",
            "frame",
            "friends",
            "hand_menu",
            "join_code",
            "lobby_hop",
            "menu_rectangle",
            "menu_sfx",
            "mic",
            "net",
            "notify",
            "outfit",
            "pc_ui",
            "quick_report",
            "ring_menu",
            "spotify",
            "swap_hands",
            "tags",
            "time",
            "watcher",
            "youtube_chat",
    };

    public static string TierLabel => "Standalone";

    public static bool Plus => true;

    public static bool Has(string feature) => Features.Contains(feature);
}