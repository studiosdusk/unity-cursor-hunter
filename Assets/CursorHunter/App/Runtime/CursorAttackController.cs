using System;
using CursorHunter.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CursorHunter.App
{
    /// <summary>
    /// Routes one left-click from the world cursor to every Walker_Stump whose
    /// world position is inside the cursor's world-space Collider2D.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CursorAttackController : MonoBehaviour
    {
        private const string WalkerStumpName = "Walker_Stump";
        private const string CursorObjectName = "cursor_image";
        private const string LegacyCursorObjectName = "cursor_Image";

        [SerializeField] private MainCursorController cursorController;
        [SerializeField] private Collider2D hitBox;

        private void Awake()
        {
            if (cursorController == null)
            {
                cursorController = GetComponent<MainCursorController>();
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

            if (cursorController == null || hitBox == null)
            {
                Debug.LogWarning(
                    "CursorAttackController requires MainCursorController and a world-space cursor Collider2D.",
                    this);
            }
        }

        private void Update()
        {
            if (cursorController == null || !cursorController.IsCustomCursorActive)
            {
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
            {
                return;
            }

            AttackAtCursor();
        }

        private void AttackAtCursor()
        {
            if (hitBox == null || !hitBox.enabled || !hitBox.gameObject.activeInHierarchy)
            {
                return;
            }

            if (!cursorController.RefreshCursorPosition())
            {
                return;
            }

            RegisterWalkerStumpsInScene();
            Physics2D.SyncTransforms();

            for (int index = WalkerStumpTarget.ActiveCount - 1; index >= 0; index--)
            {
                WalkerStumpTarget target = WalkerStumpTarget.GetActiveAt(index);
                if (target == null || target.IsDead)
                {
                    continue;
                }

                if (hitBox.OverlapPoint(target.transform.position))
                {
                    target.ReceiveHit();
                }
            }
        }

        private void RegisterWalkerStumpsInScene()
        {
            Animator[] animators = FindObjectsByType<Animator>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            foreach (Animator animator in animators)
            {
                if (animator == null || !IsWalkerStumpName(animator.gameObject.name))
                {
                    continue;
                }

                if (animator.GetComponent<WalkerStumpTarget>() == null)
                {
                    animator.gameObject.AddComponent<WalkerStumpTarget>();
                }
            }
        }

        private static bool IsWalkerStumpName(string objectName)
        {
            return objectName.StartsWith(WalkerStumpName, StringComparison.Ordinal);
        }
    }
}
