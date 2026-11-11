using System;
using System.Collections.Generic;
using CursorHunter.Progression;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CursorHunter.App
{
    [Serializable]
    public sealed class UiScreenBinding
    {
        [SerializeField] private UiScreenId screenId;
        [SerializeField] private GameObject root;
        [Tooltip("World roots sharing visibility with this screen.")]
        [SerializeField] private GameObject[] additionalRoots = Array.Empty<GameObject>();
        public UiScreenId ScreenId => screenId;
        public GameObject Root => root;
        public bool IsValid => root != null;
        public void SetVisible(bool visible)
        {
            if (root != null && root.activeSelf != visible) root.SetActive(visible);
            if (additionalRoots == null) return;
            foreach (GameObject item in additionalRoots)
                if (item != null && item.activeSelf != visible) item.SetActive(visible);
        }
    }

    /// <summary>Owns mutually exclusive application screens. Progression is the launch screen.</summary>
    [DisallowMultipleComponent]
    public sealed class AppUiRootController : MonoBehaviour
    {
        [SerializeField] private UiScreenBinding[] screenBindings = Array.Empty<UiScreenBinding>();
        [SerializeField] private UiScreenId initialScreen = UiScreenId.Trait;
        private readonly Dictionary<UiScreenId, UiScreenBinding> _bindings =
            new Dictionary<UiScreenId, UiScreenBinding>();
        private UiScreenId _currentScreen = UiScreenId.Trait;
        private bool _hasCurrentScreen;
        private bool _bindingsInitialized;
        public event Action<UiScreenId, UiScreenId> ScreenChanged;
        public UiScreenId CurrentScreen => _hasCurrentScreen ? _currentScreen : initialScreen;

        private void Awake()
        {
            InitializeBindings();
            if (!_bindings.ContainsKey(initialScreen) || !_bindings[initialScreen].IsValid)
                initialScreen = UiScreenId.Trait;
            ShowScreen(initialScreen);
        }

        private void OnEnable()
        {
            if (_hasCurrentScreen) AppAmbientMusic.SetEnabled(_currentScreen != UiScreenId.Combat);
        }

        private void Update() => AppAmbientMusic.Tick(Time.unscaledDeltaTime);
        private void OnDisable() => AppAmbientMusic.Stop();

        // Kept as compatibility entry points for older scene buttons. The menu is no longer a destination.
        public void ShowMainMenu() => ShowTrait();
        public void ShowTrait() => ShowScreen(UiScreenId.Trait);
        public void ShowFieldSelect() => ShowScreen(UiScreenId.FieldSelect);
        public void ShowBossSelect() => ShowScreen(UiScreenId.BossSelect);
        public void ShowSettings() => ShowScreen(UiScreenId.Settings);
        public void EnterCombat() => ShowScreen(UiScreenId.Combat);
        public void CloseFieldSelect() => ShowTrait();
        public void CloseBossSelect() => ShowTrait();
        public void CloseTrait() => ShowTrait();
        public void CloseSettings() => ShowTrait();

        public bool ShowScreen(UiScreenId screenId)
        {
            InitializeBindings();
            if (screenId == UiScreenId.MainMenu) screenId = UiScreenId.Trait;
            if (!_bindings.TryGetValue(screenId, out UiScreenBinding target) || !target.IsValid)
            {
                InitializeBindings(true);
                if (!_bindings.TryGetValue(screenId, out target) || !target.IsValid)
                {
                    Debug.LogError("AppUiRootController has no valid binding for screen '" + screenId + "'.", this);
                    return false;
                }
            }
            UiScreenId previous = CurrentScreen;
            foreach (UiScreenBinding binding in _bindings.Values)
                if (binding != target) binding.SetVisible(false);
            target.SetVisible(true);
            AppAmbientMusic.SetEnabled(screenId != UiScreenId.Combat);
            if (screenId != UiScreenId.Trait) InstallButtonAudio(target.Root);
            bool changed = !_hasCurrentScreen || previous != screenId;
            _currentScreen = screenId;
            _hasCurrentScreen = true;
            if (changed) ScreenChanged?.Invoke(previous, screenId);
            return true;
        }

        public bool IsVisible(UiScreenId screenId)
        {
            InitializeBindings();
            return _bindings.TryGetValue(screenId, out UiScreenBinding binding) &&
                binding.IsValid && binding.Root.activeSelf;
        }

        private void InitializeBindings(bool force = false)
        {
            if (_bindingsInitialized && !force) return;
            _bindings.Clear();
            if (screenBindings != null)
            {
                foreach (UiScreenBinding binding in screenBindings)
                {
                    if (binding == null || !binding.IsValid) continue;
                    if (_bindings.ContainsKey(binding.ScreenId))
                    {
                        Debug.LogError("AppUiRootController contains duplicate screen binding '" + binding.ScreenId + "'.", this);
                        continue;
                    }
                    _bindings.Add(binding.ScreenId, binding);
                }
            }
            _bindingsInitialized = true;
        }

        private static void InstallButtonAudio(GameObject root)
        {
            if (root == null) return;
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
                if (button != null && button.GetComponent<AppUiButtonClickFeedback>() == null)
                    button.gameObject.AddComponent<AppUiButtonClickFeedback>();
        }
    }

    public sealed class AppUiButtonClickFeedback : MonoBehaviour, IPointerDownHandler
    {
        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData != null && eventData.button == PointerEventData.InputButton.Left)
                TraitAudioFeedback.PlayClick();
        }
    }

    /// <summary>Loops the authored peaceful theme outside of combat.</summary>
    internal static class AppAmbientMusic
    {
        private const string RootName = "CursorHunter_NonCombatMusic";
        private const string ResourcePath = "Audio/PeacefulFantasyLoop";
        private const float MusicVolume = 0.24f;
        private const float FadeSpeed = 0.32f;
        private static GameObject _root;
        private static AudioSource _source;
        private static float _targetVolume;
        private static bool _missingClipReported;

        public static void Stop()
        {
            _targetVolume = 0f;
            if (_source == null) return;
            _source.Stop();
            _source.volume = 0f;
        }

        public static void SetEnabled(bool enabled)
        {
            if (!Application.isPlaying)
            {
                return;
            }

            EnsureInitialized();
            if (_source == null || _source.clip == null)
            {
                if (!_missingClipReported)
                {
                    Debug.LogWarning(
                        "Non-combat music is missing. Expected Resources/Audio/PeacefulFantasyLoop.wav.");
                    _missingClipReported = true;
                }
                return;
            }

            _targetVolume = enabled ? MusicVolume : 0f;
            if (enabled && !_source.isPlaying)
            {
                _source.volume = 0f;
                _source.Play();
            }
        }

        public static void Tick(float deltaTime)
        {
            if (_source == null || !_source.isPlaying)
            {
                return;
            }

            _source.volume = Mathf.MoveTowards(
                _source.volume,
                _targetVolume,
                Mathf.Max(0f, deltaTime) * FadeSpeed);
            if (_targetVolume <= 0f && _source.volume <= 0.001f)
            {
                _source.Stop();
            }
        }

        private static void EnsureInitialized()
        {
            if (_source != null)
            {
                return;
            }

            _root = GameObject.Find(RootName);
            if (_root == null)
            {
                _root = new GameObject(RootName);
                UnityEngine.Object.DontDestroyOnLoad(_root);
            }

            _source = _root.GetComponent<AudioSource>();
            if (_source == null)
            {
                _source = _root.AddComponent<AudioSource>();
            }

            _source.playOnAwake = false;
            _source.loop = true;
            _source.spatialBlend = 0f;
            _source.volume = 0f;
            _source.priority = 0;
            _source.clip = Resources.Load<AudioClip>(ResourcePath);
        }
    }
}
