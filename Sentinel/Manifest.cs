using System.Collections.Generic;

namespace Sentinel.Sentinel;

public static class Manifest
{
    public static readonly HashSet<string> Features =
    [
            "disc",
            "ring_menu",
            "tags",
            "watcher",
            "lobby_hop",
            "quick_report",
            "swap_hands",
            "friends",
            "custom_theme",
            "hand_menu",
            "time",
            "auto_join",
            "mic",
            "outfit",
            "board_colors",
            "frame",
            "net",
    ];

    public static bool Plus => true;

    public static bool Has(string feature) => Features.Contains(feature);
}