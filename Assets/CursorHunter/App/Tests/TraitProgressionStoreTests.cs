using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CursorHunter.Progression;
using NUnit.Framework;
using UnityEngine;

namespace CursorHunter.App.Tests
{
    // Editor-only tests for later execution. Original local values are restored.
    public sealed class TraitProgressionStoreTests
    {
        private const string Key = "cursor_hunter.progression.v1";
        private const string BackupKey = Key + ".before-v6";
        private const string BuildResetKey = "cursor_hunter.first_build_reset.0.0.5";
        private bool _hadSave;
        private bool _hadBackup;
        private bool _hadBuildReset;
        private int _buildReset;
        private string _save;
        private string _backup;

        [SetUp]
        public void CaptureOriginalKeys()
        {
            _hadSave = PlayerPrefs.HasKey(Key);
            _hadBackup = PlayerPrefs.HasKey(BackupKey);
            _hadBuildReset = PlayerPrefs.HasKey(BuildResetKey);
            _buildReset = PlayerPrefs.GetInt(BuildResetKey);
            _save = PlayerPrefs.GetString(Key, "");
            _backup = PlayerPrefs.GetString(BackupKey, "");
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.DeleteKey(BackupKey);
            PlayerPrefs.DeleteKey(BuildResetKey);
        }

        [TearDown]
        public void RestoreOriginalKeys()
        {
            if (_hadSave) PlayerPrefs.SetString(Key, _save); else PlayerPrefs.DeleteKey(Key);
            if (_hadBackup) PlayerPrefs.SetString(BackupKey, _backup); else PlayerPrefs.DeleteKey(BackupKey);
            if (_hadBuildReset) PlayerPrefs.SetInt(BuildResetKey, _buildReset); else PlayerPrefs.DeleteKey(BuildResetKey);
            PlayerPrefs.Save();
        }

        [Test]
        public void CurrentBuildResetsProgressOnceAndPreservesSettings()
        {
            const string settingsKey = "TraitProgressionStoreTests.user_setting";
            bool hadSetting = PlayerPrefs.HasKey(settingsKey);
            string previousSetting = PlayerPrefs.GetString(settingsKey);
            try
            {
                PlayerPrefs.SetString(Key, "{\"version\":13,\"garnetBalance\":717}");
                PlayerPrefs.SetString(BackupKey, "previous migration backup");
                PlayerPrefs.SetString(settingsKey, "english");
                Assert.That(ApplyBuildReset("0.0.5", false), Is.True);
                Assert.That(PlayerPrefs.HasKey(Key), Is.False);
                Assert.That(PlayerPrefs.HasKey(BackupKey), Is.False);
                Assert.That(PlayerPrefs.GetInt(BuildResetKey), Is.EqualTo(1));
                Assert.That(PlayerPrefs.GetString(settingsKey), Is.EqualTo("english"));

                var cards = new TraitCardProgressData
                {
                    points = 5,
                    selectedStageId = "desert",
                    purchasedStageIds = new List<string> { "desert" }
                };
                Assert.That(TraitProgressionStore.Save(42, new long[] { 42 }, new string[0], null, null,
                    experiencePoints: 1000, highestBossStageCleared: 2, newTraitCards: cards), Is.True);
                string newProgress = PlayerPrefs.GetString(Key);
                Assert.That(ApplyBuildReset("0.0.5", false), Is.True);
                Assert.That(PlayerPrefs.GetString(Key), Is.EqualTo(newProgress));
                Assert.That(TraitProgressionStore.Load(out var loaded), Is.EqualTo(ProgressionLoadStatus.Loaded));
                Assert.That(loaded.garnetBalance, Is.EqualTo(42));
                Assert.That(loaded.newTraitCards.selectedStageId, Is.EqualTo("desert"));
                Assert.That(loaded.highestBossStageCleared, Is.EqualTo(2));

                var clear = typeof(TraitProgressionStore).GetMethod("TryClearEntireProfile",
                    BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That((bool)clear.Invoke(null, new object[] { null }), Is.True);
                Assert.That(PlayerPrefs.GetInt(BuildResetKey), Is.EqualTo(1));
                Assert.That(TraitProgressionStore.Save(12, new long[] { 12 }, new string[0], null, null), Is.True);
                string afterManualReset = PlayerPrefs.GetString(Key);
                Assert.That(ApplyBuildReset("0.0.5", false), Is.True);
                Assert.That(PlayerPrefs.GetString(Key), Is.EqualTo(afterManualReset));
            }
            finally
            {
                if (hadSetting) PlayerPrefs.SetString(settingsKey, previousSetting); else PlayerPrefs.DeleteKey(settingsKey);
            }
        }

        [TestCase("0.0.4", false)]
        [TestCase("0.0.6", false)]
        [TestCase("0.0.5", true)]
        public void OtherVersionsAndEditorPreserveProgress(string version, bool isEditor)
        {
            const string original = "{\"version\":13,\"garnetBalance\":717}";
            PlayerPrefs.SetString(Key, original);
            PlayerPrefs.SetString(BackupKey, "backup");
            Assert.That(ApplyBuildReset(version, isEditor), Is.True);
            Assert.That(PlayerPrefs.GetString(Key), Is.EqualTo(original));
            Assert.That(PlayerPrefs.GetString(BackupKey), Is.EqualTo("backup"));
            Assert.That(PlayerPrefs.HasKey(BuildResetKey), Is.False);
        }

        [Test]
        public void FreshBuildLaunchMarksResetBeforeAnyProgressIsCreated()
        {
            Assert.That(ApplyBuildReset("0.0.5", false), Is.True);
            Assert.That(PlayerPrefs.GetInt(BuildResetKey), Is.EqualTo(1));
            Assert.That(TraitProgressionStore.Save(12, new long[] { 12 }, new string[0], null, null), Is.True);
            string progress = PlayerPrefs.GetString(Key);
            Assert.That(ApplyBuildReset("0.0.5", false), Is.True);
            Assert.That(PlayerPrefs.GetString(Key), Is.EqualTo(progress));
        }

        [Test]
        public void FirstBuildResetClearsUnreadableOldProgress()
        {
            PlayerPrefs.SetString(Key, "unreadable older save");
            PlayerPrefs.SetString(BackupKey, "old backup");
            Assert.That(ApplyBuildReset("0.0.5", false), Is.True);
            Assert.That(PlayerPrefs.HasKey(Key), Is.False);
            Assert.That(PlayerPrefs.HasKey(BackupKey), Is.False);
            Assert.That(PlayerPrefs.GetInt(BuildResetKey), Is.EqualTo(1));
        }

        private static bool ApplyBuildReset(string version, bool isEditor) =>
            (bool)typeof(TraitProgressionStore).GetMethod("TryResetForFirstBuildLaunch",
                BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { version, isEditor });

        [Test]
        public void FirstCurrentSavePreservesExactLegacyDataAndBackupIsNotOverwritten()
        {
            const string old = "{\"version\":2,\"garnetBalance\":42,\"purchasedNodeIds\":[\"relic.monster.01.level.01\"],\"fragmentCurrencyIds\":[\"fragment.relic.monster.01\"],\"fragmentBalances\":[77]}";
            PlayerPrefs.SetString(Key, old);
            Assert.That(TraitProgressionStore.Save(42, new long[] { 42 },
                new[] { "stat.attack.01" }, null, null, 0,
                new[] { "relic.red_potion" }), Is.True);
            Assert.That(PlayerPrefs.GetString(BackupKey), Is.EqualTo(old));
            Assert.That(TraitProgressionStore.TryLoad(out var current), Is.True);
            Assert.That(current.version, Is.GreaterThanOrEqualTo(6));
            Assert.That(current.ownedRelicIds, Is.EqualTo(new[] { "relic.red_potion" }));
            Assert.That(PlayerPrefs.GetString(Key), Does.Not.Contain("fragment"));
            Assert.That(TraitProgressionStore.Save(43, new long[] { 43 }, new[] { "stat.attack.01" }, null, null), Is.True);
            Assert.That(PlayerPrefs.GetString(BackupKey), Is.EqualTo(old));
        }

        [TestCase(0, 3, 3)]
        [TestCase(7, 0, 5)]
        public void LegacyBossAndResearchProgressRestoreWithoutLosingWallet(
            int highestStage, int unlockTier, int expectedTier)
        {
            string legacy = "{\"version\":5,\"garnetBalance\":99," +
                "\"highestBossStageCleared\":" + highestStage +
                ",\"unlockedBossTier\":" + unlockTier + "}";
            PlayerPrefs.SetString(Key, legacy);
            Assert.That(TraitProgressionStore.TryLoad(out var loaded), Is.True);
            Assert.That(loaded.highestBossStageCleared, Is.EqualTo(highestStage));
            Assert.That(loaded.unlockedBossTier, Is.EqualTo(expectedTier));
            Assert.That(loaded.garnetBalance, Is.EqualTo(99));
            Assert.That(loaded.ownedRelicIds, Is.Empty);
            Assert.That(loaded.settledBossRunIds, Is.Empty);
            Assert.That(PlayerPrefs.GetString(Key), Is.EqualTo(legacy));
        }

        [Test]
        public void NewDraftSurvivesAnOlderCallerSavingUnrelatedProgress()
        {
            var state = new TraitCardProgressData
            {
                points = 4, revealed = true, selectedStageId = "desert",
                purchasedStageIds = new System.Collections.Generic.List<string> { "desert" },
                ownedIds = new System.Collections.Generic.List<string> { "trait_card.lucky_settlement" },
                candidateIds = new System.Collections.Generic.List<string> { "trait_card.hunting_wisdom" }
            };
            Assert.That(TraitProgressionStore.Save(9, new long[] { 9 }, new string[0], null, null,
                experiencePoints: 1000, availableTraitCards: 7,
                ownedTraitCardIds: new[] { "talent.might", "talent.might" }, newTraitCards: state), Is.True);
            Assert.That(TraitProgressionStore.Save(10, new long[] { 10 }, new string[0], null, null,
                experiencePoints: 1000, availableTraitCards: 7,
                ownedTraitCardIds: new[] { "talent.might", "talent.might" }), Is.True);
            Assert.That(TraitProgressionStore.TryLoad(out var loaded), Is.True);
            Assert.That(loaded.newTraitCards.points, Is.EqualTo(4));
            Assert.That(loaded.newTraitCards.selectedStageId, Is.EqualTo("desert"));
            Assert.That(loaded.newTraitCards.purchasedStageIds, Is.EqualTo(new[] { "desert" }));
            Assert.That(loaded.newTraitCards.revealed, Is.True);
            Assert.That(loaded.newTraitCards.candidateIds, Is.EqualTo(state.candidateIds));
            Assert.That(loaded.newTraitCards.ownedIds, Is.EqualTo(state.ownedIds));
            Assert.That(loaded.availableTraitCards, Is.EqualTo(7));
            Assert.That(loaded.ownedTraitCardIds, Is.EqualTo(new[] { "talent.might", "talent.might" }));
        }

        [Test]
        public void SaveWithoutConnectedResearchMapPreservesTheExistingLayoutMarker()
        {
            var ranks = new System.Collections.Generic.Dictionary<string, int> { ["research.node"] = 1 };
            Assert.That(TraitProgressionStore.Save(9, new long[] { 9 }, new string[0], null, null,
                researchRanks: ranks, researchLayoutFingerprint: "saved-layout"), Is.True);
            Assert.That(TraitProgressionStore.Save(10, new long[] { 10 }, new string[0], null, null,
                researchRanks: ranks), Is.True);
            Assert.That(TraitProgressionStore.TryLoad(out var loaded), Is.True);
            Assert.That(loaded.researchLayoutFingerprint, Is.EqualTo("saved-layout"));
            Assert.That(loaded.researchRanks[0].rank, Is.EqualTo(1));
        }

        [Test]
        public void UnsupportedFutureSaveIsNotOverwritten()
        {
            const string future = "{\"version\":999,\"garnetBalance\":99}";
            PlayerPrefs.SetString(Key, future);
            Assert.That(TraitProgressionStore.Save(1, new long[] { 1 }, new string[0], null, null), Is.False);
            Assert.That(PlayerPrefs.GetString(Key), Is.EqualTo(future));
            Assert.That(PlayerPrefs.HasKey(BackupKey), Is.False);
        }

        [Test]
        public void MissingVersionIsNotOverwritten()
        {
            const string incomplete = "{}";
            PlayerPrefs.SetString(Key, incomplete);
            Assert.That(TraitProgressionStore.TryLoad(out _), Is.False);
            Assert.That(TraitProgressionStore.Save(1, new long[] { 1 }, new string[0], null, null), Is.False);
            Assert.That(PlayerPrefs.GetString(Key), Is.EqualTo(incomplete));
        }

        [TestCase(null, ProgressionLoadStatus.Missing)]
        [TestCase("", ProgressionLoadStatus.Corrupt)]
        [TestCase("{", ProgressionLoadStatus.Corrupt)]
        [TestCase("{}", ProgressionLoadStatus.Corrupt)]
        [TestCase("{\"version\":13,\"hasNewTraitCardProgress\":true}", ProgressionLoadStatus.Corrupt)]
        [TestCase("{\"version\":13,\"hasNewTraitCardProgress\":true,\"newTraitCards\":null}", ProgressionLoadStatus.Corrupt)]
        [TestCase("{\"version\":13,\"hasNewTraitCardProgress\":true,\"newTraitCards\":[]}", ProgressionLoadStatus.Corrupt)]
        [TestCase("{\"version\":13,\"hasNewTraitCardProgress\":true,\"other\":{\"newTraitCards\":{}},\"note\":\"\\\"newTraitCards\\\":{}\"}", ProgressionLoadStatus.Corrupt)]
        [TestCase("{\"version\":999}", ProgressionLoadStatus.Unsupported)]
        [TestCase("{\"gemstoneBalances\":{\"garnet\":17},\"version\":999}", ProgressionLoadStatus.Unsupported)]
        [TestCase("{\"version\":12,\"garnetBalance\":17}", ProgressionLoadStatus.Loaded)]
        [TestCase("{\"version\":13,\"garnetBalance\":17}", ProgressionLoadStatus.Loaded)]
        public void ReadingClassifiesTheSourceWithoutRewritingIt(string source, ProgressionLoadStatus expected)
        {
            if (source != null) PlayerPrefs.SetString(Key, source);
            PlayerPrefs.SetString(BackupKey, "previous migration");
            Assert.That(TraitProgressionStore.Load(out var data), Is.EqualTo(expected));
            if (expected == ProgressionLoadStatus.Loaded)
                Assert.That(data.garnetBalance, Is.EqualTo(17));
            else
                Assert.That(data, Is.Null);
            Assert.That(PlayerPrefs.HasKey(Key), Is.EqualTo(source != null));
            if (source != null) Assert.That(PlayerPrefs.GetString(Key), Is.EqualTo(source));
            Assert.That(PlayerPrefs.GetString(BackupKey), Is.EqualTo("previous migration"));
        }

        [Test]
        public void InvalidIncomingCardDraftLeavesTheEntireHealthyProfileUntouched()
        {
            Assert.That(TraitProgressionStore.Save(71, new long[] { 71 }, null, null, null), Is.True);
            string original = PlayerPrefs.GetString(Key);
            var invalid = new TraitCardProgressData { revealed = true };
            Assert.That(TraitProgressionStore.Save(0, new long[] { 0 }, null, null, null,
                newTraitCards: invalid), Is.False);
            Assert.That(PlayerPrefs.GetString(Key), Is.EqualTo(original));
            Assert.That(TraitProgressionStore.Load(out var restored), Is.EqualTo(ProgressionLoadStatus.Loaded));
            Assert.That(restored.garnetBalance, Is.EqualTo(71));
        }

        [Test]
        public void ManySettlementReceiptsKeepFirstOccurrenceOrderAndLegacyTalentStacksRepeat()
        {
            string[] unique = Enumerable.Range(0, 1000).Select(i => $"run-{i}").ToArray();
            var receipts = new List<string> { null, "", unique[0] };
            receipts.AddRange(unique);
            receipts.AddRange(unique.Reverse());
            string[] stackedTalents = { "talent.might", "talent.might" };
            Assert.That(TraitProgressionStore.Save(42, new long[] { 42 }, null, null, null,
                settledBossRunIds: receipts, ownedTraitCardIds: stackedTalents), Is.True);
            Assert.That(TraitProgressionStore.Load(out var data), Is.EqualTo(ProgressionLoadStatus.Loaded));
            Assert.That(data.version, Is.EqualTo(13));
            Assert.That(data.settledBossRunIds, Is.EqualTo(unique));
            Assert.That(data.ownedTraitCardIds, Is.EqualTo(stackedTalents));
        }
    }
}
