using System.Collections;
using System.Linq;
using CursorHunter.Progression.ResearchPrototype;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CursorHunter.App.Tests
{
    public sealed class ResearchPrototypePlayModeTests
    {
        [UnityTest]
        public IEnumerator CenterNodeRevealsBranchesAndTooltipOnlyWhileHovered()
        {
#if UNITY_EDITOR
            var scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/CursorHunter/Progression/Scenes/ResearchPrototype.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            var map = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ResearchTestMap>(true)).Single();
            Assert.That(map.nodes.Count(n => n.gameObject.activeSelf), Is.EqualTo(1));
            var rootNode = map.nodes[0];
            Assert.That(((RectTransform)rootNode.transform).anchoredPosition, Is.EqualTo(Vector2.zero));
            Assert.That(map.tooltipRoot.activeSelf, Is.False);
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            rootNode.OnPointerEnter(pointer);
            Assert.That(map.tooltipRoot.activeSelf, Is.True);
            Assert.That(map.effectDescription.text, Is.EqualTo("기본 공격 피해 증가"));
            Assert.That(map.beforeValue.rectTransform.anchoredPosition.x, Is.LessThan(0f));
            Assert.That(map.afterValue.gameObject.activeSelf, Is.True);
            Assert.That(map.costLabel.gameObject.activeSelf, Is.True);
            Assert.That(map.beforeValue.text, Is.EqualTo("0"));
            Assert.That(map.afterValue.text, Is.EqualTo("2"));
            Assert.That(map.tooltipRoot.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(rootNode.transform.localScale.x, Is.EqualTo(1.1f).Within(.01f));
            rootNode.OnPointerClick(pointer);
            Assert.That(rootNode.rank, Is.EqualTo(1));
            Assert.That(map.nodes.Count(n => n.gameObject.activeSelf), Is.EqualTo(7));
            Assert.That(map.beforeValue.text, Is.EqualTo("2"));
            Assert.That(map.afterValue.text, Is.EqualTo("4"));
            Assert.That(map.nodes[7].gameObject.activeSelf, Is.False);
            yield return new WaitForSecondsRealtime(.08f);
            Assert.That(rootNode.transform.localScale.x, Is.GreaterThan(1f));
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(rootNode.transform.localScale.x, Is.EqualTo(1.1f).Within(.01f));
            Assert.That(map.nodes[1].transform.localScale.x, Is.EqualTo(1f).Within(.001f));
            rootNode.OnPointerExit(pointer);
            Assert.That(map.tooltipRoot.activeSelf, Is.False);
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(rootNode.transform.localScale.x, Is.EqualTo(1f).Within(.01f));
            rootNode.OnPointerEnter(pointer);
            yield return null;
            rootNode.OnPointerExit(pointer);
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(rootNode.transform.localScale.x, Is.EqualTo(1f).Within(.01f));
            rootNode.OnPointerEnter(pointer);
            yield return null;
            rootNode.gameObject.SetActive(false);
            Assert.That(rootNode.transform.localScale.x, Is.EqualTo(1f).Within(.01f));
            Assert.That(map.tooltipRoot.activeSelf, Is.False);
            yield return SceneManager.UnloadSceneAsync(scene);
#else
            yield return null;
#endif
        }
        [UnityTest]
        public IEnumerator SparklesOnlyRunAtMaximumRankAndStopWhenHidden()
        {
#if UNITY_EDITOR
            var scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/CursorHunter/Progression/Scenes/ResearchPrototype.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            var map = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ResearchTestMap>(true)).Single();
            var node = map.nodes[0];
            Assert.That(node.maxSparkles, Is.Not.Null);
            Assert.That(node.maxSparkles.visualRoot.activeSelf, Is.False);
            Assert.That(node.maxSparkles.sparkles.All(s => s.sprite != null && !s.raycastTarget), Is.True);
            for (int i = 0; i < node.definition.ranks.Length - 1; i++) map.Purchase(node);
            Assert.That(node.maxSparkles.visualRoot.activeSelf, Is.False);
            map.Purchase(node);
            Assert.That(node.rank, Is.EqualTo(node.definition.ranks.Length));
            Assert.That(node.maxSparkles.visualRoot.activeSelf, Is.True);
            Assert.That(map.effectDescription.text, Is.EqualTo("기본 공격 피해 증가"));
            Assert.That(map.beforeValue.text, Is.EqualTo("14"));
            Assert.That(map.beforeValue.rectTransform.anchoredPosition.x, Is.EqualTo(0f).Within(.01f));
            Assert.That(map.afterValue.gameObject.activeSelf, Is.False);
            Assert.That(map.arrow.activeSelf, Is.False);
            Assert.That(map.gemIcon.activeSelf, Is.False);
            Assert.That(map.costLabel.gameObject.activeSelf, Is.False);
            Assert.That(map.costValue.text, Is.EqualTo("최대 강화"));
            Assert.That(map.costValue.rectTransform.anchoredPosition.x, Is.EqualTo(0f).Within(.01f));
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(node.maxSparkles.sparkles[0].color.a, Is.GreaterThan(.05f));
            var pointer = new PointerEventData(EventSystem.current);
            node.OnPointerEnter(pointer);
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(node.maxSparkles.visualRoot.activeSelf, Is.True);
            node.OnPointerExit(pointer);
            node.gameObject.SetActive(false);
            Assert.That(node.maxSparkles.visualRoot.activeSelf, Is.False);
            node.gameObject.SetActive(true);
            node.Refresh();
            Assert.That(node.maxSparkles.visualRoot.activeSelf, Is.True);
            node.rank--;
            node.Refresh();
            map.Select(node);
            Assert.That(node.maxSparkles.visualRoot.activeSelf, Is.False);
            Assert.That(map.beforeValue.rectTransform.anchoredPosition.x, Is.LessThan(0f));
            Assert.That(map.afterValue.gameObject.activeSelf, Is.True);
            Assert.That(map.arrow.activeSelf, Is.True);
            Assert.That(map.costLabel.gameObject.activeSelf, Is.True);
            yield return SceneManager.UnloadSceneAsync(scene);
#else
            yield return null;
#endif
        }
        [UnityTest]
        public IEnumerator TooltipUsesLanguageIndependentNumbersAndRoundedFrames()
        {
#if UNITY_EDITOR
            var scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/CursorHunter/Progression/Scenes/ResearchPrototype.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            var map = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ResearchTestMap>(true)).Single();
            map.Purchase(map.nodes[0]);
            map.Select(map.nodes[1]);
            Assert.That(map.beforeValue.text, Is.EqualTo("0"));
            Assert.That(map.afterValue.text, Is.EqualTo("0.06"));
            map.Purchase(map.nodes[1]);
            Assert.That(map.beforeValue.text, Is.EqualTo("0.06"));
            Assert.That(map.afterValue.text, Is.EqualTo("0.12"));
            map.Select(map.nodes[2]);
            Assert.That(map.beforeValue.text, Is.EqualTo("0"));
            Assert.That(map.afterValue.text, Is.EqualTo("-0.08"));
            map.Select(map.nodes[3]);
            Assert.That(map.beforeValue.text, Is.EqualTo("0.00%"));
            Assert.That(map.afterValue.text, Is.EqualTo("5.00%"));
            map.Select(map.nodes[4]);
            Assert.That(map.beforeValue.text, Is.EqualTo("0"));
            Assert.That(map.afterValue.text, Is.EqualTo("0.1"));
            map.Select(map.nodes[5]);
            Assert.That(map.beforeValue.text, Is.EqualTo("0.00%"));
            Assert.That(map.afterValue.text, Is.EqualTo("1.00%"));
            map.Purchase(map.nodes[5]);
            Assert.That(map.beforeValue.text, Is.EqualTo("1.00%"));
            Assert.That(map.afterValue.text, Is.EqualTo("25.00%"));
            map.Select(map.nodes[11]);
            Assert.That(map.beforeValue.text, Is.EqualTo("0.00%"), "A new placement has no effect before purchase.");
            Assert.That(map.afterValue.text, Is.EqualTo("1.00%"));
            var band=map.tooltipPanel.Find("EffectBand").GetComponent<UnityEngine.UI.Image>();
            var header=map.tooltipPanel.Find("TopFrame_Dark").GetComponent<UnityEngine.UI.Image>();
            Assert.That(band.sprite.name, Is.EqualTo("BasicFrame_Rectangle02_s_Navy"));
            Assert.That(header.enabled, Is.False);
            Assert.That(map.title.gameObject.activeInHierarchy, Is.True);
            Assert.That(header.sprite, Is.EqualTo(band.sprite));
            Assert.That(band.sprite.border.y, Is.GreaterThan(0));
            Assert.That(band.sprite.border.w, Is.EqualTo(band.sprite.border.y));
            Assert.That(((RectTransform)header.transform).sizeDelta.x, Is.EqualTo(-48));
            yield return SceneManager.UnloadSceneAsync(scene);
#else
            yield return null;
#endif
        }
        [UnityTest]
        public IEnumerator EffectBandRendersRoundedCorners()
        {
#if UNITY_EDITOR
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("A graphics device is required to verify rendered corners.");
            var scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/CursorHunter/Progression/Scenes/ResearchPrototype.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            var map = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ResearchTestMap>(true)).Single();
            map.Purchase(map.nodes[0]);
            map.Select(map.nodes[1]);
            var cameraObject = new GameObject("TooltipRenderTestCamera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            var target = new RenderTexture(1280, 720, 24);
            camera.targetTexture = target;
            var canvas = map.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            Canvas.ForceUpdateCanvases();
            var band = (RectTransform)map.tooltipPanel.Find("EffectBand");
            var corners = new Vector3[4];
            band.GetWorldCorners(corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            camera.Render();
            var oldTarget = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            image.Apply();
            float Sample(float x, float y) => image.GetPixel(
                Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(min.x, max.x, x)), 0, image.width - 1),
                Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(min.y, max.y, y)), 0, image.height - 1)).grayscale;
            float centerTop = Sample(.5f, .98f);
            float centerBottom = Sample(.5f, .02f);
            Assert.That(Sample(.01f, .98f), Is.GreaterThan(centerTop + .015f), "upper-left corner");
            Assert.That(Sample(.99f, .98f), Is.GreaterThan(centerTop + .015f), "upper-right corner");
            Assert.That(Sample(.01f, .02f), Is.GreaterThan(centerBottom + .015f), "lower-left corner");
            Assert.That(Sample(.99f, .02f), Is.GreaterThan(centerBottom + .015f), "lower-right corner");
            RenderTexture.active = oldTarget;
            camera.targetTexture = null;
            Object.Destroy(image);
            Object.Destroy(target);
            Object.Destroy(cameraObject);
            yield return SceneManager.UnloadSceneAsync(scene);
#else
            yield return null;
#endif
        }
        [UnityTest]
        public IEnumerator TooltipSwaysOnOpeningAndSuccessfulUpgrade()
        {
#if UNITY_EDITOR
            var scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/CursorHunter/Progression/Scenes/ResearchPrototype.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            var map = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ResearchTestMap>(true)).Single();
            var node = map.nodes[0];
            var initialRotation = map.tooltipPanel.localRotation;
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            node.OnPointerEnter(pointer);
            Assert.That(map.tooltipRoot.activeSelf, Is.True);
            yield return new WaitForSecondsRealtime(.05f);
            Assert.That(Quaternion.Angle(map.tooltipPanel.localRotation, initialRotation), Is.GreaterThan(.1f));
            yield return new WaitForSecondsRealtime(.45f);
            Assert.That(Quaternion.Angle(map.tooltipPanel.localRotation, initialRotation), Is.LessThan(.01f));
            node.OnPointerClick(pointer);
            Assert.That(node.rank, Is.EqualTo(1));
            Assert.That(map.beforeValue.text, Is.EqualTo("2"));
            yield return new WaitForSecondsRealtime(.05f);
            Assert.That(Quaternion.Angle(map.tooltipPanel.localRotation, initialRotation), Is.GreaterThan(.1f));
            node.OnPointerExit(pointer);
            Assert.That(map.tooltipRoot.activeSelf, Is.False);
            Assert.That(Quaternion.Angle(map.tooltipPanel.localRotation, initialRotation), Is.LessThan(.01f));
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(Quaternion.Angle(map.tooltipPanel.localRotation, initialRotation), Is.LessThan(.01f));
            yield return SceneManager.UnloadSceneAsync(scene);
#else
            yield return null;
#endif
        }
        [UnityTest]
        public IEnumerator BoardDragsWithoutBuyingAndZoomsAroundPointer()
        {
#if UNITY_EDITOR
            var scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/CursorHunter/Progression/Scenes/ResearchPrototype.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            var map = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ResearchTestMap>(true)).Single();
            var nav = map.navigation;
            Assert.That(nav, Is.Not.Null);
            Assert.That(ExecuteEvents.GetEventHandler<IDragHandler>(map.nodes[0].gameObject), Is.EqualTo(nav.gameObject));
            Assert.That(map.transform.Find("Background").GetComponent<UnityEngine.UI.Image>().raycastTarget, Is.True);
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, pointerId = -1 };
            Vector2 ScreenAt(Vector2 p) => RectTransformUtility.WorldToScreenPoint(null, nav.viewport.TransformPoint(p));
            pointer.position = ScreenAt(Vector2.zero);
            map.nodes[0].OnPointerEnter(pointer);
            Assert.That(map.tooltipRoot.activeSelf, Is.True);
            nav.OnBeginDrag(pointer);
            Assert.That(nav.IsDragging, Is.True);
            Assert.That(map.tooltipRoot.activeSelf, Is.False);
            pointer.position = ScreenAt(new Vector2(100f, 60f));
            nav.OnDrag(pointer);
            Assert.That(nav.board.localPosition.x, Is.EqualTo(100f).Within(.1f));
            Assert.That(nav.board.localPosition.y, Is.EqualTo(60f).Within(.1f));
            map.nodes[0].OnPointerClick(pointer);
            Assert.That(map.nodes[0].rank, Is.Zero);
            nav.OnEndDrag(pointer);
            Assert.That(nav.IsDragging, Is.False);
            pointer.position = ScreenAt(new Vector2(130f, 75f));
            Assert.That(RectTransformUtility.ScreenPointToLocalPointInRectangle(nav.board, pointer.position, null, out Vector2 before), Is.True);
            pointer.scrollDelta = Vector2.up;
            nav.OnScroll(pointer);
            Assert.That(nav.Zoom, Is.EqualTo(1.12f).Within(.001f));
            Assert.That(RectTransformUtility.ScreenPointToLocalPointInRectangle(nav.board, pointer.position, null, out Vector2 after), Is.True);
            Assert.That(Vector2.Distance(before, after), Is.LessThan(.01f));
            pointer.scrollDelta = Vector2.down;
            nav.OnScroll(pointer);
            Assert.That(nav.Zoom, Is.EqualTo(1f).Within(.001f));
            for (int i = 0; i < 50; i++) { pointer.scrollDelta = Vector2.up; nav.OnScroll(pointer); }
            Assert.That(nav.Zoom, Is.EqualTo(1.8f).Within(.001f));
            for (int i = 0; i < 50; i++) { pointer.scrollDelta = Vector2.down; nav.OnScroll(pointer); }
            Assert.That(nav.Zoom, Is.EqualTo(.6f).Within(.001f));
            pointer.position = ScreenAt(Vector2.zero);
            nav.OnBeginDrag(pointer);
            pointer.position = ScreenAt(new Vector2(50000f, 50000f));
            nav.OnDrag(pointer);
            Assert.That(nav.board.localPosition.x, Is.LessThan(nav.viewport.rect.xMax + nav.board.rect.width * nav.Zoom));
            Assert.That(nav.board.localPosition.y, Is.LessThan(nav.viewport.rect.yMax + nav.board.rect.height * nav.Zoom));
            nav.OnEndDrag(pointer);
            yield return SceneManager.UnloadSceneAsync(scene);
#else
            yield return null;
#endif
        }
        [UnityTest]
        public IEnumerator ResearchMapPreservesAuthoredTextFontAndStyle()
        {
#if UNITY_EDITOR
            var root = new GameObject("Font Preservation Test", typeof(RectTransform));
            root.SetActive(false);
            var map = root.AddComponent<ResearchTestMap>();
            TMPro.TextMeshProUGUI Label(string name)
            {
                var child = new GameObject(name, typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
                child.transform.SetParent(root.transform, false);
                return child.GetComponent<TMPro.TextMeshProUGUI>();
            }
            map.tooltipPanel = root.GetComponent<RectTransform>();
            map.nodes = System.Array.Empty<ResearchTestNode>();
            map.title = Label("title");
            map.beforeValue = Label("before");
            map.afterValue = Label("after");
            map.costValue = Label("cost");
            map.effectDescription = Label("description");
            var authoredFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(
                "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            Assert.That(authoredFont, Is.Not.Null);
            map.title.font = authoredFont;
            map.title.fontStyle = TMPro.FontStyles.Italic;
            root.SetActive(true);
            yield return null;
            Assert.That(map.title.font, Is.SameAs(authoredFont));
            Assert.That(map.title.fontStyle, Is.EqualTo(TMPro.FontStyles.Italic));
            Object.Destroy(root);
#else
            yield return null;
#endif
        }
        [UnityTest]
        public IEnumerator TooltipExpandsForLargePricesWithoutOverlappingGemIcon()
        {
#if UNITY_EDITOR
            var scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/CursorHunter/Progression/Scenes/ResearchPrototype.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            var map = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ResearchTestMap>(true)).Single();
            var node = map.nodes[0];
            var original = node.definition;
            var definition = Object.Instantiate(original);
            definition.ranks = (ResearchRank[])original.ranks.Clone();
            node.definition = definition;
            float initialWidth = map.tooltipPanel.rect.width;
            try
            {
                foreach (long price in new long[] { 1000L, 100000000L, long.MaxValue })
                {
                    definition.ranks[0].garnetCost = price;
                    map.Select(node);
                    Canvas.ForceUpdateCanvases();
                    map.costValue.ForceMeshUpdate();
                    var icon = (RectTransform)map.gemIcon.transform;
                    var value = map.costValue.rectTransform;
                    var iconCorners = new Vector3[4];
                    var valueCorners = new Vector3[4];
                    icon.GetWorldCorners(iconCorners);
                    value.GetWorldCorners(valueCorners);
                    Assert.That(valueCorners[0].x, Is.GreaterThan(iconCorners[2].x), "Price area must not touch the gemstone icon.");
                    Assert.That(value.anchoredPosition.x + value.rect.width * .5f,
                        Is.EqualTo(map.tooltipPanel.rect.width * .5f - 24f).Within(.01f), "Price stays aligned to the tooltip's right edge.");
                    Assert.That(value.anchoredPosition.x - value.rect.width * .5f -
                        (icon.anchoredPosition.x + icon.rect.width * .5f), Is.EqualTo(12f).Within(.01f));
                    Assert.That(map.costValue.text, Is.EqualTo(price.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)));
                    Assert.That(map.costValue.isTextTruncated, Is.False);
                    if (price == long.MaxValue) Assert.That(map.tooltipPanel.rect.width, Is.GreaterThan(initialWidth));
                }
                definition.ranks[0].garnetCost = 100;
                for (int i = 0; i < definition.ranks.Length; i++) definition.ranks[i].levelDelta = 1234567890123456f;
                map.Select(node);
                Canvas.ForceUpdateCanvases();
                map.afterValue.ForceMeshUpdate();
                var arrowCorners = new Vector3[4];
                var afterCorners = new Vector3[4];
                ((RectTransform)map.arrow.transform).GetWorldCorners(arrowCorners);
                map.afterValue.rectTransform.GetWorldCorners(afterCorners);
                Assert.That(afterCorners[0].x, Is.GreaterThan(arrowCorners[2].x));
                Assert.That(map.tooltipPanel.rect.width, Is.GreaterThan(initialWidth));
                Assert.That(map.costValue.rectTransform.anchoredPosition.x + map.costValue.rectTransform.rect.width * .5f,
                    Is.EqualTo(map.tooltipPanel.rect.width * .5f - 24f).Within(.01f), "The price follows the edge when effects widen the tooltip.");
                Assert.That(map.afterValue.isTextTruncated, Is.False);
                for (int i = 0; i < definition.ranks.Length; i++) definition.ranks[i].levelDelta = original.ranks[i].levelDelta;
                node.rank = definition.ranks.Length;
                map.Select(node);
                Canvas.ForceUpdateCanvases();
                map.costValue.ForceMeshUpdate();
                Assert.That(map.costValue.isTextTruncated, Is.False);
                Assert.That(map.tooltipPanel.rect.width, Is.EqualTo(initialWidth).Within(.01f));
            }
            finally
            {
                node.definition = original;
                Object.Destroy(definition);
            }
            yield return SceneManager.UnloadSceneAsync(scene);
#else
            yield return null;
#endif
        }
        [UnityTest]
        public IEnumerator FramesAndUpgradeIndicatorsFollowPurchaseAvailability()
        {
#if UNITY_EDITOR
            var scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/CursorHunter/Progression/Scenes/ResearchPrototype.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            var map = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ResearchTestMap>(true)).Single();
            Assert.That(map.nodes.All(node => node.icon != null && node.icon.sprite == node.definition.icon), Is.True);
            Assert.That(map.nodes.All(node => node.border != null && node.border.sprite.name == "ItemFrame01_White2" && !node.border.raycastTarget), Is.True);
            Assert.That(map.nodes.All(node => node.upgradeIndicator != null &&
                node.upgradeIndicator.GetComponent<UnityEngine.UI.Image>().sprite.name == "UI_Symbol_Plus_02_Green" &&
                !node.upgradeIndicator.GetComponent<UnityEngine.UI.Image>().raycastTarget), Is.True);
            var first = map.nodes[0];
            var originalDefinition = first.definition;
            first.definition = map.nodes[1].definition;
            first.Refresh();
            Assert.That(first.icon.sprite, Is.EqualTo(first.definition.icon), "The definition selects the icon.");
            first.definition = originalDefinition;
            first.Refresh();
            Assert.That(first.upgradeIndicator.activeSelf, Is.False, "A rank-zero node does not show the plus icon.");
            Assert.That(map.nodes.Skip(1).All(node => !node.gameObject.activeInHierarchy && !node.upgradeIndicator.activeSelf), Is.True);
            map.testGarnet = 100;
            map.RefreshNodes();
            map.Purchase(first);
            Assert.That(map.testGarnet, Is.Zero);
            Assert.That(map.nodes.Where(node => node.gameObject.activeInHierarchy).All(node => !node.upgradeIndicator.activeSelf), Is.True);
            map.testGarnet = 10000;
            map.RefreshNodes();
            Assert.That(first.upgradeIndicator.activeSelf, Is.True);
            Assert.That(map.nodes.Skip(1).Where(node => node.gameObject.activeInHierarchy).All(node => !node.upgradeIndicator.activeSelf), Is.True);
            while (first.rank < first.definition.ranks.Length) map.Purchase(first);
            Assert.That(first.upgradeIndicator.activeSelf, Is.False);
            Assert.That(map.nodes[1].upgradeIndicator.activeSelf, Is.False);
            map.Purchase(map.nodes[1]);
            Assert.That(map.nodes[1].upgradeIndicator.activeSelf, Is.True);
            yield return SceneManager.UnloadSceneAsync(scene);
#else
            yield return null;
#endif
        }
    }
}
