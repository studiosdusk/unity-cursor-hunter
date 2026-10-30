using UnityEngine;

namespace CursorHunter.Progression
{
    /// <summary>Short synthesized UI and reward sounds for the research screen.</summary>
    public static class TraitAudioFeedback
    {
        private const int SampleRate = 22050;
        private static readonly float[] UpgradeNotes = { 523.25f, 659.25f, 783.99f };
        private static readonly float[] RelicNotes =
            { 523.25f, 659.25f, 783.99f, 1046.5f, 1318.5f };
        private static readonly AudioSource[] Sources = new AudioSource[5];
        private static AudioClip[] _clips;
        private static GameObject _root;
        private static int _nextSource;

        public static void PlayClick() => Play(0, 0.16f);
        public static void PlayUpgrade() => Play(1, 0.26f);
        public static void PlayRelicReveal() => Play(2, 0.34f);

        private static void Play(int cue, float volume)
        {
            if (!Application.isPlaying)
            {
                return;
            }

            EnsureInitialized();
            for (int attempt = 0; attempt < Sources.Length; attempt++)
            {
                int index = (_nextSource + attempt) % Sources.Length;
                AudioSource source = Sources[index];
                if (source == null || source.isPlaying)
                {
                    continue;
                }

                _nextSource = (index + 1) % Sources.Length;
                source.PlayOneShot(_clips[cue], volume);
                return;
            }
        }

        private static void EnsureInitialized()
        {
            if (_root == null)
            {
                _root = new GameObject("CursorHunter_TraitAudio");
                Object.DontDestroyOnLoad(_root);
                for (int i = 0; i < Sources.Length; i++)
                {
                    AudioSource source = _root.AddComponent<AudioSource>();
                    source.playOnAwake = false;
                    source.spatialBlend = 0f;
                    Sources[i] = source;
                }
            }

            if (_clips == null)
            {
                _clips = new[] { CreateCue(0), CreateCue(1), CreateCue(2) };
            }
        }

        private static AudioClip CreateCue(int cue)
        {
            float duration = cue == 0 ? 0.095f : cue == 1 ? 0.35f : 0.72f;
            int count = Mathf.CeilToInt(SampleRate * duration);
            float[] samples = new float[count];
            float[] notes = cue == 1 ? UpgradeNotes : RelicNotes;
            for (int i = 0; i < count; i++)
            {
                float time = i / (float)SampleRate;
                float phase = time / duration;
                float envelope = cue == 0
                    ? Mathf.Exp(-phase * 8f)
                    : Mathf.Sin(phase * Mathf.PI) * Mathf.Pow(1f - phase, cue == 1 ? 0.2f : 0.05f);
                float sample;
                if (cue == 0)
                {
                    // A soft wooden-pluck attack with a small glassy overtone.
                    float fundamental = 660f - 90f * phase;
                    sample = Mathf.Sin(2f * Mathf.PI * fundamental * time) * 0.42f +
                        Mathf.Sin(2f * Mathf.PI * (fundamental * 1.5f) * time) * 0.18f +
                        Mathf.Sin(2f * Mathf.PI * (fundamental * 2f) * time) * 0.09f +
                        Mathf.Sin(2f * Mathf.PI * 1980f * time) * Mathf.Exp(-phase * 28f) * 0.07f;
                }
                else
                {
                    sample = 0f;
                    for (int note = 0; note < notes.Length; note++)
                    {
                        float noteStart = note / (float)notes.Length * 0.72f;
                        float notePhase = (phase - noteStart) / 0.30f;
                        if (notePhase >= 0f && notePhase < 1f)
                        {
                            float noteEnvelope = Mathf.Sin(notePhase * Mathf.PI) * (1f - notePhase * 0.35f);
                            sample += Mathf.Sin(2f * Mathf.PI * notes[note] * time) * noteEnvelope *
                                (cue == 1 ? 0.2f : 0.15f);
                        }
                    }

                    if (cue == 2)
                    {
                        sample += Mathf.Sin(2f * Mathf.PI * 1760f * time) *
                            Mathf.Sin(phase * Mathf.PI) * 0.07f;
                    }
                }

                samples[i] = Mathf.Clamp(sample * envelope, -0.85f, 0.85f);
            }

            AudioClip clip = AudioClip.Create("CursorHunter_TraitCue_" + cue,
                count, 1, SampleRate, false);
            clip.SetData(samples, 0);
            clip.hideFlags = HideFlags.HideAndDontSave;
            return clip;
        }
    }
}
