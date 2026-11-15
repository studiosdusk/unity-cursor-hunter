using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace CursorHunter.Progression
{
    /// <summary>Resolves a relic text key from the Excel-backed Relic string table.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TextMeshProUGUI))]
    public sealed class RelicLocalizedLabel : MonoBehaviour
    {
        private const string TableName = "Relic";

        [SerializeField] private TextMeshProUGUI label;
        [SerializeField] private string entryKey;
        private LocalizedString _localizedString;

        private void Awake()
        {
            if (label == null)
                label = GetComponent<TextMeshProUGUI>();
        }

        private void OnEnable() => Subscribe();

        private void OnDisable() => Unsubscribe();

        private void OnDestroy() => Unsubscribe();

        public void Bind(string key)
        {
            if (entryKey == key)
            {
                if (isActiveAndEnabled && _localizedString == null)
                    Subscribe();
                return;
            }

            Unsubscribe();
            entryKey = key;
            if (isActiveAndEnabled)
                Subscribe();
        }

        public void Clear()
        {
            Unsubscribe();
            entryKey = string.Empty;
        }

        private void Subscribe()
        {
            if (label == null)
                label = GetComponent<TextMeshProUGUI>();
            if (label == null || string.IsNullOrWhiteSpace(entryKey) || _localizedString != null)
                return;

            _localizedString = new LocalizedString(TableName, entryKey)
            {
                FallbackState = FallbackBehavior.UseFallback
            };
            _localizedString.StringChanged += Apply;
            _localizedString.RefreshString();
        }

        private void Unsubscribe()
        {
            if (_localizedString == null)
                return;
            _localizedString.StringChanged -= Apply;
            _localizedString = null;
        }

        private void Apply(string value)
        {
            if (label != null)
                label.text = value ?? string.Empty;
        }
    }
}
