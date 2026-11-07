using UnityEngine;

namespace CursorHunter.Combat
{
    public enum LaserEffectPhase { Inactive, Telegraph, Firing, Afterglow }

    public sealed partial class LaserEffect
    {
        [Header("Isometric beam")]
        [SerializeField] private Material beamMaterial;
        [Tooltip("World width before skill, research and battlefield scaling.")]
        [SerializeField, Min(0.01f)] private float diagonalWidthWorldUnits = 1.6f;
        [SerializeField, Range(0.05f, 0.4f)] private float telegraphFraction = 0.18f;
        [SerializeField, Range(0.1f, 0.5f)] private float firingFraction = 0.32f;

        private LaserBeamVisual _beamVisual;
        private bool _diagonal;
        private bool _hasFired;
        private bool _beamFits;
        private float _startedAt;
        private float _duration;
        private float _beamWidth;
        private Vector2 _pathPoint;
        private Vector2 _pathDirection;
        private LaserEffectPhase _phase;

        public LaserEffectPhase Phase => !_active ? LaserEffectPhase.Inactive :
            _diagonal ? _phase : LaserEffectPhase.Firing;
        internal bool HasDiagonalVisual => beamMaterial != null && diagonalWidthWorldUnits > 0f;
        internal float DiagonalWidth(float multiplier) => diagonalWidthWorldUnits * multiplier;

        private void InitializeDiagonalVisual()
        {
            if (beamMaterial == null) return;
            var renderer = visualAnchor.GetComponent<ParticleSystemRenderer>();
            _beamVisual = new LaserBeamVisual(transform, beamMaterial,
                renderer != null ? renderer.sortingLayerID : 0);
        }

        internal bool ActivateDiagonal(LaserSkillController owner, Contracts.RunId runId,
            Vector2 point, Vector2 direction, float z, float now, float duration)
        {
            _owner = owner;
            _runId = runId;
            _diagonal = true;
            _hasFired = false;
            _phase = LaserEffectPhase.Telegraph;
            _startedAt = now;
            _duration = duration;
            _returnAt = now + duration;
            _pathPoint = point;
            _pathDirection = direction.normalized;
            _hitTargets.Clear();
            transform.position = new Vector3(point.x, point.y, z);
            visualAnchor.gameObject.SetActive(false);
            gameObject.SetActive(true);
            _active = true;
            FitDiagonal();
            hitbox.enabled = false;
            if (_beamFits) UpdateDiagonalVisual(now);
            return _beamFits;
        }

        private void FitDiagonal()
        {
            _beamWidth = DiagonalWidth(_owner.EffectiveSizeMultiplier);
            Vector2 start = default, end = default;
            _beamFits = _beamVisual != null && _owner.Battlefield != null &&
                _owner.Battlefield.TryGetBeamSegment(_pathPoint, _pathDirection,
                    _beamWidth * LaserBeamVisual.HaloWidthMultiplier, out start, out end);
            if (!_beamFits) { hitbox.enabled = false; _beamVisual?.Hide(); return; }
            Vector2 center = (start + end) * 0.5f;
            transform.SetPositionAndRotation(new Vector3(center.x, center.y, transform.position.z),
                Quaternion.Euler(0f, 0f, Mathf.Atan2(_pathDirection.y, _pathDirection.x) * Mathf.Rad2Deg - 90f));
            Vector3 scale = hitbox.transform.lossyScale;
            hitbox.size = new Vector2(_beamWidth / Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
                Vector2.Distance(start, end) / Mathf.Max(0.0001f, Mathf.Abs(scale.y)));
            _beamVisual.SetPath(start, end, transform.position.z, _beamWidth);
        }

        private bool TickDiagonal(float now, ContactFilter2D filter)
        {
            if (!_beamFits) return false;
            float progress = Mathf.Max(0f, (now - _startedAt) / _duration);
            bool firstFire = !_hasFired && progress >= telegraphFraction;
            bool firing = progress >= telegraphFraction && progress < telegraphFraction + firingFraction;
            if (firstFire)
            {
                _hasFired = true;
                _phase = LaserEffectPhase.Firing;
                _beamVisual.FirePulse(now);
                CombatAudioFeedback.PlaySkill(LaserSkillController.SkillId);
            }
            // Even a long frame crossing the whole firing phase resolves one shot.
            // The owner checks run expiry first, so a finished run can never be hit.
            hitbox.enabled = firing || firstFire;
            if (hitbox.enabled)
            {
                Physics2D.SyncTransforms();
                if (!HitOverlaps(filter, now)) return false;
            }
            hitbox.enabled = firing;
            if (now >= _returnAt) return false;
            UpdateDiagonalVisual(now);
            return true;
        }

        private void UpdateDiagonalVisual(float now)
        {
            float progress = Mathf.Clamp01((now - _startedAt) / _duration);
            float phaseProgress;
            if (progress < telegraphFraction)
            {
                _phase = LaserEffectPhase.Telegraph;
                phaseProgress = progress / telegraphFraction;
            }
            else if (progress < telegraphFraction + firingFraction)
            {
                _phase = LaserEffectPhase.Firing;
                phaseProgress = (progress - telegraphFraction) / firingFraction;
            }
            else
            {
                _phase = LaserEffectPhase.Afterglow;
                phaseProgress = (progress - telegraphFraction - firingFraction) /
                    (1f - telegraphFraction - firingFraction);
            }
            _beamVisual.Show(_phase, phaseProgress, now - _startedAt, now);
        }

        private void RefreshDiagonal(float now, float duration)
        {
            float progress = Mathf.Clamp01((now - _startedAt) / _duration);
            _duration = duration;
            _startedAt = now - progress * duration;
            _returnAt = _startedAt + duration;
            // Resize the original advertised path; never select another target mid-shot.
            FitDiagonal();
            if (_beamFits) UpdateDiagonalVisual(now);
        }

        private void ResetDiagonal()
        {
            _diagonal = false;
            _hasFired = false;
            _beamFits = false;
            _phase = LaserEffectPhase.Inactive;
            _beamVisual?.Hide();
            if (visualAnchor != null) visualAnchor.gameObject.SetActive(true);
        }
    }
}
