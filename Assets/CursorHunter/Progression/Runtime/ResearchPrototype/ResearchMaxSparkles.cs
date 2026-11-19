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
        private Tween sparkleTween;
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
            }
            if (!Application.isPlaying) return;
            // One looping tween drives every sparkle on this node. Previously
            // each sparkle kept a sequence and four child tweeners alive.
            float elapsed = 0f;
            const float cycle = 1.83f;
            sparkleTween = DOTween.To(() => elapsed, value =>
            {
                elapsed = value;
                for (int i = 0; i < sparkles.Length && i < baseScales.Count; i++)
                {
                    var sparkle = sparkles[i];
                    if (sparkle == null) continue;
                    float t = Mathf.Repeat(elapsed - i * .48f, cycle);
                    float intensity = t < .28f ? t / .28f : t < .43f ? 1f :
                        t < .78f ? 1f - (t - .43f) / .35f : 0f;
                    var color = sparkle.color;
                    color.a = .9f * intensity;
                    sparkle.color = color;
                    sparkle.transform.localScale = baseScales[i] * Mathf.Lerp(.35f, 1f, intensity);
                }
            }, cycle, cycle).SetEase(Ease.Linear).SetLoops(-1, LoopType.Restart)
              .SetUpdate(true).SetRecyclable(false);
        }

        private void StopEffect()
        {
            sparkleTween?.Kill();
            sparkleTween = null;
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
