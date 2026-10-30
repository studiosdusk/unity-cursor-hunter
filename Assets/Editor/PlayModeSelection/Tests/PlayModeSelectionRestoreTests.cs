using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CursorHunter.EditorTools.Tests
{
    public sealed class PlayModeSelectionRestoreTests
    {
        private const string Folder = "Assets/__PlayModeSelectionTests";
        private const string ScenePath = Folder + "/Selection.unity";
        private const string OtherScenePath = Folder + "/Other.unity";
        private const string PreviousEnabledKey = "PlayModeSelectionTests.PreviousEnabled";
        private const string PreviousActiveSceneKey = "PlayModeSelectionTests.PreviousActiveScene";
        private const string OwnsFolderKey = "PlayModeSelectionTests.OwnsFolder";

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            SessionState.SetBool(OwnsFolderKey, false);
            Assert.That(AssetDatabase.IsValidFolder(Folder), Is.False, "Temporary test folder already exists.");
            Scene active = SceneManager.GetActiveScene();
            bool replaceEmptyScene = SceneManager.sceneCount == 1 && string.IsNullOrEmpty(active.path) && !active.isDirty;
            if (!replaceEmptyScene)
                for (int i = 0; i < SceneManager.sceneCount; i++)
                    if (string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path))
                        Assert.Ignore("Save untitled scenes before running these editor tests.");
            SessionState.SetBool(PreviousEnabledKey, PlayModeSelectionRestore.Enabled);
            SessionState.SetInt(PreviousActiveSceneKey, SceneManager.GetActiveScene().handle);
            PlayModeSelectionRestore.Enabled = true;
            PlayModeSelectionRestore.ClearPending();
            AssetDatabase.CreateFolder("Assets", "__PlayModeSelectionTests");
            SessionState.SetBool(OwnsFolderKey, true);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                replaceEmptyScene ? NewSceneMode.Single : NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            for (int i = 0; i < 2; i++)
            {
                GameObject root = new GameObject("Root");
                Child(root, "Same/Name[0]");
                GameObject branch = Child(root, "Same/Name[0]");
                Child(branch, "Leaf").SetActive(false);
            }
            EditorSceneManager.SaveScene(scene, ScenePath);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (!SessionState.GetBool(OwnsFolderKey, false))
                yield break;
            if (EditorApplication.isPlaying)
                yield return new ExitPlayMode();
            PlayModeSelectionRestore.ClearPending();
            PlayModeSelectionRestore.Enabled = SessionState.GetBool(PreviousEnabledKey, true);
            bool onlyTestScenes = true;
            for (int i = 0; i < SceneManager.sceneCount; i++)
                onlyTestScenes &= SceneManager.GetSceneAt(i).path.StartsWith(Folder + "/", System.StringComparison.Ordinal);
            if (onlyTestScenes)
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.path.StartsWith(Folder + "/", System.StringComparison.Ordinal))
                    EditorSceneManager.CloseScene(scene, true);
            }
            int previousHandle = SessionState.GetInt(PreviousActiveSceneKey, 0);
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene previous = SceneManager.GetSceneAt(i);
                if (previous.handle == previousHandle && previous.isLoaded)
                    SceneManager.SetActiveScene(previous);
            }
            Assert.That(AssetDatabase.DeleteAsset(Folder), Is.True);
            SessionState.EraseBool(OwnsFolderKey);
            yield return null;
        }

        [Test]
        public void JsonRoundTripDistinguishesDuplicateRootsChildrenAndSlashNames()
        {
            GameObject leaf = Leaf();
            SelectionPath path = RoundTrip(SelectionPath.Capture(leaf));
            Assert.That(path.hierarchy[0].sameNameIndex, Is.EqualTo(1));
            Assert.That(path.hierarchy[1].sameNameIndex, Is.EqualTo(1));
            Assert.That(path.Resolve(), Is.EqualTo(leaf));
            Assert.That(leaf.activeInHierarchy, Is.False);
        }

        [Test]
        public void MissingDescendantsResolveToNearestSurvivingParent()
        {
            GameObject parent = Leaf();
            GameObject runtime = Child(Child(parent, "Runtime"), "Nested runtime");
            SelectionPath path = RoundTrip(SelectionPath.Capture(runtime));
            Object.DestroyImmediate(runtime.transform.parent.gameObject);
            Assert.That(path.Resolve(), Is.EqualTo(parent));
        }

        [Test]
        public void MissingRootDoesNotMatchAnotherRootWithTheSameName()
        {
            SelectionPath path = SelectionPath.Capture(Leaf());
            Object.DestroyImmediate(Root());
            Assert.That(path.Resolve(), Is.Null);
        }

        [Test]
        public void ReorderedSameNameSiblingsStillResolveToTheSameObject()
        {
            GameObject leaf = Leaf();
            SelectionPath path = SelectionPath.Capture(leaf);
            leaf.transform.parent.SetAsFirstSibling();
            leaf.transform.root.SetAsFirstSibling();
            Assert.That(path.Resolve(), Is.EqualTo(leaf));
        }

        [Test]
        public void ClosedSceneIsNotOpenedAndDoesNotMatchIdenticalHierarchy()
        {
            SelectionPath path = SelectionPath.Capture(Leaf());
            Scene original = SceneManager.GetSceneByPath(ScenePath);
            EditorSceneManager.SaveScene(original, OtherScenePath, true);
            EditorSceneManager.OpenScene(OtherScenePath, OpenSceneMode.Additive);
            EditorSceneManager.CloseScene(original, true);
            int count = SceneManager.sceneCount;
            Assert.That(path.Resolve(), Is.Null);
            Assert.That(SceneManager.sceneCount, Is.EqualTo(count));
            Assert.That(SceneManager.GetSceneByPath(ScenePath).isLoaded, Is.False);
        }

        [Test]
        public void UnsavedSceneAndAssetsAreIgnored()
        {
            Scene unsaved = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                var target = new GameObject("Unsaved");
                SceneManager.MoveGameObjectToScene(target, unsaved);
                Assert.That(SelectionPath.Capture(target), Is.Null);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(Root(), Folder + "/Test.prefab");
                Assert.That(SelectionPath.Capture(prefab), Is.Null);
                Assert.That(SelectionPath.Capture(null), Is.Null);
            }
            finally
            {
                EditorSceneManager.CloseScene(unsaved, true);
            }
        }

        [UnityTest]
        public IEnumerator RestoresLastPlaySelectionAcrossDomainReload()
        {
            Selection.activeGameObject = Root();
            yield return new EnterPlayMode();
            Selection.activeGameObject = Leaf();
            yield return new ExitPlayMode();
            yield return WaitForRestore();
            Assert.That(Selection.activeGameObject, Is.EqualTo(Leaf()));
            Assert.That(SceneManager.GetSceneByPath(ScenePath).isDirty, Is.False);
        }

        [UnityTest]
        public IEnumerator RuntimeDescendantsFallBackToTheirNearestEditParent()
        {
            yield return new EnterPlayMode();
            Selection.activeGameObject = Child(Child(Leaf(), "Runtime"), "Nested runtime");
            yield return new ExitPlayMode();
            yield return WaitForRestore();
            Assert.That(Selection.activeGameObject, Is.EqualTo(Leaf()));
        }

        [UnityTest]
        public IEnumerator RuntimeReplacementAtIdenticalPathDoesNotSelectAuthoredObject()
        {
            yield return new EnterPlayMode();
            GameObject branch = Leaf().transform.parent.gameObject;
            Object.DestroyImmediate(branch.transform.GetChild(0).gameObject);
            Selection.activeGameObject = Child(branch, "Leaf");
            yield return new ExitPlayMode();
            yield return WaitForRestore();
            Assert.That(Selection.activeGameObject, Is.EqualTo(Leaf().transform.parent.gameObject));
        }

        [UnityTest]
        public IEnumerator RuntimeSceneReloadUsesScenePathInsteadOfInstanceId()
        {
            yield return new EnterPlayMode();
            // EditorSceneManager's play-only loader avoids changing Build Settings.
            EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            Selection.activeGameObject = Leaf();
            yield return new ExitPlayMode();
            yield return WaitForRestore();
            Assert.That(Selection.activeGameObject, Is.EqualTo(Leaf()));
        }

        [UnityTest]
        public IEnumerator AuthoredPrefabChildIsRestoredAfterPlay()
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(Root(), Folder + "/Authored.prefab");
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = "Prefab instance";
            EditorSceneManager.SaveScene(SceneManager.GetSceneByPath(ScenePath));
            yield return new EnterPlayMode();
            Selection.activeGameObject = PrefabLeaf();
            yield return new ExitPlayMode();
            yield return WaitForRestore();
            Assert.That(Selection.activeGameObject, Is.EqualTo(PrefabLeaf()));
        }

        [UnityTest]
        public IEnumerator RuntimeOnlySceneDoesNotOpenInEditModeOrReplaceSelection()
        {
            Scene original = SceneManager.GetSceneByPath(ScenePath);
            EditorSceneManager.SaveScene(original, OtherScenePath, true);
            Selection.activeGameObject = Root();
            SessionState.SetInt("PlayModeSelectionTests.SceneCount", SceneManager.sceneCount);
            yield return new EnterPlayMode();
            EditorSceneManager.LoadSceneInPlayMode(OtherScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var runtimeSelection = new GameObject("Runtime-only selection");
            SceneManager.MoveGameObjectToScene(
                runtimeSelection, SceneManager.GetSceneByPath(OtherScenePath));
            Selection.activeGameObject = runtimeSelection;
            yield return new ExitPlayMode();
            Object selectionAfterUnityRestore = Selection.activeObject;
            yield return WaitForRestore();
            Assert.That(Selection.activeObject, Is.EqualTo(selectionAfterUnityRestore));
            Assert.That(SceneManager.sceneCount, Is.EqualTo(SessionState.GetInt("PlayModeSelectionTests.SceneCount", -1)));
            Assert.That(SceneManager.GetSceneByPath(OtherScenePath).isLoaded, Is.False);
        }

        [UnityTest]
        public IEnumerator RuntimeOnlyRootPreservesExistingSelection()
        {
            Selection.activeGameObject = Root();
            yield return new EnterPlayMode();
            Selection.activeGameObject = new GameObject("Runtime only root");
            yield return new ExitPlayMode();
            Object selectionAfterUnityRestore = Selection.activeObject;
            yield return WaitForRestore();
            Assert.That(Selection.activeObject, Is.EqualTo(selectionAfterUnityRestore));
        }

        [UnityTest]
        public IEnumerator DisabledFeaturePreservesUnitySelection()
        {
            Assert.That(EditorApplication.ExecuteMenuItem(PlayModeSelectionRestore.MenuPath), Is.True);
            Assert.That(PlayModeSelectionRestore.Enabled, Is.False);
            Selection.activeGameObject = Root();
            yield return new EnterPlayMode();
            Selection.activeGameObject = Leaf();
            yield return new ExitPlayMode();
            Object selectionAfterUnityRestore = Selection.activeObject;
            yield return WaitForRestore();
            Assert.That(Selection.activeObject, Is.EqualTo(selectionAfterUnityRestore));
        }

        [UnityTest]
        public IEnumerator HierarchyFramingExpandsParentsAndScrollsToSelection()
        {
            if (Application.isBatchMode)
                Assert.Ignore("Requires an editor window; run this test without -batchmode.");

            GameObject target = Leaf();
            for (int i = 0; i < 150; i++)
                new GameObject("Before selection " + i);
            target.transform.root.SetAsLastSibling();
            Assert.That(EditorApplication.ExecuteMenuItem("Window/General/Hierarchy"), Is.True);
            EditorWindow window = EditorWindow.focusedWindow;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            object hierarchy = window.GetType().GetProperty("sceneHierarchy", flags).GetValue(window);
            hierarchy.GetType().GetMethod("CollapseAll", flags).Invoke(hierarchy, null);
            Selection.activeObject = null;
            yield return WaitForRestore();

            Selection.activeGameObject = target;
            PlayModeSelectionRestore.RevealInHierarchy(target);
            for (int i = 0; i < 20; i++)
                yield return null;

            var expanded = (IEnumerable)window.GetType().GetMethod("GetExpandedIDs", flags).Invoke(window, null);
            CollectionAssert.Contains(expanded, target.transform.parent.gameObject.GetEntityId());
            CollectionAssert.Contains(expanded, target.transform.root.gameObject.GetEntityId());
            object tree = hierarchy.GetType().GetProperty("treeView", flags).GetValue(hierarchy);
            object state = tree.GetType().GetProperty("state", flags).GetValue(tree);
            Vector2 scroll = (Vector2)state.GetType().GetField("scrollPos", flags).GetValue(state);
            Assert.That(scroll.y, Is.GreaterThan(0), "Selection below 150 roots must scroll into view.");
        }

        private static IEnumerator WaitForRestore()
        {
            for (int i = 0; i < 5; i++)
                yield return null;
        }

        private static SelectionPath RoundTrip(SelectionPath path) =>
            JsonUtility.FromJson<SelectionPath>(JsonUtility.ToJson(path));

        private static GameObject Root() =>
            SceneManager.GetSceneByPath(ScenePath).GetRootGameObjects()[1];

        private static GameObject Leaf() => Root().transform.GetChild(1).GetChild(0).gameObject;

        private static GameObject PrefabLeaf()
        {
            foreach (GameObject root in SceneManager.GetSceneByPath(ScenePath).GetRootGameObjects())
                if (root.name == "Prefab instance")
                    return root.transform.GetChild(1).GetChild(0).gameObject;
            return null;
        }

        private static GameObject Child(GameObject parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent.transform, false);
            return child;
        }
    }
}
