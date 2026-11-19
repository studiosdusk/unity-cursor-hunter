using UnityEngine;

namespace CursorHunter.App
{
    /// <summary>Inspector settings for the relic icons shown during a run.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class RunRelicCollectionSettings : MonoBehaviour
    {
        [SerializeField, Min(16f), InspectorName("유물 아이콘 크기")]
        [Tooltip("전투 중 획득한 유물 아이콘의 너비와 높이입니다.")]
        private float iconSize = 40f;

        [SerializeField, Min(0f), InspectorName("유물 아이콘 간격")]
        private float iconSpacing = 4f;

        [SerializeField, HideInInspector] private RectTransform rightBoundary;

        public float IconSize => Mathf.Max(16f, iconSize);
        public float IconSpacing => Mathf.Max(0f, iconSpacing);
        public RectTransform RightBoundary => rightBoundary;
    }
}
