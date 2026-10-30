using UnityEngine;

namespace CursorHunter.Combat
{
    /// <summary>
    /// Lightweight synthesized combat cues. This keeps the prototype self
    /// contained while giving hits, deaths, skills, and the companion distinct
    /// sounds without allocating an AudioSource for every event.
    /// </summary>
    internal static class CombatAudioFeedback
    {
        private const int SampleRate = 22050;
        private const int SkillVoiceCount = 7;
        private const int ImpactVoiceCount = 12;
        private const int MonsterCount = 10;
        private const int FirstMonsterHitCue = 9;
        private const int FirstMonsterDeathCue = FirstMonsterHitCue + MonsterCount;
        private const int FirstSkillCue = 2;
        private static readonly AudioSource[] SkillSources = new AudioSource[SkillVoiceCount];
        private static readonly AudioSource[] ImpactSources = new AudioSource[ImpactVoiceCount];
        private static AudioClip[] _clips;
        private static GameObject _root;
        private static int _impactSourceIndex;

        public static void PlayMonsterHit(string monsterId) =>
            PlayImpact(FirstMonsterHitCue + GetMonsterIndex(monsterId),
                0.22f, GetMonsterPitch(monsterId));

        public static void PlayMonsterDeath(string monsterId) =>
            PlayImpact(FirstMonsterDeathCue + GetMonsterIndex(monsterId),
                0.31f, GetMonsterPitch(monsterId) * 0.9f);

        public static void PlaySkill(string skillId)
        {
            int cue = skillId switch
            {
                TornadoSkillController.SkillId => 2,
                HeadshotSkillController.SkillId => 3,
                ThunderSkillController.SkillId => 4,
                FlameExhaustSkillController.SkillId => 5,
                MagicBallSkillController.SkillId => 6,
                LaserSkillController.SkillId => 7,
                PetSkillController.SkillId => 8,
                _ => -1
            };

            if (cue >= 0)
            {
                PlaySkillCue(cue, cue == 4 ? 0.39f : 0.22f, 1f);
            }
        }

        private static void PlaySkillCue(int cue, float volume, float pitch)
        {
            if (!Application.isPlaying)
            {
                return;
            }

            EnsureInitialized();
            int skillIndex = cue - FirstSkillCue;
            if (_clips == null || cue < FirstSkillCue || cue >= FirstSkillCue + SkillVoiceCount ||
                skillIndex < 0 || skillIndex >= SkillSources.Length || _clips[cue] == null)
            {
                return;
            }

            // Reserve one voice per skill so monster hit/death bursts cannot
            // consume the channels needed to hear an ability activate.
            AudioSource source = SkillSources[skillIndex];
            if (source == null || source.isPlaying)
            {
                return;
            }

            source.pitch = Mathf.Clamp(pitch, 0.72f, 1.28f);
            source.PlayOneShot(_clips[cue], volume);
        }

        private static void PlayImpact(int cue, float volume, float pitch)
        {
            if (!Application.isPlaying)
            {
                return;
            }

            EnsureInitialized();
            if (_clips == null || cue < 0 || cue >= _clips.Length || _clips[cue] == null)
            {
                return;
            }

            for (int attempt = 0; attempt < ImpactSources.Length; attempt++)
            {
                int index = (_impactSourceIndex + attempt) % ImpactSources.Length;
                AudioSource source = ImpactSources[index];
                if (source == null || source.isPlaying)
                {
                    continue;
                }

                _impactSourceIndex = (index + 1) % ImpactSources.Length;
                source.pitch = Mathf.Clamp(pitch, 0.72f, 1.28f);
                source.PlayOneShot(_clips[cue], volume);
                return;
            }
        }

        private static void EnsureInitialized()
        {
            if (_root == null)
            {
                _root = new GameObject("CursorHunter_CombatAudio");
                UnityEngine.Object.DontDestroyOnLoad(_root);
                for (int i = 0; i < SkillSources.Length; i++)
                {
                    SkillSources[i] = CreateSource("Skill_" + i, 16);
                }
                for (int i = 0; i < ImpactSources.Length; i++)
                {
                    ImpactSources[i] = CreateSource("Impact_" + i, 128);
                }
            }

            if (_clips == null)
            {
                _clips = new AudioClip[FirstMonsterDeathCue + MonsterCount];
                for (int cue = 0; cue < _clips.Length; cue++)
                {
                    _clips[cue] = CreateCue(cue);
                }
            }
        }

        private static AudioSource CreateSource(string name, int priority)
        {
            var sourceObject = new GameObject(name);
            sourceObject.transform.SetParent(_root.transform, false);
            AudioSource source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 1f;
            source.priority = priority;
            return source;
        }

        private static AudioClip CreateCue(int cue)
        {
            if (cue >= FirstMonsterHitCue)
            {
                bool isDeath = cue >= FirstMonsterDeathCue;
                return CreateMonsterCue(
                    cue - (isDeath ? FirstMonsterDeathCue : FirstMonsterHitCue),
                    isDeath);
            }

            float duration = GetSkillCueDuration(cue);
            int count = Mathf.CeilToInt(SampleRate * duration);
            float[] samples = new float[count];
            for (int i = 0; i < count; i++)
            {
                float time = i / (float)SampleRate;
                float phase = time / duration;
                float noise = Mathf.Sin((i + 1) * (12.9898f + cue * 1.731f)) * 0.5f +
                    Mathf.Sin((i + 7) * (78.233f + cue * 4.117f)) * 0.25f;
                samples[i] = Mathf.Clamp(Shape(cue, time, phase, noise), -0.92f, 0.92f);
            }

            AudioClip clip = AudioClip.Create(
                "CursorHunter_CombatCue_" + cue,
                count,
                1,
                SampleRate,
                false);
            clip.SetData(samples, 0);
            clip.hideFlags = HideFlags.HideAndDontSave;
            return clip;
        }

        private static float GetSkillCueDuration(int cue)
        {
            return cue switch
            {
                0 => 0.075f,
                1 => 0.28f,
                2 => 0.36f,
                3 => 0.18f,
                4 => 0.82f,
                5 => 0.22f,
                6 => 0.34f,
                7 => 0.25f,
                _ => 0.3f
            };
        }

        private static AudioClip CreateMonsterCue(int monsterIndex, bool death)
        {
            float duration = death ? 0.24f : 0.075f;
            int count = Mathf.CeilToInt(SampleRate * duration);
            float[] samples = new float[count];
            for (int i = 0; i < count; i++)
            {
                float time = i / (float)SampleRate;
                float phase = time / duration;
                float noise = Mathf.Sin((i + 1) * (12.9898f + monsterIndex * 2.17f)) * 0.5f +
                    Mathf.Sin((i + 7) * (78.233f + monsterIndex * 3.41f)) * 0.25f;
                samples[i] = Mathf.Clamp(
                    ShapeMonsterCue(monsterIndex, death, time, phase, noise),
                    -0.9f,
                    0.9f);
            }

            AudioClip clip = AudioClip.Create(
                "CursorHunter_Monster" + (monsterIndex + 1) + (death ? "Death" : "Hit"),
                count,
                1,
                SampleRate,
                false);
            clip.SetData(samples, 0);
            clip.hideFlags = HideFlags.HideAndDontSave;
            return clip;
        }

        private static float ShapeMonsterCue(
            int monsterIndex,
            bool death,
            float time,
            float phase,
            float noise)
        {
            float envelope = death
                ? Mathf.Pow(1f - phase, 1.5f)
                : Mathf.Exp(-phase * 10f);
            float fundamental = death ? 150f - 75f * phase : 230f - 90f * phase;
            switch (monsterIndex)
            {
                case 0: // Stump: woody, hollow knock.
                    return (Mathf.Sin(2f * Mathf.PI * fundamental * time) * 0.62f +
                            Mathf.Sin(2f * Mathf.PI * fundamental * 2.1f * time) * 0.16f +
                            noise * 0.22f) * envelope;
                case 1: // Queen Bee: short, modulated buzz.
                    return Mathf.Sin(2f * Mathf.PI * (death ? 190f : 330f) * time) *
                        (0.38f + 0.24f * Mathf.Sin(2f * Mathf.PI * 42f * time)) * envelope +
                        noise * envelope * 0.12f;
                case 2: // Poison mushroom: soft, dampened plop.
                    return (Mathf.Sin(2f * Mathf.PI * (death ? 105f : 175f) * time) * 0.68f +
                            noise * 0.32f) * envelope;
                case 3: // Mask ghost: airy descending wisp.
                    return (noise * 0.45f +
                            Mathf.Sin(2f * Mathf.PI * (death ? 620f : 860f) * time) * 0.35f) *
                        envelope * (0.55f + phase * 0.45f);
                case 4: // Armored soldier: bright metallic strike.
                    return (Mathf.Sin(2f * Mathf.PI * (death ? 270f : 430f) * time) * 0.48f +
                            Mathf.Sin(2f * Mathf.PI * (death ? 710f : 1120f) * time) * 0.30f +
                            noise * 0.22f) * envelope;
                case 5: // Snow fairy: clear, icy chime.
                    return (Mathf.Sin(2f * Mathf.PI * (death ? 520f : 1380f) * time) * 0.58f +
                            Mathf.Sin(2f * Mathf.PI * (death ? 790f : 2060f) * time) * 0.22f) *
                        envelope;
                case 6: // Spider hive: dry, papery click.
                    return (noise * 0.76f +
                            Mathf.Sin(2f * Mathf.PI * (death ? 185f : 520f) * time) * 0.24f) *
                        envelope;
                case 7: // Iron golem: heavy stone impact.
                    return (Mathf.Sin(2f * Mathf.PI * (death ? 78f : 112f) * time) * 0.76f +
                            noise * 0.24f) * envelope;
                case 8: // Fire bud: warm, soft organic thump.
                    return (Mathf.Sin(2f * Mathf.PI * (death ? 125f : 205f) * time) * 0.52f +
                            Mathf.Sin(2f * Mathf.PI * (death ? 250f : 410f) * time) * 0.18f +
                            noise * 0.30f) * envelope;
                default: // Pandora: dark, resonant rattle.
                    return (Mathf.Sin(2f * Mathf.PI * (death ? 92f : 165f) * time) * 0.55f +
                            Mathf.Sin(2f * Mathf.PI * (death ? 310f : 490f) * time) * 0.25f +
                            noise * 0.20f) * envelope;
            }
        }

        private static int GetMonsterIndex(string monsterId)
        {
            if (!string.IsNullOrEmpty(monsterId))
            {
                int separator = monsterId.LastIndexOf('.');
                if (separator >= 0 && separator < monsterId.Length - 1 &&
                    int.TryParse(monsterId.Substring(separator + 1), out int number))
                {
                    return Mathf.Clamp(number - 1, 0, MonsterCount - 1);
                }
            }

            return 0;
        }

        private static float Shape(int cue, float t, float phase, float noise)
        {
            switch (cue)
            {
                case 0: // Soft, short impact.
                    return (Mathf.Sin(2f * Mathf.PI * (155f - 65f * phase) * t) * 0.7f +
                            noise * 0.3f) * Mathf.Exp(-phase * 9f);
                case 1: // Falling, rounded defeat cue.
                    return (Mathf.Sin(2f * Mathf.PI * (170f - 92f * phase) * t) * 0.8f +
                            noise * 0.18f) * Mathf.Pow(1f - phase, 2f);
                case 2: // Circling airy gust.
                    return noise * Mathf.Sin(phase * Mathf.PI) * 0.34f +
                        Mathf.Sin(2f * Mathf.PI * (270f + 180f * phase) * t) *
                        Mathf.Sin(phase * Mathf.PI) * 0.24f;
                case 3: // Quick aimed shot and bright impact.
                    return (Mathf.Sin(2f * Mathf.PI * (360f - 180f * phase) * t) * 0.42f +
                            noise * 0.36f) * Mathf.Exp(-phase * 5f);
                case 4: // Sharp lightning crack over a longer, low thunder roll.
                {
                    float crack = noise * Mathf.Exp(-phase * 20f) * 0.72f;
                    float body = Mathf.Sin(2f * Mathf.PI * (78f - 42f * phase) * t) *
                        Mathf.Pow(1f - phase, 0.72f) * 0.52f;
                    float rumble = Mathf.Sin(2f * Mathf.PI * (43f - 18f * phase) * t) *
                        Mathf.Pow(1f - phase, 0.48f) * 0.32f;
                    float mid = Mathf.Sin(2f * Mathf.PI * (164f - 96f * phase) * t) *
                        Mathf.Pow(1f - phase, 1.1f) * 0.18f;
                    return crack + body + rumble + mid;
                }
                case 5: // Brief flame whoosh.
                    return (noise * 0.55f + Mathf.Sin(2f * Mathf.PI * 190f * t) * 0.22f) *
                        Mathf.Sin(phase * Mathf.PI) * (0.45f + phase * 0.55f);
                case 6: // Magical, bright two-overtone pulse.
                    return (Mathf.Sin(2f * Mathf.PI * 520f * t) * 0.55f +
                            Mathf.Sin(2f * Mathf.PI * 780f * t) * 0.2f) *
                        Mathf.Sin(phase * Mathf.PI) * 0.65f;
                case 7: // Laser sweep down in pitch.
                    return (Mathf.Sin(2f * Mathf.PI * (1180f - 780f * phase) * t) * 0.65f +
                            noise * 0.18f) * Mathf.Pow(1f - phase, 0.65f);
                default: // Warm companion chirp.
                    return (Mathf.Sin(2f * Mathf.PI * (620f + 210f * Mathf.Sin(phase * Mathf.PI)) * t) * 0.62f +
                            Mathf.Sin(2f * Mathf.PI * 930f * t) * 0.18f) *
                        Mathf.Sin(phase * Mathf.PI) * 0.72f;
            }
        }

        private static float GetMonsterPitch(string monsterId)
        {
            if (string.IsNullOrEmpty(monsterId))
            {
                return 1f;
            }

            int hash = 17;
            for (int i = 0; i < monsterId.Length; i++)
            {
                hash = unchecked(hash * 31 + monsterId[i]);
            }

            return 0.88f + (hash & 7) * 0.035f;
        }
    }
}
