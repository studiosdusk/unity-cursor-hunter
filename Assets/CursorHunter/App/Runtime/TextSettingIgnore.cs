using UnityEngine;

namespace CursorHunter.App
{
    /// <summary>
    /// Keeps the TMP font on this GameObject when AppTextBootstrap applies the shared font.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Cursor Hunter/UI/Text Setting Ignore")]
    public sealed class TextSettingIgnore : MonoBehaviour
    {
    }
}
