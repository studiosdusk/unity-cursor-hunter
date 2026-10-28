using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[assembly: InternalsVisibleTo("CursorHunter.PlayModeSelection.Editor.Tests")]

namespace CursorHunter.EditorTools
{
    /// <summary>Restores the active scene GameObject selected immediately before Play Mode ends.</summary>
    [InitializeOnLoad]
    internal static class PlayModeSelectionRestore
    {
        internal const string MenuPath = "Tools/Restore Selection After Play Mode";
        private const string PendingKey = "CursorHunter.PlayModeSelection.Pending.v1";
        private static string PreferenceKey => "CursorHunter.PlayModeSelection.Enabled." + Application.dataPath;

        internal static bool Enabled
        {
            get => EditorPrefs.GetBool(PreferenceKey, true);
            set
            {
                EditorPrefs.SetBool(PreferenceKey, value);
                if (!value)
                    ClearPending();
            }
        }

        static PlayModeSelectionRestore()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            // Also resumes a pending restoration if scripts reload during the transition.
            ScheduleRestore();
        }

        [MenuItem(MenuPath)]
        private static void ToggleEnabled()
        {
            Enabled = !Enabled;
            Menu.SetChecked(MenuPath, Enabled);
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateMenu()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.ExitingEditMode:
                    ClearPending();
                    break;
                case PlayModeStateChange.ExitingPlayMode:
                    ClearPending();
                    if (Enabled)
                    {
                        SelectionPath path = SelectionPath.Capture(Selection.activeGameObject);
                        if (path != null)
                            SessionState.SetString(PendingKey, JsonUtility.ToJson(path));
                    }
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    ScheduleRestore();
                    break;
            }
        }

        private static void ScheduleRestore()
        {
            // Unlike delayCall, update also runs when no Inspector is repainting
            // (for example in a headless editor or during editor tests).
            EditorApplication.update -= RestorePending;
            EditorApplication.update += RestorePending;
        }

        internal static void ClearPending()
        {
            SessionState.EraseString(PendingKey);
            EditorApplication.update -= RestorePending;
        }

        private static void RestorePending()
        {
            EditorApplication.update -= RestorePending;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return; // EnteredEditMode will schedule us again.

            if (!Enabled)
            {
                ClearPending();
                return;
            }

            string json = SessionState.GetString(PendingKey, string.Empty);
            if (string.IsNullOrEmpty(json))
                return;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                ScheduleRestore();
                return;
            }

            ClearPending();
            SelectionPath path;
            try
            {
                path = JsonUtility.FromJson<SelectionPath>(json);
            }
            catch (ArgumentException)
            {
                return;
            }

            GameObject target = path?.Resolve();
            if (target == null)
                return; // Never clear Unity's existing selection on a failed lookup.

            Selection.activeGameObject = target;
            // Let Unity finish restoring its window layout and hierarchy tree first.
            EditorApplication.delayCall += () => RevealInHierarchy(target);
        }

        internal static void RevealInHierarchy(GameObject target)
        {
            if (Application.isBatchMode || !Enabled || target == null ||
                EditorApplication.isPlayingOrWillChangePlaymode || Selection.activeGameObject != target)
                return;

            // The Hierarchy's own FrameSelected command clears its search, expands the
            // ancestor chain and scrolls to the selection. No Scene View framing or
            // private reflection API is needed, including for Unity's new Hierarchy.
            if (EditorApplication.ExecuteMenuItem("Window/General/Hierarchy"))
            {
                EditorWindow hierarchy = EditorWindow.focusedWindow;
                if (hierarchy != null)
                {
                    hierarchy.SendEvent(EditorGUIUtility.CommandEvent("FrameSelected"));
                    hierarchy.Repaint();
                }
            }
        }
    }

    [Serializable]
    internal sealed class SelectionPath
    {
        public string scenePath;
        public List<HierarchySegment> hierarchy = new List<HierarchySegment>();

        internal static SelectionPath Capture(GameObject selected)
        {
            if (selected == null || EditorUtility.IsPersistent(selected))
                return null;

            Scene scene = selected.scene;
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(scene.path) ||
                EditorSceneManager.IsPreviewScene(scene))
                return null;

            var result = new SelectionPath { scenePath = scene.path };
            for (Transform current = selected.transform; current != null; current = current.parent)
            {
                result.hierarchy.Add(new HierarchySegment
                {
                    name = current.name,
                    sameNameIndex = GetSameNameIndex(current),
                    objectId = GlobalObjectId.GetGlobalObjectIdSlow(current.gameObject).ToString()
                });
            }
            result.hierarchy.Reverse();
            return result;
        }

        internal GameObject Resolve()
        {
            if (string.IsNullOrEmpty(scenePath) || hierarchy == null || hierarchy.Count == 0)
                return null;

            // Only inspect scenes Unity has already restored. Never load a scene or asset.
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            if (!scene.IsValid() || !scene.isLoaded || EditorSceneManager.IsPreviewScene(scene))
                return null;

            Transform nearest = null;
            foreach (HierarchySegment segment in hierarchy)
            {
                Transform match = FindChild(scene, nearest, segment);
                if (match == null)
                    break;
                nearest = match;
            }
            return nearest != null ? nearest.gameObject : null;
        }

        private static int GetSameNameIndex(Transform target)
        {
            int index = 0;
            if (target.parent != null)
            {
                for (int i = 0; i < target.GetSiblingIndex(); i++)
                    if (target.parent.GetChild(i).name == target.name)
                        index++;
            }
            else
            {
                foreach (GameObject root in target.gameObject.scene.GetRootGameObjects())
                {
                    if (root.transform == target)
                        break;
                    if (root.name == target.name)
                        index++;
                }
            }
            return index;
        }

        private static Transform FindChild(Scene scene, Transform parent, HierarchySegment segment)
        {
            if (segment == null || segment.sameNameIndex < 0 ||
                !GlobalObjectId.TryParse(segment.objectId, out GlobalObjectId expected) ||
                expected.identifierType != 2 || expected.targetObjectId == 0)
                return null;

            GameObject[] roots = parent == null ? scene.GetRootGameObjects() : null;
            int count = parent != null ? parent.childCount : roots.Length;
            int sameNameIndex = 0;
            Transform identityMatch = null;
            for (int i = 0; i < count; i++)
            {
                Transform child = parent != null ? parent.GetChild(i) : roots[i].transform;
                if (!string.Equals(child.name, segment.name, StringComparison.Ordinal))
                    continue;

                // A runtime clone can occupy exactly the same name/index as an authored
                // object. Verify scene identity so that clone falls back to its parent.
                bool sameObject = GlobalObjectId.GetGlobalObjectIdSlow(child.gameObject).Equals(expected);
                if (sameNameIndex == segment.sameNameIndex && sameObject)
                    return child;
                if (sameObject)
                    identityMatch = child;
                sameNameIndex++;
            }

            // Runtime insertion/deletion/reordering of same-name siblings must not make
            // us select their neighbour when Unity restores the original sibling order.
            return identityMatch;
        }
    }

    [Serializable]
    internal sealed class HierarchySegment
    {
        public string name;
        public int sameNameIndex;
        public string objectId;
    }
}
