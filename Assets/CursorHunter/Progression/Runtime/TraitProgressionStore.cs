using System;
using System.Collections.Generic;
using UnityEngine;

namespace CursorHunter.Progression
{
    /// <summary>
    /// Small PlayerPrefs-backed prototype store for the trait screen. The
    /// production save service can replace this class without changing the UI
    /// because the controller only depends on the DTO-shaped methods below.
    /// PlayerPrefs is used here so tests persist between Play Mode sessions and
    /// no file path or platform-specific API leaks into Progression UI code.
    /// </summary>
    public static class TraitProgressionStore
    {
        private const string SaveKey = "cursor_hunter.progression.v1";
        private const int CurrentVersion = 4;
        private const string LegacyBackupKey = SaveKey + ".before-v4";

        [Serializable]
        public sealed class SaveData
        {
            // Missing version must stay invalid when deserializing an incomplete save.
            public int version;
            public long garnetBalance;
            public List<long> gemstoneBalances = new List<long>();
            public List<string> purchasedNodeIds = new List<string>();
            public List<int> spentCosts = new List<int>();
            public List<string> spentCurrencyIds = new List<string>();
        }

        public static bool TryLoad(out SaveData data)
        {
            data = null;
            if (!PlayerPrefs.HasKey(SaveKey))
            {
                return false;
            }

            string json = PlayerPrefs.GetString(SaveKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                SaveData loaded = JsonUtility.FromJson<SaveData>(json);
                if (loaded == null || loaded.version <= 0 || loaded.version > CurrentVersion)
                {
                    return false;
                }

                loaded.gemstoneBalances = loaded.gemstoneBalances ??
                    new List<long>();
                loaded.purchasedNodeIds = loaded.purchasedNodeIds ??
                    new List<string>();
                loaded.spentCosts = loaded.spentCosts ?? new List<int>();
                loaded.spentCurrencyIds = loaded.spentCurrencyIds ??
                    new List<string>();
                data = loaded;
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"Trait progression save could not be read: {exception.Message}");
                return false;
            }
        }
        public static bool Save(
            long garnetBalance,
            IReadOnlyList<long> gemstoneBalances,
            IEnumerable<string> purchasedNodeIds,
            IReadOnlyDictionary<string, int> spentCosts,
            IReadOnlyDictionary<string, string> spentCurrencyIds)
        {
            SaveData data = new SaveData
            {
                version = CurrentVersion,
                garnetBalance = Math.Max(0L, garnetBalance),
                gemstoneBalances = CopyBalances(gemstoneBalances),
                purchasedNodeIds = CopyIds(purchasedNodeIds),
                spentCosts = new List<int>(),
                spentCurrencyIds = new List<string>()
            };
            // Keep both lists aligned with purchasedNodeIds so the DTO can be
            // restored without serializing dictionaries or relying on their
            // enumeration order.
            for (int i = 0; i < data.purchasedNodeIds.Count; i++)
            {
                string nodeId = data.purchasedNodeIds[i];
                data.spentCosts.Add(
                    spentCosts != null && spentCosts.TryGetValue(nodeId, out int cost)
                        ? Math.Max(0, cost)
                        : 0);
                data.spentCurrencyIds.Add(
                    spentCurrencyIds != null &&
                    spentCurrencyIds.TryGetValue(nodeId, out string currency)
                        ? currency ?? string.Empty
                        : string.Empty);
            }

            try
            {
                string json = JsonUtility.ToJson(data);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return false;
                }

                // Preserve the exact old save (including retired data) before the first v4 write.
                // Unknown/corrupt versions must not be silently overwritten.
                if (PlayerPrefs.HasKey(SaveKey))
                {
                    if (!TryLoad(out SaveData previous)) return false;
                    if (previous.version < CurrentVersion && !PlayerPrefs.HasKey(LegacyBackupKey))
                    {
                        PlayerPrefs.SetString(LegacyBackupKey, PlayerPrefs.GetString(SaveKey));
                        PlayerPrefs.Save();
                    }
                }
                PlayerPrefs.SetString(SaveKey, json);
                PlayerPrefs.Save();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"Trait progression save could not be written: {exception.Message}");
                return false;
            }
        }

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
        }

        private static List<long> CopyBalances(IReadOnlyList<long> source)
        {
            List<long> copy = new List<long>();
            if (source == null)
            {
                return copy;
            }

            for (int i = 0; i < source.Count; i++)
            {
                copy.Add(Math.Max(0L, source[i]));
            }

            return copy;
        }

        private static List<string> CopyIds(IEnumerable<string> source)
        {
            List<string> copy = new List<string>();
            if (source == null)
            {
                return copy;
            }

            foreach (string id in source)
            {
                if (!string.IsNullOrEmpty(id) && !copy.Contains(id))
                {
                    copy.Add(id);
                }
            }

            return copy;
        }
    }
}
