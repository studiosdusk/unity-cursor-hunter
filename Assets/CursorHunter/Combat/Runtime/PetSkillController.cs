using System.Collections.Generic;
using CursorHunter.Contracts;
using Spine;
using Spine.Unity;
using UnityEngine;
using UnityEngine.Rendering;

namespace CursorHunter.Combat
{
    /// <summary>
    /// Runs the scene's single BabyDragon through the shared skill lifecycle.
    /// The Spine object and its child ally ring are never cloned per attack.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    public sealed class PetSkillController : MonoBehaviour, IRunSkillController, ISkillBalanceController
    {
        public const string SkillId = "skill.pet_baby_dragon";
        private const int GroundRingSortingOrder = -1;
        private const float ScanIntervalSeconds = 0.1f;

        string IRunSkillController.Id => SkillId;
        bool IRunSkillController.IsAcquiredForRun(bool savedAcquired) =>
            IsAcquiredForRun(savedAcquired);
        bool IRunSkillController.ConfigureForRun(CombatRunController combat,
            RunId runId, ulong seed, SkillCombatSnapshot skill) =>
            ConfigureForRun(combat, runId, seed, skill);
        void IRunSkillController.StopSkill() => StopSkill();

        [Header("Editor Play Mode test")]
        [SerializeField, InspectorName("Skill Acquired"),
         Tooltip("Treat BabyDragon as acquired in Editor Play Mode without changing saved progress.")]
        private bool editorSkillAcquired;

        [Header("Scene references")]
        [SerializeField] private Transform petRoot;
        [SerializeField] private Transform groundRing;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private GameObject attackEffectPrefab;

        [Header("Base balance")]
        [SerializeField] private PetBaseStat baseStat = new PetBaseStat
        {
            DamageMultiplier = 0.5f,
            AttackIntervalSeconds = 1.2f,
            MoveSpeedWorldUnitsPerSecond = 4f,
            DetectionRadiusWorldUnits = 8f,
            AttackRangeWorldUnits = 1.5f
        };

        [Header("Attack presentation")]
        [SerializeField, Min(0f)] private float impactDelaySeconds = 0.167f;
        [SerializeField, Min(0.1f)] private float attackAnimationSeconds = 0.5f;
        [SerializeField, Min(0.01f)] private float effectEmissionSeconds = 0.3f;
        [SerializeField, Min(0.1f)] private float effectMaximumSeconds = 4f;
        [SerializeField, Min(1)] private int maximumPooledEffects = 8;

        private readonly List<MonsterCombatTarget> _eligibleTargets =
            new List<MonsterCombatTarget>(80);
        private readonly List<ImpactEffect> _activeEffects =
            new List<ImpactEffect>(4);
        private readonly Stack<ImpactEffect> _freeEffects =
            new Stack<ImpactEffect>(4);

        private CombatRunController _combat;
        private SkeletonAnimation _animation;
        private SortingGroup _visualSortingGroup;
        private SortingGroup _groundRingSortingGroup;
        private Camera _sortingCamera;
        private MonsterCombatTarget _target;
        private RunId _runId;
        private PetRuntimeStat _runtime;
        private float _lastElapsedSeconds;
        private float _nextScanAt;
        private float _nextAttackAt;
        private float _impactAt;
        private float _attackEndsAt;
        private bool _configured;
        private SkillBalanceSettings _balance;
        private bool _visible;
        private bool _attackPending;
        private TrackEntry _attackTrack;
        private bool _impactResolved;
        private bool _visualsPaused;
        private float _lastVisualSortingY;
        private bool _hasVisualSortingY;
        private Vector3 _spawnPosition;
        private bool _hasSpawnPosition;
        private string _currentAnimation;

        public bool IsPetVisible => _visible && petRoot != null &&
            petRoot.gameObject.activeInHierarchy;
        public int ActiveEffectCount => _activeEffects.Count;
        internal PetRuntimeStat RuntimeStat => _runtime;
        internal MonsterCombatTarget CurrentTarget => _target;

        private void Awake()
        {
            if (petRoot != null)
            {
                _animation = petRoot.GetComponent<SkeletonAnimation>();
                petRoot.gameObject.SetActive(false);
            }
        }

        internal bool IsAcquiredForRun(bool savedAcquired) =>
            _balance != null ? _balance.Enabled :
            savedAcquired || Application.isEditor && editorSkillAcquired;

        internal bool ConfigureForRun(CombatRunController combat, RunId runId,
            ulong seed, SkillCombatSnapshot skill = default)
        {
            StopSkill();
            if (combat == null || !runId.IsValid || petRoot == null ||
                attackEffectPrefab == null)
                return false;
            if (!string.IsNullOrEmpty(skill.SkillId) &&
                (skill.SkillId != SkillId || !skill.IsValid)) return false;

            // Capture once so a later run starts at the authored scene position,
            // rather than wherever the last target led the pet.
            if (!_hasSpawnPosition)
            {
                _spawnPosition = petRoot.position;
                _hasSpawnPosition = true;
            }
            _combat = combat;
            _runId = runId;
            if (_balance != null) skill = default;
            _runtime = new PetRuntimeStat(BalanceBasis, skill,
                combat.CombatSnapshot.AttackPower,
                combat.RelicModifiers);
            _animation = petRoot.GetComponent<SkeletonAnimation>();
            ConfigureVisualSorting();
            _lastElapsedSeconds = combat.ElapsedSeconds;
            _nextScanAt = combat.ElapsedSeconds;
            _nextAttackAt = combat.ElapsedSeconds;
            _configured = true;
            _visualsPaused = false;
            while (_activeEffects.Count + _freeEffects.Count <
                Mathf.Max(1, maximumPooledEffects))
            {
                GameObject instance = Instantiate(attackEffectPrefab, transform);
                _freeEffects.Push(new ImpactEffect(instance));
            }
            return true;
        }

        private void LateUpdate()
        {
            if (!_configured) return;
            if (_combat == null || !_combat.IsRunActive ||
                _combat.CurrentRunId != _runId)
            {
                StopSkill();
                return;
            }

            if (!_visible) ShowPet();
            if (_combat.IsPaused)
            {
                SetVisualPaused(true);
                return;
            }
            if (!_combat.IsRunning) return;
            SetVisualPaused(false);

            float now = _combat.ElapsedSeconds;
            float delta = Mathf.Max(0f, now - _lastElapsedSeconds);
            _lastElapsedSeconds = now;
            UpdateEffects(now);

            if (_attackPending)
            {
                if (!_impactResolved && now >= _impactAt)
                {
                    _impactResolved = true;
                    ResolveImpact(now);
                }
                // Combat time and Spine playback can advance at different rates.
                // Keep locomotion locked until the actual attack finishes as well.
                if (now < _attackEndsAt ||
                    _attackTrack != null && !_attackTrack.IsComplete) return;
                _attackPending = false;
                _attackTrack = null;
                PlayAnimation("Idle", true);
                // Let the renderer replace the attack pose before moving the root.
                return;
            }

            Vector2 petPosition = petRoot.position;
            if (!IsValidTarget(_target)) _target = null;
            // Keep a valid target. Only scan while idle, at most ten times a second.
            if (_target == null && now >= _nextScanAt)
            {
                SelectNearestTarget(petPosition);
                _nextScanAt = now + ScanIntervalSeconds;
            }

            if (_target != null)
            {
                Vector2 hitPoint = _target.GetClosestHitPoint(petPosition);
                float distanceSquared = (hitPoint - petPosition).sqrMagnitude;
                if (distanceSquared > _runtime.AttackRangeWorldUnits *
                    _runtime.AttackRangeWorldUnits)
                {
                    float distance = Mathf.Sqrt(distanceSquared);
                    float step = Mathf.Min(delta * _runtime.MoveSpeedWorldUnitsPerSecond,
                        distance - _runtime.AttackRangeWorldUnits * 0.9f);
                    MoveTowards(hitPoint, step);
                    PlayAnimation("Walk", true);
                    return;
                }
                Face(hitPoint);
                if (now >= _nextAttackAt && TryStartAttack(now)) return;
                PlayAnimation("Idle", true);
                return;
            }

            PlayAnimation("Idle", true);
        }

        private void ShowPet()
        {
            petRoot.position = _spawnPosition;
            petRoot.gameObject.SetActive(true);
            RefreshVisualSorting();
            _visible = true;
            _currentAnimation = null;
            if (_animation != null) _animation.timeScale = 1f;
            PlayAnimation("Idle", true);
        }

        private bool IsValidTarget(MonsterCombatTarget target)
        {
            return target != null && target.gameObject.activeInHierarchy &&
                target.RunId == _runId && target.IsActive &&
                target.IsRegistered && target.IsVulnerable;
        }

        private void SelectNearestTarget(Vector2 petPosition)
        {
            MonsterCombatTarget.CollectActiveForRun(_runId, _eligibleTargets);
            if (_eligibleTargets.Count == 0) return;
            Physics2D.SyncTransforms();
            float bestDistanceSquared = _runtime.DetectionRadiusWorldUnits *
                _runtime.DetectionRadiusWorldUnits;
            for (int i = 0; i < _eligibleTargets.Count; i++)
            {
                MonsterCombatTarget candidate = _eligibleTargets[i];
                Vector2 point = candidate.GetClosestHitPoint(petPosition);
                float distanceSquared = (point - petPosition).sqrMagnitude;
                if (distanceSquared > bestDistanceSquared) continue;
                bestDistanceSquared = distanceSquared;
                _target = candidate;
            }
        }

        private void MoveTowards(Vector2 destination, float distance)
        {
            if (distance <= 0f) return;
            Vector2 start = petRoot.position;
            Vector2 end = Vector2.MoveTowards(start, destination, distance);
            Face(end);
            Vector3 position = petRoot.position;
            petRoot.position = new Vector3(end.x, end.y, position.z);
            RefreshVisualSorting();
        }

        private void ConfigureVisualSorting()
        {
            _sortingCamera = worldCamera != null ? worldCamera : Camera.main;
            _hasVisualSortingY = false;
            if (_visualSortingGroup == null)
            {
                _visualSortingGroup = petRoot.GetComponent<SortingGroup>();
                if (_visualSortingGroup == null)
                    _visualSortingGroup =
                        petRoot.gameObject.AddComponent<SortingGroup>();
            }
            // Monster VisualRoot uses the Default Sorting Layer.
            if (_visualSortingGroup.sortingLayerID != 0)
                _visualSortingGroup.sortingLayerID = 0;

            ConfigureGroundRingSorting();
        }

        private void ConfigureGroundRingSorting()
        {
            Transform ring = groundRing != null
                ? groundRing : petRoot.Find("MagicCircle1_Green");
            if (ring == null || ring == petRoot || !ring.IsChildOf(petRoot))
                return;

            if (_groundRingSortingGroup == null ||
                _groundRingSortingGroup.transform != ring)
            {
                _groundRingSortingGroup = ring.GetComponent<SortingGroup>();
                if (_groundRingSortingGroup == null)
                    _groundRingSortingGroup =
                        ring.gameObject.AddComponent<SortingGroup>();
            }

            // A nested group normally inherits the pet's 0..199 order.
            // Sort at root keeps the child ring behind every monster group.
            if (!_groundRingSortingGroup.sortAtRoot)
                _groundRingSortingGroup.sortAtRoot = true;
            if (_groundRingSortingGroup.sortingLayerID != 0)
                _groundRingSortingGroup.sortingLayerID = 0;
            if (_groundRingSortingGroup.sortingOrder != GroundRingSortingOrder)
                _groundRingSortingGroup.sortingOrder = GroundRingSortingOrder;
        }

        internal void RefreshVisualSorting()
        {
            if (_visualSortingGroup == null || _sortingCamera == null) return;

            float worldY = petRoot.position.y;
            if (_hasVisualSortingY && _lastVisualSortingY == worldY) return;

            int order = ViewportVisualSorting.OrderAt(
                _sortingCamera, petRoot.position);
            if (_visualSortingGroup.sortingOrder != order)
                _visualSortingGroup.sortingOrder = order;

            _lastVisualSortingY = worldY;
            _hasVisualSortingY = true;
        }

        private void Face(Vector2 destination)
        {
            if (_animation == null || _animation.Skeleton == null) return;
            float difference = destination.x - petRoot.position.x;
            if (Mathf.Abs(difference) < 0.01f) return;
            float magnitude = Mathf.Abs(_animation.Skeleton.ScaleX);
            _animation.Skeleton.ScaleX = magnitude * (difference < 0f ? -1f : 1f);
        }

        private bool TryStartAttack(float now)
        {
            // Enemy transforms can move after the last physics step. Synchronize
            // only when an attack is ready, not on every pursuit frame.
            Physics2D.SyncTransforms();
            Vector2 petPosition = petRoot.position;
            Vector2 hitPoint = _target.GetClosestHitPoint(petPosition);
            if ((hitPoint - petPosition).sqrMagnitude >
                _runtime.AttackRangeWorldUnits * _runtime.AttackRangeWorldUnits)
                return false;
            Face(hitPoint);
            _attackPending = true;
            _impactResolved = false;
            _impactAt = now + Mathf.Max(0f, impactDelaySeconds);
            _attackEndsAt = now + Mathf.Max(
                attackAnimationSeconds, impactDelaySeconds);
            _nextAttackAt = now + _runtime.AttackIntervalSeconds;
            _attackTrack = PlayAnimation("Attack", false);
            return true;
        }

        private void ResolveImpact(float now)
        {
            if (!IsValidTarget(_target))
                return;
            Physics2D.SyncTransforms();
            Vector2 petPosition = petRoot.position;
            Vector2 nearestPoint = _target.GetClosestHitPoint(petPosition);
            if ((nearestPoint - petPosition).sqrMagnitude >
                _runtime.AttackRangeWorldUnits *
                _runtime.AttackRangeWorldUnits)
                return;

            Vector3 effectPosition = _target.TryGetHitEffectBounds(out Bounds bounds)
                ? bounds.center : (Vector3)nearestPoint;
            if (_combat.TryApplyPetDamage(_runId, _target, _runtime.Damage))
                PlayImpactEffect(effectPosition, now);
        }

        private TrackEntry PlayAnimation(string name, bool loop)
        {
            if (_animation == null || loop && _currentAnimation == name) return null;
            TrackEntry entry = _animation.AnimationState.SetAnimation(0, name, loop);
            // Attack is a stationary action: neither the walk pose on entry nor
            // the completed attack on exit should blend into a moving root.
            if (name == "Attack" || entry.MixingFrom != null &&
                entry.MixingFrom.Animation.Name == "Attack")
                entry.MixDuration = 0f;
            _currentAnimation = name;
            return entry;
        }

        private void PlayImpactEffect(Vector3 position, float now)
        {
            if (_freeEffects.Count == 0) return;
            ImpactEffect effect = _freeEffects.Pop();
            effect.Play(position, now, effectEmissionSeconds,
                effectMaximumSeconds);
            _activeEffects.Add(effect);
        }

        private void UpdateEffects(float now)
        {
            for (int i = _activeEffects.Count - 1; i >= 0; i--)
            {
                ImpactEffect effect = _activeEffects[i];
                if (effect.Tick(now)) continue;
                _activeEffects.RemoveAt(i);
                effect.ReturnToPool();
                _freeEffects.Push(effect);
            }
        }

        private void SetVisualPaused(bool paused)
        {
            if (_visualsPaused == paused) return;
            _visualsPaused = paused;
            if (_animation != null) _animation.timeScale = paused ? 0f : 1f;
            for (int i = 0; i < _activeEffects.Count; i++)
                _activeEffects[i].SetPaused(paused);
        }

        private PetBaseStat BalanceBasis => _balance == null ? baseStat :
            SkillBalanceMath.Read<PetBaseStat>(_balance);

        public SkillBalanceSettings CaptureBalanceSettings() =>
            SkillBalanceMath.Capture(SkillId, _balance != null ? _balance.Enabled :
                _configured || Application.isEditor && editorSkillAcquired,
                BalanceBasis, _runtime, _configured);

        public bool ValidateBalanceSettings(SkillBalanceSettings settings, out string error)
        {
            if (!SkillBalanceMath.Validate(settings, CaptureBalanceSettings(), out error))
                return false;
            if (settings.Enabled && (petRoot == null || attackEffectPrefab == null))
            {
                error = "스킬 프리팹·카메라·전투 참조를 확인하세요.";
                return false;
            }
            return true;
        }

        public bool ApplyBalanceSettings(CombatRunController combat, RunId runId,
            ulong seed, SkillBalanceSettings settings)
        {
            if (combat == null || !runId.IsValid ||
                !ValidateBalanceSettings(settings, out _)) return false;
            SkillBalanceSettings next = settings.Clone();
            if (!next.Enabled)
            {
                StopSkill();
                _balance = next;
                return true;
            }
            if (!_configured || _combat != combat || _runId != runId)
            {
                SkillBalanceSettings previousSettings = _balance;
                _balance = next;
                if (ConfigureForRun(combat, runId, seed)) return true;
                _balance = previousSettings;
                return false;
            }

            PetRuntimeStat previous = _runtime;
            _balance = next;
            _runtime = new PetRuntimeStat(BalanceBasis, default,
                combat.CombatSnapshot.AttackPower,
                combat.RelicModifiers);
            float now = combat.ElapsedSeconds;
            _nextAttackAt = SkillBalanceMath.Retime(_nextAttackAt, now,
                previous.AttackIntervalSeconds, _runtime.AttackIntervalSeconds);
            return true;
        }

        public void ClearBalanceSettings() => _balance = null;

        internal void StopSkill()
        {
            for (int i = 0; i < _activeEffects.Count; i++)
            {
                ImpactEffect effect = _activeEffects[i];
                effect.ReturnToPool();
                _freeEffects.Push(effect);
            }
            _activeEffects.Clear();
            _eligibleTargets.Clear();
            _target = null;
            _combat = null;
            _runId = default;
            _runtime = default;
            _configured = false;
            _visible = false;
            _attackPending = false;
            _attackTrack = null;
            _impactResolved = false;
            _visualsPaused = false;
            _currentAnimation = null;
            if (_animation != null) _animation.timeScale = 1f;
            if (petRoot != null) petRoot.gameObject.SetActive(false);
        }

        private void OnDisable() => StopSkill();

        private sealed class ImpactEffect
        {
            private readonly GameObject _root;
            private readonly ParticleSystem[] _particles;
            private float _stopEmissionAt;
            private float _endBy;
            private bool _emitting;

            internal ImpactEffect(GameObject root)
            {
                _root = root;
                _particles = root.GetComponentsInChildren<ParticleSystem>(true);
                for (int i = 0; i < _particles.Length; i++)
                {
                    ParticleSystem.MainModule main = _particles[i].main;
                    main.loop = false;
                    main.playOnAwake = false;
                }
                ReturnToPool();
            }

            internal void Play(Vector3 position, float now,
                float emissionSeconds, float maximumSeconds)
            {
                _root.transform.position = position;
                _root.SetActive(true);
                _stopEmissionAt = now + Mathf.Max(0.01f, emissionSeconds);
                _endBy = now + Mathf.Max(maximumSeconds, emissionSeconds);
                _emitting = true;
                for (int i = 0; i < _particles.Length; i++)
                {
                    _particles[i].Stop(false,
                        ParticleSystemStopBehavior.StopEmittingAndClear);
                    _particles[i].Play(false);
                }
            }

            internal bool Tick(float now)
            {
                if (now >= _endBy) return false;
                if (_emitting && now >= _stopEmissionAt)
                {
                    _emitting = false;
                    for (int i = 0; i < _particles.Length; i++)
                        _particles[i].Stop(false,
                            ParticleSystemStopBehavior.StopEmitting);
                }
                for (int i = 0; i < _particles.Length; i++)
                    if (_particles[i].IsAlive(false)) return true;
                return false;
            }

            internal void SetPaused(bool paused)
            {
                for (int i = 0; i < _particles.Length; i++)
                {
                    if (paused) _particles[i].Pause(false);
                    else
                    {
                        _particles[i].Play(false);
                        if (!_emitting)
                            _particles[i].Stop(false,
                                ParticleSystemStopBehavior.StopEmitting);
                    }
                }
            }

            internal void ReturnToPool()
            {
                for (int i = 0; i < _particles.Length; i++)
                    _particles[i].Stop(false,
                        ParticleSystemStopBehavior.StopEmittingAndClear);
                _root.SetActive(false);
                _emitting = false;
            }
        }
    }
}
