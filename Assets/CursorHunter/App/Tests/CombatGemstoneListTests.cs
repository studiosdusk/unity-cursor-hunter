using System;
using System.Linq;
using System.Reflection;
using CursorHunter.Combat;
using CursorHunter.Contracts;
using CursorHunter.Data;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CursorHunter.App.Tests
{
    public sealed class CombatGemstoneListTests
    {
        private GameObject _root;
        private CombatGemstoneList _list;
        private CombatRunController _combat;
        private MonsterSpawner _spawner;
        private static readonly string[] GemIds =
        {
            "gem.garnet", "gem.topaz", "gem.amethyst", "gem.sapphire", "gem.diamond", "gem.dragon"
        };

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Gemstone HUD test", typeof(RectTransform), typeof(Canvas));
            _root.GetComponent<RectTransform>().sizeDelta = new Vector2(1920f, 1080f);
            var scene = EditorSceneManager.OpenPreviewScene("Assets/CursorHunter/App/Scenes/Main.unity");
            try
            {
                CombatGemstoneList source = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<CombatGemstoneList>(true)).Single();
                _list = Object.Instantiate(source, _root.transform);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }

            _combat = _root.AddComponent<CombatRunController>();
            _spawner = _root.AddComponent<MonsterSpawner>();
            StartRun("gemstones-first");
        }

        [TearDown]
        public void TearDown() { Object.DestroyImmediate(_root); }

        [Test]
        public void SceneContainsSixBoundRowsAndSecondaryDropsCompactIntoTwoColumns()
        {
            var icons = _list.GetComponentsInChildren<GemstoneIconView>(true);
            Assert.That(icons.Select(icon => icon.GemstoneId), Is.EqualTo(GemIds));
            foreach (GemstoneIconView icon in icons)
                Assert.That(icon.GetComponent<Image>().sprite,
                    Is.EqualTo(GemstoneIconCatalog.LoadDefault().GetIcon(icon.GemstoneId)));
            Assert.That(_list.GetComponentsInChildren<Button>(true), Is.Empty);
            int objectCount = _list.GetComponentsInChildren<Transform>(true).Length;

            SetRoster(new[]
            {
                Spawn("source", "gem.dragon"),
                Spawn("disabled", "gem.topaz"),
                Spawn("zero-chance", "gem.sapphire", chance: 0f),
                Spawn("zero-amount", "gem.amethyst", amount: 0L)
            }, new[] { true, false, true, true });
            _list.ShowRunning(_combat, _spawner);
            Assert.That(VisibleIds(), Is.EqualTo(new[] { "gem.garnet", "gem.dragon" }));
            Assert.That(Text("gem.garnet").text, Is.EqualTo("0"));
            Assert.That(Text("gem.dragon").text, Is.EqualTo("0"));

            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)_list.transform);
            var layout = _list.GetComponent<VerticalLayoutGroup>();
            RectTransform first = Row("gem.garnet"), last = Row("gem.dragon");
            var grid = last.parent.GetComponent<GridLayoutGroup>();
            Assert.That(grid.constraint, Is.EqualTo(GridLayoutGroup.Constraint.FixedColumnCount));
            Assert.That(grid.constraintCount, Is.EqualTo(2));
            Assert.That(first.anchoredPosition.y, Is.EqualTo(-layout.padding.top).Within(0.01f));
            Assert.That(first.anchoredPosition.y - ((RectTransform)grid.transform).anchoredPosition.y,
                Is.EqualTo(first.rect.height + layout.spacing).Within(0.01f));
            Assert.That(last.anchoredPosition, Is.EqualTo(Vector2.zero), "Hidden gems must not leave empty grid cells.");
            Assert.That(first.rect.height, Is.EqualTo(106.317f).Within(0.01f));
            Assert.That(last.rect.height, Is.EqualTo(first.rect.height / 2f).Within(0.01f));

            SetRoster(GemIds.Skip(1).Select(id => Spawn(id, id)).ToArray(),
                new[] { true, true, true, true, true });
            _list.ShowRunning(_combat, _spawner);
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)_list.transform);
            for (int index = 0; index < 5; index++)
            {
                RectTransform row = Row(GemIds[index + 1]);
                Assert.That(row.anchoredPosition.x,
                    Is.EqualTo(index % 2 * (grid.cellSize.x + grid.spacing.x)).Within(0.01f));
                Assert.That(row.anchoredPosition.y,
                    Is.EqualTo(-(index / 2) * (grid.cellSize.y + grid.spacing.y)).Within(0.01f));
            }
            Assert.That(((RectTransform)grid.transform).rect.height,
                Is.EqualTo(3f * grid.cellSize.y + 2f * grid.spacing.y).Within(0.01f));
            Assert.That(grid.cellSize.x * 2f + grid.spacing.x, Is.EqualTo(first.rect.width).Within(0.01f));
            Assert.That(_list.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(objectCount));
        }

        [Test]
        public void KillRewardsUpdateLiveAndFinalKillIsIncludedWithoutAnotherHudFrame()
        {
            SetRoster(new[] { Spawn("source", "gem.topaz") }, new[] { true });
            _list.ShowRunning(_combat, _spawner);
            Kill("gem.topaz", 3L);
            _list.ShowRunning(_combat, _spawner);
            Assert.That(Text("gem.topaz").text, Is.EqualTo("3"));
            Assert.That(Text("gem.garnet").text, Is.EqualTo("2"));
            Assert.That(_combat.GetResourceEarned("gem.topaz"), Is.EqualTo(3L));
            Assert.That(_combat.GetResourceEarned("gem.unknown"), Is.Zero);

            Kill("gem.topaz", 3L);
            Assert.That(_combat.TryAbortRun(RunEndReason.UserExit,
                RunSettlementPolicy.Eligible, out RunResult result), Is.True);
            _spawner.StopRun();
            _list.ShowResult(result);
            Assert.That(Text("gem.topaz").text, Is.EqualTo("6"));
            Assert.That(Text("gem.garnet").text, Is.EqualTo("4"));

            _list.Clear();
            Assert.That(VisibleIds(), Is.Empty);
            StartRun("gemstones-next");
            SetRoster(new[] { Spawn("new-source", "gem.sapphire") }, new[] { true });
            _list.ShowRunning(_combat, _spawner);
            _list.ShowResult(result);
            Assert.That(VisibleIds(), Is.EqualTo(new[] { "gem.garnet", "gem.sapphire" }));
            Assert.That(Text("gem.garnet").text, Is.EqualTo("0"));
            Assert.That(Text("gem.topaz").text, Is.EqualTo("0"));
            Assert.That(_combat.GetResourceEarned("gem.topaz"), Is.Zero);
        }

        [Test]
        public void LiveRosterChangesKeepEarnedDropsAndExactInt64Amounts()
        {
            SetRoster(new[] { Spawn("source", "gem.dragon", garnet: 0L) }, new[] { true });
            _list.ShowRunning(_combat, _spawner);
            Assert.That(VisibleIds(), Is.EqualTo(new[] { "gem.dragon" }));
            Kill("gem.dragon", long.MaxValue, garnet: 0L);
            SetRoster(new[] { Spawn("source", "gem.dragon", garnet: 0L) }, new[] { false });
            _list.ShowRunning(_combat, _spawner);
            Assert.That(VisibleIds(), Is.EqualTo(new[] { "gem.dragon" }));
            Assert.That(Text("gem.dragon").text, Is.EqualTo("9223372036854775807"));
        }

        private void StartRun(string id)
        {
            Assert.That(_combat.StartRun(new RunRequest(id, 30f),
                new CombatSnapshot(10L, 1f, 0.1f, 1, 0f, 1f, true, 0.1f)), Is.True);
        }

        private static SpawnSnapshot Spawn(string monster, string gem, long amount = 1L,
            float chance = 100f, long garnet = 2L) =>
            new SpawnSnapshot(monster, "test-visual", 1L, 1f, 1, 80, garnet, gem, amount, chance);

        private void SetRoster(SpawnSnapshot[] snapshots, bool[] enabled)
        {
            var plan = new SpawnPlan(snapshots.Select(s => new SpawnPlanEntry(s, null)).ToArray());
            SetField(_spawner, "_currentSpawnPlan", plan);
            SetField(_spawner, "_entries", plan.Entries);
            SetField(_spawner, "_enabledSpecies", enabled);
        }

        private void Kill(string gem, long amount, long garnet = 2L)
        {
            var target = new RewardTarget(_combat.CurrentRunId, gem, amount, garnet);
            typeof(CombatRunController).GetMethod("ApplyBundle", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_combat, new object[] { target, 10L, CombatDamageSource.CursorAttack, false });
        }

        private string[] VisibleIds() => _list.GetComponentsInChildren<GemstoneIconView>()
            .Select(icon => icon.GemstoneId).ToArray();
        private RectTransform Row(string id) => (RectTransform)_list
            .GetComponentsInChildren<GemstoneIconView>(true).Single(icon => icon.GemstoneId == id).transform.parent;
        private TMP_Text Text(string id) => Row(id).GetComponentInChildren<TMP_Text>(true);
        private static void SetField(object target, string name, object value) => target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        private sealed class RewardTarget : ICombatTarget
        {
            public RewardTarget(RunId run, string gem, long amount, long garnet)
            { RunId = run; BonusDropCurrencyId = gem; BonusDropAmount = amount; GarnetReward = garnet; }
            public RunId RunId { get; }
            public bool IsRegistered => true;
            public bool IsActive { get; private set; } = true;
            public long GarnetReward { get; }
            public string BonusDropCurrencyId { get; }
            public long BonusDropAmount { get; }
            public float BonusDropChancePercent => 100f;
            public bool ApplyDamage(RunId runId, long damage, out long effectiveDamage, out bool killed)
            {
                killed = IsActive && runId == RunId && damage > 0L;
                effectiveDamage = killed ? 1L : 0L;
                if (killed) IsActive = false;
                return killed;
            }
        }
    }
}
