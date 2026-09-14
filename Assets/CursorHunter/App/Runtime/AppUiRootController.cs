using System;
using System.Collections.Generic;
using UnityEngine;

namespace CursorHunter.App
{
    /// <summary>
    /// Serialized binding between an application screen id and its authored
    /// scene root. The root controller is the only application service that
    /// changes screen visibility.
    /// </summary>
    [Serializable]
    public sealed class UiScreenBinding
    {
        [SerializeField] private UiScreenId screenId;
        [SerializeField] private GameObject root;

        public UiScreenId ScreenId => screenId;

        public GameObject Root => root;

        public bool IsValid => root != null;

        public void SetVisible(bool isVisible)
        {
            if (root != null && root.activeSelf != isVisible)
            {
                root.SetActive(isVisible);
            }
        }
    }

    /// <summary>
    /// Owns application-level screen navigation for the one-scene runtime.
    /// Feature screens keep their own presentation logic; this controller
    /// only selects the active root and prevents multiple base screens from
    /// remaining visible at the same time.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AppUiRootController : MonoBehaviour
    {
        [SerializeField] private UiScreenBinding[] screenBindings =
            new UiScreenBinding[0];
        [SerializeField] private UiScreenId initialScreen = UiScreenId.MainMenu;

        private readonly Dictionary<UiScreenId, UiScreenBinding> _bindings =
            new Dictionary<UiScreenId, UiScreenBinding>();
        private UiScreenId _currentScreen;
        private bool _hasCurrentScreen;
        private bool _bindingsInitialized;

        public event Action<UiScreenId, UiScreenId> ScreenChanged;

        public UiScreenId CurrentScreen => _hasCurrentScreen
            ? _currentScreen
            : initialScreen;

        private void Awake()
        {
            InitializeBindings();
            ShowScreen(initialScreen);
        }

        public void ShowMainMenu()
        {
            ShowScreen(UiScreenId.MainMenu);
        }

        public void ShowFieldSelect()
        {
            ShowScreen(UiScreenId.FieldSelect);
        }

        public void ShowBossSelect()
        {
            ShowScreen(UiScreenId.BossSelect);
        }

        public void ShowTrait()
        {
            ShowScreen(UiScreenId.Trait);
        }

        public void ShowSettings()
        {
            ShowScreen(UiScreenId.Settings);
        }

        public void EnterCombat()
        {
            ShowScreen(UiScreenId.Combat);
        }

        public void CloseFieldSelect()
        {
            ShowMainMenu();
        }

        public void CloseBossSelect()
        {
            ShowMainMenu();
        }

        public void CloseTrait()
        {
            ShowMainMenu();
        }

        public void CloseSettings()
        {
            ShowMainMenu();
        }

        public bool ShowScreen(UiScreenId screenId)
        {
            InitializeBindings();

            if (!_bindings.TryGetValue(screenId, out UiScreenBinding binding) ||
                !binding.IsValid)
            {
                Debug.LogError(
                    $"AppUiRootController has no valid binding for screen " +
                    $"'{screenId}'.",
                    this);
                return false;
            }

            UiScreenId previousScreen = CurrentScreen;
            foreach (UiScreenBinding screenBinding in _bindings.Values)
            {
                screenBinding.SetVisible(false);
            }

            binding.SetVisible(true);
            bool changed = !_hasCurrentScreen || previousScreen != screenId;
            _currentScreen = screenId;
            _hasCurrentScreen = true;

            if (changed)
            {
                ScreenChanged?.Invoke(previousScreen, screenId);
            }

            return true;
        }

        public bool IsVisible(UiScreenId screenId)
        {
            InitializeBindings();

            return _bindings.TryGetValue(screenId, out UiScreenBinding binding) &&
                   binding.IsValid &&
                   binding.Root.activeSelf;
        }

        private void InitializeBindings()
        {
            if (_bindingsInitialized)
            {
                return;
            }

            _bindings.Clear();
            if (screenBindings != null)
            {
                foreach (UiScreenBinding binding in screenBindings)
                {
                    if (binding == null || !binding.IsValid)
                    {
                        Debug.LogWarning(
                            "AppUiRootController contains an empty screen binding.",
                            this);
                        continue;
                    }

                    if (_bindings.ContainsKey(binding.ScreenId))
                    {
                        Debug.LogError(
                            $"AppUiRootController contains duplicate screen " +
                            $"binding '{binding.ScreenId}'.",
                            this);
                        continue;
                    }

                    _bindings.Add(binding.ScreenId, binding);
                }
            }

            _bindingsInitialized = true;
        }
    }
}
