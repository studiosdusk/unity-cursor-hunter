using System;
using System.Collections.Generic;
using CursorHunter.Combat;
using CursorHunter.Contracts;
using CursorHunter.Data;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CursorHunter.App
{
    [DefaultExecutionOrder(150)]
    [DisallowMultipleComponent]
    [AddComponentMenu("Cursor Hunter/UI/Gemstone Collection Controller")]
    public sealed class GemstoneCollectionController : MonoBehaviour
    {
        [Serializable] public sealed class HudBinding
        {
            public string currencyId;
            public GameObject row;
            public TMP_Text amountText;
            [Tooltip("수량 텍스트 영역 중심. 확대되는 TMP의 자식으로 두지 마세요.")]
            public RectTransform targetPoint;
            public Vector2 targetOffset;
            public Color numberColor = Color.white;
        }
        [SerializeField] private CombatRunController combat;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private Canvas hudCanvas;
        [SerializeField] private AppUiRootController uiRootController;
        [SerializeField] private RectTransform effectRoot;
        [SerializeField] private GemstoneFlyView flyPrefab;
        [SerializeField] private GemstoneIconCatalog iconCatalog;
        [SerializeField] private HudBinding[] bindings = Array.Empty<HudBinding>();
        [SerializeField] private GemstoneCollectionSettings settings = new GemstoneCollectionSettings();
        [SerializeField] private Transform previewOrigin;
        [SerializeField] private string previewCurrency = "gem.garnet";
        [SerializeField, Min(1)] private long previewAmount = 43;
        [SerializeField, Range(1, 80)] private int previewKills = 1;

        private struct Pending
        {
            public int Row, Medium, Small;
            public long Large;
            public Vector2 Origin;
            public float NextLaunch;
            public bool Empty => Large == 0 && Medium == 0 && Small == 0;
        }
        private Queue<Pending> _queue;
        private GemstoneFlyView[] _pool;
        private GemstoneCounterPresenter[] _counters;
        private Sprite[] _sprites;
        private Vector2[] _destinations;
        private Image[] _glows;
        private long[] _finalAmounts, _previewAmounts;
        private bool[] _previewVisibility;
        private AudioSource[] _voices;
        private Canvas _effectCanvas;
        private CombatRunController _subscribed;
        private GemstoneCollectionSettings _live;
        private RunId _runId;
        private long _lastSequence;
        private uint _random = 0x73C51u;
        private float _clock, _finishLeft, _previewHold, _lastSound = -10f;
        private bool _ready, _finishing, _preview, _hasFocus = true, _dirtySettings = true, _soundPaused;
        private bool _cancelling;
        private UiScreenId _previewScreen;
        private int _active, _garnetActive, _capacity, _queueCapacity, _configuredQueueCapacity, _soundRow = -1;
        public event Action PresentationFinished;
        public bool IsFinishing => _finishing;
        public bool IsPreviewing => _preview;
        public int ActiveCount => _active;
        public int PendingBatches => _queue?.Count ?? 0;
        public int PeakActiveCount { get; private set; }
        public long EmittedSmall { get; private set; }
        public long EmittedMedium { get; private set; }
        public long EmittedLarge { get; private set; }
        public long ArrivedObjects { get; private set; }
        public int QueueGrowthCount { get; private set; }
        public GemstoneCollectionSettings Settings => settings;
        public IReadOnlyList<HudBinding> Bindings => bindings;
        public bool Owns(RunId id) => isActiveAndEnabled && _ready && !_preview && _runId.IsValid && _runId == id;
        public long Shown(string id) { int row = IndexOf(id); return row >= 0 && _ready ? _counters[row].Shown : 0L; }
        public long Arrived(string id) { int row = IndexOf(id); return row >= 0 && _ready ? _counters[row].Arrived : 0L; }
        public long PendingAmount(string id) { int row = IndexOf(id); return row >= 0 && _ready ? _counters[row].Actual - _counters[row].Arrived : 0L; }

        private void OnEnable() { if (Application.isPlaying) Initialize(); }
        private void OnDisable() { Shutdown(); }
        private void OnDestroy() { Shutdown(); }
        private void OnApplicationFocus(bool focus) { _hasFocus = focus; }
        private void OnValidate() { _dirtySettings = true; }

        public string ValidateConfiguration()
        {
            if (combat == null || hudCanvas == null || effectRoot == null || flyPrefab == null || worldCamera == null)
                return "Combat / Camera / HUD Canvas / Effect Root / Fly Prefab 연결을 확인하세요.";
            if (effectRoot.GetComponent<Canvas>() == null) return "Effect Root에는 전용 Canvas가 필요합니다.";
            if (bindings == null || bindings.Length == 0) return "젬스톤 HUD 바인딩이 없습니다.";
            for (int i = 0; i < bindings.Length; i++)
            {
                HudBinding b = bindings[i];
                if (b == null || string.IsNullOrWhiteSpace(b.currencyId) || b.row == null || b.amountText == null || b.targetPoint == null)
                    return "HUD 바인딩의 ID / Row / TMP / TargetPoint 연결을 확인하세요.";
                if (b.targetPoint.IsChildOf(b.amountText.transform)) return "TargetPoint는 확대되는 TMP와 분리해야 합니다.";
                for (int j = 0; j < i; j++) if (bindings[j].currencyId == b.currencyId) return "재화 ID가 중복되었습니다.";
            }
            var s = settings;
            if (s == null || s.sizes == null || s.motion == null || s.counter == null || s.budget == null || s.sound == null)
                return "연출 설정 그룹이 누락되었습니다.";
            if (!(s.sizes.pixels.x > 0 && s.sizes.pixels.y > s.sizes.pixels.x && s.sizes.pixels.z > s.sizes.pixels.y))
                return "젬 크기는 0 < 소 < 중 < 대여야 합니다.";
            if (s.budget.poolSize < 1 || s.budget.maxActive < 1 || s.budget.maxActive > s.budget.poolSize ||
                s.budget.bonusReserved < 0 || s.budget.bonusReserved >= s.budget.maxActive ||
                s.budget.launchesPerFrame < 1 || s.budget.queueCapacity < 1 || s.budget.pressureFlightDuration <= 0)
                return "풀 / 활성 상한 / 추가 젬 예약 / 발사량 / 대기 용량 설정을 확인하세요.";
            if (!Range(s.motion.scatterDuration) || !Range(s.motion.scatterRadius) || !Range(s.motion.holdDuration) ||
                !Range(s.motion.flightDuration) || s.motion.flightDuration.x <= 0 || !Range(s.motion.launchInterval) ||
                !Range(s.motion.curvature) || s.motion.scatterCurve == null || s.motion.attractionCurve == null ||
                s.motion.birthDuration <= 0 || s.motion.shrinkDuration <= 0 || s.motion.fullDistance <= 0 ||
                s.counter.countDuration <= 0 || s.counter.riseDuration <= 0 || s.counter.returnDuration <= 0 ||
                s.counter.minimumPopInterval <= 0 || s.finishDuration < 0 || s.sound.voices < 1 ||
                s.sound.minimumInterval <= 0 || !Range(s.sound.pitch))
                return "시간·범위·곡선의 최솟값과 최댓값을 확인하세요.";
            return null;
        }
        private static bool Range(Vector2 value) => !float.IsNaN(value.x) && !float.IsNaN(value.y) &&
            !float.IsInfinity(value.x) && !float.IsInfinity(value.y) && value.x >= 0 && value.y >= value.x;

        public bool Initialize()
        {
            if (_ready) return true;
            string error = ValidateConfiguration();
            if (error != null) { Debug.LogWarning("Gemstone collection: " + error, this); return false; }
            if (iconCatalog == null) iconCatalog = GemstoneIconCatalog.LoadDefault();
            if (iconCatalog == null) return false;
            RefreshSettings();
            _capacity = settings.budget.poolSize;
            DOTween.Init();
            // Capacity changes are safe only before any owner's tweens are active.
            if (DOTween.TotalActiveTweens() == 0)
                DOTween.SetTweensCapacity(Mathf.Max(DOTween.tweenersCapacity, _capacity + bindings.Length * 2 + 256),
                    Mathf.Max(DOTween.sequencesCapacity, 64));
            _queueCapacity = _configuredQueueCapacity = settings.budget.queueCapacity;
            _queue = new Queue<Pending>(_queueCapacity);
            _counters = new GemstoneCounterPresenter[bindings.Length];
            _sprites = new Sprite[bindings.Length]; _destinations = new Vector2[bindings.Length];
            _glows = new Image[bindings.Length]; _finalAmounts = new long[bindings.Length];
            _previewAmounts = new long[bindings.Length]; _previewVisibility = new bool[bindings.Length];
            _effectCanvas = effectRoot.GetComponent<Canvas>();
            _effectCanvas.overrideSorting = true;
            _effectCanvas.sortingLayerID = hudCanvas.sortingLayerID;
            _effectCanvas.sortingOrder = hudCanvas.sortingOrder + 10;
            for (int i = 0; i < bindings.Length; i++)
            {
                HudBinding b = bindings[i];
                _sprites[i] = iconCatalog.GetIcon(b.currencyId);
                var glow = new GameObject("AbsorbGlow_" + b.currencyId, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                glow.transform.SetParent(effectRoot, false);
                _glows[i] = glow.GetComponent<Image>(); _glows[i].sprite = _sprites[i];
                _glows[i].raycastTarget = false; _glows[i].preserveAspect = true;
                var foreground = b.amountText.GetComponent<Canvas>();
                if (foreground == null) foreground = b.amountText.gameObject.AddComponent<Canvas>();
                foreground.overrideSorting = true; foreground.sortingLayerID = hudCanvas.sortingLayerID;
                foreground.sortingOrder = hudCanvas.sortingOrder + 11;
                b.amountText.color = b.numberColor; b.amountText.raycastTarget = false;
                _counters[i] = new GemstoneCounterPresenter(b.amountText, _glows[i]);
            }
            _pool = new GemstoneFlyView[_capacity];
            for (int i = 0; i < _pool.Length; i++)
            {
                _pool[i] = Instantiate(flyPrefab, effectRoot);
                _pool[i].Initialize();
            }
            _voices = new AudioSource[settings.sound.voices];
            for (int i = 0; i < _voices.Length; i++)
            {
                var voice = new GameObject("GemstoneAudio_" + i);
                voice.transform.SetParent(transform, false);
                _voices[i] = voice.AddComponent<AudioSource>();
                _voices[i].playOnAwake = false; _voices[i].spatialBlend = 0f;
                _voices[i].ignoreListenerPause = false;
            }
            _subscribed = combat;
            _subscribed.RewardResolved += HandleReward;
            _subscribed.PresentationReset += HandleCombatReset;
            _ready = true;
            if (combat.IsRunActive) BeginRun(combat);
            return true;
        }

        private void HandleCombatReset()
        {
            // CommitStart emits this while active; normal completion emits it after deactivation.
            if (combat != null && combat.IsRunActive) BeginRun(combat);
        }
        private void RefreshSettings()
        {
            if (!_dirtySettings && _live != null) return;
            if (ValidateConfiguration() != null && _live != null) { _dirtySettings = false; return; }
            _live = JsonUtility.FromJson<GemstoneCollectionSettings>(JsonUtility.ToJson(settings));
            _dirtySettings = false;
            if (_counters != null)
                for (int i = 0; i < bindings.Length; i++) bindings[i].amountText.color = bindings[i].numberColor;
        }
        public void SettingsChanged() { _dirtySettings = true; }

        public void BeginRun(CombatRunController source)
        {
            if (!isActiveAndEnabled || source == null || !source.IsRunActive || !Initialize()) return;
            if (Owns(source.CurrentRunId)) return;
            if (_capacity != settings.budget.poolSize || _voices.Length != settings.sound.voices ||
                _configuredQueueCapacity != settings.budget.queueCapacity)
            {
                Shutdown();
                if (!Initialize() || Owns(source.CurrentRunId)) return;
            }
            CancelPresentation(false, false);
            _runId = source.CurrentRunId; _lastSequence = 0; _clock = 0f;
            ResetStatistics();
            for (int i = 0; i < bindings.Length; i++) _counters[i].Snap(source.GetResourceEarned(bindings[i].currencyId));
            Canvas.ForceUpdateCanvases(); UpdateDestinations();
        }
        private void ResetStatistics()
        {
            EmittedSmall = EmittedMedium = EmittedLarge = ArrivedObjects = 0L;
            PeakActiveCount = QueueGrowthCount = 0; _lastSound = -10f;
        }
        private void HandleReward(CombatRewardResolved reward)
        {
            if (!_ready || !isActiveAndEnabled || combat == null || !combat.IsRunActive || reward.RunId != combat.CurrentRunId) return;
            if (!Owns(reward.RunId))
            {
                BeginRun(combat);
                // Rebinding a HUD mid-run synchronizes facts without replaying old rewards.
                return;
            }
            if (reward.Sequence <= _lastSequence) return;
            _lastSequence = reward.Sequence;
            Vector2 origin = WorldToEffect(reward.WorldPosition, reward.HasWorldPosition);
            AddReward("gem.garnet", reward.Garnet, origin);
            AddReward(reward.BonusCurrencyId, reward.BonusAmount, origin);
        }
        private int IndexOf(string id)
        {
            for (int i = 0; i < bindings.Length; i++) if (bindings[i].currencyId == id) return i;
            return -1;
        }
        private void AddReward(string id, long amount, Vector2 origin)
        {
            if (amount <= 0L) return;
            int row = IndexOf(id); if (row < 0) return;
            var counter = _counters[row];
            if (long.MaxValue - counter.Actual < amount) return;
            counter.Actual += amount;
            bindings[row].row.SetActive(true);
            if (_sprites[row] == null) { counter.Snap(counter.Actual); return; }
            GemstoneDenominations.Split(amount, out long large, out int medium, out int small);
            Enqueue(new Pending { Row = row, Large = large, Medium = medium, Small = small, Origin = origin, NextLaunch = _clock });
        }
        private void Enqueue(Pending job)
        {
            if (_queue.Count >= _queueCapacity)
            {
                _queueCapacity = checked(_queueCapacity * 2);
                var expanded = new Queue<Pending>(_queueCapacity);
                while (_queue.Count > 0) expanded.Enqueue(_queue.Dequeue());
                _queue = expanded;
                QueueGrowthCount++;
            }
            _queue.Enqueue(job);
        }
        private float Random01()
        {
            _random ^= _random << 13; _random ^= _random >> 17; _random ^= _random << 5;
            return (_random & 0xffffff) / 16777216f;
        }
        private float Between(Vector2 range) => Mathf.Lerp(range.x, range.y, Random01());
        private Vector2 WorldToEffect(Vector3 point, bool valid)
        {
            Vector3 screen = valid ? worldCamera.WorldToScreenPoint(point) : new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 1f);
            if (screen.z < 0f) { screen.x = Screen.width - screen.x; screen.y = Screen.height - screen.y; }
            screen.x = Mathf.Clamp(screen.x, 24f, Mathf.Max(24f, Screen.width - 24f));
            screen.y = Mathf.Clamp(screen.y, 24f, Mathf.Max(24f, Screen.height - 24f));
            Camera uiCamera = hudCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : hudCanvas.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(effectRoot, screen, uiCamera, out Vector2 local);
            return local;
        }
        private void UpdateDestinations()
        {
            for (int i = 0; i < bindings.Length; i++)
            {
                HudBinding b = bindings[i];
                if (b.targetPoint != null)
                    _destinations[i] = (Vector2)effectRoot.InverseTransformPoint(b.targetPoint.position) + b.targetOffset;
            }
        }
        private void LateUpdate() { AdvancePresentation(Time.unscaledDeltaTime); }
        public void AdvancePresentation(float seconds)
        {
            if (!_ready || seconds <= 0f || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            bool paused = !_hasFocus || (!_preview && !_finishing && combat != null && combat.IsPaused);
            PauseAudio(paused);
            if (paused || (!_runId.IsValid && !_preview)) return;
            RefreshSettings(); UpdateDestinations();
            _clock += seconds;
            if (!_finishing) LaunchPending();
            _soundRow = -1;
            for (int i = 0; i < _pool.Length; i++)
            {
                GemstoneFlyView view = _pool[i];
                if (!view.Flying || !view.Advance(seconds, _destinations[view.Row], _finishing ? Mathf.Max(0.001f, _finishLeft) : 0f)) continue;
                int row = view.Row;
                _counters[row].Arrive(view.Value, _clock, bindings[row].currencyId == "gem.garnet", _live);
                if (_soundRow < 0 || bindings[row].currencyId != "gem.garnet") _soundRow = row;
                _active--; if (bindings[row].currencyId == "gem.garnet") _garnetActive--;
                ArrivedObjects++; view.Release();
            }
            bool settling = false;
            for (int i = 0; i < _counters.Length; i++)
            {
                _counters[i].Advance(seconds, _destinations[i]); settling |= _counters[i].Settling;
            }
            PlayArrivalSound();
            if (_finishing)
            {
                _finishLeft -= seconds;
                if (_finishLeft <= 0f || _active == 0 && !settling) FinishNow();
            }
            else if (_preview && _active == 0 && _queue.Count == 0 && !settling)
            {
                _previewHold += seconds;
                if (_previewHold >= 1.5f) CancelPresentation(false);
            }
        }
        private void LaunchPending()
        {
            int inspected = _queue.Count, emitted = 0;
            int maxActive = Mathf.Min(_capacity, _live.budget.maxActive);
            bool pressure = inspected >= _live.budget.pressureThreshold;
            while (inspected-- > 0 && emitted < _live.budget.launchesPerFrame && _active < maxActive)
            {
                Pending job = _queue.Dequeue();
                bool garnet = bindings[job.Row].currencyId == "gem.garnet";
                if (job.NextLaunch > _clock || garnet && _garnetActive >= Mathf.Max(1, maxActive - _live.budget.bonusReserved))
                { Enqueue(job); continue; }
                GemstoneFlyView free = null;
                for (int i = 0; i < _pool.Length; i++) if (!_pool[i].Flying) { free = _pool[i]; break; }
                if (free == null) { Enqueue(job); break; }
                int value;
                if (job.Large > 0L) { job.Large--; value = 10; EmittedLarge++; }
                else if (job.Medium > 0) { job.Medium--; value = 5; EmittedMedium++; }
                else { job.Small--; value = 1; EmittedSmall++; }
                float angle = Random01() * Mathf.PI * 2f;
                free.Launch(job.Row, value, _sprites[job.Row], job.Origin, _destinations[job.Row], _live,
                    angle, Between(_live.motion.scatterRadius), Between(_live.motion.curvature) * (Random01() < 0.5f ? -1f : 1f), Random01(), pressure);
                _active++; if (garnet) _garnetActive++; emitted++;
                PeakActiveCount = Mathf.Max(PeakActiveCount, _active);
                if (!job.Empty)
                {
                    job.NextLaunch = _clock + (pressure ? 0f : Between(_live.motion.launchInterval));
                    Enqueue(job);
                }
            }
        }
        public void CompleteRun(RunResult result)
        {
            if (!Owns(result.RunId)) return;
            _queue.Clear();
            for (int i = 0; i < bindings.Length; i++)
            {
                long amount = 0L;
                if (result.Rewards != null)
                    foreach (ResourceRewardSnapshot reward in result.Rewards)
                        if (reward.CurrencyId == bindings[i].currencyId) { amount = reward.Amount; break; }
                _finalAmounts[i] = amount;
                _counters[i].Actual = Math.Max(_counters[i].Actual, amount);
                if (amount > 0L) bindings[i].row.SetActive(true);
            }
            _finishing = true; _finishLeft = _live.finishDuration;
            if (_active == 0 || _finishLeft <= 0f) FinishNow();
        }
        private void FinishNow()
        {
            ClearFlights();
            for (int i = 0; i < bindings.Length; i++) _counters[i].Snap(_finalAmounts[i]);
            _finishing = false; _finishLeft = 0f;
            PresentationFinished?.Invoke();
        }
        public void CancelPresentation(bool clearNumbers = true, bool restoreScreen = true)
        {
            if (!_ready || _cancelling) return;
            _cancelling = true;
            bool restore = _preview;
            ClearFlights(); _finishing = false; _preview = false; _runId = default; _lastSequence = 0;
            for (int i = 0; i < bindings.Length; i++)
            {
                _counters[i].Snap(restore ? _previewAmounts[i] : clearNumbers ? 0L : _counters[i].Actual);
                if (restore && bindings[i].row != null) bindings[i].row.SetActive(_previewVisibility[i]);
            }
            foreach (AudioSource voice in _voices) if (voice != null) voice.Stop();
            _soundPaused = false;
            if (restore && restoreScreen && uiRootController != null &&
                (combat == null || !combat.IsRunActive)) uiRootController.ShowScreen(_previewScreen);
            _cancelling = false;
        }
        private void ClearFlights()
        {
            _queue.Clear(); foreach (GemstoneFlyView view in _pool) if (view != null && view.Flying) view.Release();
            _active = _garnetActive = 0;
        }
        public bool Preview(long amount, int kills = 1, string id = null)
        {
            if (!Application.isPlaying || !isActiveAndEnabled || combat != null && combat.IsRunActive || _finishing || amount < 1L || kills < 1 || amount > long.MaxValue / kills) return false;
            if (!Initialize()) return false;
            CancelPresentation(false); RefreshSettings(); ResetStatistics();
            for (int i = 0; i < bindings.Length; i++)
            {
                long.TryParse(bindings[i].amountText.text, out _previewAmounts[i]);
                _previewVisibility[i] = bindings[i].row.activeSelf;
                _counters[i].Snap(0L);
            }
            if (uiRootController != null)
            {
                _previewScreen = uiRootController.CurrentScreen;
                uiRootController.EnterCombat();
            }
            _preview = true; _previewHold = 0f; _clock = 0f;
            Vector2 origin = WorldToEffect(previewOrigin != null ? previewOrigin.position : Vector3.zero, previewOrigin != null);
            for (int i = 0; i < kills; i++)
                AddReward(id ?? previewCurrency, amount, origin + (kills > 1 ? new Vector2((Random01() - 0.5f) * 300f, (Random01() - 0.5f) * 140f) : Vector2.zero));
            Canvas.ForceUpdateCanvases(); UpdateDestinations();
            return true;
        }
        public void PreviewConfigured() { Preview(previewAmount, previewKills, previewCurrency); }
        private void PauseAudio(bool paused)
        {
            if (_soundPaused == paused || _voices == null) return;
            _soundPaused = paused;
            foreach (AudioSource voice in _voices) { if (paused) voice.Pause(); else voice.UnPause(); }
        }
        private void PlayArrivalSound()
        {
            if (_soundRow < 0 || _clock - _lastSound < _live.sound.minimumInterval || _live.sound.volume <= 0f) return;
            bool bonus = bindings[_soundRow].currencyId != "gem.garnet";
            AudioClip clip = bonus ? _live.sound.bonusClip : _live.sound.commonClip;
            if (clip == null) return;
            AudioSource free = null;
            foreach (AudioSource voice in _voices) if (!voice.isPlaying) { free = voice; break; }
            if (free == null) return;
            float cadence = _clock - _lastSound < 0.35f ? 0.6f : 0f;
            free.clip = clip; free.outputAudioMixerGroup = _live.sound.mixer;
            free.volume = _live.sound.volume;
            free.pitch = Mathf.Lerp(_live.sound.pitch.x, _live.sound.pitch.y, Mathf.Clamp01(cadence + Random01() * 0.4f));
            free.Play(); _lastSound = _clock;
        }
        private void Shutdown()
        {
            if (_subscribed != null)
            {
                _subscribed.RewardResolved -= HandleReward; _subscribed.PresentationReset -= HandleCombatReset;
                _subscribed = null;
            }
            if (!_ready) return;
            CancelPresentation(false);
            foreach (var counter in _counters) counter.Dispose();
            foreach (var view in _pool) if (view != null) DestroyOwned(view.gameObject);
            foreach (var glow in _glows) if (glow != null) DestroyOwned(glow.gameObject);
            foreach (var voice in _voices) if (voice != null) DestroyOwned(voice.gameObject);
            _ready = false;
        }
        private static void DestroyOwned(GameObject item)
        {
            if (Application.isPlaying) Destroy(item); else DestroyImmediate(item);
        }
    }
}
