using UnityEngine;

namespace CursorHunter.Combat
{
    /// <summary>Scales only the visual child, leaving facing, hitboxes and authored sizes intact.</summary>
    [DisallowMultipleComponent]
    public sealed class MonsterSpawnPresentation : MonoBehaviour
    {
        public const float PopupSeconds = 0.25f;
        private Transform _pivot;
        private MonsterCombatTarget _target;
        private CombatRunController _combat;
        private float _elapsed, _delay;
        private bool _manual;
        public bool IsFinished { get; private set; }
        public float Scale => _pivot != null ? _pivot.localScale.x : 1f;

        public void Play(Transform pivot, MonsterCombatTarget target, CombatRunController combat,
            bool manual = false, float delay = 0f)
        {
            _pivot = pivot;
            _target = target;
            _combat = combat;
            _manual = manual;
            _delay = delay;
            _elapsed = 0f;
            IsFinished = false;
            enabled = true;
            Render();
        }

        private void LateUpdate()
        {
            if (_target == null || !_target.IsActive || _combat == null || !_combat.IsRunActive)
            {
                Finish();
                return;
            }
            if (!_manual && _combat.IsRunning) Advance(Time.deltaTime);
        }

        public void ScheduleInitial(float delay)
        {
            _manual = true;
            _delay = delay;
            _elapsed = 0f;
            Render();
        }

        public void Advance(float seconds)
        {
            if (IsFinished || seconds <= 0f || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            _elapsed += seconds;
            Render();
        }

        private void Render()
        {
            if (_pivot == null) { Finish(); return; }
            float time = _elapsed - _delay;
            if (time < 0f) { _pivot.localScale = Vector3.zero; return; }
            float progress = Mathf.Clamp01(time / PopupSeconds);
            if (progress >= 1f) { Finish(); return; }
            float scale;
            if (progress < 0.65f)
            {
                float t = progress / 0.65f;
                scale = Mathf.LerpUnclamped(0.25f, 1.1f, 1f - Mathf.Pow(1f - t, 3f));
            }
            else
            {
                float t = (progress - 0.65f) / 0.35f;
                scale = Mathf.Lerp(1.1f, 1f, t * t * (3f - 2f * t));
            }
            _pivot.localScale = Vector3.one * scale;
        }

        private void Finish()
        {
            IsFinished = true;
            if (_pivot != null) _pivot.localScale = Vector3.one;
            enabled = false;
        }

        private void OnDisable()
        {
            if (_pivot != null) _pivot.localScale = Vector3.one;
        }
    }
}
