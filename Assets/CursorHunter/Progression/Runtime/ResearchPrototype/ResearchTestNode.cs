using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CursorHunter.Progression.ResearchPrototype
{
    public sealed class ResearchTestNode : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public string placementId;
        public ResearchDefinition definition;
        public ResearchTestMap map;
        [Tooltip("Purchase at least one rank in this many listed prerequisite nodes.")]
        public ResearchTestNode[] prerequisites = Array.Empty<ResearchTestNode>();
        [Min(0)] public int requiredPrerequisites;
        public Image frame;
        public Image icon;
        public GameObject incomingLine;
        public GameObject[] incomingLines = Array.Empty<GameObject>();
        [Tooltip("활성화하면 이 배치의 레벨별 비용을 사용합니다.")]
        public bool usePlacementCosts;
        public ResearchCost[] placementCosts = Array.Empty<ResearchCost>();
        public ResearchMaxSparkles maxSparkles;
        public Image border;
        public GameObject upgradeIndicator;
        [NonSerialized] public int rank;

        public bool TryGetCost(int rankIndex, out ResearchCost cost)
        {
            cost = default;
            if (definition == null || rankIndex < 0 || rankIndex >= definition.ranks.Length) return false;
            if (usePlacementCosts)
            {
                if (placementCosts == null || placementCosts.Length != definition.ranks.Length) return false;
                cost = placementCosts[rankIndex];
            }
            else cost = definition.ranks[rankIndex].cost; // Existing scenes until explicitly converted.
            return ResearchDefinition.IsSupportedCurrency(cost.currencyId) && cost.amount >= 0;
        }

        public bool IsUnlocked
        {
            get
            {
                if (prerequisites == null || prerequisites.Length == 0) return requiredPrerequisites == 0;
                int count = 0;
                foreach (var prerequisite in prerequisites)
                    if (prerequisite != null && prerequisite.rank > 0) count++;
                return count >= requiredPrerequisites;
            }
        }
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (map == null || !IsUnlocked || (map.navigation != null && map.navigation.IsDragging)) return;
            map.SetNodeHover(this, true);
            map.Select(this);
        }
        public void OnPointerExit(PointerEventData eventData)
        {
            if (map == null) return;
            map.SetNodeHover(this, false);
            map.HideTooltip(this);
        }
        private void OnDisable() { if (map != null) map.ResetNodeVisual(this); }
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && map != null &&
                !eventData.dragging && (map.navigation == null || !map.navigation.IsDragging)) map.Purchase(this);
        }
        public void Refresh()
        {
            bool unlocked = IsUnlocked;
            gameObject.SetActive(unlocked);
            if (incomingLine != null) incomingLine.SetActive(unlocked);
            if (incomingLines != null) foreach (var line in incomingLines) if (line != null) line.SetActive(unlocked);
            if (upgradeIndicator != null) upgradeIndicator.SetActive(unlocked && rank > 0 && map != null && map.CanPurchase(this));
            if (!unlocked || frame == null || definition == null || definition.ranks == null) return;
            if (icon != null && definition.presentation != null) icon.sprite = definition.presentation.icon;
            frame.color = rank == 0 ? map.unpurchasedColor : rank < definition.ranks.Length ? map.inProgressColor : map.completedColor;
            if (maxSparkles != null) maxSparkles.SetCompleted(rank > 0 && rank >= definition.ranks.Length);
        }
    }
}
