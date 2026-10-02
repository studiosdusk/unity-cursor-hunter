using CursorHunter.Combat;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace CursorHunter.App
{
    /// <summary>
    /// Routes the cursor's automatic radius attack to the active combat run.
    /// The cursor range follows the live runtime stats during Play Mode.
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
            SyncCursorAttackRange();

            if (!CanAttack())
            {
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null || !Application.isFocused)
            {
                return;
            }

            TryPerformAttack(false);
        }

        private void SyncCursorAttackRange()
        {
            if (cursorController == null || combatRunController == null)
            {
                return;
            }

            PlayerCombatStatsRuntime stats = combatRunController.PlayerCombatStatsRuntime;
            if (stats == null || !combatRunController.IsRunActive)
            {
                return;
            }

            float desiredMultiplier = stats.AttackRangeMultiplier;
            if (!Mathf.Approximately(cursorController.RangeMultiplier, desiredMultiplier))
            {
                cursorController.SetRangeMultiplier(desiredMultiplier);
            }
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
                   combatRunController.PlayerCombatStatsRuntime != null &&
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

            if (allowUiPointer ||
                combatRunController.PlayerCombatStatsRuntime.AutoAttackEnabled)
            {
                combatRunController.TryAttack(hitBox);
            }

            combatRunController.TryUseSkills(hitBox.bounds.center);
        }
    }
}
