using System;
using System.Globalization;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CursorHunter.App
{
    /// <summary>Single writer of a row's number and scale; integer balance never enters a float.</summary>
    internal sealed class GemstoneCounterPresenter : IDisposable
    {
        private readonly TMP_Text _text;
        private readonly Image _glow;
        private readonly Vector3 _baseScale;
        private readonly Tweener _countTween, _popTween;
        private long _from;
        private float _countProgress, _popProgress, _lastPop = -10f, _popFrom = 1f;
        private float _peak, _riseFraction, _glowAlpha, _glowSize;
        private Ease _riseEase, _returnEase;
        public long Actual { get; set; }
        public long Arrived { get; private set; }
        public long Shown { get; private set; }
        public bool Settling => _countTween.IsPlaying() || _popTween.IsPlaying();

        public GemstoneCounterPresenter(TMP_Text text, Image glow)
        {
            _text = text; _glow = glow; _baseScale = text.rectTransform.localScale;
            _countTween = DOTween.To(() => _countProgress, value => _countProgress = value, 1f, 1f)
                .SetEase(Ease.OutCubic).SetAutoKill(false).SetUpdate(UpdateType.Manual).Pause();
            _popTween = DOTween.To(() => _popProgress, value => _popProgress = value, 1f, 1f)
                .SetEase(Ease.Linear).SetAutoKill(false).SetUpdate(UpdateType.Manual).Pause();
            _glow.enabled = false;
        }

        public void Arrive(long value, float clock, bool main, GemstoneCollectionSettings settings)
        {
            Arrived += Math.Min(Math.Max(0L, Actual - Arrived), Math.Max(0L, value));
            if (!_countTween.IsPlaying())
            {
                _from = Shown; _countProgress = 0f;
                _countTween.ChangeValues(0f, 1f, Mathf.Max(0.01f, settings.counter.countDuration)).Restart();
            }
            if (clock - _lastPop < settings.counter.minimumPopInterval) return;
            _lastPop = clock;
            _popFrom = _baseScale.x == 0f ? 1f : _text.rectTransform.localScale.x / _baseScale.x;
            _peak = settings.reducedMotion ? 1.05f : main ? settings.counter.mainScale : settings.counter.secondaryScale;
            _peak = Mathf.Max(_peak, _popFrom);
            float total = Mathf.Max(0.02f, settings.counter.riseDuration + settings.counter.returnDuration);
            _riseFraction = Mathf.Clamp(settings.counter.riseDuration / total, 0.01f, 0.99f);
            _riseEase = settings.counter.riseEase; _returnEase = settings.counter.returnEase;
            _glowAlpha = settings.reducedMotion ? 0f : settings.sizes.glowAlpha;
            _glowSize = settings.sizes.glowSize;
            _popProgress = 0f;
            _popTween.ChangeValues(0f, 1f, total).Restart();
        }

        public void Advance(float seconds, Vector2 destination)
        {
            if (_text == null || _glow == null) return;
            if (_countTween.IsPlaying())
            {
                _countTween.ManualUpdate(seconds, seconds);
                long next = _countProgress >= 1f ? Arrived :
                    _from + (long)decimal.Floor(((decimal)Arrived - _from) * (decimal)Mathf.Clamp01(_countProgress));
                Write(Math.Max(Shown, Math.Min(Arrived, next)));
            }
            if (_popTween.IsPlaying())
            {
                _popTween.ManualUpdate(seconds, seconds);
                float scale = _popProgress < _riseFraction
                    ? DOVirtual.EasedValue(_popFrom, _peak, _popProgress / _riseFraction, _riseEase)
                    : DOVirtual.EasedValue(_peak, 1f, (_popProgress - _riseFraction) / (1f - _riseFraction), _returnEase);
                _text.rectTransform.localScale = _baseScale * Mathf.Clamp(scale, 1f, 1.18f);
                _glow.rectTransform.anchoredPosition = destination;
                _glow.rectTransform.sizeDelta = Vector2.one * _glowSize * (1f + _popProgress * 0.3f);
                _glow.color = new Color(_text.color.r, _text.color.g, _text.color.b, _glowAlpha * (1f - _popProgress));
                _glow.enabled = _popProgress < 1f && _glowAlpha > 0f;
            }
        }

        private void Write(long value)
        {
            if (_text == null) { Shown = value; return; }
            if (Shown == value && _text.text != null && _text.text.Length > 0) return;
            Shown = value; _text.text = value.ToString(CultureInfo.InvariantCulture);
        }
        public void Snap(long amount)
        {
            _countTween.Pause(); _popTween.Pause(); _countProgress = _popProgress = 1f;
            Actual = Arrived = amount; Shown = -1L; Write(amount);
            _lastPop = -10f;
            if (_text != null) _text.rectTransform.localScale = _baseScale;
            if (_glow != null) _glow.enabled = false;
        }
        public void Dispose()
        {
            _countTween.Kill(); _popTween.Kill();
            if (_text != null) _text.rectTransform.localScale = _baseScale;
        }
    }
}
