using System;
using System.Linq;
using CursorHunter.Data;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CursorHunter.App.Editor
{
    /// <summary>Authors the result view in Main without modifying supplier assets.</summary>
    public static class BattleResultSceneSetup
    {
        private const string MainScenePath = "Assets/CursorHunter/App/Scenes/Main.unity";
        private const string Sprites = "Assets/DownLoadAssets/Casual_GUI/GUI Pro-SimpleCasual/ResourcesData/Sprites/Components/";
        private const string ExperienceIconPath = "Assets/DownLoadAssets/IconPack/2D Minimal-IconPack/Icons/128/UI_Play_Exp_01_Purple.png";
        private static TMP_FontAsset _font;
        private static Sprite _panel, _band, _purple, _red, _experienceIcon;

        [MenuItem("Tools/Cursor Hunter/Setup Battle Result in Main")]
        public static void ConfigureMain()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.path != MainScenePath)
                throw new InvalidOperationException("Open Main.unity before authoring the battle result.");
            if (scene.isDirty)
                throw new InvalidOperationException("Save the current Main scene before rebuilding ResultUI.");

            var hud = Object.FindFirstObjectByType<PrototypeRunHud>(FindObjectsInactive.Include);
            var presenter = hud != null ? hud.GetComponent<BattleResultPresentation>() : null;
            if (presenter == null)
                throw new InvalidOperationException("Main is missing its battle result presenter.");
            var so = new SerializedObject(presenter);
            var all = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<RectTransform>(true)).ToArray();
            RectTransform result = all.Single(x => x.name == "ResultUI");
            RectTransform timeout = all.Single(x => x.name == "TimeOutPanel");
            var overlay = (RectTransform)((GameObject)so.FindProperty("overlay").objectReferenceValue).transform;
            var layout = (RectTransform)overlay.Find("ResponsiveLayout");
            if (layout == null) throw new InvalidOperationException("Battle result layout is missing.");

            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/CursorHunter/UI/font/NanumSquareRound-Bold SDF.asset");
            _panel = AssetDatabase.LoadAssetAtPath<Sprite>(Sprites + "Popup/Popup_Demo_Dark/Popup00_Frame1.png");
            _band = AssetDatabase.LoadAssetAtPath<Sprite>(Sprites + "Frame/Frame_Demo_Dark/BasicFrame_Rectangle02_s_Navy.png");
            _purple = AssetDatabase.LoadAssetAtPath<Sprite>(Sprites + "Button/Button_Demo_Common/Btn_Rectangle00_n_Purple.png");
            _red = AssetDatabase.LoadAssetAtPath<Sprite>(Sprites + "Button/Button_Demo_Common/Btn_Rectangle00_n_Red.png");
            _experienceIcon = AssetDatabase.LoadAssetAtPath<Sprite>(ExperienceIconPath);
            if (_font == null || _panel == null || _band == null || _purple == null || _red == null || _experienceIcon == null)
                throw new InvalidOperationException("A battle result font or UI sprite is missing.");
            so.FindProperty("relicDiscoveryFont").objectReferenceValue = _font;

            Place(layout, Vector2.zero, new Vector2(1240f, 760f));

            // Keep the end announcement's timing and hierarchy.
            var popup = (RectTransform)timeout.Find("Popup");
            var timeLabel = (TMP_Text)so.FindProperty("timeOutTitle").objectReferenceValue;
            if (popup == null || timeLabel == null) throw new InvalidOperationException("Announcement popup is missing.");
            var popupImage = popup.GetComponent<Image>();
            StyleSliced(popupImage, _panel, 1f);
            DisableOutline(popup.gameObject);
            timeLabel.font = _font;
            timeLabel.color = Color.white;
            timeLabel.enableAutoSizing = true;
            timeLabel.fontSizeMin = 36f;
            timeLabel.fontSizeMax = 70f;

            result.SetParent(layout, false);
            Place(result, Vector2.zero, new Vector2(1240f, 760f));
            for (int i = result.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(result.GetChild(i).gameObject);
            Image resultImage = result.GetComponent<Image>();
            StyleSliced(resultImage, _panel, 1f);
            resultImage.raycastTarget = true;
            DisableOutline(result.gameObject);
            CanvasGroup resultGroup = Ensure<CanvasGroup>(result.gameObject);
            Button resultSkip = Ensure<Button>(result.gameObject);
            resultSkip.transition = Selectable.Transition.None;
            resultSkip.navigation = new Navigation { mode = Navigation.Mode.None };

            TMP_Text title = Text("Title", result, "전투 종료", new Vector2(0f, -58f), new Vector2(1140f, 76f), 56f, Color.white);
            Top(title.rectTransform);
            AutoSize(title, 32f, 56f);
            TMP_Text subtitle = Text("Subtitle", result, "테스트 전투 · 보상 미지급", new Vector2(0f, -120f), new Vector2(1120f, 32f), 22f, new Color32(210, 220, 234, 255));
            Top(subtitle.rectTransform);
            AutoSize(subtitle, 16f, 22f);
            subtitle.gameObject.SetActive(false);
            TMP_Text heading = Text("RewardHeading", result, "획득 보상", new Vector2(0f, -158f), new Vector2(1100f, 50f), 36f, Color.white);
            Top(heading.rectTransform);
            AutoSize(heading, 24f, 36f);

            Image band = Image("RewardPanel", result, new Vector2(0f, -190f), new Vector2(1150f, 400f), _band, Color.white);
            Top(band.rectTransform);
            band.rectTransform.pivot = new Vector2(0.5f, 1f);
            StyleSliced(band, _band, 1.3f);
            RectTransform rowRoot = Rect("RewardRows", band.transform, new Vector2(0f, -12f), new Vector2(1090f, 282f));
            Top(rowRoot);
            rowRoot.pivot = new Vector2(0.5f, 1f);
            var rowLayout = rowRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            rowLayout.spacing = 6f;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.childControlWidth = rowLayout.childControlHeight = false;
            rowLayout.childForceExpandWidth = rowLayout.childForceExpandHeight = false;

            var rows = so.FindProperty("rewards");
            rows.arraySize = 6;
            string[] ids = { "gem.garnet", "gem.topaz", "gem.amethyst", "gem.sapphire", "gem.diamond", "gem.dragon" };
            string[] names = { "가넷", "토파즈", "자수정", "사파이어", "다이아몬드", "드래곤 젬" };
            Color32[] colors = { new Color32(245, 109, 116, 255), new Color32(238, 207, 95, 255), new Color32(200, 135, 235, 255), new Color32(95, 172, 231, 255), new Color32(119, 212, 158, 255), new Color32(226, 230, 240, 255) };
            GemstoneIconCatalog catalog = GemstoneIconCatalog.LoadDefault();
            if (catalog == null) throw new InvalidOperationException("Gemstone icon catalog is missing.");
            for (int i = 0; i < ids.Length; i++)
            {
                RectTransform row = Rect("Reward_" + ids[i].Substring(4), rowRoot, Vector2.zero, new Vector2(1090f, 42f));
                CanvasGroup group = row.gameObject.AddComponent<CanvasGroup>();
                Image icon = Image("GemIcon", row, new Vector2(60f, 0f), new Vector2(42f, 42f), catalog.GetIcon(ids[i]), Color.white);
                Left(icon.rectTransform);
                icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                icon.preserveAspect = true;
                TMP_Text name = Text("Name", row, names[i], new Vector2(140f, 0f), new Vector2(420f, 42f), 30f, Color.white, TextAlignmentOptions.Left);
                Left(name.rectTransform);
                TMP_Text amount = Text("Amount", row, "+0", new Vector2(-10f, 0f), new Vector2(460f, 42f), 30f, colors[i], TextAlignmentOptions.Right);
                Right(amount.rectTransform);
                AutoSize(name, 20f, 30f);
                AutoSize(amount, 20f, 30f);
                var entry = rows.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("currencyId").stringValue = ids[i];
                entry.FindPropertyRelative("group").objectReferenceValue = group;
                entry.FindPropertyRelative("icon").objectReferenceValue = icon.rectTransform;
                entry.FindPropertyRelative("name").objectReferenceValue = name;
                entry.FindPropertyRelative("amount").objectReferenceValue = amount;
                row.gameObject.SetActive(false);
            }

            TMP_Text empty = Text("EmptyRewards", band.transform, "획득한 젬스톤이 없습니다.", new Vector2(0f, -155f), new Vector2(1050f, 60f), 30f, new Color32(210, 220, 234, 255));
            Top(empty.rectTransform);
            AutoSize(empty, 20f, 30f);
            empty.gameObject.SetActive(false);
            Image separator = Image("ExperienceSeparator", band.transform, new Vector2(0f, 94f), new Vector2(1090f, 2f), null, new Color32(89, 108, 137, 255));
            Bottom(separator.rectTransform);
            RectTransform experience = Rect("ExperienceRow", band.transform, new Vector2(0f, 18f), new Vector2(1090f, 64f));
            Bottom(experience);
            experience.pivot = new Vector2(0.5f, 0f);
            Image badge = Image("ExperienceBadge", experience, new Vector2(60f, 0f), new Vector2(56f, 56f), _experienceIcon, Color.white);
            Left(badge.rectTransform);
            badge.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            badge.preserveAspect = true;
            TMP_Text experienceLabel = Text("ExperienceLabel", experience, "획득 경험치", new Vector2(140f, 0f), new Vector2(420f, 64f), 30f, Color.white, TextAlignmentOptions.Left);
            Left(experienceLabel.rectTransform);
            TMP_Text experienceAmount = Text("ExperienceAmount", experience, "+0", new Vector2(-10f, 0f), new Vector2(460f, 64f), 30f, new Color32(207, 194, 247, 255), TextAlignmentOptions.Right);
            Right(experienceAmount.rectTransform);
            AutoSize(experienceLabel, 20f, 30f);
            AutoSize(experienceAmount, 20f, 30f);

            RectTransform actions = Rect("Actions", result, new Vector2(0f, 20f), new Vector2(1160f, 100f));
            Bottom(actions);
            actions.pivot = new Vector2(0.5f, 0f);
            CanvasGroup actionGroup = actions.gameObject.AddComponent<CanvasGroup>();
            Button upgrade = Action(actions, "UpgradeButton", "강화하기", -290f, _purple);
            Button retry = Action(actions, "RetryButton", "다시 도전", 290f, _red);
            TMP_Text hint = Text("Hint", result, "클릭하면 보상을 바로 볼 수 있어요.", new Vector2(0f, 150f), new Vector2(1100f, 32f), 20f, new Color32(210, 220, 234, 255));
            Bottom(hint.rectTransform);
            AutoSize(hint, 16f, 20f);

            Bind(so, "resultUI", resultGroup);
            Bind(so, "title", title);
            Bind(so, "subtitle", subtitle);
            Bind(so, "rewardHeading", heading);
            Bind(so, "hint", hint);
            Bind(so, "emptyRewards", empty);
            Bind(so, "rewardRows", rowRoot);
            Bind(so, "experienceIcon", badge.rectTransform);
            Bind(so, "experienceLabel", experienceLabel);
            Bind(so, "experienceAmount", experienceAmount);
            Bind(so, "actions", actionGroup);
            Bind(so, "upgradeButton", upgrade);
            Bind(so, "retryButton", retry);
            Bind(so, "resultSkipButton", resultSkip);
            so.ApplyModifiedPropertiesWithoutUndo();

            foreach (Component component in timeout.GetComponentsInChildren<Component>(true))
                if (component != null && PrefabUtility.IsPartOfPrefabInstance(component))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Failed to save the battle result in Main.");
            Debug.Log("Battle result authored: adaptive earned gemstones and experience.");
        }

        private static Button Action(Transform parent, string name, string label, float x, Sprite sprite)
        {
            Image face = Image(name, parent, new Vector2(x, 0f), new Vector2(560f, 100f), sprite, Color.white);
            StyleSliced(face, sprite, 2f);
            face.raycastTarget = true;
            Button button = face.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            ColorBlock colors = button.colors;
            colors.disabledColor = colors.normalColor;
            button.colors = colors;
            TMP_Text text = Text("Label", face.transform, label, Vector2.zero, new Vector2(520f, 80f), 42f, Color.white);
            AutoSize(text, 24f, 42f);
            return button;
        }

        private static void StyleSliced(Image image, Sprite sprite, float pixelsPerUnit)
        {
            if (image == null) throw new InvalidOperationException("Expected UI Image is missing.");
            image.sprite = sprite;
            image.type = UnityEngine.UI.Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = pixelsPerUnit;
            image.color = Color.white;
        }

        private static void DisableOutline(GameObject go)
        {
            foreach (Outline outline in go.GetComponents<Outline>()) outline.enabled = false;
        }

        private static void AutoSize(TMP_Text text, float minimum, float maximum)
        {
            text.enableAutoSizing = true;
            text.fontSizeMin = minimum;
            text.fontSizeMax = maximum;
            text.fontSize = maximum;
        }

        private static TMP_Text Text(string name, Transform parent, string text, Vector2 position, Vector2 size, float fontSize, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            RectTransform rect = Rect(name, parent, position, size);
            TMP_Text label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = _font;
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = color;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            return label;
        }

        private static Image Image(string name, Transform parent, Vector2 position, Vector2 size, Sprite sprite, Color color)
        {
            Image image = Rect(name, parent, position, size).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            Place(rect, position, size);
            return rect;
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
        }

        private static void Top(RectTransform rect) => rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        private static void Bottom(RectTransform rect) => rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        private static void Left(RectTransform rect) { rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f); rect.pivot = new Vector2(0f, 0.5f); }
        private static void Right(RectTransform rect) { rect.anchorMin = rect.anchorMax = new Vector2(1f, 0.5f); rect.pivot = new Vector2(1f, 0.5f); }
        private static T Ensure<T>(GameObject go) where T : Component => go.GetComponent<T>() ?? go.AddComponent<T>();
        private static void Bind(SerializedObject so, string property, Object value) => so.FindProperty(property).objectReferenceValue = value;
    }
}
