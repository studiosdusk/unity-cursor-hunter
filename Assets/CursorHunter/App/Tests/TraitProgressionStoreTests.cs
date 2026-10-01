using CursorHunter.Progression;
using NUnit.Framework;
using UnityEngine;

namespace CursorHunter.App.Tests
{
    // Editor-only tests for later execution. Original local values are restored.
    [NonParallelizable]
    public sealed class TraitProgressionStoreTests
    {
        private const string Key = "cursor_hunter.progression.v1";
        private const string BackupKey = Key + ".before-v3";
        private bool _hadSave;
        private bool _hadBackup;
        private string _save;
        private string _backup;

        [SetUp]
        public void CaptureOriginalKeys()
        {
            _hadSave = PlayerPrefs.HasKey(Key);
            _hadBackup = PlayerPrefs.HasKey(BackupKey);
            _save = PlayerPrefs.GetString(Key, "");
            _backup = PlayerPrefs.GetString(BackupKey, "");
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.DeleteKey(BackupKey);
        }

        [TearDown]
        public void RestoreOriginalKeys()
        {
            if (_hadSave) PlayerPrefs.SetString(Key, _save); else PlayerPrefs.DeleteKey(Key);
            if (_hadBackup) PlayerPrefs.SetString(BackupKey, _backup); else PlayerPrefs.DeleteKey(BackupKey);
            PlayerPrefs.Save();
        }

        [Test]
        public void FirstV3SavePreservesExactLegacyDataAndBackupIsNotOverwritten()
        {
            const string old = "{\"version\":2,\"garnetBalance\":42,\"purchasedNodeIds\":[\"relic.monster.01.level.01\"],\"fragmentCurrencyIds\":[\"fragment.relic.monster.01\"],\"fragmentBalances\":[77]}";
            PlayerPrefs.SetString(Key, old);
            Assert.That(TraitProgressionStore.Save(42, new long[] { 42 }, new[] { "stat.attack.01" }, null, null), Is.True);
            Assert.That(PlayerPrefs.GetString(BackupKey), Is.EqualTo(old));
            Assert.That(TraitProgressionStore.TryLoad(out var current), Is.True);
            Assert.That(current.version, Is.EqualTo(3));
            Assert.That(PlayerPrefs.GetString(Key), Does.Not.Contain("fragment"));
            Assert.That(TraitProgressionStore.Save(43, new long[] { 43 }, new[] { "stat.attack.01" }, null, null), Is.True);
            Assert.That(PlayerPrefs.GetString(BackupKey), Is.EqualTo(old));
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
    }
}
