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
            ResolveReferences();
        }

        private void ResolveReferences()
        {
            if (cursorController == null)
            {
                cursorController = GetComponent<MainCursorController>();
            }

            if (cursorController == null)
            {
                cursorController = FindFirstObjectByType<MainCursorController>(
                    FindObjectsInactive.Include);
            }

            if (combatRunController == null)
            {
                combatRunController = GetComponent<CombatRunController>();
            }

            if (combatRunController == null)
            {
                combatRunController = FindFirstObjectByType<CombatRunController>(
                    FindObjectsInactive.Include);
            }

            if (hitBox == null)
            {
                Transform cursorTransform = transform.Find(CursorObjectName);
                if (cursorTransform == null)
                {
                    cursorTransform = transform.Find(LegacyCursorObjectName);
                }

                if (cursorTransform == null && cursorController != null)
                {
                    cursorTransform = cursorController.CursorTransform;
                }

                if (cursorTransform != null)
                {
                    hitBox = cursorTransform.GetComponent<Collider2D>();
                }
            }

            if (hitBox == null)
            {
                hitBox = GetComponentInChildren<Collider2D>(true);
            }
        }

        private void Update()
        {
            ResolveReferences();

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
            ResolveReferences();

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
