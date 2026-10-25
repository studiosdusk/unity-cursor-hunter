using System;
using System.Collections.Generic;
using System.Reflection;
using CursorHunter.Contracts;
using UnityEngine;

namespace CursorHunter.Combat
{
    /// <summary>Transient final skill values. Never written into authored scene data.</summary>
    [Serializable]
    public sealed class SkillBalanceSettings
    {
        public string SkillId;
        public bool Enabled;
        public List<SkillBalanceValue> Values = new List<SkillBalanceValue>();

        public SkillBalanceSettings Clone()
        {
            var copy = new SkillBalanceSettings { SkillId = SkillId, Enabled = Enabled };
            if (Values != null)
                foreach (SkillBalanceValue value in Values)
                    copy.Values.Add(value == null ? null : new SkillBalanceValue
                    {
                        Id = value.Id, Value = value.Value, Minimum = value.Minimum,
                        Maximum = value.Maximum, WholeNumber = value.WholeNumber
                    });
            return copy;
        }
    }

    [Serializable]
    public sealed class SkillBalanceValue
    {
        public string Id;
        public float Value;
        public float Minimum;
        public float Maximum = float.MaxValue;
        public bool WholeNumber;
    }

    public interface ISkillBalanceController
    {
        SkillBalanceSettings CaptureBalanceSettings();
        bool ValidateBalanceSettings(SkillBalanceSettings settings, out string error);
        bool ApplyBalanceSettings(CombatRunController combat, RunId runId,
            ulong seed, SkillBalanceSettings settings);
        void ClearBalanceSettings();
    }

    /// <summary>Reflection is restricted to explicit editor edits, never the simulation loop.</summary>
    internal static class SkillBalanceMath
    {
        private const BindingFlags RuntimeProperties =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        internal static SkillBalanceSettings Capture<TBase, TRuntime>(string id,
            bool enabled, TBase basis, TRuntime runtime, bool configured,
            int maximumCount = int.MaxValue) where TBase : struct
        {
            var result = new SkillBalanceSettings { SkillId = id, Enabled = enabled };
            foreach (FieldInfo field in typeof(TBase).GetFields())
            {
                var min = field.GetCustomAttribute<MinAttribute>();
                var range = field.GetCustomAttribute<RangeAttribute>();
                PropertyInfo actual = typeof(TRuntime).GetProperty(field.Name,
                    RuntimeProperties);
                float value = Convert.ToSingle(configured && actual != null
                    ? actual.GetValue(runtime) : field.GetValue(basis));
                result.Values.Add(new SkillBalanceValue
                {
                    Id = char.ToLowerInvariant(field.Name[0]) + field.Name.Substring(1),
                    Value = value,
                    Minimum = range != null ? range.min : min != null ? min.min : 0f,
                    Maximum = field.FieldType == typeof(int) ? maximumCount :
                        range != null ? range.max : float.MaxValue,
                    WholeNumber = field.FieldType == typeof(int)
                });
            }
            return result;
        }

        internal static bool Validate(SkillBalanceSettings settings,
            SkillBalanceSettings schema, out string error)
        {
            error = null;
            if (settings == null || settings.SkillId != schema.SkillId ||
                settings.Values == null || settings.Values.Count != schema.Values.Count)
            {
                error = "스킬 설정 항목이 일치하지 않습니다.";
                return false;
            }
            var seen = new HashSet<string>();
            foreach (SkillBalanceValue value in settings.Values)
            {
                SkillBalanceValue definition = value == null ? null :
                    schema.Values.Find(candidate => candidate.Id == value.Id);
                if (definition == null || !seen.Add(value.Id) ||
                    float.IsNaN(value.Value) || float.IsInfinity(value.Value) ||
                    value.Value < definition.Minimum || value.Value > definition.Maximum ||
                    definition.WholeNumber && value.Value != Mathf.Floor(value.Value))
                {
                    error = (value?.Id ?? "스킬") + ": 허용 범위의 유효한 값을 입력하세요.";
                    return false;
                }
            }
            return true;
        }

        internal static TBase Read<TBase>(SkillBalanceSettings settings)
            where TBase : struct
        {
            object result = default(TBase);
            foreach (FieldInfo field in typeof(TBase).GetFields())
            {
                string id = char.ToLowerInvariant(field.Name[0]) + field.Name.Substring(1);
                float value = settings.Values.Find(candidate => candidate.Id == id).Value;
                object converted = field.FieldType == typeof(int) ? (object)(int)value : value;
                field.SetValue(result, converted);
            }
            return (TBase)result;
        }

        /// <summary>Preserve remaining progress, including an already due event.</summary>
        internal static float Retime(float dueAt, float now,
            float oldInterval, float newInterval)
        {
            if (dueAt <= now) return now;
            float fraction = oldInterval > 0f
                ? Mathf.Clamp01((dueAt - now) / oldInterval) : 0f;
            return now + fraction * newInterval;
        }

        internal static int PoolNeed(float duration, float cooldown,
            int maximum, int count = 1)
        {
            maximum = Mathf.Max(count, maximum);
            float needed = Mathf.Min(maximum, (duration / cooldown + 1f) * count);
            return Mathf.Clamp(Mathf.CeilToInt(needed), count, maximum);
        }
    }
}
