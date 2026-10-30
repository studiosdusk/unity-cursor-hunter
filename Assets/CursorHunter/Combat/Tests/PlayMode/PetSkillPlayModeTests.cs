using System.Collections;
using System.Reflection;
using CursorHunter.Contracts;
using NUnit.Framework;
using Spine;
using Spine.Unity;
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
        private GameObject _effectPrefab;
        private GameObject _combatRoot;
        private CombatRunController _combat;
        private PetSkillController _skill;

        [UnityTearDown]
        public IEnumerator CleanUp()
        {
            if (_pet != null) Object.Destroy(_pet);
            if (_combatRoot != null) Object.Destroy(_combatRoot);
            if (_effectPrefab != null) Object.Destroy(_effectPrefab);
            if (_sandboxScene.IsValid() && _sandboxScene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(_sandboxScene);
            yield return null;
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

        [UnityTest]
        public IEnumerator IdleScansAreThrottledAndChooseNearestHitArea()
        {
            Vector3 spawn = new Vector3(30f, 0f, 0f);
            CreateIsolatedPet(spawn);
            CreateTarget(spawn + Vector3.right * 10f);
            yield return Tick(0.01f);
            Assert.That(CurrentTarget, Is.Null, "Targets outside detection range are ignored.");
            Assert.That(_pet.transform.position, Is.EqualTo(spawn));

            MonsterCombatTarget nearerCenter = CreateTarget(spawn + Vector3.right * 2f, 0.25f);
            MonsterCombatTarget nearerHitArea = CreateTarget(spawn + Vector3.right * 4f, 3f);
            MonsterCombatTarget invulnerable = CreateTarget(spawn + Vector3.left);
            SetField(invulnerable, "_isVulnerable", false);
            yield return Tick(0.04f);
            Assert.That(CurrentTarget, Is.Null, "Do not rescan every frame while idle.");
            yield return Tick(0.07f);
            Assert.That(CurrentTarget, Is.SameAs(nearerHitArea));

            // A closer newcomer must not interrupt the current attack or cause target churn.
            nearerCenter.transform.position = spawn;
            yield return Tick(0.6f);
            Assert.That(CurrentTarget, Is.SameAs(nearerHitArea));
            nearerHitArea.gameObject.SetActive(false);
            yield return Tick(0.1f);
            Assert.That(CurrentTarget, Is.SameAs(nearerCenter));

            nearerCenter.gameObject.SetActive(false);
            yield return Tick(0.6f);
            Assert.That(CurrentTarget, Is.Null);
            Assert.That(_pet.transform.position, Is.EqualTo(spawn));
        }

        [UnityTest]
        public IEnumerator PendingHitsRecheckRangeAndVulnerabilityWithoutACursor()
        {
            Vector3 spawn = new Vector3(30f, 0f, 0f);
            CreateIsolatedPet(spawn);
            MonsterCombatTarget target = CreateTarget(spawn + Vector3.right * 1.95f);
            yield return Tick(0.01f);
            target.transform.position += Vector3.right * 0.1f;
            yield return Tick(0.2f);
            Assert.That(target.CurrentHealth, Is.EqualTo(target.MaxHealth),
                "Even a 0.1-unit step across the range boundary cancels the delayed hit.");
            Assert.That(_pet.transform.position, Is.EqualTo(spawn));
            target.transform.position = spawn + Vector3.right * 20f;
            yield return Tick(0.4f);
            yield return Tick(0.01f);
            Assert.That(CurrentTarget, Is.SameAs(target));
            Assert.That(_pet.transform.position.x, Is.GreaterThan(spawn.x),
                "Pursuit is independent of the old cursor leash, even beyond detection range.");

            target.transform.position = _pet.transform.position + Vector3.right;
            yield return Tick(0.7f);
            SetField(target, "_isVulnerable", false);
            yield return Tick(0.2f);
            Assert.That(target.CurrentHealth, Is.EqualTo(target.MaxHealth));
            yield return Tick(0.4f);
            Assert.That(CurrentTarget, Is.Null);

            SetField(target, "_isVulnerable", true);
            yield return Tick(0.7f);
            yield return Tick(0.2f);
            Assert.That(target.CurrentHealth, Is.LessThan(target.MaxHealth),
                "A nearby valid enemy must be hit without any cursor reference.");
        }

        [UnityTest]
        public IEnumerator AttackLocksPositionUntilSpineCompletesThenClearsAttackPose()
        {
#if UNITY_EDITOR
            Vector3 spawn = new Vector3(30f, 0f, 0f);
            CreateIsolatedPet(spawn, true);
            MonsterCombatTarget target = CreateTarget(spawn + Vector3.right);
            yield return Tick(0.01f);
            SkeletonAnimation animation = _pet.GetComponent<SkeletonAnimation>();
            TrackEntry attack = animation.AnimationState.GetTrack(0);
            Assert.That(attack.Animation.Name, Is.EqualTo("Attack"));
            animation.timeScale = 0f;
            attack.TrackTime = attack.AnimationEnd - 0.01f;
            target.transform.position = spawn + Vector3.right * 5f;

            // The combat timer has ended, but the actual Spine motion has not.
            yield return Tick(0.6f);
            Assert.That(attack.IsComplete, Is.False);
            Assert.That(_pet.transform.position, Is.EqualTo(spawn));
            Assert.That(animation.AnimationState.GetTrack(0), Is.SameAs(attack));

            attack.TrackTime = attack.AnimationEnd;
            yield return Tick(0.01f);
            Assert.That(_pet.transform.position, Is.EqualTo(spawn),
                "The completion frame must clear the attack pose before locomotion resumes.");
            TrackEntry idle = animation.AnimationState.GetTrack(0);
            Assert.That(idle.Animation.Name, Is.EqualTo("Idle"));
            Assert.That(idle.MixDuration, Is.Zero,
                "The source asset's 0.2-second attack crossfade must not follow the moving root.");
            yield return Tick(0.1f);
            Assert.That(_pet.transform.position.x, Is.GreaterThan(spawn.x));
            Assert.That(animation.AnimationState.GetTrack(0).Animation.Name, Is.EqualTo("Walk"));
#else
            Assert.Ignore("Requires the authored BabyDragon Spine prefab.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator FastConsecutiveAttacksRestartSpineAndApplyOneHitEach()
        {
#if UNITY_EDITOR
            Vector3 spawn = new Vector3(30f, 0f, 0f);
            CreateIsolatedPet(spawn, true);
            SkillBalanceSettings settings = _skill.CaptureBalanceSettings();
            settings.Values.Find(value => value.Id == "attackIntervalSeconds").Value = 0.5f;
            Assert.That(_skill.ApplyBalanceSettings(_combat, _combat.CurrentRunId, 1UL,
                settings), Is.True);
            object runtime = typeof(PetSkillController).GetField("_runtime",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_skill);
            long petDamage = (long)runtime.GetType().GetProperty("Damage",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(runtime);
            Assert.That(petDamage, Is.EqualTo(1L),
                "Pet damage is its own authored stat; player attack power must not multiply it.");
            MonsterCombatTarget target = CreateTarget(spawn + Vector3.right);
            yield return Tick(0.01f);
            SkeletonAnimation animation = _pet.GetComponent<SkeletonAnimation>();
            animation.timeScale = 0f;
            TrackEntry first = animation.AnimationState.GetTrack(0);
            first.TrackTime = 0.2f;
            yield return Tick(0.2f);
            Assert.That(target.CurrentHealth, Is.EqualTo(target.MaxHealth - 1L));
            first.TrackTime = first.AnimationEnd;
            yield return Tick(0.31f);
            yield return Tick(0.01f);
            TrackEntry second = animation.AnimationState.GetTrack(0);
            Assert.That(second.Animation.Name, Is.EqualTo("Attack"));
            Assert.That(second.TrackTime, Is.Zero, "Each non-looping attack must restart.");
            Assert.That(target.CurrentHealth, Is.EqualTo(target.MaxHealth - 1L));
            second.TrackTime = 0.2f;
            yield return Tick(0.2f);
            Assert.That(target.CurrentHealth, Is.EqualTo(target.MaxHealth - 2L));
            Assert.That(_pet.transform.position, Is.EqualTo(spawn));
#else
            Assert.Ignore("Requires the authored BabyDragon Spine prefab.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator StalePhysicsPositionCannotStartAnOutOfRangeAttack()
        {
            Vector3 spawn = new Vector3(30f, 0f, 0f);
            CreateIsolatedPet(spawn);
            MonsterCombatTarget target = CreateTarget(spawn + Vector3.right);
            SetField(_skill, "_target", target);
            Physics2D.SyncTransforms();
            target.transform.position = spawn + Vector3.right * 5f;
            // Invoke in this frame before a physics step can synchronize the collider.
            typeof(PetSkillController).GetMethod("LateUpdate",
                BindingFlags.NonPublic | BindingFlags.Instance).Invoke(_skill, null);
            Assert.That(typeof(PetSkillController).GetField("_attackPending",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_skill), Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PauseAndRestartPreserveSpawnAndRejectOldRunTargets()
        {
            Vector3 spawn = new Vector3(30f, -2f, 0f);
            CreateIsolatedPet(spawn);
            MonsterCombatTarget target = CreateTarget(spawn + Vector3.right * 5f);
            yield return Tick(0.5f);
            Vector3 chasingPosition = _pet.transform.position;
            Assert.That(chasingPosition.x, Is.GreaterThan(spawn.x));
            Assert.That(_combat.PauseRun(), Is.True);
            yield return Tick(2f);
            Assert.That(_pet.transform.position, Is.EqualTo(chasingPosition));
            Assert.That(target.CurrentHealth, Is.EqualTo(target.MaxHealth));
            Assert.That(_combat.ResumeRun(), Is.True);
            Assert.That(_combat.TryAbortRun(RunEndReason.UserExit, default, out _), Is.True);

            StartIsolatedRun("pet-restart");
            yield return Tick(0.01f);
            Assert.That(_pet.transform.position, Is.EqualTo(spawn));
            Assert.That(CurrentTarget, Is.Null, "An old run's enemy must not be acquired.");
            target = CreateTarget(spawn + Vector3.right);
            yield return Tick(0.11f);
            Assert.That(CurrentTarget, Is.SameAs(target));
            Assert.That(_combat.TryAbortRun(RunEndReason.UserExit, default, out _), Is.True);
            yield return Tick(1f);
            Assert.That(target.CurrentHealth, Is.EqualTo(target.MaxHealth));
            Assert.That(_skill.IsPetVisible, Is.False);
            Assert.That(_skill.ActiveEffectCount, Is.Zero);
        }

        private MonsterCombatTarget CurrentTarget => (MonsterCombatTarget)
            typeof(PetSkillController).GetField("_target",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_skill);

        private void CreateIsolatedPet(Vector3 spawn, bool withSpine = false)
        {
            _combatRoot = new GameObject("Pet autonomous combat test");
            _combat = _combatRoot.AddComponent<CombatRunController>();
            _combat.enabled = false; // Advance only the explicit test clock.
#if UNITY_EDITOR
            _pet = withSpine ? Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/DownLoadAssets/2D Characters-PetPack4/Scene/Spine GameObject (BabyDragon) (1).prefab")) :
                new GameObject("IndependentBabyDragon");
#else
            _pet = new GameObject("IndependentBabyDragon");
#endif
            _pet.transform.position = spawn;
            _effectPrefab = new GameObject("TestImpact");
            _effectPrefab.SetActive(false);
            var skillObject = new GameObject("PetController");
            skillObject.SetActive(false);
            skillObject.transform.SetParent(_combatRoot.transform, false);
            _skill = skillObject.AddComponent<PetSkillController>();
            SetField(_skill, "petRoot", _pet.transform);
            SetField(_skill, "attackEffectPrefab", _effectPrefab);
            skillObject.SetActive(true);
            StartIsolatedRun("pet-autonomous");
        }

        private void StartIsolatedRun(string runId)
        {
            Assert.That(_combat.PrepareRun(new RunRequest(runId, 30f),
                new CombatSnapshot(10L, 1f, 0.2f, 1)), Is.True);
            Assert.That(_combat.ConfigureSkills(new[]
            {
                new SkillCombatSnapshot(PetSkillController.SkillId, true)
            }), Is.True);
            Assert.That(_combat.CommitStart(), Is.True);
        }

        private MonsterCombatTarget CreateTarget(Vector3 position, float radius = 0.5f)
        {
            var root = new GameObject("Pet test enemy");
            root.transform.SetParent(_combatRoot.transform, false);
            root.transform.position = position;
            CircleCollider2D hitArea = root.AddComponent<CircleCollider2D>();
            hitArea.radius = radius;
            hitArea.isTrigger = true;
            var target = root.AddComponent<MonsterCombatTarget>();
            target.Initialize(new SpawnSnapshot("pet-test", "pet-test", 1000L,
                1f, 1, 80, 0L), _combat.CurrentRunId);
            return target;
        }

        private IEnumerator Tick(float seconds)
        {
            _combat.AdvanceTime(seconds);
            yield return null;
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
