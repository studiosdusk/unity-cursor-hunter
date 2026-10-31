using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace CursorHunter.App
{
    /// <summary>A warmed pool slot. No completion delegate survives a pool return.</summary>
    public sealed class GemstoneFlyView : MonoBehaviour
    {
        [SerializeField] private Image body;
        [SerializeField] private Image[] trails;
        private RectTransform _rect;
        private Tweener _tween;
        private float _progress;
        private Vector2 _origin, _scatter, _previous, _older;
        private float _scatterTime, _hold, _flight, _total, _arc, _angle, _size;
        private float _birthScale, _arrivalScale, _birthTime, _shrink, _trailAlpha;
        private int _trails;
        private AnimationCurve _scatterCurve, _flightCurve;
        public bool Flying { get; private set; }
        public int Row { get; private set; }
        public long Value { get; private set; }
        public Vector2 Position => _rect.anchoredPosition;

        public void Initialize()
        {
            if (_tween != null) return;
            _rect = (RectTransform)transform;
            _tween = DOTween.To(() => _progress, value => _progress = value, 1f, 1f)
                .SetEase(Ease.Linear).SetAutoKill(false).SetUpdate(UpdateType.Manual).Pause();
            gameObject.SetActive(false);
        }

        public void Launch(int row, long value, Sprite sprite, Vector2 origin, Vector2 destination,
            GemstoneCollectionSettings settings, float scatterAngle, float radial, float bend,
            float variation, bool pressure, Vector2? scatterCenter = null)
        {
            Row = row; Value = value; Flying = true;
            _origin = origin + settings.motion.originOffset;
            _scatter = (scatterCenter ?? _origin) + new Vector2(Mathf.Cos(scatterAngle), Mathf.Sin(scatterAngle)) * radial;
            _previous = _older = _origin;
            _scatterTime = settings.reducedMotion ? 0f : Mathf.Lerp(settings.motion.scatterDuration.x, settings.motion.scatterDuration.y, variation);
            _hold = settings.reducedMotion || pressure ? 0f : Mathf.Lerp(settings.motion.holdDuration.x, settings.motion.holdDuration.y, variation);
            _flight = settings.reducedMotion || pressure ? settings.budget.pressureFlightDuration :
                Mathf.Lerp(settings.motion.flightDuration.x, settings.motion.flightDuration.y,
                    Mathf.Clamp01(Vector2.Distance(origin, destination) / settings.motion.fullDistance));
            if (pressure) _scatterTime *= 0.5f;
            _total = Mathf.Max(0.01f, _scatterTime + _hold + _flight);
            _arc = settings.reducedMotion ? 0f : bend;
            _angle = settings.reducedMotion ? 0f : (variation * 2f - 1f) * settings.motion.rotationDegrees;
            _size = value >= 10 ? settings.sizes.pixels.z : value >= 5 ? settings.sizes.pixels.y : settings.sizes.pixels.x;
            _birthScale = settings.sizes.birthScale; _arrivalScale = settings.sizes.arrivalScale;
            _birthTime = Mathf.Max(0.01f, settings.motion.birthDuration); _shrink = Mathf.Max(0.01f, settings.motion.shrinkDuration);
            // Curves are copied once per configuration revision by the controller, never per slot.
            _scatterCurve = settings.motion.scatterCurve; _flightCurve = settings.motion.attractionCurve;
            _trails = settings.reducedMotion || pressure || !settings.sizes.trails ? 0 : settings.sizes.trailCount;
            _trailAlpha = settings.sizes.trailAlpha;
            body.sprite = sprite; body.color = Color.white;
            body.rectTransform.sizeDelta = Vector2.one * _size;
            for (int i = 0; i < trails.Length; i++)
            {
                trails[i].sprite = sprite;
                trails[i].enabled = false;
                trails[i].rectTransform.sizeDelta = Vector2.one * _size;
            }
            _progress = 0f;
            _tween.ChangeValues(0f, 1f, _total).Restart();
            gameObject.SetActive(true);
            Render(destination);
        }

        public bool Advance(float seconds, Vector2 destination, float finishRemaining)
        {
            if (!Flying) return false;
            float rate = finishRemaining > 0f ? Mathf.Max(1f, (1f - _progress) * _total / finishRemaining) : 1f;
            _tween.ManualUpdate(seconds * rate, seconds * rate);
            Render(destination);
            return _progress >= 1f;
        }

        private void Render(Vector2 destination)
        {
            float time = _progress * _total;
            Vector2 point;
            float rotation = _angle;
            float scale = Mathf.Lerp(_birthScale, 1f, Mathf.Clamp01(time / _birthTime));
            if (_scatterTime > 0f && time < _scatterTime)
                point = Vector2.LerpUnclamped(_origin, _scatter, Mathf.Clamp01(_scatterCurve.Evaluate(time / _scatterTime)));
            else if (time < _scatterTime + _hold) point = _scatter;
            else
            {
                float flight = Mathf.Clamp01((time - _scatterTime - _hold) / Mathf.Max(0.01f, _flight));
                float t = flight >= 1f ? 1f : Mathf.Clamp01(_flightCurve.Evaluate(flight));
                Vector2 direction = destination - _scatter;
                Vector2 normal = direction.sqrMagnitude > 0f ? new Vector2(-direction.y, direction.x).normalized : Vector2.up;
                Vector2 control = (_scatter + destination) * 0.5f + normal * _arc;
                point = (1f - t) * (1f - t) * _scatter + 2f * (1f - t) * t * control + t * t * destination;
                rotation *= 1f - flight;
                scale *= Mathf.Lerp(1f, _arrivalScale, Mathf.Clamp01((time - (_total - _shrink)) / _shrink));
            }
            _rect.anchoredPosition = point;
            body.rectTransform.localScale = Vector3.one * scale;
            body.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rotation);
            for (int i = 0; i < trails.Length; i++)
            {
                Image trail = trails[i];
                trail.enabled = i < _trails && time > _scatterTime + _hold && _progress < 0.9f;
                if (!trail.enabled) continue;
                trail.rectTransform.anchoredPosition = (i == 0 ? _previous : _older) - point;
                trail.rectTransform.localScale = Vector3.one * scale * (i == 0 ? 0.85f : 0.65f);
                trail.rectTransform.localRotation = body.rectTransform.localRotation;
                trail.color = new Color(1f, 1f, 1f, _trailAlpha / (i + 1));
            }
            _older = _previous; _previous = point;
        }

        public void Release()
        {
            Flying = false; _tween?.Pause();
            if (_rect != null) { _rect.anchoredPosition = Vector2.zero; _rect.localScale = Vector3.one; }
            gameObject.SetActive(false);
        }
        private void OnDestroy() { _tween?.Kill(); _tween = null; }
    }
}
