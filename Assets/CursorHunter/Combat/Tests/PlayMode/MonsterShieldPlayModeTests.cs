using System.Collections;
using CursorHunter.Contracts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CursorHunter.Combat.Tests
{
    public sealed class MonsterShieldPlayModeTests
    {
        [UnityTest]
        public IEnumerator ShieldParticlesGrowPauseShrinkAndClearWithTheCombatState()
        {
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CursorHunter/Combat/Prefabs/Monsters/MonsterRoot.prefab");
            var sandbox = new GameObject("MonsterShieldSandbox");
            try
            {
                var combat = sandbox.AddComponent<CombatRunController>();
                var request = new RunRequest(RunId.Create(), 1, 1, RunMode.NormalField, "", 7UL, 20f);
                var player = new CombatSnapshot(10L, 1f, 1f, 1, 0f, 1f, false, 0f);
                Assert.That(combat.StartRun(request, player), Is.True);

                var snapshot = new SpawnSnapshot("monster.05", "sandbox", 24L, 1f, 1, 1, 0L, "", 0L, 0f,
                    movementMode: MonsterMovementMode.Stationary,
                    specialAbility: MonsterSpecialAbility.Shield, specialAbilityIntervalSeconds: 4.5f,
                    specialAbilityDurationSeconds: 1.7f, specialAbilityStrength: 0.65f);
                MonsterCombatTarget SpawnSoldier()
                {
                    var soldier = Object.Instantiate(prefab, sandbox.transform).GetComponent<MonsterCombatTarget>();
                    soldier.Initialize(snapshot, request.RunId);
                    Assert.That(soldier.GetComponent<MonsterBehaviorController>().ConfigureMovement(
                        snapshot, soldier, combat, new Bounds(Vector3.zero, new Vector3(10f, 10f, 1f)), 11UL), Is.True);
                    return soldier;
                }

                var target = SpawnSoldier();
                var behavior = target.GetComponent<MonsterBehaviorController>();
                var tick = typeof(MonsterBehaviorController).GetMethod("Update",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

                void Advance(float seconds)
                {
                    combat.AdvanceTime(seconds);
                    tick.Invoke(behavior, null);
                }

                Advance(4.49f);
                Assert.That(target.IsShielded, Is.False);
                Assert.That(target.GetComponentInChildren<ParticleSystem>(true), Is.Null);

                Advance(0.02f);
                Assert.That(target.IsShielded, Is.True);
                var effect = target.GetComponentInChildren<ParticleSystem>();
                Assert.That(effect, Is.Not.Null);
                Assert.That(effect.particleCount, Is.GreaterThan(0));
                float startScale = effect.transform.localScale.x;
                Advance(0.2f);
                float fullScale = effect.transform.localScale.x;
                Assert.That(fullScale, Is.GreaterThan(startScale));
                Assert.That(target.ApplyDamage(request.RunId, 10L, out long damage, out _), Is.True);
                Assert.That(damage, Is.EqualTo(3));

                Assert.That(combat.PauseRun(), Is.True);
                float particleTime = effect.time;
                yield return null;
                Assert.That(effect.time, Is.EqualTo(particleTime));
                Assert.That(effect.transform.localScale.x, Is.EqualTo(fullScale));
                Assert.That(combat.ResumeRun(), Is.True);

                Advance(1.42f);
                Assert.That(target.IsShielded, Is.True);
                Assert.That(effect.transform.localScale.x, Is.InRange(0.01f, fullScale - 0.01f));
                Advance(0.1f);
                Assert.That(target.IsShielded, Is.False);
                Assert.That(effect.gameObject.activeSelf, Is.False);
                Assert.That(effect.transform.parent, Is.SameAs(combat.transform));
                Assert.That(effect.particleCount, Is.Zero);

                Advance(2.8f);
                Assert.That(target.IsShielded, Is.True);
                Assert.That(target.GetComponentInChildren<ParticleSystem>(), Is.SameAs(effect));
                Assert.That(target.ApplyDamage(request.RunId, 100L, out _, out bool killed), Is.True);
                Assert.That(killed, Is.True);
                Assert.That(effect.gameObject.activeSelf, Is.False);

                Object.Destroy(target.gameObject);
                yield return null;
                Assert.That(effect != null, Is.True, "A returned effect outlives its previous monster.");

                var nextSoldier = SpawnSoldier();
                var concurrentSoldier = SpawnSoldier();
                combat.AdvanceTime(4.51f);
                tick.Invoke(nextSoldier.GetComponent<MonsterBehaviorController>(), null);
                tick.Invoke(concurrentSoldier.GetComponent<MonsterBehaviorController>(), null);
                Assert.That(nextSoldier.GetComponentInChildren<ParticleSystem>(), Is.SameAs(effect));
                var concurrentEffect = concurrentSoldier.GetComponentInChildren<ParticleSystem>();
                Assert.That(concurrentEffect, Is.Not.Null.And.Not.SameAs(effect));

                nextSoldier.gameObject.SetActive(false);
                Assert.That(effect.gameObject.activeSelf, Is.False);
                Assert.That(effect.transform.parent, Is.SameAs(combat.transform));
                concurrentSoldier.Unregister();
                Assert.That(concurrentEffect.gameObject.activeSelf, Is.False);
                Assert.That(concurrentEffect.transform.parent, Is.SameAs(combat.transform));

                Assert.That(combat.TryAbortRun(RunEndReason.UserExit, default, out _), Is.True);
                request = new RunRequest(RunId.Create(), 1, 1, RunMode.NormalField, "", 8UL, 20f);
                Assert.That(combat.StartRun(request, player), Is.True);
                var retrySoldier = SpawnSoldier();
                combat.AdvanceTime(4.51f);
                tick.Invoke(retrySoldier.GetComponent<MonsterBehaviorController>(), null);
                Assert.That(retrySoldier.GetComponentInChildren<ParticleSystem>(), Is.SameAs(concurrentEffect));
                Object.Destroy(retrySoldier.gameObject);
                yield return null;
                Assert.That(concurrentEffect != null, Is.True);
                Assert.That(concurrentEffect.transform.parent, Is.SameAs(combat.transform));
            }
            finally
            {
                Object.Destroy(sandbox);
            }
#else
            Assert.Ignore("This test loads the combat prefab through the Editor.");
            yield break;
#endif
        }
    }
}
