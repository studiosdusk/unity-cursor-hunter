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
    public sealed class PetMainScenePlayModeTests
    {
        private Scene _mainScene;

        [UnityTearDown]
        public IEnumerator CleanUp()
        {
            if (_mainScene.IsValid() && _mainScene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(_mainScene);
        }

        [UnityTest]
        public IEnumerator MainSceneUsesExistingBabyDragonAndExplosionPrefab()
        {
#if UNITY_EDITOR
            _mainScene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/CursorHunter/App/Scenes/Main.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;

            PetSkillController controller =
                Object.FindFirstObjectByType<PetSkillController>();
            Assert.That(controller, Is.Not.Null);
            FieldInfo field = typeof(PetSkillController).GetField("petRoot",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null);
            Transform pet = (Transform)field.GetValue(controller);
            Assert.That(pet, Is.Not.Null);
            Transform ring = pet.Find("MagicCircle1_Green");
            Assert.That(ring, Is.Not.Null);
            Transform configuredRing = (Transform)typeof(PetSkillController)
                .GetField("groundRing",
                    BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(controller);
            Assert.That(configuredRing, Is.EqualTo(ring));
            Assert.That(pet.gameObject.activeSelf, Is.False);
            Camera worldCamera = (Camera)typeof(PetSkillController)
                .GetField("worldCamera",
                    BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(controller);
            Assert.That(worldCamera, Is.Not.Null);

            CombatRunController combat =
                controller.GetComponentInParent<CombatRunController>();
            Assert.That(combat.PrepareRun(new RunRequest("main-pet-smoke", 5f),
                new CombatSnapshot(10L, 1f, 0.2f, 1)), Is.True);
            Assert.That(combat.ConfigureSkills(new[]
            {
                new SkillCombatSnapshot(PetSkillController.SkillId, true)
            }), Is.True);
            Assert.That(combat.CommitStart(), Is.True);
            yield return null;

            Assert.That(controller.IsPetVisible, Is.True);
            Assert.That(ring.gameObject.activeInHierarchy, Is.True);
            SortingGroup sortingGroup = pet.GetComponent<SortingGroup>();
            Assert.That(sortingGroup, Is.Not.Null);
            Assert.That(sortingGroup.sortingLayerID, Is.Zero);
            Assert.That(sortingGroup.sortingOrder,
                Is.EqualTo(Mathf.Clamp(Mathf.RoundToInt(
                    (1f - worldCamera.WorldToViewportPoint(pet.position).y) *
                    199f), 0, 199)));
            SortingGroup ringSortingGroup = ring.GetComponent<SortingGroup>();
            Assert.That(ring.gameObject.layer, Is.Zero);
            Assert.That(ringSortingGroup, Is.Not.Null);
            Assert.That(ringSortingGroup.sortAtRoot, Is.True);
            Assert.That(ringSortingGroup.sortingLayerID, Is.Zero);
            Assert.That(ringSortingGroup.sortingOrder, Is.EqualTo(-1));
            Assert.That(pet.GetComponents<MonoBehaviour>(),
                Has.Some.Matches<MonoBehaviour>(x =>
                    x != null && x.GetType().Name == "SkeletonAnimation"));
            Assert.That(controller.GetComponentsInChildren<ParticleSystem>(true).Length,
                Is.GreaterThan(0));

            Assert.That(combat.TryAbortRun(RunEndReason.UserExit,
                default, out _), Is.True);
            Assert.That(pet.gameObject.activeSelf, Is.False);
            Assert.That(ring.gameObject.activeInHierarchy, Is.False);
#else
            Assert.Ignore("Requires the Editor PlayMode scene loader.");
            yield break;
#endif
        }
    }
}
