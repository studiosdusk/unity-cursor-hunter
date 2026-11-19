using System;
using System.Collections.Generic;
using CursorHunter.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CursorHunter.Progression
{
    public enum ProgressionLoadStatus
    {
        Missing,
        Loaded,
        Corrupt,
        Unsupported,
        ResetFailed
    }

    /// <summary>Persists versioned player progression snapshots in PlayerPrefs.</summary>
    public static class TraitProgressionStore
    {
        private const string SaveKey = "cursor_hunter.progression.v1";
        private const int CurrentVersion = 13;
        private const string LegacyBackupKey = SaveKey + ".before-v6";
        private const string ResetBuildVersion = "0.0.5";
        private const string BuildResetMarker = "cursor_hunter.first_build_reset.0.0.5";
        private static HashSet<string> runtimeCardIds;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCardCatalog() => runtimeCardIds = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetProgressForFirstBuildLaunch() =>
            TryResetForFirstBuildLaunch(Application.version, Application.isEditor);

        internal static bool TryResetForFirstBuildLaunch(string version, bool isEditor)
        {
            if (isEditor || version != ResetBuildVersion) return true;
            try
            {
                if (PlayerPrefs.HasKey(BuildResetMarker)) return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"첫 실행 초기화를 확인하지 못했습니다. {exception.Message}");
                return false;
            }
            return TryClearEntireProfile(BuildResetMarker);
        }

        [Serializable]
        public sealed class SaveData
        {
            // Missing version must stay invalid when deserializing an incomplete save.
            public int version;
            public long garnetBalance;
            public List<long> gemstoneBalances = new List<long>();
            public List<string> purchasedNodeIds = new List<string>();
            public List<ResearchRankEntry> researchRanks = new List<ResearchRankEntry>();
            public string researchLayoutFingerprint;
            public List<string> ownedRelicIds = new List<string>();
            public List<string> unseenRelicIds = new List<string>();
            public long experiencePoints;
            public int availableTraitCards;
            public int traitCardDrawCount;
            public int selectedFieldTier = 1;
            public List<string> ownedTraitCardIds = new List<string>();
            public TraitCardProgressData newTraitCards;
            public bool hasNewTraitCardProgress;
            public List<int> spentCosts = new List<int>();
            public List<string> spentCurrencyIds = new List<string>();
            public List<SpentCostEntry> spentCostEntries = new List<SpentCostEntry>();
            public int highestBossStageCleared;
            public List<string> settledBossRunIds = new List<string>();
            public int unlockedBossTier;

            internal void CaptureWallet(long garnet, IReadOnlyList<long> balances)
            {
                garnetBalance = Math.Max(0L, garnet);
                gemstoneBalances = CopyBalances(balances);
            }

            internal void CaptureResearch(IReadOnlyDictionary<string, int> ranks, string layout)
            {
                researchLayoutFingerprint = layout;
                if (ranks == null) return;
                foreach (var entry in ranks)
                    if (!string.IsNullOrWhiteSpace(entry.Key) && entry.Value > 0)
                        researchRanks.Add(new ResearchRankEntry { placementId = entry.Key, rank = entry.Value });
            }

            internal void CaptureLegacyPurchases(IEnumerable<string> purchases,
                IReadOnlyDictionary<string, int> costs, IReadOnlyDictionary<string, string> currencies,
                IReadOnlyDictionary<string, List<SpentCostEntry>> receipts)
            {
                purchasedNodeIds = CopyIds(purchases);
                // Legacy costs and currency IDs are parallel lists indexed by purchased node.
                foreach (var nodeId in purchasedNodeIds)
                {
                    spentCosts.Add(costs != null && costs.TryGetValue(nodeId, out int cost) ? Math.Max(0, cost) : 0);
                    spentCurrencyIds.Add(currencies != null && currencies.TryGetValue(nodeId, out var currency)
                        ? currency ?? string.Empty : string.Empty);
                }

                if (receipts == null) return;
                foreach (var node in receipts)
                {
                    if (string.IsNullOrWhiteSpace(node.Key) || node.Value == null) continue;
                    foreach (var receipt in node.Value)
                    {
                        if (receipt == null || string.IsNullOrWhiteSpace(receipt.currencyId) || receipt.amount <= 0)
                            continue;
                        spentCostEntries.Add(new SpentCostEntry
                        {
                            nodeId = node.Key,
                            currencyId = receipt.currencyId,
                            amount = receipt.amount
                        });
                    }
                }
            }

            internal void CaptureRelics(IEnumerable<string> owned, IEnumerable<string> unseen)
            {
                ownedRelicIds = CopyIds(owned);
                unseenRelicIds = CopyIds(unseen);
            }

            internal void CaptureTraits(long experience, TraitCardProgressData cards, int legacyAvailableCards,
                int legacyDrawCount, IEnumerable<string> legacyOwnedCards)
            {
                experiencePoints = Math.Max(0L, experience);
                newTraitCards = cards?.Copy();
                hasNewTraitCardProgress = newTraitCards != null;
                availableTraitCards = Math.Max(0, legacyAvailableCards);
                traitCardDrawCount = Math.Max(0, legacyDrawCount);
                ownedTraitCardIds = CopyRepeatedIds(legacyOwnedCards);
            }

            internal void CaptureBattleProgress(int fieldTier, int bossTier, int highestCleared, IEnumerable<string> settledRuns)
            {
                selectedFieldTier = Math.Max(1, Math.Min(5, fieldTier));
                unlockedBossTier = Math.Max(0, Math.Min(5, bossTier));
                highestBossStageCleared = Math.Max(0, Math.Min(20, highestCleared));
                settledBossRunIds = CopyIds(settledRuns);
            }
        }

        [Serializable]
        public sealed class ResearchRankEntry
        {
            public string placementId;
            public int rank;
        }

        [Serializable]
        public sealed class SpentCostEntry
        {
            public string nodeId;
            public string currencyId;
            public int amount;
        }

        public static bool TryLoad(out SaveData data) => Load(out data) == ProgressionLoadStatus.Loaded;

        public static ProgressionLoadStatus Load(out SaveData data)
        {
            if (!TryResetForFirstBuildLaunch(Application.version, Application.isEditor))
            {
                data = null;
                return ProgressionLoadStatus.ResetFailed;
            }
            if (!PlayerPrefs.HasKey(SaveKey))
            {
                data = null;
                return ProgressionLoadStatus.Missing;
            }
            return ReadProfile(PlayerPrefs.GetString(SaveKey, string.Empty), out data);
        }

        private static ProgressionLoadStatus ReadProfile(string json, out SaveData data)
        {
            data = null;
            try
            {
                json = json.Trim();
                if (json.Length < 2 || json[0] != '{' || json[json.Length - 1] != '}')
                    return ProgressionLoadStatus.Corrupt;

                // Unity creates nested serializable objects even when their JSON member is absent.
                // Read into the same DTO with a parser that preserves missing and null card data.
                SaveData loaded;
                try
                {
                    loaded = JsonConvert.DeserializeObject<SaveData>(json);
                }
                catch (JsonException)
                {
                    // A newer schema may have changed the type of a field in our DTO.
                    // Check its version independently before reporting a damaged profile.
                    var version = JObject.Parse(json)[nameof(SaveData.version)] as JValue;
                    if (version?.Type == JTokenType.Integer && version.CompareTo(new JValue(CurrentVersion)) > 0)
                        return ProgressionLoadStatus.Unsupported;
                    throw;
                }
                if (loaded == null || loaded.version <= 0) return ProgressionLoadStatus.Corrupt;
                if (loaded.version > CurrentVersion) return ProgressionLoadStatus.Unsupported;
                if (loaded.hasNewTraitCardProgress)
                {
                    var status = ValidateSavedCards(loaded.newTraitCards);
                    if (status != ProgressionLoadStatus.Loaded) return status;
                }

                loaded.gemstoneBalances = loaded.gemstoneBalances ??
                    new List<long>();
                loaded.researchRanks = loaded.researchRanks ?? new List<ResearchRankEntry>();
                loaded.purchasedNodeIds = loaded.purchasedNodeIds ??
                    new List<string>();
                loaded.ownedRelicIds = loaded.ownedRelicIds ??
                    new List<string>();
                loaded.unseenRelicIds = loaded.unseenRelicIds ?? new List<string>();
                loaded.experiencePoints = Math.Max(0L, loaded.experiencePoints);
                loaded.availableTraitCards = Math.Max(0, loaded.availableTraitCards);
                loaded.traitCardDrawCount = Math.Max(0, loaded.traitCardDrawCount);
                loaded.selectedFieldTier = Math.Max(1, Math.Min(5, loaded.selectedFieldTier));
                loaded.ownedTraitCardIds = loaded.ownedTraitCardIds ?? new List<string>();
                loaded.spentCosts = loaded.spentCosts ?? new List<int>();
                loaded.spentCurrencyIds = loaded.spentCurrencyIds ??
                    new List<string>();
                loaded.spentCostEntries = loaded.spentCostEntries ??
                    new List<SpentCostEntry>();
                loaded.highestBossStageCleared = Math.Max(0, Math.Min(20, loaded.highestBossStageCleared));
                // Boss-branch saves predate the separate research unlock tier.
                loaded.unlockedBossTier = Math.Max(Math.Min(5, loaded.highestBossStageCleared),
                    Math.Max(0, Math.Min(5, loaded.unlockedBossTier)));
                loaded.settledBossRunIds = loaded.settledBossRunIds ?? new List<string>();
                data = loaded;
                return ProgressionLoadStatus.Loaded;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"Trait progression save could not be read: {exception.Message}");
                return ProgressionLoadStatus.Corrupt;
            }
        }

        private static ProgressionLoadStatus ValidateSavedCards(TraitCardProgressData cards)
        {
            if (cards == null) return ProgressionLoadStatus.Corrupt;
            cards.ownedIds ??= new List<string>();
            cards.candidateIds ??= new List<string>();
            var ids = runtimeCardIds;
            if (ids == null)
            {
                if (!TraitCardDefinitionResources.TryLoad(out var definitions, out _))
                    return ProgressionLoadStatus.Unsupported;
                ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (var definition in definitions) ids.Add(definition.stableId);
                if (Application.isPlaying) runtimeCardIds = ids;
            }
            return TraitCardProgress.ValidateDraft(cards, ids);
        }

        // Compatibility for callers that predate full profile snapshots.
        public static bool Save(
            long garnetBalance,
            IReadOnlyList<long> gemstoneBalances,
            IEnumerable<string> purchasedNodeIds,
            IReadOnlyDictionary<string, int> spentCosts,
            IReadOnlyDictionary<string, string> spentCurrencyIds,
            int unlockedBossTier = 0,
            IEnumerable<string> ownedRelicIds = null,
            int highestBossStageCleared = 0,
            IEnumerable<string> settledBossRunIds = null,
            IEnumerable<string> unseenRelicIds = null,
            long experiencePoints = 0L,
            int availableTraitCards = 0,
            int traitCardDrawCount = 0,
            int selectedFieldTier = 1,
            IEnumerable<string> ownedTraitCardIds = null,
            IReadOnlyDictionary<string, List<SpentCostEntry>> spentCostEntries = null,
            IReadOnlyDictionary<string, int> researchRanks = null,
            TraitCardProgressData newTraitCards = null,
            string researchLayoutFingerprint = null)
        {
            var data = new SaveData();
            data.CaptureWallet(garnetBalance, gemstoneBalances);
            data.CaptureResearch(researchRanks, researchLayoutFingerprint);
            data.CaptureLegacyPurchases(purchasedNodeIds, spentCosts, spentCurrencyIds, spentCostEntries);
            data.CaptureRelics(ownedRelicIds, unseenRelicIds);
            data.CaptureTraits(experiencePoints, newTraitCards, availableTraitCards, traitCardDrawCount, ownedTraitCardIds);
            data.CaptureBattleProgress(selectedFieldTier, unlockedBossTier, highestBossStageCleared, settledBossRunIds);
            return Save(data);
        }

        internal static bool Save(SaveData snapshot)
        {
            if (!TryResetForFirstBuildLaunch(Application.version, Application.isEditor)) return false;
            snapshot.version = CurrentVersion;
            bool hadPreviousSave = PlayerPrefs.HasKey(SaveKey);
            string previousJson = PlayerPrefs.GetString(SaveKey, string.Empty);
            try
            {
                if (snapshot.hasNewTraitCardProgress &&
                    ValidateSavedCards(snapshot.newTraitCards) != ProgressionLoadStatus.Loaded) return false;

                // Validate the same bytes held for rollback and the first migration backup.
                if (hadPreviousSave)
                {
                    if (ReadProfile(previousJson, out SaveData previous) != ProgressionLoadStatus.Loaded) return false;
                    // Older callers must not erase the new draft when saving unrelated progress.
                    if (!snapshot.hasNewTraitCardProgress && previous.hasNewTraitCardProgress)
                    {
                        snapshot.newTraitCards = previous.newTraitCards?.Copy();
                        snapshot.hasNewTraitCardProgress = snapshot.newTraitCards != null;
                    }
                    // Callers without a connected research map must not erase its layout marker.
                    if (snapshot.researchLayoutFingerprint == null)
                        snapshot.researchLayoutFingerprint = previous.researchLayoutFingerprint;
                    if (previous.version < CurrentVersion && !PlayerPrefs.HasKey(LegacyBackupKey))
                    {
                        PlayerPrefs.SetString(LegacyBackupKey, previousJson);
                        PlayerPrefs.Save();
                    }
                }
                string json = JsonUtility.ToJson(snapshot);
                if (string.IsNullOrWhiteSpace(json)) return false;
                PlayerPrefs.SetString(SaveKey, json);
                PlayerPrefs.Save();
                return true;
            }
            catch (Exception exception)
            {
                // SetString changes the in-memory cache before Save can fail.
                // Restore it as well as the caller's transaction state so a retry cannot
                // accidentally persist a failed card purchase in an unrelated later save.
                if (hadPreviousSave) PlayerPrefs.SetString(SaveKey, previousJson);
                else PlayerPrefs.DeleteKey(SaveKey);
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

        internal static bool TryClearEntireProfile(string completionMarker = null)
        {
            // The explicit full reset also removes migrations' exact, potentially unreadable old save.
            string current = null;
            string backup = null;
            try
            {
                current = PlayerPrefs.HasKey(SaveKey) ? PlayerPrefs.GetString(SaveKey) : null;
                backup = PlayerPrefs.HasKey(LegacyBackupKey) ? PlayerPrefs.GetString(LegacyBackupKey) : null;
                PlayerPrefs.DeleteKey(SaveKey);
                PlayerPrefs.DeleteKey(LegacyBackupKey);
                if (!string.IsNullOrEmpty(completionMarker)) PlayerPrefs.SetInt(completionMarker, 1);
                PlayerPrefs.Save();
                return true;
            }
            catch (Exception exception)
            {
                if (current != null) PlayerPrefs.SetString(SaveKey, current);
                if (backup != null) PlayerPrefs.SetString(LegacyBackupKey, backup);
                if (!string.IsNullOrEmpty(completionMarker)) PlayerPrefs.DeleteKey(completionMarker);
                Debug.LogWarning($"진행도 초기화를 저장하지 못했습니다. {exception.Message}");
                return false;
            }
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


        private static List<string> CopyRepeatedIds(IEnumerable<string> source)
        {
            var copy = new List<string>();
            if (source == null) return copy;
            foreach (string id in source)
                if (!string.IsNullOrWhiteSpace(id)) copy.Add(id);
            return copy;
        }

        private static List<string> CopyIds(IEnumerable<string> source)
        {
            List<string> copy = new List<string>();
            if (source == null)
            {
                return copy;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in source)
            {
                if (!string.IsNullOrEmpty(id) && seen.Add(id))
                {
                    copy.Add(id);
                }
            }

            return copy;
        }
    }
}
