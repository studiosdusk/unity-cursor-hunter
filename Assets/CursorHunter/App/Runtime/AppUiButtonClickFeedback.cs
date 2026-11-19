using CursorHunter.Progression;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CursorHunter.App
{
    public sealed class AppUiButtonClickFeedback : MonoBehaviour, IPointerDownHandler
    {
        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData != null && eventData.button == PointerEventData.InputButton.Left)
                TraitAudioFeedback.PlayClick();
        }
    }
}
