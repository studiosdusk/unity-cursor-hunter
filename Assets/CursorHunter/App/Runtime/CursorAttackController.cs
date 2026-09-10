using CursorHunter.Combat;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace CursorHunter.App
{
    /// <summary>
    /// Routes one left-click from the world cursor to the active combat run.
    /// CombatRunController owns cooldown, overlap resolution, and damage.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CursorAttackController : MonoBehaviour
    {
        private const string CursorObjectName = "cursor_image";
        private const string LegacyCursorObjectName = "cursor_Image";

        [SerializeField] private MainCursorController cursorController;
        [SerializeField] private CombatRunController combatRunController;
        [SerializeField] private Collider2D hitBox;

        private void Awake()
        {
            if (cursorController == null)
            {
                cursorController = GetComponent<MainCursorController>();
            }

            if (cursorController == null)
            {
                cursorController = FindFirstObjectByType<MainCursorController>();
            }

            if (combatRunController == null)
            {
                combatRunController = GetComponent<CombatRunController>();
            }

            if (combatRunController == null)
            {
                combatRunController = FindFirstObjectByType<CombatRunController>();
            }

            if (hitBox == null)
            {
                Transform cursorTransform = transform.Find(CursorObjectName);
                if (cursorTransform == null)
                {
                    cursorTransform = transform.Find(LegacyCursorObjectName);
                }

                if (cursorTransform != null)
                {
                    hitBox = cursorTransform.GetComponent<Collider2D>();
                }
            }

            if (cursorController == null ||
                combatRunController == null ||
                hitBox == null)
            {
                Debug.LogWarning(
                    "CursorAttackController requires cursor, combat, and cursor Collider2D references.",
                    this);
            }
        }

        private void Update()
        {
            if (!CanAttack())
            {
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
            {
                return;
            }

            TryPerformAttack(false);
        }

        /// <summary>
        /// Performs one attack from the current cursor position when invoked by
        /// the prototype test button. UI pointer blocking is bypassed because
        /// the button itself is intentionally the test input.
        /// </summary>
        public void TestAttack()
        {
            TryPerformAttack(true);
        }

        private bool CanAttack()
        {
            return cursorController != null &&
                   combatRunController != null &&
                   hitBox != null &&
                   cursorController.IsCustomCursorActive &&
                   combatRunController.IsRunning;
        }

        private void TryPerformAttack(bool allowUiPointer)
        {
            if (!CanAttack())
            {
                return;
            }

            // Normal mouse attacks must not pass through UI controls. The test
            // button explicitly opts out of this guard above.
            if (!allowUiPointer &&
                EventSystem.current != null &&
                EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            if (!cursorController.RefreshCursorPosition())
            {
                return;
            }

            combatRunController.TryAttack(hitBox);
        }
    }
}
