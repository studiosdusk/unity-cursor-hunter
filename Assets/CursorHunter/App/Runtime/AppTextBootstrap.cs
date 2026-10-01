using CursorHunter.Data;
using TMPro;
using UnityEngine;

namespace CursorHunter.App
{
    public static class AppTextBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Apply()
        {
            var font = LocalizedTypography.GetFont(null);
            if (font == null) return;
            foreach (var text in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                text.font = font;
                if (text.GetComponent<LocalizedTmpLabel>() == null)
                    LocalizationCatalog.BindKnown(text, text.text);
            }
        }
    }
}
