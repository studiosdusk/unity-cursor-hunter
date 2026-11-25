using System;
using System.Globalization;
using System.Collections.Generic;
using CursorHunter.Combat;
using CursorHunter.Contracts;
using CursorHunter.Progression;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CursorHunter.App
{
    /// <summary>Displays this run's drops using the rows already authored in the scene.</summary>
    [DisallowMultipleComponent]
    public sealed class CombatGemstoneList : MonoBehaviour
    {
        [Serializable]
        private sealed class Entry
        {
            public GameObject root;
            public GemstoneIconView icon;
            public TMP_Text amountText;
            [NonSerialized] private long _displayedAmount = -1L;
            public void InvalidateAmount() { _displayedAmount = -1L; }

            public void SetAmount(long amount)
            {
                if (amountText == null || _displayedAmount == amount) return;
                amountText.text = amount.ToString(CultureInfo.InvariantCulture);
                _displayedAmount = amount;
            }
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();
        [SerializeField] private GemstoneCollectionController collectionController;
        [SerializeField] private RunRelicCollectionSettings relicCollection;
        private RunId _runId;
        private CombatRunController _subscribedCombat;
        private TraitScreenController _progression;
        private RectTransform _relicCollectionRoot;
        private RectTransform _relicCollectionContent;
        private ScrollRect _relicCollectionScroller;
        private HorizontalLayoutGroup _relicCollectionLayout;
        private float _appliedRelicIconSize = -1f;
        private float _appliedRelicIconSpacing = -1f;
        private readonly HashSet<string> _collectedRelicIds = new HashSet<string>(System.StringComparer.Ordinal);
        public bool IsFinishingCollection => collectionController != null && collectionController.IsFinishing;

        private void OnEnable()
        {
            ResolveProgression();
            EnsureRelicCollectionRoot();
        }

        private void OnDisable()
        {
            UnsubscribeFromCombat();
            if (collectionController != null) collectionController.CancelPresentation(false, false);
        }

        private void OnDestroy() => UnsubscribeFromCombat();

        public void ShowRunning(CombatRunController combat, MonsterSpawner spawner)
        {
            if (combat == null || !combat.IsRunActive) return;
            if (_runId != combat.CurrentRunId)
            {
                Clear();
                _runId = combat.CurrentRunId;
            }
            SubscribeToCombat(combat);
            EnsureRelicCollectionRoot();
            if (collectionController != null && Application.isPlaying)
                collectionController.BeginRun(combat);

            foreach (Entry entry in entries)
            {
                if (entry == null || entry.root == null || entry.icon == null) continue;
                string id = entry.icon.GemstoneId;
                long earned = combat.GetResourceEarned(id);
                // Keep already earned drops visible if a live balance edit disables their source.
                bool visible = earned > 0L || CanDrop(combat, spawner, id);
                if (collectionController != null && collectionController.Owns(_runId)) entry.InvalidateAmount();
                else entry.SetAmount(earned);
                if (entry.root.activeSelf != visible) entry.root.SetActive(visible);
            }
        }

        public void ShowResult(RunResult result)
        {
            if (!_runId.IsValid || result.RunId != _runId) return;
            if (collectionController != null && collectionController.Owns(_runId))
            {
                collectionController.CompleteRun(result);
                return;
            }
            foreach (Entry entry in entries)
            {
                if (entry == null || entry.root == null || entry.icon == null) continue;
                long earned = 0L;
                foreach (ResourceRewardSnapshot reward in result.Rewards)
                {
                    if (reward.CurrencyId != entry.icon.GemstoneId) continue;
                    earned = reward.Amount;
                    break;
                }

                entry.SetAmount(earned);
                if (earned > 0L) entry.root.SetActive(true);
            }
        }

        public void Clear()
        {
            UnsubscribeFromCombat();
            if (collectionController != null) collectionController.CancelPresentation();
            _runId = default;
            _collectedRelicIds.Clear();
            if (_relicCollectionContent != null)
                for (int i = _relicCollectionContent.childCount - 1; i >= 0; i--)
                {
                    _relicCollectionContent.GetChild(i).DOKill();
                    Destroy(_relicCollectionContent.GetChild(i).gameObject);
                }
            if (_relicCollectionRoot != null) _relicCollectionRoot.gameObject.SetActive(false);
            if (_relicCollectionScroller != null)
                _relicCollectionScroller.horizontalNormalizedPosition = 0f;
            foreach (Entry entry in entries)
            {
                if (entry == null) continue;
                entry.SetAmount(0L);
                if (entry.root != null) entry.root.SetActive(false);
            }
        }

        private void SubscribeToCombat(CombatRunController source)
        {
            if (_subscribedCombat == source) return;
            UnsubscribeFromCombat();
            _subscribedCombat = source;
            _subscribedCombat.RewardResolved += HandleRewardResolved;
        }

        private void UnsubscribeFromCombat()
        {
            if (_subscribedCombat != null)
                _subscribedCombat.RewardResolved -= HandleRewardResolved;
            _subscribedCombat = null;
        }

        private void HandleRewardResolved(CombatRewardResolved reward)
        {
            if (!_runId.IsValid || reward.RunId != _runId ||
                string.IsNullOrWhiteSpace(reward.RelicAwardId) ||
                !_collectedRelicIds.Add(reward.RelicAwardId)) return;

            ResolveProgression();
            EnsureRelicCollectionRoot();
            if (_relicCollectionRoot == null || _relicCollectionContent == null || _progression == null ||
                !_progression.TryGetRelicIcon(reward.RelicAwardId, out Sprite icon)) return;

            GameObject iconObject = new GameObject(
                "CollectedRelic_" + reward.RelicAwardId,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = iconObject.transform as RectTransform;
            rect.SetParent(_relicCollectionContent, false);
            float iconSize = relicCollection != null ? relicCollection.IconSize : 40f;
            rect.sizeDelta = new Vector2(iconSize, iconSize);
            Image image = iconObject.GetComponent<Image>();
            image.sprite = icon;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.enabled = false;
            _relicCollectionRoot.gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_relicCollectionContent);
            if (_relicCollectionScroller != null)
                _relicCollectionScroller.horizontalNormalizedPosition = 1f;
            Canvas.ForceUpdateCanvases();

            void RevealCollectedIcon()
            {
                if (image == null || rect == null) return;
                image.enabled = true;
                rect.localScale = Vector3.one * 0.62f;
                rect.DOScale(1f, 0.28f).SetEase(Ease.OutBack).SetUpdate(true);
            }

            bool flying = collectionController != null && collectionController.PresentRelicDrop(
                icon, reward.WorldPosition, reward.HasWorldPosition, rect, RevealCollectedIcon);
            if (!flying) RevealCollectedIcon();
        }

        private void EnsureRelicCollectionRoot()
        {
            if (_relicCollectionRoot == null)
            {
                if (entries == null || entries.Length == 0 ||
                    entries[0] == null || entries[0].root == null) return;
                Transform parent = entries[0].root.transform.parent;
                if (parent == null) return;

                GameObject root = relicCollection != null ? relicCollection.gameObject :
                    new GameObject("RunRelicCollection", typeof(RectTransform), typeof(RunRelicCollectionSettings));
                if (relicCollection == null) relicCollection = root.GetComponent<RunRelicCollectionSettings>();
                _relicCollectionRoot = root.transform as RectTransform;
                if (_relicCollectionRoot.parent != parent) _relicCollectionRoot.SetParent(parent, false);
                _relicCollectionRoot.anchorMin = _relicCollectionRoot.anchorMax = new Vector2(0f, 1f);
                _relicCollectionRoot.pivot = new Vector2(0f, 0.5f);
                if (root.GetComponent<CanvasRenderer>() == null) root.AddComponent<CanvasRenderer>();
                if (root.GetComponent<Image>() == null) root.AddComponent<Image>();
                if (root.GetComponent<RectMask2D>() == null) root.AddComponent<RectMask2D>();
                LayoutElement layoutElement = root.GetComponent<LayoutElement>();
                if (layoutElement == null) layoutElement = root.AddComponent<LayoutElement>();
                layoutElement.ignoreLayout = true;

                Transform existingContent = root.transform.Find("RelicIcons");
                GameObject content = existingContent != null ? existingContent.gameObject :
                    new GameObject("RelicIcons", typeof(RectTransform), typeof(HorizontalLayoutGroup),
                        typeof(ContentSizeFitter));
                _relicCollectionContent = content.transform as RectTransform;
                if (_relicCollectionContent.parent != _relicCollectionRoot)
                    _relicCollectionContent.SetParent(_relicCollectionRoot, false);
                _relicCollectionContent.anchorMin = new Vector2(0f, 0.5f);
                _relicCollectionContent.anchorMax = new Vector2(0f, 0.5f);
                _relicCollectionContent.pivot = new Vector2(0f, 0.5f);
                _relicCollectionContent.anchoredPosition = Vector2.zero;

                _relicCollectionLayout = content.GetComponent<HorizontalLayoutGroup>();
                if (_relicCollectionLayout == null)
                    _relicCollectionLayout = content.AddComponent<HorizontalLayoutGroup>();
                _relicCollectionLayout.padding = new RectOffset(2, 2, 2, 2);
                _relicCollectionLayout.childAlignment = TextAnchor.MiddleLeft;
                _relicCollectionLayout.childControlWidth = false;
                _relicCollectionLayout.childControlHeight = false;
                _relicCollectionLayout.childForceExpandWidth = false;
                _relicCollectionLayout.childForceExpandHeight = false;
                ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
                if (fitter == null) fitter = content.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                _relicCollectionScroller = root.GetComponent<ScrollRect>();
                if (_relicCollectionScroller == null) _relicCollectionScroller = root.AddComponent<ScrollRect>();
                _relicCollectionScroller.horizontal = true;
                _relicCollectionScroller.vertical = false;
                _relicCollectionScroller.movementType = ScrollRect.MovementType.Clamped;
                _relicCollectionScroller.scrollSensitivity = 30f;
                _relicCollectionScroller.viewport = _relicCollectionRoot;
                _relicCollectionScroller.content = _relicCollectionContent;
                root.SetActive(false);
            }

            RectTransform parentRect = _relicCollectionRoot.parent as RectTransform;
            RectTransform garnet = entries[0].root.transform as RectTransform;
            if (parentRect == null || garnet == null) return;
            const float gap = 16f;
            Vector3 garnetRight = parentRect.InverseTransformPoint(garnet.TransformPoint(
                new Vector3(garnet.rect.xMax, garnet.rect.center.y, 0f)));
            float right = parentRect.rect.xMax;
            RectTransform boundary = relicCollection != null ? relicCollection.RightBoundary : null;
            if (boundary != null)
                right = Mathf.Min(right, parentRect.InverseTransformPoint(boundary.TransformPoint(
                    new Vector3(boundary.rect.xMin, boundary.rect.center.y, 0f))).x);
            float start = garnetRight.x + gap;
            float iconSize = relicCollection != null ? relicCollection.IconSize : 40f;
            float iconSpacing = relicCollection != null ? relicCollection.IconSpacing : 4f;
            Vector2 position = new Vector2(
                start - parentRect.rect.xMin, garnetRight.y - parentRect.rect.yMax);
            Vector2 size = new Vector2(Mathf.Max(0f, right - gap - start), iconSize + 12f);
            if (_relicCollectionRoot.anchoredPosition != position) _relicCollectionRoot.anchoredPosition = position;
            if (_relicCollectionRoot.sizeDelta != size) _relicCollectionRoot.sizeDelta = size;
            if (!Mathf.Approximately(_appliedRelicIconSize, iconSize) ||
                !Mathf.Approximately(_appliedRelicIconSpacing, iconSpacing))
            {
                _appliedRelicIconSize = iconSize;
                _appliedRelicIconSpacing = iconSpacing;
                _relicCollectionContent.sizeDelta = new Vector2(0f, iconSize + 8f);
                _relicCollectionLayout.spacing = iconSpacing;
                for (int i = 0; i < _relicCollectionContent.childCount; i++)
                    if (_relicCollectionContent.GetChild(i) is RectTransform icon)
                        icon.sizeDelta = new Vector2(iconSize, iconSize);
                LayoutRebuilder.MarkLayoutForRebuild(_relicCollectionContent);
            }
            Image viewportImage = _relicCollectionRoot.GetComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0f);
            viewportImage.raycastTarget = true;
        }

        private void ResolveProgression()
        {
            if (_progression == null)
                _progression = FindFirstObjectByType<TraitScreenController>(FindObjectsInactive.Include);
        }

        private static bool CanDrop(CombatRunController combat, MonsterSpawner spawner, string currencyId)
        {
            if (combat.IsBossRun)
                foreach (ResourceRewardSnapshot reward in combat.BossClearRewards)
                    if (reward.CurrencyId == currencyId && reward.Amount > 0) return true;
            foreach (TreasureChestTraitSnapshot chest in combat.TraitChestSpawns)
                if (CanDropFrom(chest.Chest, currencyId)) return true;
            SpawnPlan plan = spawner != null ? spawner.CurrentSpawnPlan : null;
            if (plan == null) return false;
            for (int index = 0; index < plan.Entries.Count; index++)
            {
                SpawnSnapshot snapshot = plan.Entries[index].Snapshot;
                if (!spawner.IsSpeciesEnabled(snapshot.MonsterId)) continue;
                if (CanDropFrom(snapshot, currencyId)) return true;
            }

            return false;
        }

        private static bool CanDropFrom(SpawnSnapshot snapshot, string currencyId) =>
            currencyId == "gem.garnet" && snapshot.GarnetReward > 0L ||
            snapshot.BonusDropCurrencyId == currencyId &&
            snapshot.BonusDropAmount > 0L && snapshot.BonusDropChancePercent > 0f;
    }
}
