using System.Linq;
using CursorHunter.Data;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CursorHunter.App.Editor
{
    /// <summary>Explicit scene authoring command. Vendor assets are referenced, never edited.</summary>
    public static class BattleResultSceneSetup
    {
        private const string Art = "Assets/DownLoadAssets/GUI-CasualFantasy/ResourcesData/Sprites/Components/";
        private static readonly Color Ink = new Color32(24, 25, 23, 247);
        private static readonly Color Muted = new Color32(174, 176, 166, 255);
        private static TMP_FontAsset _font;
        private static Sprite _solid;

        [MenuItem("Tools/Cursor Hunter/Setup Battle Result in Main")]
        public static void ConfigureMain()
        {
            var scene = EditorSceneManager.OpenScene("Assets/CursorHunter/App/Scenes/Main.unity");
            var hud = Object.FindFirstObjectByType<PrototypeRunHud>(FindObjectsInactive.Include);
            var presenter = hud.GetComponent<BattleResultPresentation>();
            bool existing = presenter != null;
            if (!existing) presenter = hud.gameObject.AddComponent<BattleResultPresentation>();
            var so = new SerializedObject(presenter);
            var all = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<RectTransform>(true)).ToArray();
            RectTransform result = all.Single(x => x.name == "ResultUI");
            RectTransform timeout = all.Single(x => x.name == "TimeOutPanel");
            Transform parent = existing ? ((GameObject)so.FindProperty("overlay").objectReferenceValue).transform.parent : result.parent;
            Canvas mainCanvas = parent.GetComponentInParent<Canvas>();
            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/DownLoadAssets/Casual_GUI/GUI Pro-SimpleCasual/ResourcesData/Fonts/Rubik-SemiBold SDF.asset");
            _solid = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "Button/Button_SquareSolid03_White.png");
            RectTransform overlay = existing
                ? (RectTransform)((GameObject)so.FindProperty("overlay").objectReferenceValue).transform
                : Rect("BattleEndOverlay", parent, Vector2.zero, Vector2.zero);
            Stretch(overlay);
            Canvas canvas = Ensure<Canvas>(overlay.gameObject);
            canvas.overrideSorting = true; canvas.sortingLayerID = mainCanvas.sortingLayerID;
            canvas.sortingOrder = mainCanvas.sortingOrder + 60;
            Ensure<GraphicRaycaster>(overlay.gameObject);
            Image dim = existing ? overlay.Find("Backdrop").GetComponent<Image>()
                : Image("Backdrop", overlay, Vector2.zero, Vector2.zero, null, Color.black);
            dim.color = new Color(0, 0, 0, 0.76f); Stretch(dim.rectTransform); dim.raycastTarget = true;
            CanvasGroup dimGroup = Ensure<CanvasGroup>(dim.gameObject);
            Button skip = Ensure<Button>(dim.gameObject); skip.transition = Selectable.Transition.None;
            skip.navigation = new Navigation { mode = Navigation.Mode.None };
            RectTransform layout = existing ? (RectTransform)overlay.Find("ResponsiveLayout")
                : Rect("ResponsiveLayout", overlay, Vector2.zero, new Vector2(1040, 646));
            Place(layout, Vector2.zero, new Vector2(1040, 646));

            timeout.SetParent(layout, false); Stretch(timeout);
            var popup = (RectTransform)timeout.Find("Popup");
            Place(popup, Vector2.zero, new Vector2(720, 170));
            foreach (var behaviour in timeout.GetComponentsInChildren<MonoBehaviour>(true))
                if (!(behaviour is TMP_Text) && !(behaviour is Graphic)) behaviour.enabled = false;
            Image popupImage = popup.GetComponent<Image>();
            foreach (var image in timeout.GetComponentsInChildren<Image>(true))
            {
                image.raycastTarget = false;
                if (image != popupImage) image.enabled = false;
            }
            TMP_Text timeLabel = existing ? (TMP_Text)so.FindProperty("timeOutTitle").objectReferenceValue
                : timeout.GetComponentsInChildren<TMP_Text>(true).First(x => x.text.Contains("Time Out"));
            foreach (TMP_Text label in timeout.GetComponentsInChildren<TMP_Text>(true))
                if (label != timeLabel) label.gameObject.SetActive(false);
            timeLabel.font = _font; timeLabel.text = "TIME OUT!"; timeLabel.fontSize = 70;
            timeLabel.enableAutoSizing = false; timeLabel.raycastTarget = false; timeLabel.color = Color.white;
            popupImage.sprite = _solid; popupImage.type = UnityEngine.UI.Image.Type.Sliced;
            popupImage.pixelsPerUnitMultiplier = 8f; popupImage.color = Ink;
            Edge(popupImage.gameObject);
            CanvasGroup timeoutGroup = Ensure<CanvasGroup>(timeout.gameObject);

            result.SetParent(layout, false);
            Place(result, new Vector2(0, 55), new Vector2(1040, 496));
            // This subtree is the result view authored by this command; retain the user's ResultUI root.
            for (int i = result.childCount - 1; i >= 0; i--) Object.DestroyImmediate(result.GetChild(i).gameObject);
            Image resultImage = result.GetComponent<Image>(); resultImage.sprite = _solid;
            resultImage.type = UnityEngine.UI.Image.Type.Sliced; resultImage.pixelsPerUnitMultiplier = 8f;
            resultImage.color = Ink; resultImage.raycastTarget = true;
            Edge(result.gameObject);
            CanvasGroup resultGroup = Ensure<CanvasGroup>(result.gameObject);
            Button resultSkip = Ensure<Button>(result.gameObject); resultSkip.transition = Selectable.Transition.None;
            resultSkip.navigation = new Navigation { mode = Navigation.Mode.None };
            TMP_Text title = Text("Title", result, "전투 종료", new Vector2(0, -45), new Vector2(970, 60), 44, Color.white);
            Top(title.rectTransform);
            TMP_Text subtitle = Text("Subtitle", result, "테스트 전투 / 보상 지급 없음", new Vector2(0, -79), new Vector2(940, 26), 18, Muted);
            Top(subtitle.rectTransform); subtitle.gameObject.SetActive(false);
            Top(Text("RewardHeading", result, "획득 보상", new Vector2(-315, -109), new Vector2(340, 34), 24, Muted, TextAlignmentOptions.Left).rectTransform);
            Top(Text("StatsHeading", result, "전투 기록", new Vector2(335, -109), new Vector2(280, 34), 24, Muted, TextAlignmentOptions.Left).rectTransform);
            RectTransform rowRoot = Rect("RewardRows", result, new Vector2(-180, -137), new Vector2(620, 336));
            Top(rowRoot); rowRoot.pivot = new Vector2(0.5f, 1f);
            var rowLayout = rowRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            rowLayout.spacing = 10; rowLayout.childAlignment = TextAnchor.UpperLeft;
            rowLayout.childControlWidth = rowLayout.childControlHeight = false;
            rowLayout.childForceExpandWidth = rowLayout.childForceExpandHeight = false;
            var rows = so.FindProperty("rewards"); rows.arraySize = 6;
            string[] ids = { "gem.garnet", "gem.topaz", "gem.amethyst", "gem.sapphire", "gem.diamond", "gem.dragon" };
            string[] names = { "가넷", "토파즈", "자수정", "사파이어", "다이아몬드", "드래곤 젬" };
            Color32[] colors = { new Color32(245, 109, 116, 255), new Color32(238, 207, 95, 255), new Color32(200, 135, 235, 255), new Color32(95, 172, 231, 255), new Color32(119, 212, 158, 255), new Color32(205, 207, 217, 255) };
            for (int i = 0; i < ids.Length; i++)
            {
                RectTransform row = Rect("Reward_" + ids[i].Substring(4), rowRoot, Vector2.zero, new Vector2(620, 46));
                CanvasGroup group = row.gameObject.AddComponent<CanvasGroup>();
                Image icon = Image("GemIcon", row, new Vector2(-280, 0), new Vector2(42, 42), GemstoneIconCatalog.LoadDefault().GetIcon(ids[i]), Color.white);
                icon.preserveAspect = true;
                Text("Name", row, names[i], new Vector2(-133, 0), new Vector2(210, 46), 26, Color.white, TextAlignmentOptions.Left);
                TMP_Text amount = Text("Amount", row, "+0", new Vector2(145, 0), new Vector2(290, 46), 30, colors[i], TextAlignmentOptions.Right);
                amount.enableAutoSizing = true; amount.fontSizeMin = 15; amount.fontSizeMax = 30;
                var entry = rows.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("currencyId").stringValue = ids[i];
                entry.FindPropertyRelative("group").objectReferenceValue = group;
                entry.FindPropertyRelative("icon").objectReferenceValue = icon.rectTransform;
                entry.FindPropertyRelative("amount").objectReferenceValue = amount;
                row.gameObject.SetActive(false);
            }
            TMP_Text empty = Text("EmptyRewards", result, "획득한 보상이 없습니다.", new Vector2(-180, -168), new Vector2(620, 46), 24, Muted);
            Top(empty.rectTransform); empty.gameObject.SetActive(false);
            TMP_Text kills = Stat(result, "Kills", "처치 수", 164);
            TMP_Text duration = Stat(result, "Duration", "전투 시간", 229);
            TMP_Text damage = Stat(result, "Damage", "총 피해량", 294);
            RectTransform actions = Rect("Actions", result, new Vector2(0, -58), new Vector2(1040, 78));
            Bottom(actions);
            CanvasGroup actionGroup = actions.gameObject.AddComponent<CanvasGroup>();
            Button upgrade = Action(actions, "UpgradeButton", "업그레이드 (로비)", -268);
            Button retry = Action(actions, "RetryButton", "다시 전투하기", 268);
            TMP_Text hint = Text("Hint", result, "클릭하면 바로 보기", new Vector2(0, -122), new Vector2(1000, 28), 18, Muted);
            Bottom(hint.rectTransform);
            Bind(so, "overlay", overlay.gameObject); Bind(so, "layout", layout); Bind(so, "backdrop", dimGroup);
            Bind(so, "timeOutPanel", timeout.gameObject); Bind(so, "timeOutPopup", popup); Bind(so, "timeOutGroup", timeoutGroup); Bind(so, "timeOutTitle", timeLabel);
            Bind(so, "resultUI", resultGroup); Bind(so, "title", title); Bind(so, "subtitle", subtitle);
            Bind(so, "kills", kills); Bind(so, "duration", duration); Bind(so, "damage", damage); Bind(so, "hint", hint);
            Bind(so, "emptyRewards", empty); Bind(so, "rewardRows", rowRoot);
            Bind(so, "actions", actionGroup); Bind(so, "upgradeButton", upgrade); Bind(so, "retryButton", retry); Bind(so, "skipButton", skip);
            Bind(so, "resultSkipButton", resultSkip);
            Bind(so, "huntManager", Object.FindFirstObjectByType<HuntManager>(FindObjectsInactive.Include));
            Bind(so, "uiRoot", Object.FindFirstObjectByType<AppUiRootController>(FindObjectsInactive.Include));
            Bind(so, "collection", Object.FindFirstObjectByType<CombatGemstoneList>(FindObjectsInactive.Include));
            so.FindProperty("popupSeconds").floatValue = 0.15f;
            so.FindProperty("holdSeconds").floatValue = 0.65f;
            so.FindProperty("dismissSeconds").floatValue = 0.05f;
            so.FindProperty("countSeconds").floatValue = 0.3f;
            so.FindProperty("buttonPopupSeconds").floatValue = 0.22f;
            so.ApplyModifiedPropertiesWithoutUndo();
            var hudSO = new SerializedObject(hud); Bind(hudSO, "battleResult", presenter); hudSO.ApplyModifiedPropertiesWithoutUndo();
            timeout.gameObject.SetActive(false); result.gameObject.SetActive(false); overlay.gameObject.SetActive(false);
            foreach (Component component in timeout.GetComponentsInChildren<Component>(true))
                if (component != null && PrefabUtility.IsPartOfPrefabInstance(component))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log("Battle result authored: plain panels, short count-up, earned gemstones only.");
        }

        private static TMP_Text Stat(Transform parent, string name, string label, float y)
        {
            Top(Text(name + "Label", parent, label, new Vector2(254, -y), new Vector2(118, 34), 21, Muted, TextAlignmentOptions.Left).rectTransform);
            TMP_Text value = Text(name, parent, "0", new Vector2(389, -y), new Vector2(182, 42), 26, Color.white, TextAlignmentOptions.Right);
            Top(value.rectTransform); value.enableAutoSizing = true; value.fontSizeMin = 12; value.fontSizeMax = 26;
            return value;
        }
        private static Button Action(Transform parent, string name, string label, float x)
        {
            Image face = Image(name, parent, new Vector2(x, 0), new Vector2(502, 78), _solid, new Color32(40, 42, 37, 255));
            face.type = UnityEngine.UI.Image.Type.Sliced; face.pixelsPerUnitMultiplier = 8f;
            face.raycastTarget = true; Edge(face.gameObject);
            Button button = face.gameObject.AddComponent<Button>(); button.targetGraphic = face;
            ColorBlock colors = button.colors; colors.highlightedColor = new Color(1.35f, 1.35f, 1.35f); colors.pressedColor = new Color(0.6f, 0.6f, 0.6f); colors.fadeDuration = 0f; button.colors = colors;
            Text("Label", face.transform, label, Vector2.zero, new Vector2(470, 66), 30, Color.white);
            return button;
        }
        private static void Edge(GameObject go)
        {
            var edge = Ensure<Outline>(go); edge.enabled = true;
            edge.effectColor = new Color32(115, 117, 106, 255); edge.effectDistance = new Vector2(2, -2); edge.useGraphicAlpha = false;
        }
        private static TMP_Text Text(string name, Transform parent, string text, Vector2 position, Vector2 size, float fontSize, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            RectTransform rect = Rect(name, parent, position, size);
            TMP_Text label = rect.gameObject.AddComponent<TextMeshProUGUI>(); label.font = _font; label.text = text;
            label.fontSize = fontSize; label.alignment = alignment; label.color = color; label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Ellipsis;
            return label;
        }
        private static Image Image(string name, Transform parent, Vector2 position, Vector2 size, Sprite sprite, Color color)
        {
            Image image = Rect(name, parent, position, size).gameObject.AddComponent<Image>();
            image.sprite = sprite; image.color = color; image.raycastTarget = false;
            return image;
        }
        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.layer = 5;
            var rect = (RectTransform)go.transform; rect.SetParent(parent, false); Place(rect, position, size); return rect;
        }
        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position; rect.sizeDelta = size; rect.localScale = Vector3.one;
        }
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        private static void Top(RectTransform rect) => rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        private static void Bottom(RectTransform rect) => rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        private static T Ensure<T>(GameObject go) where T : Component => go.GetComponent<T>() ?? go.AddComponent<T>();
        private static void Bind(SerializedObject so, string property, Object value) => so.FindProperty(property).objectReferenceValue = value;
    }
}
