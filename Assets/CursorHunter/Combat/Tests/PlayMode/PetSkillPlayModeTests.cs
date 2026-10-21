using System.Collections;
using System.Reflection;
using CursorHunter.Contracts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CursorHunter.Combat.Tests
{
    public sealed class PetSkillPlayModeTests
    {
        private Scene _sandboxScene;
        private GameObject _pet;
        private GameObject _cursor;
        private GameObject _effectPrefab;

        [UnityTearDown]
        public IEnumerator CleanUp()
        {
            if (_pet != null) Object.Destroy(_pet);
            if (_cursor != null) Object.Destroy(_cursor);
            if (_effectPrefab != null) Object.Destroy(_effectPrefab);
            if (_sandboxScene.IsValid() && _sandboxScene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(_sandboxScene);
        }

        [UnityTest]
        public IEnumerator UnlockShowsExistingPetThenItMovesHitsPausesAndHides()
        {
#if UNITY_EDITOR
            _sandboxScene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/CursorHunter/Combat/Scenes/CombatSandbox.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;

            CursorHitEffectSandbox sandbox = Object.FindFirstObjectByType<CursorHitEffectSandbox>();
            CombatRunController combat = sandbox.GetComponent<CombatRunController>();
            SetField(sandbox, "_autoHit", false);
            MonsterCombatTarget[] targets =
                sandbox.GetComponentsInChildren<MonsterCombatTarget>();
            Assert.That(targets.Length, Is.GreaterThan(0));
            for (int i = 1; i < targets.Length; i++)
                targets[i].gameObject.SetActive(false);
            MonsterCombatTarget target = targets[0];
            MonsterBehaviorController behavior =
                target.GetComponent<MonsterBehaviorController>();
            if (behavior != null) behavior.enabled = false;
            target.transform.position = new Vector3(5f, 0f, 0f);

            _pet = new GameObject("TestBabyDragon");
            GameObject ring = new GameObject("AllyRing");
            ring.transform.SetParent(_pet.transform, false);
            _cursor = new GameObject("TestCursorAnchor");
            _effectPrefab = new GameObject("TestBlueExplosion");
            _effectPrefab.SetActive(false);
            ParticleSystem particle = _effectPrefab.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particle.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 0.2f;
            main.startLifetime = 0.1f;

            GameObject skillObject = new GameObject("PetSkillTestController");
            skillObject.SetActive(false);
            skillObject.transform.SetParent(combat.transform, false);
            PetSkillController skill = skillObject.AddComponent<PetSkillController>();
            SetField(skill, "petRoot", _pet.transform);
            SetField(skill, "groundRing", ring.transform);
            SetField(skill, "cursorAnchor", _cursor.transform);
            SetField(skill, "worldCamera", Camera.main);
            SetField(skill, "attackEffectPrefab", _effectPrefab);
            skillObject.SetActive(true);

            Assert.That(combat.ConfigureSkills(new[]
            {
                new SkillCombatSnapshot(PetSkillController.SkillId, false)
            }), Is.True);
            combat.AdvanceTime(0.1f);
            yield return null;
            Assert.That(_pet.activeSelf, Is.False);
            Assert.That(ring.activeInHierarchy, Is.False);

            Assert.That(combat.ConfigureSkills(new[]
            {
                new SkillCombatSnapshot(PetSkillController.SkillId, true)
            }), Is.True);
            int hits = 0;
            combat.DamageApplied += applied =>
            {
                if (applied.Source == CombatDamageSource.Skill &&
                    ReferenceEquals(applied.Target, target)) hits++;
            };
            combat.AdvanceTime(0.2f);
            yield return null;
            Assert.That(skill.IsPetVisible, Is.True);
            Assert.That(ring.activeInHierarchy, Is.True);
            Assert.That(_pet.transform.position.x, Is.GreaterThan(-1.5f));
            SortingGroup sortingGroup = _pet.GetComponent<SortingGroup>();
            Assert.That(sortingGroup, Is.Not.Null);
            Assert.That(sortingGroup.sortingLayerID, Is.Zero);
            Assert.That(sortingGroup.sortingOrder,
                Is.EqualTo(ExpectedSortingOrder(
                    Camera.main, _pet.transform.position)));
            SortingGroup ringSortingGroup = ring.GetComponent<SortingGroup>();
            Assert.That(ring.transform.parent, Is.EqualTo(_pet.transform));
            Assert.That(ring.layer, Is.Zero);
            Assert.That(ringSortingGroup, Is.Not.Null);
            Assert.That(ringSortingGroup.sortAtRoot, Is.True);
            Assert.That(ringSortingGroup.sortingLayerID, Is.Zero);
            Assert.That(ringSortingGroup.sortingOrder, Is.EqualTo(-1));

            for (int i = 0; i < 15 && hits == 0; i++)
            {
                combat.AdvanceTime(0.2f);
                yield return null;
            }
            Assert.That(hits, Is.EqualTo(1));
            Assert.That(skill.ActiveEffectCount, Is.EqualTo(1));
            ParticleSystem clonedEffect =
                skill.GetComponentInChildren<ParticleSystem>(true);
            Assert.That(clonedEffect, Is.Not.Null);
            Assert.That(clonedEffect.main.loop, Is.False);

            int originalOrder = sortingGroup.sortingOrder;
            _pet.transform.position += Vector3.down;
            RefreshSorting(skill);
            Assert.That(sortingGroup.sortingOrder, Is.GreaterThan(originalOrder));
            Assert.That(ringSortingGroup.sortingOrder, Is.EqualTo(-1));
            int lowerOrder = sortingGroup.sortingOrder;
            sortingGroup.sortingOrder = -7;
            _pet.transform.position += Vector3.right;
            RefreshSorting(skill);
            Assert.That(sortingGroup.sortingOrder, Is.EqualTo(-7),
                "Moving only on X must not recalculate camera depth.");
            sortingGroup.sortingOrder = lowerOrder;

            Assert.That(combat.PauseRun(), Is.True);
            combat.AdvanceTime(2f);
            yield return null;
            Assert.That(hits, Is.EqualTo(1));
            Assert.That(skill.IsPetVisible, Is.True);
            Assert.That(combat.ResumeRun(), Is.True);
            Assert.That(combat.TryAbortRun(RunEndReason.UserExit, default,
                out _), Is.True);
            combat.AdvanceTime(2f);
            yield return null;
            Assert.That(hits, Is.EqualTo(1));
            Assert.That(_pet.activeSelf, Is.False);
            Assert.That(ring.activeInHierarchy, Is.False);
            Assert.That(skill.ActiveEffectCount, Is.Zero);
#else
            Assert.Ignore("The sandbox scene is loaded through the Editor PlayMode runner.");
            yield break;
#endif
        }

        private static void SetField(object instance, string name, object value)
        {
            FieldInfo field = instance.GetType().GetField(name,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(instance, value);
        }

        private static void RefreshSorting(PetSkillController skill)
        {
            MethodInfo method = typeof(PetSkillController).GetMethod(
                "RefreshVisualSorting",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null);
            method.Invoke(skill, null);
        }

        private static int ExpectedSortingOrder(Camera camera, Vector3 position)
        {
            float viewportY = camera.WorldToViewportPoint(position).y;
            return Mathf.Clamp(Mathf.RoundToInt((1f - viewportY) * 199f),
                0, 199);
        }
    }
}
