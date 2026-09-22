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
        private const int CurrentVersion = 1;

        [Serializable]
        public sealed class SaveData
        {
            public int version = CurrentVersion;
            public long garnetBalance;
            public List<long> gemstoneBalances = new List<long>();
            public List<string> purchasedNodeIds = new List<string>();
            public List<int> spentCosts = new List<int>();
            public List<string> spentCurrencyIds = new List<string>();
            public List<string> fragmentCurrencyIds = new List<string>();
            public List<long> fragmentBalances = new List<long>();
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
                if (loaded == null || loaded.version <= 0)
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
                loaded.fragmentCurrencyIds = loaded.fragmentCurrencyIds ??
                    new List<string>();
                loaded.fragmentBalances = loaded.fragmentBalances ??
                    new List<long>();
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
            return Save(
                garnetBalance,
                gemstoneBalances,
                purchasedNodeIds,
                spentCosts,
                spentCurrencyIds,
                null);
        }

        public static bool Save(
            long garnetBalance,
            IReadOnlyList<long> gemstoneBalances,
            IEnumerable<string> purchasedNodeIds,
            IReadOnlyDictionary<string, int> spentCosts,
            IReadOnlyDictionary<string, string> spentCurrencyIds,
            IReadOnlyDictionary<string, long> fragmentBalances)
        {
            SaveData data = new SaveData
            {
                version = CurrentVersion,
                garnetBalance = Math.Max(0L, garnetBalance),
                gemstoneBalances = CopyBalances(gemstoneBalances),
                purchasedNodeIds = CopyIds(purchasedNodeIds),
                spentCosts = new List<int>(),
                spentCurrencyIds = new List<string>(),
                fragmentCurrencyIds = new List<string>(),
                fragmentBalances = new List<long>()
            };

            if (fragmentBalances != null)
            {
                foreach (KeyValuePair<string, long> pair in fragmentBalances)
                {
                    if (string.IsNullOrEmpty(pair.Key))
                    {
                        continue;
                    }

                    data.fragmentCurrencyIds.Add(pair.Key);
                    data.fragmentBalances.Add(Math.Max(0L, pair.Value));
                }
            }

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
