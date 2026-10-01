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
        public void Bind(string localizationKey, string fallbackText)
        {
            key = localizationKey;
            fallback = fallbackText;
            Refresh();
        }
        private void OnEnable() { LocalizationCatalog.Changed += Refresh; Refresh(); }
        private void OnDisable() { LocalizationCatalog.Changed -= Refresh; }
        private void Refresh()
        {
            if (_text == null) _text = GetComponent<TMP_Text>();
            if (_text != null && !string.IsNullOrEmpty(key)) _text.text = LocalizationCatalog.Get(key, fallback);
        }
    }
}
