using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace Sentinel.Sentinel;

public static class HamburburData
{
    private const string DataUrl = "https://hamburbur.org/data";

    private const float RefreshInterval = 600f;

    public static readonly Dictionary<string, string> KnownMods =
            new(StringComparer.Ordinal);

    public static readonly Dictionary<string, string> KnownCheats =
            new(StringComparer.Ordinal);

    public static bool Loaded { get; private set; }

    public static IEnumerator RefreshLoop()
    {
        while (true)
        {
            yield return Refresh();
            yield return new WaitForSecondsRealtime(RefreshInterval);
        }
    }

    public static IEnumerator Refresh()
    {
        using UnityWebRequest request = UnityWebRequest.Get(DataUrl);

        request.timeout = 5;

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"[Sentinel] Failed to download hamburbur data. {request.error}");

            yield break;
        }

        JObject root;

        try
        {
            root = JObject.Parse(request.downloadHandler.text);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[Sentinel] Failed to parse hamburbur data. {exception.Message}");

            yield break;
        }

        if (root["knownMods"] is not JObject knownMods || root["knownCheats"] is not JObject knownCheats)
        {
            Debug.LogWarning("[Sentinel] hamburbur data did not contain knownMods or knownCheats.");

            yield break;
        }

        Dictionary<string, string> mods =
                new(StringComparer.Ordinal);

        Dictionary<string, string> cheats =
                new(StringComparer.Ordinal);

        ReadDictionary(knownMods,   mods);
        ReadDictionary(knownCheats, cheats);

        KnownMods.Clear();
        KnownCheats.Clear();

        foreach (KeyValuePair<string, string> entry in mods)
        {
            KnownMods[entry.Key] = entry.Value;
        }

        foreach (KeyValuePair<string, string> entry in cheats)
        {
            KnownCheats[entry.Key] = entry.Value;
        }

        Loaded = true;

        Debug.Log(
                $"[Sentinel] Loaded {KnownMods.Count} known mods and {KnownCheats.Count} known cheats from hamburbur.");
    }

    private static void ReadDictionary(
            JObject                    source,
            Dictionary<string, string> destination)
    {
        foreach (JProperty property in source.Properties())
        {
            string name = property.Value.Value<string>();

            if (string.IsNullOrEmpty(name))
            {
                name = property.Name;
            }

            destination[property.Name] = name;
        }
    }
}