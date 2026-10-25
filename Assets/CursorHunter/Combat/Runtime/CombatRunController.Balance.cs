using System.Collections.Generic;
using CursorHunter.Contracts;
using UnityEngine;

namespace CursorHunter.Combat
{
    public sealed partial class CombatRunController
    {
        private List<SkillBalanceSettings> _skillBalanceOverrides;
        public float DurationSeconds => _durationSeconds;

        public List<SkillBalanceSettings> CaptureSkillBalanceSettings()
        {
            RefreshSkillControllers();
            var result = new List<SkillBalanceSettings>();
            foreach (IRunSkillController controller in _skillControllers)
                if (controller is ISkillBalanceController live)
                    result.Add(live.CaptureBalanceSettings());
            return result;
        }

        public bool ValidateSkillBalanceSettings(IReadOnlyList<SkillBalanceSettings> settings,
            out string error)
        {
            error = string.Empty;
            if (!RefreshSkillControllers() || settings == null)
            {
                error = "스킬 컨트롤러 또는 설정을 확인하세요.";
                return false;
            }
            var seen = new HashSet<string>();
            foreach (SkillBalanceSettings value in settings)
            {
                if (value == null || string.IsNullOrEmpty(value.SkillId) ||
                    !seen.Add(value.SkillId) ||
                    !_skillControllersById.TryGetValue(value.SkillId, out IRunSkillController controller) ||
                    !(controller is ISkillBalanceController live))
                {
                    error = "중복되거나 없는 스킬 설정입니다.";
                    return false;
                }
                if (!live.ValidateBalanceSettings(value, out error)) return false;
                if (value.Enabled && (!(controller is MonoBehaviour component) ||
                    !component.isActiveAndEnabled))
                {
                    error = value.SkillId + ": 컨트롤러를 활성화하세요.";
                    return false;
                }
            }
            if (seen.Count != _skillControllers.Count)
            {
                error = "모든 스킬의 사용 여부와 수치가 필요합니다.";
                return false;
            }
            return true;
        }

        public bool SetSkillBalanceOverrides(IReadOnlyList<SkillBalanceSettings> settings,
            out string error)
        {
            if (!ValidateSkillBalanceSettings(settings, out error)) return false;
            _skillBalanceOverrides = CopySkills(settings);
            return true;
        }

        public void ClearSkillBalanceOverrides()
        {
            _skillBalanceOverrides = null;
            foreach (IRunSkillController controller in _skillControllers)
                if (controller is ISkillBalanceController live) live.ClearBalanceSettings();
        }

        public bool TryApplyLiveBalance(CombatSnapshot snapshot, float duration,
            IReadOnlyList<SkillBalanceSettings> skills, out string error)
        {
            error = string.Empty;
            if (!IsRunActive || !snapshot.IsValid || duration < 15f || duration > 30f ||
                float.IsNaN(duration) || float.IsInfinity(duration) ||
                !ValidateSkillBalanceSettings(skills, out error))
            {
                if (string.IsNullOrEmpty(error)) error = "실행 중인 전투와 유효한 수치가 필요합니다.";
                return false;
            }
            CombatSnapshot previous = CombatSnapshot;
            List<SkillBalanceSettings> previousSkills = CaptureSkillBalanceSettings();
            if (!playerCombatStatsRuntime.TryCaptureSnapshot(snapshot)) return false;
            foreach (SkillBalanceSettings value in skills)
            {
                var live = (ISkillBalanceController)_skillControllersById[value.SkillId];
                if (live.ApplyBalanceSettings(this, _runRequest.RunId, _runRequest.Seed, value))
                    continue;
                playerCombatStatsRuntime.TryCaptureSnapshot(previous);
                foreach (SkillBalanceSettings old in previousSkills)
                    ((ISkillBalanceController)_skillControllersById[old.SkillId]).ApplyBalanceSettings(
                        this, _runRequest.RunId, _runRequest.Seed, old);
                error = value.SkillId + ": 실시간 변경을 적용하지 못했습니다.";
                return false;
            }

            // Keep every target's remaining cooldown fraction, including readiness.
            float oldInterval = previous.AttackCooldownSeconds;
            float nextInterval = snapshot.AttackCooldownSeconds;
            var targets = new List<ICombatTarget>(_lastBasicHitAt.Keys);
            foreach (ICombatTarget target in targets)
            {
                float elapsed = Mathf.Max(0f, _elapsedSeconds - _lastBasicHitAt[target]);
                float remaining = oldInterval > 0f
                    ? Mathf.Clamp01(1f - elapsed / oldInterval) * nextInterval : 0f;
                _lastBasicHitAt[target] = _elapsedSeconds - nextInterval + remaining;
            }
            if (!Mathf.Approximately(previous.RangeMultiplier, snapshot.RangeMultiplier) ||
                previous.AutoAttackEnabled != snapshot.AutoAttackEnabled) ResetAttackPath();
            _durationSeconds = duration;
            _skillBalanceOverrides = CopySkills(skills);
            return true;
        }

        private SkillBalanceSettings FindSkillBalance(string id) =>
            _skillBalanceOverrides?.Find(value => value.SkillId == id);

        private static List<SkillBalanceSettings> CopySkills(IReadOnlyList<SkillBalanceSettings> source)
        {
            var result = new List<SkillBalanceSettings>(source.Count);
            foreach (SkillBalanceSettings value in source) result.Add(value.Clone());
            return result;
        }
    }
}
