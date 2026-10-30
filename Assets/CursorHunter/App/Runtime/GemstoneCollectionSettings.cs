using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Audio;

namespace CursorHunter.App
{
    [Serializable]
    public sealed class GemstoneCollectionSettings
    {
        public SizeOptions sizes = new SizeOptions();
        public MotionOptions motion = new MotionOptions();
        public CounterOptions counter = new CounterOptions();
        public SoundOptions sound = new SoundOptions();
        public BudgetOptions budget = new BudgetOptions();
        public BossBurstOptions bossBurst = new BossBurstOptions();
        [Tooltip("전투 종료 때 남은 비행을 마무리하는 최대 시간(초)")]
        [Range(0f, 0.5f)] public float finishDuration = 0.3f;
        [Tooltip("분출·회전·잔상을 생략하고 팝업 강도를 줄입니다. 1/5/10 개수는 보존합니다.")]
        public bool reducedMotion;

        [Serializable] public sealed class BossBurstOptions
        {
            [Tooltip("보스 토벌 시 동시에 터지는 젬 수. 큰 보상은 젬 한 개에 여러 개를 묶어 표시합니다.")]
            [Range(8, 256)] public int count = 128;
            public Vector2 scatterRadius = new Vector2(100f, 310f);
            public Vector2 scatterDuration = new Vector2(0.18f, 0.35f);
            public Vector2 holdDuration = new Vector2(0.12f, 0.3f);
            public Vector2 flightDuration = new Vector2(0.45f, 0.8f);
            [Range(1f, 3f)] public float finishDuration = 1.8f;
        }

        [Serializable] public sealed class SizeOptions
        {
            [Tooltip("소형 1개 / 중형 5개 / 대형 10개. 1920×1080 기준 UI 크기(px)")]
            public Vector3 pixels = new Vector3(22f, 30f, 40f);
            [Range(0.05f, 1f)] public float birthScale = 0.4f;
            [Range(0f, 1f)] public float arrivalScale = 0.15f;
            public bool trails = true;
            [Range(0, 2)] public int trailCount = 2;
            [Range(0f, 0.3f)] public float trailAlpha = 0.12f;
            [Range(0f, 0.3f)] public float glowAlpha = 0.12f;
            [Min(1f)] public float glowSize = 58f;
        }
        [Serializable] public sealed class MotionOptions
        {
            [Tooltip("생성 확대 시간(초)")][Min(0.01f)] public float birthDuration = 0.04f;
            [Tooltip("분출 시간 최소/최대(초)")] public Vector2 scatterDuration = new Vector2(0.1f, 0.15f);
            [Tooltip("분출 반경 최소/최대(px)")] public Vector2 scatterRadius = new Vector2(18f, 48f);
            public Vector2 originOffset;
            [Tooltip("분출 후 흡수 전 대기 시간(초)")] public Vector2 holdDuration = new Vector2(0.03f, 0.07f);
            [Tooltip("흡수 비행 시간 최소/최대(초)")] public Vector2 flightDuration = new Vector2(0.28f, 0.52f);
            [Tooltip("같은 처치에서 발사하는 젬 사이 간격(초)")] public Vector2 launchInterval = new Vector2(0.012f, 0.028f);
            [Tooltip("곡선의 굴곡 크기(px)")] public Vector2 curvature = new Vector2(24f, 80f);
            [Range(0f, 90f)] public float rotationDegrees = 20f;
            [Range(0.01f, 0.2f)] public float shrinkDuration = 0.065f;
            [Min(1f)] public float fullDistance = 1200f;
            public AnimationCurve scatterCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            public AnimationCurve attractionCurve = new AnimationCurve(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, 2f, 2f));
        }
        [Serializable] public sealed class CounterOptions
        {
            [Range(1f, 1.18f)] public float mainScale = 1.12f;
            [Range(1f, 1.18f)] public float secondaryScale = 1.16f;
            [Min(0.01f)] public float riseDuration = 0.055f;
            [Min(0.01f)] public float returnDuration = 0.15f;
            [Min(0.01f)] public float minimumPopInterval = 0.15f;
            [Min(0.01f)] public float countDuration = 0.12f;
            public Ease riseEase = Ease.OutCubic;
            public Ease returnEase = Ease.OutQuad;
        }
        [Serializable] public sealed class SoundOptions
        {
            public AudioClip commonClip;
            public AudioClip bonusClip;
            public AudioMixerGroup mixer;
            [Range(0f, 1f)] public float volume = 0.22f;
            public Vector2 pitch = new Vector2(0.96f, 1.12f);
            [Min(0.05f)] public float minimumInterval = 0.1f;
            [Range(1, 16)] public int voices = 8;
        }
        [Serializable] public sealed class BudgetOptions
        {
            [Tooltip("풀 변경은 다음 전투부터 적용합니다.")][Range(8, 2048)] public int poolSize = 256;
            [Range(1, 2048)] public int maxActive = 256;
            [Min(0)] public int bonusReserved = 64;
            [Range(1, 128)] public int launchesPerFrame = 24;
            [Min(8)] public int queueCapacity = 1024;
            [Tooltip("대기 항목이 이 수 이상이면 장식을 줄이고 비행을 단축합니다.")][Min(1)] public int pressureThreshold = 32;
            [Range(0.1f, 0.5f)] public float pressureFlightDuration = 0.18f;
        }
    }

    public static class GemstoneDenominations
    {
        public static void Split(long amount, out long large, out int medium, out int small)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            large = amount / 10L; medium = (int)(amount % 10L) / 5; small = (int)(amount % 5L);
        }
    }
}
