using System.Collections.Generic;
using System.Globalization;
using CursorHunter.Data;
using CursorHunter.Contracts;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace CursorHunter.Progression.ResearchPrototype
{
    // An unbound map remains an isolated preview; Main binds the player progression.
    public sealed class ResearchTestMap : MonoBehaviour
    {
        public ResearchTestNode[] nodes;
        [HideInInspector] public string appliedSceneFingerprint;
        [HideInInspector] public ResearchMapDefinition appliedVersion;
        public TextMeshProUGUI title, effectDescription, beforeValue, afterValue, costLabel, costValue, rankInfo, wallet;
        private Vector2 normalBeforePosition, normalAfterPosition, normalCostPosition;
        private float normalTooltipWidth, normalCostWidth, normalCostFontSize;
        private RectTransform effectBand, separator;
        private float normalBandWidth, normalSeparatorWidth, normalDescriptionWidth, normalTitleWidth;
        private float normalBeforeWidth, normalAfterWidth, normalBeforeFontSize, normalAfterFontSize;
        public GameObject arrow, gemIcon, tooltipRoot;
        public RectTransform tooltipPanel;
        public ResearchMapNavigation navigation;
        private ResearchTestNode hoveredNode;
        private LocalizedString localizedTitle, localizedDescription, localizedCostLabel, localizedFree, localizedMax;
        private string freeText = "", maxText = "";
        private string boundTextKey;
        private Tween tooltipTween;
        private Quaternion tooltipBaseRotation = Quaternion.identity;
        private readonly Dictionary<ResearchTestNode, Tween> animations = new Dictionary<ResearchTestNode, Tween>();
        private readonly HashSet<ResearchTestNode> hoveredNodes = new HashSet<ResearchTestNode>();
        [SerializeField, Min(1f)] private float hoverScale = 1.1f;
        [SerializeField, Min(0.01f)] private float hoverDuration = 0.14f;
        private readonly Dictionary<ResearchTestNode, Vector3> baseScales = new Dictionary<ResearchTestNode, Vector3>();
        public long testGarnet = 10000;
        [Tooltip("Preview balances for topaz, amethyst, sapphire, diamond and dragon stone.")]
        public long[] testOtherGemstones = new long[5];
        private Sprite garnetCostSprite;
        private GameInformation baseInformation;
        private readonly long[] lastBalances = new long[6];
        public TraitScreenController progression;
        public TextMeshProUGUI[] gemstoneValues;
        private long lastGarnet = -1;
        private long Garnet => progression != null ? progression.GetResearchGemstoneBalance(0) : testGarnet;
        public Color unpurchasedColor = new Color(.47f, .78f, .95f);
        public Color inProgressColor = new Color(.55f, .86f, .41f);
        public Color completedColor = new Color(1f, .76f, .32f);
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        private void Start()
        {
            if (tooltipPanel != null) tooltipBaseRotation = tooltipPanel.localRotation;
            if (gemIcon != null && gemIcon.TryGetComponent<Image>(out var costImage))
            {
                var catalog = GemstoneIconCatalog.LoadDefault();
                garnetCostSprite = catalog != null ? catalog.GetIcon("gem.garnet") : null;
                if (garnetCostSprite != null) costImage.sprite = garnetCostSprite;
                else garnetCostSprite = costImage.sprite;
            }
            normalBeforePosition = beforeValue.rectTransform.anchoredPosition;
            normalAfterPosition = afterValue.rectTransform.anchoredPosition;
            normalBeforeWidth = beforeValue.rectTransform.sizeDelta.x;
            normalAfterWidth = afterValue.rectTransform.sizeDelta.x;
            normalBeforeFontSize = beforeValue.fontSize;
            normalAfterFontSize = afterValue.fontSize;
            normalCostPosition = costValue.rectTransform.anchoredPosition;
            normalTooltipWidth = tooltipPanel.sizeDelta.x;
            normalCostWidth = costValue.rectTransform.sizeDelta.x;
            normalCostFontSize = costValue.fontSize;
            effectBand = tooltipPanel.Find("EffectBand") as RectTransform;
            separator = tooltipPanel.Find("Separator") as RectTransform;
            if (effectBand != null) normalBandWidth = effectBand.sizeDelta.x;
            if (separator != null) normalSeparatorWidth = separator.sizeDelta.x;
            normalDescriptionWidth = effectDescription.rectTransform.sizeDelta.x;
            normalTitleWidth = title.rectTransform.sizeDelta.x;
            foreach (var node in nodes) if (node != null) baseScales[node] = node.transform.localScale;
            if (progression != null)
                foreach (var node in nodes)
                    if (node != null && node.definition != null && node.definition.ranks != null)
                        node.rank = Mathf.Clamp(progression.GetResearchRank(node.placementId), 0, node.definition.ranks.Length);
            RefreshNodes();
            RefreshWallet();
            if (tooltipRoot != null) tooltipRoot.SetActive(false);
            if (wallet != null) wallet.text = Garnet.ToString("N0", Invariant);
        }
        private void Update()
        {
            if (progression == null) return;
            bool changed = false;
            for (int i = 0; i < lastBalances.Length; i++)
                if (lastBalances[i] != progression.GetResearchGemstoneBalance(i)) { changed = true; break; }
            if (changed)
            {
                RefreshWallet();
                RefreshNodes();
                if (hoveredNode != null) Select(hoveredNode);
            }
        }
        private void RefreshWallet()
        {
            lastGarnet = Garnet;
            if (wallet != null) wallet.text = lastGarnet.ToString("N0", Invariant);
            if (progression == null) return;
            for (int i = 0; i < lastBalances.Length; i++)
            {
                long balance = progression.GetResearchGemstoneBalance(i);
                lastBalances[i] = balance;
                if (gemstoneValues == null || i >= gemstoneValues.Length || gemstoneValues[i] == null) continue;
                gemstoneValues[i].text = balance.ToString("N0", Invariant);
                bool visible = balance > 0 || progression.IsGemstoneAvailable(i);
                gemstoneValues[i].transform.parent.gameObject.SetActive(visible);
            }
        }
        private long GetCostBalance(string currencyId)
        {
            if (progression != null) return progression.GetResearchGemstoneBalance(currencyId);
            if (currencyId == "gem.garnet") return testGarnet;
            string[] ids = { "gem.topaz", "gem.amethyst", "gem.sapphire", "gem.diamond", "gem.dragon" };
            for (int i = 0; i < ids.Length; i++)
                if (ids[i] == currencyId && testOtherGemstones != null && i < testOtherGemstones.Length)
                    return System.Math.Max(0L, testOtherGemstones[i]);
            return 0L;
        }
        private void SpendPreview(ResearchCost cost)
        {
            if (cost.currencyId == "gem.garnet") { testGarnet -= cost.amount; return; }
            string[] ids = { "gem.topaz", "gem.amethyst", "gem.sapphire", "gem.diamond", "gem.dragon" };
            for (int i = 0; i < ids.Length; i++)
                if (ids[i] == cost.currencyId && testOtherGemstones != null && i < testOtherGemstones.Length)
                { testOtherGemstones[i] -= cost.amount; return; }
        }
        public void RefreshNodes()
        {
            if (nodes == null) return;
            foreach (var node in nodes) if (node != null) node.Refresh();
        }

        /// <summary>Button callback: reset the currently displayed map's research ranks.</summary>
        public void ResetResearchProgress()
        {
            if (nodes == null) return;
            if (progression != null && !progression.TryResetResearchProgress(this))
            {
                Debug.LogWarning("연구 진행 상황을 초기화하지 못했습니다. 저장 설정과 연구 맵 연결을 확인해 주세요.", this);
                return;
            }
            if (hoveredNode != null) HideTooltip(hoveredNode);
            foreach (var node in nodes) if (node != null) node.rank = 0;
            RefreshNodes();
            RefreshWallet();
        }
        public void Select(ResearchTestNode node)
        {
            if (node == null || !node.IsUnlocked || node.definition == null || !node.definition.TryValidate(out _)) return;
            bool opening = tooltipRoot != null && (!tooltipRoot.activeSelf || hoveredNode != node);
            hoveredNode = node;
            if (tooltipRoot != null) tooltipRoot.SetActive(true);
            var d = node.definition;
            BindText(d.presentation.textKey);
            bool max = node.rank >= d.ranks.Length;
            float current = d.GetEffectValue(node.rank);
            if (node.rank == 0 && (d.gameplay.kind == ResearchEffectKind.FieldDuration || d.gameplay.kind == ResearchEffectKind.MonsterProduction))
            {
                if (baseInformation == null) baseInformation = GameDataDocument.Load().information;
                if (d.gameplay.kind == ResearchEffectKind.FieldDuration) current = baseInformation.stats.normalFieldDurationSeconds;
                else if (baseInformation.monsters != null)
                    foreach (var monster in baseInformation.monsters)
                        if (monster != null && monster.id == d.gameplay.targetId) { current = monster.productionCount; break; }
            }
            beforeValue.text = Format(d.presentation.valueDisplay, current);
            beforeValue.color = max ? new Color(.65f, .91f, .75f) : Color.white;
            beforeValue.rectTransform.anchoredPosition = max
                ? new Vector2(0f, normalBeforePosition.y) : normalBeforePosition;
            afterValue.gameObject.SetActive(!max);
            afterValue.text = max ? "" : Format(d.presentation.valueDisplay, d.GetEffectValue(node.rank + 1));
            arrow.SetActive(!max);
            ResearchCost cost = max ? default : node.TryGetCost(node.rank, out var shownCost) ? shownCost : default;
            gemIcon.SetActive(!max && cost.amount > 0);
            if (!max && gemIcon.TryGetComponent<Image>(out var costImage))
            {
                var catalog = GemstoneIconCatalog.LoadDefault();
                costImage.sprite = catalog != null ? catalog.GetIcon(cost.currencyId) :
                    cost.currencyId == "gem.garnet" ? garnetCostSprite : null;
            }
            costLabel.gameObject.SetActive(!max);
            costValue.rectTransform.anchoredPosition = max
                ? new Vector2(0f, normalCostPosition.y) : normalCostPosition;
            costValue.alignment = max ? TextAlignmentOptions.Center : TextAlignmentOptions.Right;
            costValue.text = max ? maxText : cost.amount == 0 ? freeText : cost.amount.ToString("N0", Invariant);
            costValue.color = max ? new Color(.94f, .82f, .51f) :
                cost.amount > GetCostBalance(cost.currencyId) ? new Color(1f, .48f, .48f) : Color.white;
            ResizeCostLayout(max);
            PositionTooltip(node);
            rankInfo.text = node.rank + " / " + d.ranks.Length;
            if (wallet != null) wallet.text = Garnet.ToString("N0", Invariant);
            if (opening) AnimateTooltip();
        }
        private void ResizeCostLayout(bool max)
        {
            if (tooltipPanel == null || gemIcon == null) return;
            var icon = gemIcon.transform as RectTransform;
            if (icon == null) return;
            var value = costValue.rectTransform;
            costValue.enableAutoSizing = false;
            costValue.textWrappingMode = TextWrappingModes.NoWrap;
            costValue.overflowMode = TextOverflowModes.Ellipsis;
            costValue.fontSize = normalCostFontSize;

            float width = normalTooltipWidth;
            float valueWidth = normalCostWidth;
            if (!max)
            {
                // Reserve space after the label; the pair is positioned from the right edge below.
                float labelRight = costLabel.rectTransform.anchoredPosition.x + costLabel.rectTransform.rect.width * .5f;
                float minimumValueLeft = labelRight + 12f + icon.rect.width + 12f;
                float preferred = costValue.GetPreferredValues(costValue.text, 0f, 0f).x + 4f;
                valueWidth = Mathf.Max(1f, preferred);
                float maximumWidth = GetMaximumTooltipWidth();
                width = Mathf.Min(maximumWidth, Mathf.Max(normalTooltipWidth, 2f * (minimumValueLeft + valueWidth + 24f)));
                float available = Mathf.Max(1f, width * .5f - minimumValueLeft - 24f);
                if (valueWidth > available)
                {
                    costValue.fontSize = Mathf.Max(5f, normalCostFontSize * (available - 4f) / Mathf.Max(1f, preferred - 4f));
                    valueWidth = available;
                }
            }
            else
            {
                valueWidth = Mathf.Max(normalCostWidth, costValue.GetPreferredValues(costValue.text, 0f, 0f).x + 12f);
                width = Mathf.Max(width, valueWidth + 48f);
                value.anchoredPosition = new Vector2(0f, normalCostPosition.y);
            }
            value.sizeDelta = new Vector2(valueWidth, value.sizeDelta.y);
            // Grow the effect value areas away from the arrow as their numbers grow.
            beforeValue.enableAutoSizing = afterValue.enableAutoSizing = false;
            beforeValue.fontSize = normalBeforeFontSize;
            afterValue.fontSize = normalAfterFontSize;
            beforeValue.fontSizeMin = afterValue.fontSizeMin = 5f;
            beforeValue.fontSizeMax = normalBeforeFontSize;
            afterValue.fontSizeMax = normalAfterFontSize;
            beforeValue.textWrappingMode = afterValue.textWrappingMode = TextWrappingModes.NoWrap;
            beforeValue.overflowMode = afterValue.overflowMode = TextOverflowModes.Ellipsis;
            float beforeWidth = Mathf.Max(normalBeforeWidth, beforeValue.GetPreferredValues(beforeValue.text, 0f, 0f).x + 12f);
            float afterWidth = max ? 0f : Mathf.Max(normalAfterWidth, afterValue.GetPreferredValues(afterValue.text, 0f, 0f).x + 12f);
            float innerGap = Mathf.Abs(normalBeforePosition.x) - normalBeforeWidth * .5f;
            float effectWidth = max ? beforeWidth + 48f : 2f * (innerGap + Mathf.Max(beforeWidth, afterWidth) + 24f);
            float maxPanelWidth = GetMaximumTooltipWidth();
            width = Mathf.Min(maxPanelWidth, Mathf.Max(width, effectWidth));
            float maximumValueWidth = max ? width - 48f : width * .5f - innerGap - 24f;
            beforeValue.enableAutoSizing = beforeWidth > maximumValueWidth;
            afterValue.enableAutoSizing = afterWidth > maximumValueWidth;
            beforeWidth = Mathf.Min(beforeWidth, maximumValueWidth);
            afterWidth = Mathf.Min(afterWidth, maximumValueWidth);
            beforeValue.rectTransform.sizeDelta = new Vector2(beforeWidth, beforeValue.rectTransform.sizeDelta.y);
            beforeValue.rectTransform.anchoredPosition = new Vector2(max ? 0f : -innerGap - beforeWidth * .5f, normalBeforePosition.y);
            if (!max)
            {
                afterValue.rectTransform.sizeDelta = new Vector2(afterWidth, afterValue.rectTransform.sizeDelta.y);
                afterValue.rectTransform.anchoredPosition = new Vector2(innerGap + afterWidth * .5f, normalAfterPosition.y);
            }
            if (!max)
            {
                float valueRight = width * .5f - 24f;
                float valueLeft = valueRight - valueWidth;
                value.anchoredPosition = new Vector2(valueRight - valueWidth * .5f, normalCostPosition.y);
                icon.anchoredPosition = new Vector2(valueLeft - 12f - icon.rect.width * .5f, icon.anchoredPosition.y);
            }
            float expansion = width - normalTooltipWidth;
            tooltipPanel.sizeDelta = new Vector2(width, tooltipPanel.sizeDelta.y);
            if (effectBand != null) effectBand.sizeDelta = new Vector2(normalBandWidth + expansion, effectBand.sizeDelta.y);
            if (separator != null) separator.sizeDelta = new Vector2(normalSeparatorWidth + expansion, separator.sizeDelta.y);
            effectDescription.rectTransform.sizeDelta = new Vector2(normalDescriptionWidth + expansion, effectDescription.rectTransform.sizeDelta.y);
            title.rectTransform.sizeDelta = new Vector2(normalTitleWidth + expansion, title.rectTransform.sizeDelta.y);
        }
        private float GetMaximumTooltipWidth()
        {
            if (!(transform is RectTransform viewport) || tooltipPanel == null) return float.MaxValue;
            float scaleX = viewport.InverseTransformVector(tooltipPanel.TransformVector(Vector3.right)).magnitude;
            float scaleY = viewport.InverseTransformVector(tooltipPanel.TransformVector(Vector3.up)).magnitude;
            float swayAllowance = tooltipPanel.rect.height * scaleY * Mathf.Sin(3f * Mathf.Deg2Rad);
            return Mathf.Max(normalTooltipWidth, (viewport.rect.width - 36f - swayAllowance) / Mathf.Max(.01f, scaleX));
        }
        public bool CanPurchase(ResearchTestNode node)
        {
            if (node == null || !node.IsUnlocked || node.definition == null || node.definition.ranks == null ||
                node.rank < 0 || node.rank >= node.definition.ranks.Length) return false;
            if (!node.definition.TryValidate(out _)) return false;
            if (!ResearchEffectCalculator.RequiredTargetUnlocked(nodes, node.definition,
                    id => { foreach (var candidate in nodes) if (candidate != null && candidate.placementId == id) return candidate.rank; return 0; })) return false;
            if (!node.TryGetCost(node.rank, out ResearchCost cost)) return false;
            return cost.amount >= 0 && GetCostBalance(cost.currencyId) >= cost.amount;
        }
        public void Purchase(ResearchTestNode node)
        {
            if (node == null || !node.IsUnlocked || node.definition == null || node.definition.ranks == null) return;
            Select(node);
            if (!CanPurchase(node)) return;
            if (!node.TryGetCost(node.rank, out ResearchCost cost)) return;
            var previouslyVisible = new HashSet<ResearchTestNode>();
            foreach (var item in nodes) if (item != null && item.gameObject.activeSelf) previouslyVisible.Add(item);
            if (progression != null)
            {
                if (!progression.TryPurchaseResearchRank(node.placementId, node.rank, cost.currencyId, cost.amount)) return;
            }
            else SpendPreview(cost);
            node.rank++;
            RefreshWallet();
            RefreshNodes();
            if (Application.isPlaying)
            {
                AnimateNode(node, false);
                foreach (var item in nodes)
                    if (item != null && item.gameObject.activeSelf && !previouslyVisible.Contains(item)) AnimateNode(item, true);
            }
            Select(node);
            AnimateTooltip();
        }
        public void DismissTooltipForNavigation()
        {
            if (hoveredNode == null) return;
            ResearchTestNode node = hoveredNode;
            SetNodeHover(node, false);
            HideTooltip(node);
        }
        public void RefreshTooltipAfterNavigation()
        {
            if (hoveredNode != null && tooltipRoot != null && tooltipRoot.activeSelf)
                PositionTooltip(hoveredNode);
        }
        public void HideTooltip(ResearchTestNode node)
        {
            if (hoveredNode != node) return;
            hoveredNode = null;
            UnbindText();
            StopTooltipAnimation();
            if (tooltipRoot != null) tooltipRoot.SetActive(false);
        }
        private void AnimateTooltip()
        {
            if (!Application.isPlaying || tooltipPanel == null) return;
            StopTooltipAnimation();
            float angle = 0f;
            Tween RotateTo(float target, float duration) => DOTween.To(() => angle, value =>
            {
                angle = value;
                if (tooltipPanel != null) tooltipPanel.localRotation = tooltipBaseRotation * Quaternion.Euler(0f, 0f, angle);
            }, target, duration).SetEase(Ease.OutSine);
            tooltipTween = DOTween.Sequence().SetUpdate(true).SetRecyclable(false)
                .Append(RotateTo(3f, .07f))
                .Append(RotateTo(-3f, .11f))
                .Append(RotateTo(1.2f, .09f))
                .Append(RotateTo(0f, .09f));
        }
        private void StopTooltipAnimation()
        {
            tooltipTween?.Kill();
            tooltipTween = null;
            if (tooltipPanel != null) tooltipPanel.localRotation = tooltipBaseRotation;
        }
        private void PositionTooltip(ResearchTestNode node)
        {
            if (tooltipPanel == null || !(transform is RectTransform viewport) ||
                !(node.transform is RectTransform nodeRect) || tooltipPanel.parent == null) return;

            // Measure the node in the viewport, including board zoom and hover scale.
            var corners = new Vector3[4];
            nodeRect.GetWorldCorners(corners);
            Vector2 nodeMin = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 nodeMax = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (Vector3 corner in corners)
            {
                Vector3 point = viewport.InverseTransformPoint(corner);
                nodeMin = Vector2.Min(nodeMin, point);
                nodeMax = Vector2.Max(nodeMax, point);
            }

            // Calculate the real bounds at the authored scale and at both extremes
            // of the tooltip's existing sway animation, relative to its pivot.
            var panelToViewport = viewport.worldToLocalMatrix * tooltipPanel.parent.localToWorldMatrix;
            Rect panelRect = tooltipPanel.rect;
            Vector2 minOffset = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 maxOffset = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (float angle in new[] { -3f, 0f, 3f })
            {
                var matrix = panelToViewport * Matrix4x4.TRS(Vector3.zero,
                    tooltipBaseRotation * Quaternion.Euler(0f, 0f, angle), tooltipPanel.localScale);
                foreach (Vector2 point in new[] { panelRect.min, new Vector2(panelRect.xMin, panelRect.yMax),
                             panelRect.max, new Vector2(panelRect.xMax, panelRect.yMin) })
                {
                    Vector3 offset = matrix.MultiplyVector(point);
                    minOffset = Vector2.Min(minOffset, offset);
                    maxOffset = Vector2.Max(maxOffset, offset);
                }
            }

            const float margin = 18f;
            const float gap = 18f;
            Rect bounds = viewport.rect;
            float minX = bounds.xMin + margin - minOffset.x;
            float maxX = bounds.xMax - margin - maxOffset.x;
            float minY = bounds.yMin + margin - minOffset.y;
            float maxY = bounds.yMax - margin - maxOffset.y;
            float Clamp(float value, float min, float max) => min <= max ? Mathf.Clamp(value, min, max) : (min + max) * .5f;
            float centerX = (nodeMin.x + nodeMax.x - minOffset.x - maxOffset.x) * .5f;
            float centerY = (nodeMin.y + nodeMax.y - minOffset.y - maxOffset.y) * .5f;
            float right = nodeMax.x + gap - minOffset.x;
            float left = nodeMin.x - gap - maxOffset.x;
            float above = nodeMax.y + gap - minOffset.y;
            float below = nodeMin.y - gap - maxOffset.y;
            Vector2 pivot;
            if (right <= maxX && right >= minX)
                pivot = new Vector2(right, Clamp(centerY, minY, maxY));
            else if (left >= minX && left <= maxX)
                pivot = new Vector2(left, Clamp(centerY, minY, maxY));
            else if (above <= maxY && above >= minY)
                pivot = new Vector2(Clamp(centerX, minX, maxX), above);
            else if (below >= minY && below <= maxY)
                pivot = new Vector2(Clamp(centerX, minX, maxX), below);
            else
            {
                // If no side can fit, keep as much of the tooltip on screen as possible.
                float side = bounds.xMax - nodeMax.x >= nodeMin.x - bounds.xMin ? right : left;
                pivot = new Vector2(Clamp(side, minX, maxX), Clamp(centerY, minY, maxY));
            }
            Vector3 local = tooltipPanel.parent.InverseTransformPoint(viewport.TransformPoint(pivot));
            tooltipPanel.localPosition = new Vector3(local.x, local.y, tooltipPanel.localPosition.z);
        }
        public void SetNodeHover(ResearchTestNode node, bool hovered)
        {
            if (node == null || !Application.isPlaying) return;
            if (hovered) hoveredNodes.Add(node); else hoveredNodes.Remove(node);
            KillAnimation(node);
            Vector3 basis = GetBaseScale(node);
            animations[node] = node.transform.DOScale(basis * (hovered ? hoverScale : 1f), hoverDuration)
                .SetEase(Ease.OutCubic).SetUpdate(true).SetRecyclable(false);
        }
        private Vector3 GetBaseScale(ResearchTestNode node)
        {
            if (!baseScales.TryGetValue(node, out Vector3 basis))
            {
                basis = node.transform.localScale;
                baseScales[node] = basis;
            }
            return basis;
        }
        private void KillAnimation(ResearchTestNode node)
        {
            if (animations.TryGetValue(node, out Tween tween))
            {
                tween?.Kill();
                animations.Remove(node);
            }
        }
        public void ResetNodeVisual(ResearchTestNode node)
        {
            if (node == null) return;
            KillAnimation(node);
            hoveredNodes.Remove(node);
            if (baseScales.TryGetValue(node, out Vector3 basis)) node.transform.localScale = basis;
            HideTooltip(node);
        }
        private void AnimateNode(ResearchTestNode node, bool reveal)
        {
            KillAnimation(node);
            Vector3 basis = GetBaseScale(node);
            if (reveal) node.transform.localScale = basis * .65f;
            float peak = reveal ? 1.1f : 1.18f;
            float duration = reveal ? .3f : .25f;
            float destination = hoveredNodes.Contains(node) ? hoverScale : 1f;
            animations[node] = DOTween.Sequence().SetUpdate(true).SetRecyclable(false)
                .Append(node.transform.DOScale(basis * peak, duration * .5f).SetEase(Ease.OutCubic))
                .Append(node.transform.DOScale(basis * destination, duration * .5f).SetEase(Ease.OutCubic));
        }
        private void OnDisable()
        {
            UnbindText();
            foreach (var entry in animations)
            {
                entry.Value?.Kill();
                if (entry.Key != null && baseScales.TryGetValue(entry.Key, out Vector3 basis))
                    entry.Key.transform.localScale = basis;
            }
            animations.Clear();
            hoveredNodes.Clear();
            StopTooltipAnimation();
            hoveredNode = null;
            if (tooltipRoot != null) tooltipRoot.SetActive(false);
        }
        private static string Format(ResearchValueDisplay format, float delta)
        {
            switch (format)
            {
                case ResearchValueDisplay.Number: return delta.ToString("0.##", Invariant);
                case ResearchValueDisplay.PercentPoints: return delta.ToString("0.00", Invariant) + "%";
                case ResearchValueDisplay.FractionPercent: return (delta * 100f).ToString("0.00", Invariant) + "%";
                case ResearchValueDisplay.RadiusRatio: return (delta / GameInformation.BaseAttackRadiusWorldUnits).ToString("0.##", Invariant);
                default: return "—";
            }
        }
        private void BindText(string key)
        {
            if (boundTextKey == key) return;
            UnbindText();
            boundTextKey = key;
            title.text = "";
            effectDescription.text = "";
            localizedTitle = new LocalizedString("Research", key + ".title") { FallbackState = FallbackBehavior.UseFallback };
            localizedDescription = new LocalizedString("Research", key + ".description") { FallbackState = FallbackBehavior.UseFallback };
            localizedCostLabel = NewTooltipString("cost");
            localizedFree = NewTooltipString("free");
            localizedMax = NewTooltipString("max");
            localizedTitle.StringChanged += SetLocalizedTitle;
            localizedDescription.StringChanged += SetLocalizedDescription;
            localizedCostLabel.StringChanged += SetLocalizedCostLabel;
            localizedFree.StringChanged += SetLocalizedFree;
            localizedMax.StringChanged += SetLocalizedMax;
        }
        private static LocalizedString NewTooltipString(string key) =>
            new LocalizedString("Research", "research.tooltip." + key) { FallbackState = FallbackBehavior.UseFallback };
        private void SetLocalizedTitle(string value) { title.text = value; RefreshTextLayout(); }
        private void SetLocalizedDescription(string value) { effectDescription.text = value; RefreshTextLayout(); }
        private void SetLocalizedCostLabel(string value) { costLabel.text = value; RefreshTextLayout(); }
        private void SetLocalizedFree(string value) { freeText = value; RefreshCostText(); }
        private void SetLocalizedMax(string value) { maxText = value; RefreshCostText(); }
        private void RefreshCostText()
        {
            if (hoveredNode == null || hoveredNode.definition == null) return;
            var d = hoveredNode.definition;
            bool max = hoveredNode.rank >= d.ranks.Length;
            if (max) costValue.text = maxText;
            else if (hoveredNode.TryGetCost(hoveredNode.rank, out var cost) && cost.amount == 0) costValue.text = freeText;
            RefreshTextLayout();
        }
        private void RefreshTextLayout()
        {
            if (hoveredNode == null || tooltipPanel == null) return;
            ResizeCostLayout(hoveredNode.rank >= hoveredNode.definition.ranks.Length);
            PositionTooltip(hoveredNode);
        }
        private void UnbindText()
        {
            if (localizedTitle != null) { localizedTitle.StringChanged -= SetLocalizedTitle; ((System.IDisposable)localizedTitle).Dispose(); }
            if (localizedDescription != null) { localizedDescription.StringChanged -= SetLocalizedDescription; ((System.IDisposable)localizedDescription).Dispose(); }
            if (localizedCostLabel != null) { localizedCostLabel.StringChanged -= SetLocalizedCostLabel; ((System.IDisposable)localizedCostLabel).Dispose(); }
            if (localizedFree != null) { localizedFree.StringChanged -= SetLocalizedFree; ((System.IDisposable)localizedFree).Dispose(); }
            if (localizedMax != null) { localizedMax.StringChanged -= SetLocalizedMax; ((System.IDisposable)localizedMax).Dispose(); }
            localizedTitle = localizedDescription = null;
            localizedCostLabel = localizedFree = localizedMax = null;
            freeText = maxText = "";
            boundTextKey = null;
        }
    }
}
