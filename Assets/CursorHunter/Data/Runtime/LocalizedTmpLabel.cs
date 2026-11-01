using TMPro;
using UnityEngine;

namespace CursorHunter.Data
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TextMeshProUGUI))]
    public sealed class LocalizedTmpLabel : MonoBehaviour
    {
        [SerializeField] private string key;
        [SerializeField] private string fallback;
        private TMP_Text _text;
        private object[] _formatArguments;

        public void Bind(string localizationKey, string fallbackText)
        {
            key = localizationKey;
            fallback = fallbackText;
            _formatArguments = null;
            Refresh();
        }

        public void BindFormat(
            string localizationKey,
            string fallbackFormat,
            params object[] arguments)
        {
            key = localizationKey;
            fallback = fallbackFormat;
            _formatArguments = arguments;
            Refresh();
        }

        public void Unbind()
        {
            key = string.Empty;
            fallback = string.Empty;
            _formatArguments = null;
        }

        private void OnEnable() { LocalizationCatalog.Changed += Refresh; Refresh(); }
        private void OnDisable() { LocalizationCatalog.Changed -= Refresh; }
        private void Refresh()
        {
            if (_text == null) _text = GetComponent<TMP_Text>();
            if (_text == null || string.IsNullOrEmpty(key)) return;
            _text.text = _formatArguments == null
                ? LocalizationCatalog.Get(key, fallback)
                : LocalizationCatalog.Format(key, fallback, _formatArguments);
        }
    }
}
