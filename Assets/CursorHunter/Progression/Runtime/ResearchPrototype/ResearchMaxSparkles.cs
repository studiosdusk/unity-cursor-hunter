using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace CursorHunter.Progression.ResearchPrototype
{
    /// <summary>UI sparkles shown while this research placement is maxed.</summary>
    public sealed class ResearchMaxSparkles : MonoBehaviour
    {
        public GameObject visualRoot;
        public Image[] sparkles;
        private readonly List<Tween> animations = new List<Tween>();
        private readonly List<Vector3> baseScales = new List<Vector3>();
        private bool completed;

        public void SetCompleted(bool value)
        {
            bool show = value && isActiveAndEnabled && visualRoot != null;
            if (completed == show) return;
            StopEffect();
            completed = show;
            if (visualRoot != null) visualRoot.SetActive(show);
            if (!show || sparkles == null) return;
            foreach (var sparkle in sparkles)
            {
                if (sparkle == null) { baseScales.Add(Vector3.one); continue; }
                sparkle.raycastTarget = false;
                Vector3 basis = sparkle.transform.localScale;
                baseScales.Add(basis);
                sparkle.transform.localScale = basis * .35f;
                var color = sparkle.color;
                color.a = 0f;
                sparkle.color = color;
                if (!Application.isPlaying) continue;
                float delay = (baseScales.Count - 1) * .48f;
                var tween = DOTween.Sequence().SetUpdate(true).SetRecyclable(false)
                    .AppendInterval(delay)
                    .Append(Fade(sparkle, .9f, .28f))
                    .Join(sparkle.transform.DOScale(basis, .28f).SetEase(Ease.OutBack))
                    .AppendInterval(.15f)
                    .Append(Fade(sparkle, 0f, .35f))
                    .Join(sparkle.transform.DOScale(basis * .35f, .35f).SetEase(Ease.InSine))
                    .AppendInterval(1.05f)
                    .SetLoops(-1, LoopType.Restart);
                animations.Add(tween);
            }
        }

        private static Tween Fade(Image image, float alpha, float duration)
        {
            return DOTween.To(() => image.color.a, value =>
            {
                var color = image.color;
                color.a = value;
                image.color = color;
            }, alpha, duration);
        }

        private void StopEffect()
        {
            foreach (var animation in animations) animation?.Kill();
            animations.Clear();
            if (sparkles != null)
                for (int i = 0; i < sparkles.Length && i < baseScales.Count; i++)
                    if (sparkles[i] != null) sparkles[i].transform.localScale = baseScales[i];
            baseScales.Clear();
        }

        private void OnDisable()
        {
            StopEffect();
            completed = false;
            if (visualRoot != null) visualRoot.SetActive(false);
        }
    }
}
