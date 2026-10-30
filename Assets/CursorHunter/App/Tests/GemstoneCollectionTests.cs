using System;
using System.Collections.Generic;
using System.Reflection;
using CursorHunter.Combat;
using CursorHunter.Contracts;
using CursorHunter.Data;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CursorHunter.App.Tests
{
    public sealed class GemstoneCollectionTests
    {
        private GameObject _root;
        private CombatRunController _combat;
        private GemstoneCollectionController _controller;
        private GemstoneCollectionController.HudBinding[] _bindings;
        private static readonly string[] Ids = { "gem.garnet", "gem.topaz", "gem.amethyst", "gem.sapphire", "gem.diamond", "gem.dragon" };

        [SetUp] public void Setup()
        {
            _root = new GameObject("Collection sandbox");
            _combat = _root.AddComponent<CombatRunController>();
            var camera = new GameObject("Camera").AddComponent<Camera>(); camera.transform.SetParent(_root.transform);
            camera.transform.position = new Vector3(0, 0, -10); camera.orthographic = true;
            var canvas = new GameObject("HUD", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
            canvas.transform.SetParent(_root.transform); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var effects = new GameObject("Effects", typeof(RectTransform), typeof(Canvas)).GetComponent<RectTransform>();
            effects.SetParent(canvas.transform, false); effects.sizeDelta = new Vector2(1920, 1080);
            _bindings = new GemstoneCollectionController.HudBinding[6];
            for (int i = 0; i < 6; i++)
            {
                var row = new GameObject(Ids[i], typeof(RectTransform)); row.transform.SetParent(canvas.transform, false);
                var text = new GameObject("Amount", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
                text.transform.SetParent(row.transform, false); text.text = "0";
                var target = new GameObject("TargetPoint", typeof(RectTransform)).GetComponent<RectTransform>();
                target.SetParent(row.transform, false); target.anchoredPosition = new Vector2(i * 60, 100);
                _bindings[i] = new GemstoneCollectionController.HudBinding { currencyId = Ids[i], row = row,
                    amountText = text, targetPoint = target, numberColor = Color.white };
            }
            _controller = _root.AddComponent<GemstoneCollectionController>();
            Set("combat", _combat); Set("worldCamera", camera); Set("hudCanvas", canvas); Set("effectRoot", effects);
            Set("flyPrefab", AssetDatabase.LoadAssetAtPath<GemstoneFlyView>("Assets/CursorHunter/App/Prefabs/GemstoneFlyView.prefab"));
            Set("iconCatalog", GemstoneIconCatalog.LoadDefault()); Set("bindings", _bindings);
            _controller.Settings.budget.poolSize = 16; _controller.Settings.budget.maxActive = 16;
            _controller.Settings.budget.bonusReserved = 4; _controller.Settings.sound.volume = 0;
            Assert.That(_controller.Initialize(), Is.True, _controller.ValidateConfiguration());
            Start("collection-test");
        }
        [TearDown] public void Cleanup() { Object.DestroyImmediate(_root); }
        private void Set(string name, object value) => typeof(GemstoneCollectionController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_controller, value);
        private void Start(string id) => Assert.That(_combat.StartRun(new RunRequest(id, 30f), new CombatSnapshot(10, 1, .1f, 1, 0, 1, true, .1f)), Is.True);
        private Target Kill(long garnet, string bonusId = null, long bonus = 0)
        {
            var target = new Target(_combat.CurrentRunId, garnet, bonusId, bonus);
            Assert.That(_combat.TryApplySkillDamage(_combat.CurrentRunId, target, 10), Is.True);
            return target;
        }
        private void Advance(float duration)
        {
            for (float t = 0; t < duration; t += 1f / 60f) _controller.AdvancePresentation(1f / 60f);
        }

        [TestCase(0L,0L,0,0)] [TestCase(1L,0L,0,1)] [TestCase(4L,0L,0,4)]
        [TestCase(5L,0L,1,0)] [TestCase(9L,0L,1,4)] [TestCase(10L,1L,0,0)]
        [TestCase(14L,1L,0,4)] [TestCase(15L,1L,1,0)] [TestCase(43L,4L,0,3)]
        [TestCase(48L,4L,1,3)] [TestCase(120L,12L,0,0)]
        [TestCase(long.MaxValue,922337203685477580L,1,2)]
        public void DenominationsPreserveExactAmount(long amount, long large, int medium, int small)
        {
            GemstoneDenominations.Split(amount, out long l, out int m, out int s);
            Assert.That((l,m,s), Is.EqualTo((large,medium,small)));
            Assert.That(10m*l+5m*m+s, Is.EqualTo((decimal)amount));
        }

        [Test] public void FortyThreeArrivesAsFourLargeAndThreeSmallOnlyAfterFlight()
        {
            Kill(43);
            Assert.That(_combat.GarnetEarned, Is.EqualTo(43));
            Assert.That(_controller.Shown(Ids[0]), Is.Zero);
            Assert.That(_controller.PendingAmount(Ids[0]), Is.EqualTo(43));
            Advance(.12f);
            Assert.That(_controller.ActiveCount, Is.GreaterThan(0));
            Assert.That(_controller.Shown(Ids[0]), Is.Zero);
            Advance(1.2f);
            Assert.That((_controller.EmittedLarge,_controller.EmittedMedium,_controller.EmittedSmall), Is.EqualTo((4L,0L,3L)));
            Assert.That(_controller.ArrivedObjects, Is.EqualTo(7));
            Assert.That(_controller.Shown(Ids[0]), Is.EqualTo(43));
            Assert.That(_bindings[0].amountText.rectTransform.localScale, Is.EqualTo(Vector3.one));
        }
        [Test] public void EachCurrencyAndEachKillKeepsItsOwnDenominationsUnderPressure()
        {
            Kill(48, Ids[1], 6);
            for (int i=0;i<5;i++) Kill(1);
            Advance(3);
            Assert.That(_controller.Shown(Ids[0]), Is.EqualTo(53));
            Assert.That(_controller.Shown(Ids[1]), Is.EqualTo(6));
            Assert.That((_controller.EmittedLarge,_controller.EmittedMedium,_controller.EmittedSmall), Is.EqualTo((4L,2L,9L)));
            Assert.That(_controller.PeakActiveCount, Is.LessThanOrEqualTo(16));
        }
        [Test] public void EightyKillsDoNotDropObjectsWhenPoolIsFull()
        {
            for (int i=0;i<80;i++) Kill(43);
            Advance(40);
            Assert.That(_controller.Shown(Ids[0]), Is.EqualTo(3440));
            Assert.That(_controller.ArrivedObjects, Is.EqualTo(560));
            Assert.That((_controller.EmittedLarge,_controller.EmittedSmall), Is.EqualTo((320L,240L)));
            Assert.That(_controller.PeakActiveCount, Is.LessThanOrEqualTo(16));
            Assert.That(_controller.PendingBatches, Is.Zero);
            Assert.That(_controller.QueueGrowthCount, Is.Zero);
        }
        [Test] public void PauseAndFocusFreezeFlightThenResumeWithoutChangingAmount()
        {
            Kill(43); Advance(.1f); int active = _controller.ActiveCount;
            _combat.PauseRun(); Advance(2);
            Assert.That(_controller.ActiveCount, Is.EqualTo(active)); Assert.That(_controller.Shown(Ids[0]), Is.Zero);
            _combat.ResumeRun(); Set("_hasFocus", false); Advance(2);
            Assert.That(_controller.Shown(Ids[0]), Is.Zero);
            Set("_hasFocus", true); Advance(2); Assert.That(_controller.Shown(Ids[0]), Is.EqualTo(43));
        }
        [Test] public void MainPoolBudgetDrainsEightyFortyThreeDropsWithinTwoSeconds()
        {
            _controller.Settings.budget.poolSize = 256;
            _controller.Settings.budget.maxActive = 256;
            _controller.Settings.budget.bonusReserved = 64;
            _controller.SettingsChanged();
            _combat.TryAbortRun(RunEndReason.Reset, RunSettlementPolicy.Discard, out _);
            Start("larger-pool-next-run");
            for (int i = 0; i < 80; i++) Kill(43);
            Advance(2f);
            Assert.That(_controller.ArrivedObjects, Is.EqualTo(560));
            Assert.That(_controller.Shown(Ids[0]), Is.EqualTo(3440));
            Assert.That(_controller.PendingBatches, Is.Zero);
            Assert.That(_controller.PeakActiveCount, Is.LessThanOrEqualTo(256));
        }
        [Test] public void OneHundredRestartsReleaseOldFlightsAndKeepTheNewRunEmpty()
        {
            for (int i = 0; i < 100; i++)
            {
                Kill(43); Advance(.08f);
                Assert.That(_combat.TryAbortRun(RunEndReason.UserExit, RunSettlementPolicy.Discard, out _), Is.True);
                Start("restart-" + i);
                Assert.That(_controller.ActiveCount, Is.Zero);
                Assert.That(_controller.PendingBatches, Is.Zero);
                Assert.That(_controller.Shown(Ids[0]), Is.Zero);
                Assert.That(_bindings[0].amountText.rectTransform.localScale, Is.EqualTo(Vector3.one));
            }
        }
        [Test] public void LastKillCompletesWithinDrainDeadlineAndOldEventsCannotEnterRestart()
        {
            CombatRewardResolved old = default; _combat.RewardResolved += reward => old = reward;
            Kill(43); Advance(.08f);
            Assert.That(_combat.TryCompleteRun(RunEndReason.TimeExpired, RunSettlementPolicy.Eligible, out var result), Is.True);
            Assert.That(_controller.ActiveCount, Is.GreaterThan(0), "Combat presentation reset must not discard the last gems.");
            _controller.CompleteRun(result); Advance(.31f);
            Assert.That(_controller.IsFinishing, Is.False); Assert.That(_controller.Shown(Ids[0]), Is.EqualTo(43));
            Assert.That(_controller.ActiveCount, Is.Zero);
            Start("collection-next");
            typeof(GemstoneCollectionController).GetMethod("HandleReward", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_controller,new object[]{old});
            _controller.CompleteRun(result); Advance(1);
            Assert.That(_controller.Shown(Ids[0]), Is.Zero); Assert.That(_controller.ActiveCount, Is.Zero);
        }
        [Test] public void DuplicateEventsAndRepeatedDeadHitsCannotDuplicateRewards()
        {
            CombatRewardResolved captured = default; int calls = 0;
            _combat.RewardResolved += value => { captured = value; calls++; };
            Target target = Kill(43);
            Assert.That(_combat.TryApplySkillDamage(_combat.CurrentRunId, target, 10), Is.False);
            typeof(GemstoneCollectionController).GetMethod("HandleReward", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_controller,new object[]{captured});
            Advance(2);
            Assert.That(calls, Is.EqualTo(1)); Assert.That(_controller.Shown(Ids[0]), Is.EqualTo(43));
            Assert.That(_controller.ArrivedObjects, Is.EqualTo(7));
        }
        [Test] public void RewardEventContainsCommittedNormalizedAmountsAndPreDeathBodyPosition()
        {
            CombatRewardResolved captured = default;
            _combat.RewardResolved += value => {
                captured=value;
                Assert.That(_combat.GetResourceEarned(Ids[0]), Is.EqualTo(value.Garnet));
            };
            Kill(40, Ids[0], 3);
            Assert.That(captured.Garnet, Is.EqualTo(43)); Assert.That(captured.BonusAmount, Is.Zero);
            Assert.That(captured.WorldPosition, Is.EqualTo(new Vector3(3,4,0)));
            Assert.That(captured.HasWorldPosition, Is.True);
        }
        [Test] public void OverflowDoesNotPartiallyCommitAnotherCurrencyOrPublishReward()
        {
            _controller.enabled=false;
            int count=0; _combat.RewardResolved += value => count++;
            Kill(1, Ids[1], long.MaxValue);
            Kill(5, Ids[1], 1);
            Assert.That(count, Is.EqualTo(1)); Assert.That(_combat.GarnetEarned, Is.EqualTo(1));
            Assert.That(_combat.GetResourceEarned(Ids[1]), Is.EqualTo(long.MaxValue));
        }
        [Test] public void LargeIntegerCountersStayExactAndReducedMotionPreservesDenominations()
        {
            // Synchronize an already earned huge balance, then animate only its final seven units.
            var rewards=(Dictionary<string,long>)typeof(CombatRunController).GetField("_resourceRewards",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(_combat);
            rewards[Ids[1]]=long.MaxValue-7;
            _controller.CancelPresentation(); _controller.BeginRun(_combat);
            _controller.Settings.reducedMotion=true; _controller.SettingsChanged();
            Kill(0, Ids[1], 7);
            long previous=_controller.Shown(Ids[1]);
            for(int i=0;i<60;i++)
            {
                _controller.AdvancePresentation(1f/60f);
                long current=_controller.Shown(Ids[1]);
                Assert.That(current, Is.GreaterThanOrEqualTo(previous)); previous=current;
            }
            Assert.That(previous, Is.EqualTo(long.MaxValue));
            Assert.That(_bindings[1].amountText.text, Is.EqualTo("9223372036854775807"));
            Assert.That((_controller.EmittedMedium,_controller.EmittedSmall), Is.EqualTo((1L,2L)));
        }
        [TestCase(RunSettlementPolicy.Eligible)]
        [TestCase(RunSettlementPolicy.Discard)]
        public void BossBurstGroupsHugeRewardsIntoBoundedSimultaneousFlightsWithoutLosingLongPrecision(RunSettlementPolicy policy)
        {
            RunId id = _combat.CurrentRunId;
            _combat.TryCompleteRun(RunEndReason.TimeExpired, RunSettlementPolicy.Eligible, out _);
            var request = new RunRequest(id, 4, 1, RunMode.Boss, "boss.20", 1, 30, 20);
            var result = new RunResult(request, RunEndReason.BossDefeated, policy,
                12, 1, long.MaxValue, 1, new[] {
                    new ResourceRewardSnapshot(Ids[0], long.MaxValue),
                    new ResourceRewardSnapshot(Ids[1], 999999999999L),
                    new ResourceRewardSnapshot(Ids[2], 123456),
                    new ResourceRewardSnapshot(Ids[3], 98765),
                    new ResourceRewardSnapshot(Ids[4], 4321),
                    new ResourceRewardSnapshot(Ids[5], 789) });
            _controller.CompleteRun(result);
            Assert.That(_controller.ActiveCount, Is.EqualTo(16));
            _controller.CompleteRun(result);
            Assert.That(_controller.ActiveCount, Is.EqualTo(16), "Duplicate completion cannot launch another burst.");
            Advance(3);
            Assert.That(_controller.Shown(Ids[0]), Is.EqualTo(long.MaxValue));
            Assert.That(_controller.Shown(Ids[1]), Is.EqualTo(999999999999L));
            foreach (ResourceRewardSnapshot reward in result.Rewards)
                Assert.That(_controller.Shown(reward.CurrencyId), Is.EqualTo(reward.Amount));
            Assert.That(_controller.ActiveCount, Is.Zero);
            Assert.That(_controller.IsFinishing, Is.False);
        }

        private sealed class Target : ICombatTarget, IHitEffectAnchor
        {
            public Target(RunId run,long garnet,string bonus,long amount) { RunId=run;GarnetReward=garnet;BonusDropCurrencyId=bonus;BonusDropAmount=amount; }
            public RunId RunId {get;}
            public bool IsRegistered => true;
            public bool IsActive {get;private set;}=true;
            public long GarnetReward {get;}
            public string BonusDropCurrencyId {get;}
            public long BonusDropAmount {get;}
            public float BonusDropChancePercent => 100;
            public bool TryGetHitEffectBounds(out Bounds bounds) { bounds=new Bounds(IsActive?new Vector3(3,4,0):new Vector3(99,99,0),Vector3.one);return true; }
            public bool ApplyDamage(RunId run,long damage,out long effective,out bool killed)
            { killed=IsActive&&run==RunId&&damage>0;effective=killed?1:0;if(killed)IsActive=false;return killed; }
        }
    }
}
