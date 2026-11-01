using System;
using System.IO;
using System.Text.RegularExpressions;
using CursorHunter.App.Editor;
using CursorHunter.Data;
using NUnit.Framework;
using UnityEditor;

namespace CursorHunter.App.Tests
{
    public sealed class BossBalanceStoreTests
    {
        private const string Source = "Assets/CursorHunter/Data/Resources/BossDefinitions/Boss04.asset";
        private string _folder, _path;
        private byte[] _sourceBytes;
        private BossDefinition _boss;

        [SetUp]
        public void Setup()
        {
            _sourceBytes = File.ReadAllBytes(Source);
            string name = "__BossBalanceStoreTests-" + Guid.NewGuid().ToString("N");
            _folder = "Assets/" + name;
            AssetDatabase.CreateFolder("Assets", name);
            _path = _folder + "/Boss04.asset";
            Assert.That(AssetDatabase.CopyAsset(Source, _path), Is.True);
            AssetDatabase.ImportAsset(_path, ImportAssetOptions.ForceSynchronousImport);
            _boss = AssetDatabase.LoadAssetAtPath<BossDefinition>(_path);
        }

        [TearDown]
        public void Cleanup()
        {
            try { Assert.That(File.ReadAllBytes(Source), Is.EqualTo(_sourceBytes)); }
            finally { if (AssetDatabase.IsValidFolder(_folder)) AssetDatabase.DeleteAsset(_folder); }
        }

        [Test]
        public void SaveBossBodyPreservesIdentityLocalizationBehaviorRewardsAndMeta()
        {
            var settings = BossBalanceSettings.FromSnapshot(_boss.CreateSnapshot());
            settings.MaxHealth = long.MaxValue;
            settings.MoveSpeed = 1.25f; settings.VisualScale = 2.75f;
            settings.HitAreaWidth = 2.5f; settings.HitAreaHeight = 2.25f;
            byte[] meta = File.ReadAllBytes(_path + ".meta");
            string before = File.ReadAllText(_path);
            Assert.That(CombatBalanceBaseStore.TryBuildBossFiles(new[] { _boss }, new[] { settings },
                out var originals, out var updated, out string error), Is.True, error);
            Assert.That(File.ReadAllText(_path), Is.EqualTo(before), "Preparing Save must be read-only.");
            Assert.That(WithoutBody(updated[_path]), Is.EqualTo(WithoutBody(before)));
            Assert.That(CombatBalanceBaseStore.TryCommitFiles(originals, updated, out error), Is.True, error);
            AssetDatabase.ImportAsset(_path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var actual = AssetDatabase.LoadAssetAtPath<BossDefinition>(_path).CreateSnapshot();
            Assert.That(actual.MaxHealth, Is.EqualTo(long.MaxValue));
            Assert.That(actual.MoveSpeed, Is.EqualTo(1.25f));
            Assert.That(actual.VisualScale, Is.EqualTo(2.75f));
            Assert.That(actual.HitAreaWidth, Is.EqualTo(2.5f));
            Assert.That(actual.HitAreaHeight, Is.EqualTo(2.25f));
            Assert.That(actual.PackSize, Is.EqualTo(1));
            Assert.That(actual.AliveLimit, Is.EqualTo(1));
            Assert.That(File.ReadAllBytes(_path + ".meta"), Is.EqualTo(meta));
        }

        [Test]
        public void InvalidBossBodyRejectsSaveBeforeAnyFilesChange()
        {
            var settings = BossBalanceSettings.FromSnapshot(_boss.CreateSnapshot());
            settings.MoveSpeed = float.NaN;
            string before = File.ReadAllText(_path);
            Assert.That(CombatBalanceBaseStore.TryBuildBossFiles(new[] { _boss }, new[] { settings },
                out var originals, out var updated, out _), Is.False);
            Assert.That(originals, Is.Empty);
            Assert.That(updated, Is.Empty);
            Assert.That(File.ReadAllText(_path), Is.EqualTo(before));
        }

        private static string WithoutBody(string text) =>
            Regex.Replace(text, @"(?m)^  (maxHealth|moveSpeed|visualScale|hitArea):[^\r\n]*", "");
    }
}
