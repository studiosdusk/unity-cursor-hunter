using System.Linq;
using CursorHunter.App;
using CursorHunter.Combat;
using CursorHunter.Data;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CursorHunter.App.Editor
{
    /// <summary>Explicit authoring command; never changes the scene during import or play.</summary>
    public static class GemstoneCollectionSceneSetup
    {
        private const string PrefabPath = "Assets/CursorHunter/App/Prefabs/GemstoneFlyView.prefab";
        [MenuItem("Tools/Cursor Hunter/Setup Gemstone Collection in Main")]
        public static void ConfigureMain()
        {
            var scene = EditorSceneManager.OpenScene("Assets/CursorHunter/App/Scenes/Main.unity");
            CombatGemstoneList list = Object.FindFirstObjectByType<CombatGemstoneList>(FindObjectsInactive.Include);
            Canvas hud = list.GetComponentInParent<Canvas>();
            var controller = Object.FindFirstObjectByType<GemstoneCollectionController>(FindObjectsInactive.Include);
            if (controller == null) controller = new GameObject("GemstoneCollectionController").AddComponent<GemstoneCollectionController>();
            var effect = hud.transform.Find("GemstoneFlightCanvas") as RectTransform;
            if (effect == null)
            {
                var go = new GameObject("GemstoneFlightCanvas", typeof(RectTransform), typeof(Canvas));
                effect = go.GetComponent<RectTransform>(); effect.SetParent(hud.transform, false);
                effect.anchorMin = Vector2.zero; effect.anchorMax = Vector2.one;
                effect.offsetMin = effect.offsetMax = Vector2.zero;
            }
            Canvas effects = effect.GetComponent<Canvas>();
            effects.overrideSorting = true; effects.sortingLayerID = hud.sortingLayerID; effects.sortingOrder = hud.sortingOrder + 10;
            GemstoneFlyView prefab = AssetDatabase.LoadAssetAtPath<GemstoneFlyView>(PrefabPath);
            if (prefab == null)
            {
                var root = new GameObject("GemstoneFlyView", typeof(RectTransform), typeof(GemstoneFlyView));
                root.layer = 5;
                var serializedView = new SerializedObject(root.GetComponent<GemstoneFlyView>());
                var trails = serializedView.FindProperty("trails"); trails.arraySize = 2;
                for (int i = 0; i < 2; i++) trails.GetArrayElementAtIndex(i).objectReferenceValue = Image("Trail" + i, root.transform);
                serializedView.FindProperty("body").objectReferenceValue = Image("Gem", root.transform);
                serializedView.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath); Object.DestroyImmediate(root);
                prefab = AssetDatabase.LoadAssetAtPath<GemstoneFlyView>(PrefabPath);
            }
            var so = new SerializedObject(controller);
            so.FindProperty("combat").objectReferenceValue = Object.FindFirstObjectByType<CombatRunController>();
            so.FindProperty("worldCamera").objectReferenceValue = Camera.main;
            so.FindProperty("hudCanvas").objectReferenceValue = hud;
            so.FindProperty("uiRootController").objectReferenceValue = Object.FindFirstObjectByType<AppUiRootController>();
            so.FindProperty("effectRoot").objectReferenceValue = effect;
            so.FindProperty("flyPrefab").objectReferenceValue = prefab;
            so.FindProperty("iconCatalog").objectReferenceValue = GemstoneIconCatalog.LoadDefault();
            var entries = so.FindProperty("bindings");
            var icons = list.GetComponentsInChildren<GemstoneIconView>(true);
            entries.arraySize = icons.Length;
            for (int i = 0; i < icons.Length; i++)
            {
                Transform row = icons[i].transform.parent;
                TMP_Text text = row.GetComponentInChildren<TMP_Text>(true);
                RectTransform target = row.Find("TargetPoint") as RectTransform;
                Vector4 margin = text.margin;
                RectTransform rect = text.rectTransform;
                // Move margins into a fixed text region so the TMP center and absorption center coincide.
                rect.offsetMin += new Vector2(margin.x, margin.w);
                rect.offsetMax -= new Vector2(margin.z, margin.y);
                rect.pivot = new Vector2(0.5f, 0.5f); text.margin = Vector4.zero;
                text.alignment = TextAlignmentOptions.Center;
                target.anchorMin = target.anchorMax = new Vector2(0.5f, 0.5f);
                target.anchoredPosition = rect.anchoredPosition;
                target.sizeDelta = Vector2.zero;
                var foreground = text.GetComponent<Canvas>();
                if (foreground == null) foreground = text.gameObject.AddComponent<Canvas>();
                foreground.overrideSorting = true; foreground.sortingLayerID = hud.sortingLayerID; foreground.sortingOrder = hud.sortingOrder + 11;
                var entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("currencyId").stringValue = icons[i].GemstoneId;
                entry.FindPropertyRelative("row").objectReferenceValue = row.gameObject;
                entry.FindPropertyRelative("amountText").objectReferenceValue = text;
                entry.FindPropertyRelative("targetPoint").objectReferenceValue = target;
                entry.FindPropertyRelative("numberColor").colorValue = text.color;
            }
            so.FindProperty("settings.sound.commonClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/CursorHunter/App/Audio/GemCollect.wav");
            so.FindProperty("settings.sound.bonusClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/CursorHunter/App/Audio/GemCollectBonus.wav");
            so.ApplyModifiedPropertiesWithoutUndo();
            var listSo = new SerializedObject(list);
            listSo.FindProperty("collectionController").objectReferenceValue = controller;
            listSo.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Gemstone collection scene authored: " + icons.Length + " text targets.");
        }
        private static Image Image(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = 5; go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>(); image.raycastTarget = false; image.preserveAspect = true;
            return image;
        }
    }
}
