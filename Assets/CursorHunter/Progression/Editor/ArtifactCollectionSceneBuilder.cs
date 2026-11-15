using System;
using CursorHunter.Progression.ResearchPrototype;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CursorHunter.Progression.Editor
{
    /// <summary>One-time authoring utility for the scene-based relic collection screen.</summary>
    public static class ArtifactCollectionSceneBuilder
    {
        private const int TooltipCanvasSortingOrder = 1000;

        private const string MainScenePath = "Assets/CursorHunter/App/Scenes/Main.unity";
        private const string StagePrefabPath = "Assets/CursorHunter/App/Prefabs/Stage_Nomal.prefab";
        private const string BackdropPrefabPath = "Assets/CursorHunter/Progression/Prefabs/ProgressionUiBackdrop.prefab";
        private const string ArtifactGridName = "ArtifactRelicGrid";
        private const float ArtifactSlotScale = 0.75f;
        private const float ArtifactGridCellSize = 220f;
        private const float ArtifactGridHorizontalSpacing = 115f;
        // The 220-unit prefab is rendered at 75%, so a -50 layout gap leaves a 5-unit visual gap.
        private const float ArtifactGridVerticalSpacing = -50f;
        private const float ArtifactGridCenterY = 3f;

        [MenuItem("Tools/Cursor Hunter/Build Artifact Collection Canvas")]
        public static void BuildMainScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!string.Equals(scene.path, MainScenePath, StringComparison.Ordinal))
                throw new InvalidOperationException("Open Main.unity before building Artifact_Canvas.");

            GameObject uiCanvas = GameObject.Find("UICanvas");
            Transform researchTransform = uiCanvas != null ? uiCanvas.transform.Find("ResearchPrototype_Canvas") : null;
            ResearchTestMap research = researchTransform != null
                ? researchTransform.GetComponent<ResearchTestMap>()
                : null;
            if (uiCanvas == null || research == null || research.tooltipRoot == null)
                throw new InvalidOperationException("UICanvas, ResearchPrototype_Canvas, or its Tooltip_Dark_Prefab is missing.");
            Transform existingArtifact = uiCanvas.transform.Find("Artifact_Canvas");
            if (existingArtifact != null)
            {
                RepairExistingCanvas(scene, uiCanvas, existingArtifact.gameObject);
                return;
            }

            GameObject stagePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(StagePrefabPath);
            GameObject backdropPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BackdropPrefabPath);
            RelicCollectionCatalog catalog = Resources.Load<RelicCollectionCatalog>("RelicCollectionCatalog");
            if (stagePrefab == null || backdropPrefab == null || catalog == null || catalog.Entries == null || catalog.Entries.Count != 20)
                throw new InvalidOperationException("Stage_Nomal, shared backdrop, or the 20-entry RelicCollectionCatalog is missing.");

            var artifact = new GameObject("Artifact_Canvas", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(artifact, "Create Artifact_Canvas");
            artifact.layer = 5;
            RectTransform artifactRect = artifact.GetComponent<RectTransform>();
            artifactRect.SetParent(uiCanvas.transform, false);
            Stretch(artifactRect);
            int researchIndex = researchTransform.GetSiblingIndex();
            artifactRect.SetSiblingIndex(researchIndex + 1);

            GameObject backdrop = (GameObject)PrefabUtility.InstantiatePrefab(backdropPrefab, artifact.transform);
            Undo.RegisterCreatedObjectUndo(backdrop, "Add shared progression backdrop to Artifact_Canvas");
            RectTransform backdropRect = backdrop.transform as RectTransform;
            if (backdropRect != null)
                Stretch(backdropRect);
            backdrop.transform.SetAsFirstSibling();

            CreateText(artifact.transform, "ArtifactTitle", "유물 보관소",
                new Vector2(250f, 420f), new Vector2(520f, 68f), 40f, TextAlignmentOptions.Center);
            TextMeshProUGUI count = CreateText(artifact.transform, "ArtifactCollectionCount", "보유 00 / 20",
                new Vector2(800f, 420f), new Vector2(260f, 56f), 24f, TextAlignmentOptions.Right);
            count.color = new Color(0.72f, 0.82f, 0.96f, 1f);

            GridLayoutGroup artifactGrid = ConfigureArtifactGrid(artifact.transform);
            RectTransform artifactGridRect = artifactGrid.transform as RectTransform;
            var slots = new ArtifactRelicSlot[catalog.Entries.Count];
            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                RelicCollectionCatalogEntry entry = catalog.Entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.Id))
                    throw new InvalidOperationException("Relic catalog entry " + i + " has no stable ID.");

                GameObject card = (GameObject)PrefabUtility.InstantiatePrefab(stagePrefab, artifactGrid.transform);
                Undo.RegisterCreatedObjectUndo(card, "Place Stage_Nomal relic slot");
                card.name = "RelicSlot_" + (i + 1).ToString("00");
                SetLayerRecursively(card.transform, 5);
                RectTransform cardRect = card.transform as RectTransform;
                cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
                cardRect.pivot = new Vector2(0.5f, 0.5f);
                cardRect.sizeDelta = new Vector2(ArtifactGridCellSize, ArtifactGridCellSize);
                cardRect.localScale = Vector3.one * ArtifactSlotScale;
                PrefabUtility.RecordPrefabInstancePropertyModifications(cardRect);

                Image icon = card.transform.Find("MainIcon")?.GetComponent<Image>();
                GameObject lockIcon = card.transform.Find("Icon_Lock")?.gameObject;
                Transform badgeTransform = card.transform.Find("Text_Num");
                TextMeshProUGUI newBadge = badgeTransform != null ? badgeTransform.GetComponent<TextMeshProUGUI>() : null;
                if (badgeTransform is RectTransform badgeRect && newBadge != null)
                    ConfigureNewBadge(badgeRect, newBadge);
                ArtifactRelicSlot slot = Undo.AddComponent<ArtifactRelicSlot>(card);
                slot.Configure(entry.Id, icon, lockIcon);
                EditorUtility.SetDirty(slot);
                PrefabUtility.RecordPrefabInstancePropertyModifications(slot);
                slots[i] = slot;
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(artifactGridRect);
            foreach (ArtifactRelicSlot slot in slots)
                PrefabUtility.RecordPrefabInstancePropertyModifications(slot.transform as RectTransform);

            GameObject tooltip = UnityEngine.Object.Instantiate(research.tooltipRoot, artifact.transform, false);
            Undo.RegisterCreatedObjectUndo(tooltip, "Clone scene-authored relic tooltip");
            tooltip.name = "Tooltip_Dark_Prefab";
            tooltip.transform.SetAsLastSibling();
            SetLayerRecursively(tooltip.transform, 5);
            RectTransform tooltipRootRect = tooltip.transform as RectTransform;
            Stretch(tooltipRootRect);
            Transform screenDim = tooltip.transform.Find("ScreenDim");
            if (screenDim != null)
                screenDim.gameObject.SetActive(false);
            CanvasGroup tooltipGroup = tooltip.GetComponent<CanvasGroup>();
            if (tooltipGroup == null)
                tooltipGroup = Undo.AddComponent<CanvasGroup>(tooltip);
            tooltipGroup.blocksRaycasts = false;

            RectTransform popup = tooltip.transform.Find("Popup") as RectTransform;
            Transform titleTransform = tooltip.transform.Find("Popup/TopFrame_Dark/ResearchTitle");
            Transform descriptionTransform = tooltip.transform.Find("Popup/EffectDescription");
            if (popup == null || titleTransform == null || descriptionTransform == null)
                throw new InvalidOperationException("The cloned Tooltip_Dark_Prefab does not contain the research title/effect fields.");
            popup.anchorMin = popup.anchorMax = new Vector2(0.5f, 0.5f);
            popup.pivot = new Vector2(0.5f, 0.5f);
            popup.anchoredPosition = Vector2.zero;
            TextMeshProUGUI tooltipTitle = titleTransform.GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI tooltipDescription = descriptionTransform.GetComponent<TextMeshProUGUI>();
            EnsureRelicLocalizedLabel(tooltipTitle);
            EnsureRelicLocalizedLabel(tooltipDescription);
            tooltipTitle.text = string.Empty;
            tooltipDescription.text = string.Empty;
            ConfigureArtifactTooltip(tooltip, popup, tooltipTitle, tooltipDescription);
            tooltip.SetActive(false);

            ArtifactCollectionCanvas controller = Undo.AddComponent<ArtifactCollectionCanvas>(artifact);
            TraitScreenController traitController = FindTraitController(uiCanvas);
            controller.Configure(
                traitController,
                slots,
                tooltip,
                popup,
                tooltipTitle,
                tooltipDescription,
                count);

            Type menuType = Type.GetType("CursorHunter.App.ProgressionTopMenu, CursorHunter.App");
            Component menu = menuType != null ? uiCanvas.GetComponent(menuType) : null;
            if (menu == null)
                throw new InvalidOperationException("ProgressionTopMenu was not found in Main.unity.");
            var serializedMenu = new SerializedObject(menu);
            serializedMenu.FindProperty("artifactCanvas").objectReferenceValue = artifact;
            serializedMenu.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(menu);

            if (traitController != null)
            {
                var serializedTrait = new SerializedObject(traitController);
                serializedTrait.FindProperty("initialShowRelicCollection").boolValue = false;
                serializedTrait.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(traitController);
            }

            artifact.SetActive(false);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Failed to save Main.unity after building Artifact_Canvas.");
            AssetDatabase.SaveAssets();
            Debug.Log("ARTIFACT_CANVAS_BUILT: 20 scene-authored Stage_Nomal slots, cloned Dark tooltip, shared backdrop.", artifact);
        }

        private static void RepairExistingCanvas(UnityEngine.SceneManagement.Scene scene, GameObject uiCanvas, GameObject artifact)
        {
            TraitScreenController traitController = FindTraitController(uiCanvas);
            ArtifactCollectionCanvas controller = artifact.GetComponent<ArtifactCollectionCanvas>();
            if (controller == null || traitController == null)
                throw new InvalidOperationException("Artifact_Canvas or TraitScreenController is missing a required component.");

            RelicCollectionCatalog catalog = Resources.Load<RelicCollectionCatalog>("RelicCollectionCatalog");
            ArtifactRelicSlot[] authoredSlots = artifact.GetComponentsInChildren<ArtifactRelicSlot>(true);
            if (catalog == null || catalog.Entries == null || authoredSlots.Length != catalog.Entries.Count)
                throw new InvalidOperationException("Artifact_Canvas slots no longer match the relic catalog.");

            Array.Sort(authoredSlots, (left, right) => string.CompareOrdinal(left.name, right.name));
            GridLayoutGroup artifactGrid = ConfigureArtifactGrid(artifact.transform);
            RectTransform artifactGridRect = artifactGrid.transform as RectTransform;

            for (int i = 0; i < authoredSlots.Length; i++)
            {
                ArtifactRelicSlot slot = authoredSlots[i];
                RelicCollectionCatalogEntry entry = catalog.Entries[i];
                Transform card = slot.transform;
                if (card.parent != artifactGridRect)
                    card.SetParent(artifactGridRect, false);
                card.SetSiblingIndex(i);
                RectTransform cardRect = card as RectTransform;
                if (cardRect != null)
                {
                    cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
                    cardRect.pivot = new Vector2(0.5f, 0.5f);
                    cardRect.sizeDelta = new Vector2(ArtifactGridCellSize, ArtifactGridCellSize);
                    cardRect.localScale = Vector3.one * ArtifactSlotScale;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(cardRect);
                }

                slot.Configure(
                    entry.Id,
                    card.Find("MainIcon")?.GetComponent<Image>(),
                    card.Find("Icon_Lock")?.gameObject);
                EditorUtility.SetDirty(slot);
                PrefabUtility.RecordPrefabInstancePropertyModifications(slot);

                RectTransform badgeRect = card.Find("Text_Num") as RectTransform;
                TextMeshProUGUI badge = badgeRect != null ? badgeRect.GetComponent<TextMeshProUGUI>() : null;
                if (badgeRect != null && badge != null)
                    ConfigureNewBadge(badgeRect, badge);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(artifactGridRect);
            foreach (ArtifactRelicSlot slot in authoredSlots)
                PrefabUtility.RecordPrefabInstancePropertyModifications(slot.transform as RectTransform);

            RectTransform titleRect = artifact.transform.Find("ArtifactTitle") as RectTransform;
            if (titleRect != null)
            {
                titleRect.anchoredPosition = new Vector2(250f, 420f);
                TextMeshProUGUI title = titleRect.GetComponent<TextMeshProUGUI>();
                if (title != null)
                    title.alignment = TextAlignmentOptions.Center;
            }
            RectTransform countRect = artifact.transform.Find("ArtifactCollectionCount") as RectTransform;
            if (countRect != null)
                countRect.anchoredPosition = new Vector2(800f, 420f);

            GameObject tooltipRoot = artifact.transform.Find("Tooltip_Dark_Prefab")?.gameObject;
            RectTransform tooltipPanel = tooltipRoot != null ? tooltipRoot.transform.Find("Popup") as RectTransform : null;
            TextMeshProUGUI tooltipTitle = tooltipRoot != null
                ? tooltipRoot.transform.Find("Popup/TopFrame_Dark/ResearchTitle")?.GetComponent<TextMeshProUGUI>()
                : null;
            TextMeshProUGUI tooltipDescription = tooltipRoot != null
                ? tooltipRoot.transform.Find("Popup/EffectDescription")?.GetComponent<TextMeshProUGUI>()
                : null;
            if (tooltipRoot == null || tooltipPanel == null || tooltipTitle == null || tooltipDescription == null)
                throw new InvalidOperationException("The scene-authored artifact tooltip is incomplete.");
            EnsureRelicLocalizedLabel(tooltipTitle);
            EnsureRelicLocalizedLabel(tooltipDescription);
            ConfigureArtifactTooltip(tooltipRoot, tooltipPanel, tooltipTitle, tooltipDescription);
            tooltipRoot.SetActive(false);
            tooltipRoot.transform.SetAsLastSibling();

            var serializedController = new SerializedObject(controller);
            serializedController.FindProperty("progression").objectReferenceValue = traitController;
            serializedController.FindProperty("slots").arraySize = authoredSlots.Length;
            SerializedProperty slots = serializedController.FindProperty("slots");
            for (int i = 0; i < authoredSlots.Length; i++)
            {
                slots.GetArrayElementAtIndex(i).objectReferenceValue = authoredSlots[i];
            }
            serializedController.FindProperty("tooltipRoot").objectReferenceValue = tooltipRoot;
            serializedController.FindProperty("tooltipPanel").objectReferenceValue = tooltipPanel;
            serializedController.FindProperty("tooltipTitle").objectReferenceValue = tooltipTitle;
            serializedController.FindProperty("tooltipDescription").objectReferenceValue = tooltipDescription;
            serializedController.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);

            Type menuType = Type.GetType("CursorHunter.App.ProgressionTopMenu, CursorHunter.App");
            Component menu = menuType != null ? uiCanvas.GetComponent(menuType) : null;
            if (menu == null)
                throw new InvalidOperationException("ProgressionTopMenu was not found in Main.unity.");
            var serializedMenu = new SerializedObject(menu);
            serializedMenu.FindProperty("artifactCanvas").objectReferenceValue = artifact;
            serializedMenu.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(menu);

            var serializedTrait = new SerializedObject(traitController);
            serializedTrait.FindProperty("initialShowRelicCollection").boolValue = false;
            serializedTrait.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(traitController);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Failed to save Main.unity while repairing Artifact_Canvas bindings.");
            Debug.Log("ARTIFACT_CANVAS_REPAIRED: bound progression state and disabled legacy relic UI generation.", artifact);
        }

        private static TraitScreenController FindTraitController(GameObject uiCanvas)
        {
            TraitScreenController[] controllers = uiCanvas.GetComponentsInChildren<TraitScreenController>(true);
            return controllers.Length > 0 ? controllers[0] : null;
        }

        private static void EnsureRelicLocalizedLabel(TextMeshProUGUI label)
        {
            if (label != null && label.GetComponent<RelicLocalizedLabel>() == null)
                Undo.AddComponent<RelicLocalizedLabel>(label.gameObject);
        }

        private static GridLayoutGroup ConfigureArtifactGrid(Transform artifact)
        {
            Transform existing = artifact.Find(ArtifactGridName);
            GameObject gridObject;
            GridLayoutGroup grid;
            if (existing == null)
            {
                gridObject = new GameObject(ArtifactGridName, typeof(RectTransform), typeof(GridLayoutGroup));
                Undo.RegisterCreatedObjectUndo(gridObject, "Create relic slot grid");
                gridObject.layer = 5;
                gridObject.transform.SetParent(artifact, false);
                grid = gridObject.GetComponent<GridLayoutGroup>();
            }
            else
            {
                gridObject = existing.gameObject;
                grid = existing.GetComponent<GridLayoutGroup>();
                if (grid == null)
                    grid = Undo.AddComponent<GridLayoutGroup>(gridObject);
            }

            RectTransform rect = gridObject.transform as RectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, ArtifactGridCenterY);
            rect.sizeDelta = new Vector2(
                5f * ArtifactGridCellSize + 4f * ArtifactGridHorizontalSpacing,
                4f * ArtifactGridCellSize + 3f * ArtifactGridVerticalSpacing);
            rect.localScale = Vector3.one;

            grid.padding = new RectOffset(0, 0, 0, 0);
            grid.childAlignment = TextAnchor.MiddleCenter;
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.cellSize = new Vector2(ArtifactGridCellSize, ArtifactGridCellSize);
            grid.spacing = new Vector2(ArtifactGridHorizontalSpacing, ArtifactGridVerticalSpacing);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;
            grid.enabled = true;
            gridObject.SetActive(true);
            grid.transform.SetSiblingIndex(Mathf.Min(1, artifact.childCount - 1));
            EditorUtility.SetDirty(rect);
            EditorUtility.SetDirty(grid);
            return grid;
        }

        private static void ConfigureNewBadge(RectTransform rect, TextMeshProUGUI text)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-10f, -8f);
            rect.sizeDelta = new Vector2(76f, 30f);
            text.text = "NEW";
            text.fontSize = 20f;
            text.color = new Color(1f, 0.77f, 0.35f, 1f);
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.gameObject.SetActive(false);
            PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
            PrefabUtility.RecordPrefabInstancePropertyModifications(text);
        }

        private static void ConfigureArtifactTooltip(
            GameObject tooltipRoot,
            RectTransform popup,
            TextMeshProUGUI title,
            TextMeshProUGUI description)
        {
            if (tooltipRoot == null || popup == null || title == null || description == null)
                throw new InvalidOperationException("The cloned Tooltip_Dark_Prefab does not contain title and description fields.");

            SetLayerRecursively(tooltipRoot.transform, 5);
            RectTransform rootRect = tooltipRoot.transform as RectTransform;
            Stretch(rootRect);
            Transform screenDim = tooltipRoot.transform.Find("ScreenDim");
            if (screenDim != null)
                screenDim.gameObject.SetActive(false);

            CanvasGroup group = tooltipRoot.GetComponent<CanvasGroup>();
            if (group == null)
                group = Undo.AddComponent<CanvasGroup>(tooltipRoot);
            group.blocksRaycasts = false;
            EditorUtility.SetDirty(group);

            Canvas tooltipCanvas = tooltipRoot.GetComponent<Canvas>();
            if (tooltipCanvas == null)
                tooltipCanvas = Undo.AddComponent<Canvas>(tooltipRoot);
            Canvas parentCanvas = tooltipRoot.transform.parent != null
                ? tooltipRoot.transform.parent.GetComponentInParent<Canvas>()
                : null;
            tooltipCanvas.overrideSorting = true;
            tooltipCanvas.sortingLayerID = parentCanvas != null ? parentCanvas.sortingLayerID : 0;
            tooltipCanvas.sortingOrder = TooltipCanvasSortingOrder;
            EditorUtility.SetDirty(tooltipCanvas);

            popup.anchorMin = popup.anchorMax = new Vector2(0.5f, 0.5f);
            popup.pivot = new Vector2(0.5f, 0.5f);
            popup.anchoredPosition = Vector2.zero;
            popup.sizeDelta = new Vector2(520f, 236f);

            Transform header = popup.Find("TopFrame_Dark");
            RectTransform headerRect = header as RectTransform;
            RectTransform titleRect = title.transform as RectTransform;
            Transform band = popup.Find("EffectBand");
            RectTransform bandRect = band as RectTransform;
            RectTransform descriptionRect = description.transform as RectTransform;
            if (header == null || headerRect == null || titleRect == null || bandRect == null || descriptionRect == null)
                throw new InvalidOperationException("The cloned tooltip is missing its title or description layout objects.");

            foreach (Transform child in popup)
                child.gameObject.SetActive(child == header || child == band || child == description.transform);
            foreach (Transform child in header)
                child.gameObject.SetActive(child == title.transform);

            headerRect.anchorMin = new Vector2(0f, 1f);
            headerRect.anchorMax = new Vector2(1f, 1f);
            headerRect.pivot = new Vector2(0.5f, 0.5f);
            headerRect.sizeDelta = new Vector2(-48f, 64f);
            headerRect.anchoredPosition = new Vector2(0f, -42f);
            titleRect.anchorMin = titleRect.anchorMax = new Vector2(0.5f, 0.5f);
            titleRect.pivot = new Vector2(0.5f, 0.5f);
            titleRect.anchoredPosition = Vector2.zero;
            titleRect.sizeDelta = new Vector2(460f, 58f);
            title.alignment = TextAlignmentOptions.Center;
            title.enableAutoSizing = true;
            title.fontSizeMin = 24f;
            title.fontSizeMax = 40f;
            title.raycastTarget = false;
            EditorUtility.SetDirty(title);

            bandRect.anchorMin = bandRect.anchorMax = new Vector2(0.5f, 0.5f);
            bandRect.pivot = new Vector2(0.5f, 0.5f);
            bandRect.anchoredPosition = new Vector2(0f, -25f);
            bandRect.sizeDelta = new Vector2(480f, 108f);
            Image bandImage = band.GetComponent<Image>();
            if (bandImage != null)
            {
                bandImage.raycastTarget = false;
                EditorUtility.SetDirty(bandImage);
            }

            descriptionRect.anchorMin = descriptionRect.anchorMax = new Vector2(0.5f, 0.5f);
            descriptionRect.pivot = new Vector2(0.5f, 0.5f);
            descriptionRect.anchoredPosition = new Vector2(0f, -25f);
            descriptionRect.sizeDelta = new Vector2(450f, 84f);
            description.alignment = TextAlignmentOptions.Center;
            description.enableAutoSizing = true;
            description.fontSizeMin = 20f;
            description.fontSizeMax = 32f;
            description.color = new Color(0.75f, 0.91f, 0.78f, 1f);
            description.raycastTarget = false;
            EditorUtility.SetDirty(description);
            EditorUtility.SetDirty(tooltipRoot);

            PrefabUtility.RecordPrefabInstancePropertyModifications(popup);
            PrefabUtility.RecordPrefabInstancePropertyModifications(headerRect);
            PrefabUtility.RecordPrefabInstancePropertyModifications(titleRect);
            PrefabUtility.RecordPrefabInstancePropertyModifications(bandRect);
            PrefabUtility.RecordPrefabInstancePropertyModifications(descriptionRect);
            PrefabUtility.RecordPrefabInstancePropertyModifications(tooltipCanvas);
            PrefabUtility.RecordPrefabInstancePropertyModifications(description);
        }

        private static TextMeshProUGUI CreateText(
            Transform parent,
            string objectName,
            string value,
            Vector2 position,
            Vector2 size,
            float fontSize,
            TextAlignmentOptions alignment)
        {
            var textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.layer = 5;
            textObject.transform.SetParent(parent, false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.font = Resources.Load<TMP_FontAsset>("GameData/KoreanDisplayFont") ?? TMP_Settings.defaultFontAsset;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = alignment;
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            if (rect == null)
                return;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        private static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            foreach (Transform child in root)
                SetLayerRecursively(child, layer);
        }
    }
}
