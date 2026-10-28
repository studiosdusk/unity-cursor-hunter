using CursorHunter.Data;
using UnityEngine;
using UnityEngine.UI;

namespace CursorHunter.App
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    [AddComponentMenu("Cursor Hunter/UI/Gemstone Icon View")]
    public sealed class GemstoneIconView : MonoBehaviour
    {
        [Tooltip("Leave empty to use the shared gemstone icon catalog.")]
        [SerializeField] private GemstoneIconCatalog iconCatalog;
        [SerializeField] private string gemstoneId = "gem.garnet";
        private Image _image;

        public string GemstoneId => gemstoneId;

        private void OnEnable()
        {
            Refresh();
        }

        public void SetGemstone(string id)
        {
            gemstoneId = id;
            Refresh();
        }

        [ContextMenu("Refresh Gemstone Icon")]
        public void Refresh()
        {
            if (_image == null) _image = GetComponent<Image>();
            GemstoneIconCatalog catalog = iconCatalog != null
                ? iconCatalog : GemstoneIconCatalog.LoadDefault();
            Sprite icon = catalog != null ? catalog.GetIcon(gemstoneId) : null;
            _image.sprite = icon;
            _image.preserveAspect = true;
            _image.enabled = icon != null;
        }
    }
}
