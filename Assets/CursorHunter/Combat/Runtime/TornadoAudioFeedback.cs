using UnityEngine;

namespace CursorHunter.Combat
{
    /// <summary>One cast voice and one aggregate wind bed, owned by the skill controller.</summary>
    internal sealed class TornadoAudioFeedback
    {
        private const int SampleRate = 22050;
        private static AudioClip _castClip;
        private static AudioClip _windClip;
        private readonly AudioSource _cast;
        private readonly AudioSource _wind;
        private bool _paused;

        public TornadoAudioFeedback(Transform owner)
        {
            if (_castClip == null) _castClip = CreateCastClip();
            if (_windClip == null) _windClip = CreateWindClip();
            var root = new GameObject("TornadoAudio");
            root.transform.SetParent(owner, false);
            _cast = CreateSource(root.transform, "Cast", _castClip, false, 32);
            _wind = CreateSource(root.transform, "Wind", _windClip, true, 64);
        }

        public void PlayCast(float volume)
        {
            _cast.Stop();
            _cast.volume = Mathf.Clamp01(volume) * 0.32f;
            _cast.Play();
        }

        public void Tick(int activeCount, float volume, float deltaTime)
        {
            if (_paused || _wind == null) return;
            // Several tornadoes share a bounded bed instead of stacking full-volume loops.
            float target = activeCount > 0
                ? Mathf.Clamp01(volume) * 0.24f * Mathf.Min(1.12f, 1f + (activeCount - 1) * 0.04f)
                : 0f;
            if (activeCount > 0 && !_wind.isPlaying) _wind.Play();
            _wind.volume = Mathf.MoveTowards(_wind.volume, target, Mathf.Max(0f, deltaTime) * 0.68f);
            if (activeCount == 0 && _wind.volume <= 0.001f) _wind.Stop();
        }

        public void SetPaused(bool paused)
        {
            if (_paused == paused) return;
            _paused = paused;
            if (paused)
            {
                _cast.Pause();
                _wind.Pause();
            }
            else
            {
                _cast.UnPause();
                _wind.UnPause();
            }
        }

        public void Stop()
        {
            if (_cast != null) _cast.Stop();
            if (_wind != null)
            {
                _wind.Stop();
                _wind.volume = 0f;
            }
            _paused = false;
        }

        private static AudioSource CreateSource(
            Transform parent, string name, AudioClip clip, bool loop, int priority)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            AudioSource source = root.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.priority = priority;
            source.loop = loop;
            source.volume = 0f;
            source.clip = clip;
            return source;
        }

        private static AudioClip CreateCastClip()
        {
            const float duration = 0.95f;
            int frames = Mathf.CeilToInt(duration * SampleRate);
            var samples = new float[frames * 2];
            uint seed = 0xA17E39u;
            float lowLeft = 0f, bandLeft = 0f, lowRight = 0f, bandRight = 0f;
            for (int i = 0; i < frames; i++)
            {
                float t = i / (float)SampleRate;
                float envelope = Mathf.Min(1f, t / 0.018f) * Mathf.Exp(-t * 1.8f) *
                    Mathf.Clamp01((duration - t) / 0.2f);
                float rumble = Mathf.Sin(2f * Mathf.PI * (110f * t - 32f * t * t));
                float surge = Mathf.Sin(2f * Mathf.PI * (240f * t + 340f * t * t));
                float thump = Mathf.Sin(2f * Mathf.PI * 68f * t) * Mathf.Exp(-t * 15f) * 0.26f;
                float pan = Mathf.Sin(t * 2f * Mathf.PI * 1.4f) * 0.18f;
                float airLeft = FilteredWind(ref seed, ref lowLeft, ref bandLeft);
                float airRight = FilteredWind(ref seed, ref lowRight, ref bandRight);
                samples[i * 2] = envelope *
                    (airLeft * (0.85f + pan) + rumble * 0.32f + surge * 0.10f) + thump;
                samples[i * 2 + 1] = envelope *
                    (airRight * (0.85f - pan) + rumble * 0.32f + surge * 0.10f) + thump;
            }
            return CreateClip("Tornado_ImpactGust", samples, frames, 0.88f);
        }

        private static AudioClip CreateWindClip()
        {
            const float duration = 2f;
            const float overlapSeconds = 0.18f;
            int frames = Mathf.RoundToInt(duration * SampleRate);
            int overlap = Mathf.RoundToInt(overlapSeconds * SampleRate);
            var raw = new float[(frames + overlap) * 2];
            uint seed = 0x7A4D93u;
            float lowLeft = 0f, bandLeft = 0f, lowRight = 0f, bandRight = 0f;
            for (int i = 0; i < frames + overlap; i++)
            {
                float t = i / (float)SampleRate;
                float pulse = 0.72f + 0.28f * Mathf.Sin(2f * Mathf.PI * 4f * t);
                float pan = 0.22f * Mathf.Sin(2f * Mathf.PI * 0.5f * t);
                float body = Mathf.Sin(2f * Mathf.PI * 88f * t) * 0.07f;
                raw[i * 2] = FilteredWind(ref seed, ref lowLeft, ref bandLeft) *
                    pulse * (0.8f + pan) + body;
                raw[i * 2 + 1] = FilteredWind(ref seed, ref lowRight, ref bandRight) *
                    pulse * (0.8f - pan) + body;
            }
            var samples = new float[frames * 2];
            System.Array.Copy(raw, samples, samples.Length);
            for (int i = 0; i < overlap; i++)
            {
                float blend = i / (float)(overlap - 1);
                blend = blend * blend * (3f - 2f * blend);
                for (int channel = 0; channel < 2; channel++)
                    samples[i * 2 + channel] = Mathf.Lerp(
                        raw[(frames + i) * 2 + channel], raw[i * 2 + channel], blend);
            }
            return CreateClip("Tornado_CirclingWind", samples, frames, 0.78f);
        }

        private static float FilteredWind(ref uint seed, ref float low, ref float band)
        {
            seed = unchecked(seed * 1664525u + 1013904223u);
            float white = ((seed >> 8) / 8388607.5f) - 1f;
            low += (white - low) * 0.018f;
            band += (white - band) * 0.28f;
            return band - low;
        }

        private static AudioClip CreateClip(string name, float[] samples, int frames, float peak)
        {
            float maximum = 0.001f;
            for (int i = 0; i < samples.Length; i++) maximum = Mathf.Max(maximum, Mathf.Abs(samples[i]));
            float scale = peak / maximum;
            for (int i = 0; i < samples.Length; i++) samples[i] *= scale;
            AudioClip clip = AudioClip.Create(name, frames, 2, SampleRate, false);
            clip.SetData(samples, 0);
            clip.hideFlags = HideFlags.HideAndDontSave;
            return clip;
        }
    }
}
