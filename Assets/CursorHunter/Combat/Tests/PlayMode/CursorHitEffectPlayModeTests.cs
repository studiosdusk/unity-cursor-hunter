using System.Collections;
using CursorHunter.Contracts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CursorHunter.Combat.Tests
{
    public sealed class CursorHitEffectPlayModeTests
    {
        private Scene _sandboxScene;

        [UnityTearDown]
        public IEnumerator UnloadSandboxAfterEachTest()
        {
            if (_sandboxScene.IsValid() && _sandboxScene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(_sandboxScene);
        }

        [UnityTest]
        public IEnumerator InspectorArrayEditsApplyDuringPlayIncludingEmptyAndDisabledStates()
        {
#if UNITY_EDITOR
            Scene scene = _sandboxScene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/CursorHunter/Combat/Scenes/CombatSandbox.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            var sandbox = Object.FindFirstObjectByType<CursorHitEffectSandbox>();
            var combat = sandbox.GetComponent<CombatRunController>();
            var manager = sandbox.GetComponentInChildren<CursorHitEffectManager>();
            var settings = new UnityEditor.SerializedObject(manager);
            var variants = settings.FindProperty("effectVariants");
            var prefabs = new Object[variants.arraySize];
            var materials = new Object[variants.arraySize];
            for (int i = 0; i < prefabs.Length; i++)
            {
                var entry = variants.GetArrayElementAtIndex(i);
                prefabs[i] = entry.FindPropertyRelative("prefab").objectReferenceValue;
                materials[i] = entry.FindPropertyRelative("materialOverride").objectReferenceValue;
            }
            void Configure(params int[] indices)
            {
                settings.Update();
                variants = settings.FindProperty("effectVariants");
                variants.arraySize = indices.Length;
                for (int i = 0; i < indices.Length; i++)
                {
                    var entry = variants.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative("prefab").objectReferenceValue =
                        indices[i] < 0 ? null : prefabs[indices[i]];
                    entry.FindPropertyRelative("materialOverride").objectReferenceValue =
                        indices[i] < 0 ? null : materials[indices[i]];
                }
                settings.ApplyModifiedPropertiesWithoutUndo();
            }
            void AssertVariants(params int[] indices)
            {
                Assert.That(manager.EmitterCount, Is.EqualTo(indices.Length));
                var emitters = manager.GetComponentsInChildren<ParticleSystem>();
                Assert.That(emitters.Length, Is.EqualTo(indices.Length));
                Assert.That(manager.transform.childCount, Is.EqualTo(indices.Length == 0 ? 0 : 1),
                    "Old emitter roots must be removed after a rebuild.");
                for (int i = 0; i < indices.Length; i++)
                    Assert.That(emitters[i].GetComponent<ParticleSystemRenderer>().sharedMaterial,
                        Is.SameAs(materials[indices[i]]));
            }

            Assert.That(combat.PauseRun(), Is.True);
            Configure(3, 2, 1, 0, 3, 1);
            yield return null;
            yield return null;
            AssertVariants(3, 2, 1, 0, 3, 1);
            foreach (var emitter in manager.GetComponentsInChildren<ParticleSystem>())
                Assert.That(emitter.isPaused, Is.True);

            Configure(3, 1, 2);
            yield return null;
            yield return null;
            AssertVariants(3, 1, 2);
            manager.enabled = false;
            Configure();
            manager.enabled = true;
            yield return null;
            yield return null;
            AssertVariants();
            Assert.That(combat.ResumeRun(), Is.True);
            combat.AdvanceTime(0.81f);
            yield return null;
            Assert.That(manager.EmittedCount, Is.Zero);

            Configure(2);
            yield return null;
            yield return null;
            AssertVariants(2);
            // Advance the target cooldown after Update has rebuilt the list. The edit itself
            // occurs between Update and LateUpdate, where this sandbox performs its attacks.
            combat.AdvanceTime(0.81f);
            yield return null;
            Assert.That(manager.EmittedCount, Is.EqualTo(8), "Restoring a list must subscribe exactly once.");
            Assert.That(manager.ActiveCount, Is.EqualTo(8));

            combat.PauseRun();
            Configure(-1, 1, -1, 3);
            yield return null;
            yield return null;
            AssertVariants(1, 3);
            combat.TryAbortRun(RunEndReason.UserExit, default, out _);
            Assert.That(manager.ActiveCount, Is.Zero);
            yield return SceneManager.UnloadSceneAsync(scene);
#else
            Assert.Ignore("Inspector edits require the Editor PlayMode runner.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator SandboxRendersAndRealLifecyclePausesClearsAndResubscribes()
        {
#if UNITY_EDITOR
            Scene scene = _sandboxScene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/CursorHunter/Combat/Scenes/CombatSandbox.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return new WaitForSeconds(0.15f);
            var sandbox = Object.FindFirstObjectByType<CursorHitEffectSandbox>();
            Assert.That(sandbox, Is.Not.Null);
            var combat = sandbox.GetComponent<CombatRunController>();
            var manager = sandbox.GetComponentInChildren<CursorHitEffectManager>();
            Assert.That(manager.EmitterCount, Is.EqualTo(4));
            Assert.That(manager.EmittedCount, Is.EqualTo(8));
            Assert.That(manager.ActiveCount, Is.EqualTo(8));
            Assert.That(combat.PauseRun(), Is.True);
            yield return null;
            var emitters = manager.GetComponentsInChildren<ParticleSystem>();
            var particles = new ParticleSystem.Particle[256];
            float remaining = 0f;
            foreach (var emitter in emitters)
            {
                Assert.That(emitter.isPaused, Is.True);
                if (emitter.GetParticles(particles) > 0) remaining += particles[0].remainingLifetime;
            }
            yield return new WaitForSecondsRealtime(0.1f);
            float afterPause = 0f;
            foreach (var emitter in emitters)
                if (emitter.GetParticles(particles) > 0) afterPause += particles[0].remainingLifetime;
            Assert.That(afterPause, Is.EqualTo(remaining).Within(0.001f));

            // Optional rendered evidence for local runs with a graphics device.
            string capturePath = System.Environment.GetEnvironmentVariable("CURSOR_HIT_CAPTURE_PATH");
            if (!string.IsNullOrEmpty(capturePath) &&
                SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Camera camera = null;
                foreach (var root in scene.GetRootGameObjects())
                    if (root.TryGetComponent(out Camera candidate)) camera = candidate;
                Assert.That(camera, Is.Not.Null);
                var target = new RenderTexture(1280, 720, 24);
                camera.targetTexture = target;
                yield return null;
                yield return null;
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;
                var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0f, 0f, 1280f, 720f), 0, 0);
                texture.Apply();
                System.IO.File.WriteAllBytes(capturePath, texture.EncodeToPNG());
                RenderTexture.active = previous;
                camera.targetTexture = null;
                target.Release();
                Object.Destroy(texture);
                Object.Destroy(target);
            }

            manager.enabled = false;
            Assert.That(manager.ActiveCount, Is.Zero);
            manager.enabled = true;
            combat.ResumeRun();
            yield return new WaitForSeconds(0.9f);
            Assert.That(manager.EmittedCount, Is.EqualTo(8), "Reenable must subscribe exactly once.");
            Assert.That(manager.ActiveCount, Is.GreaterThan(0));
            combat.TryAbortRun(RunEndReason.UserExit, default, out _);
            Assert.That(manager.ActiveCount, Is.Zero);
            yield return SceneManager.UnloadSceneAsync(scene);
#else
            Assert.Ignore("Sandbox asset loading requires the Editor PlayMode runner.");
            yield break;
#endif
        }
    }
}
