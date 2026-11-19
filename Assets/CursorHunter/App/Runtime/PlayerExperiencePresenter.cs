using System;
using System.Globalization;
using CursorHunter.Combat;
using CursorHunter.Contracts;
using CursorHunter.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CursorHunter.App
{
    /// <summary>Shows saved XP plus this run's committed kills; never grants or saves rewards.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerExperiencePresenter : MonoBehaviour
    {
        [SerializeField] private TraitScreenController progression;
        [SerializeField] private Slider experienceSlider;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text experienceText;

        private CombatRunController combat;
        private CombatRunController subscribedCombat;
        private RunId runId;
        private long runStartExperience;
        private long displayedExperience = -1;
        private long? resultExperience;
        private bool useProgression = true;
        public long DisplayedExperience => displayedExperience;
        public int DisplayedLevel => ProgressionLevelCurve.GetLevel(displayedExperience);

        private void Awake()
        {
            if (experienceSlider != null)
            {
                experienceSlider.minValue = 0;
                experienceSlider.maxValue = 1;
                experienceSlider.wholeNumbers = false;
                experienceSlider.transition = Selectable.Transition.None;
                experienceSlider.interactable = false;
                experienceSlider.navigation = new Navigation { mode = Navigation.Mode.None };
                foreach (var graphic in experienceSlider.GetComponentsInChildren<Graphic>(true))
                    graphic.raycastTarget = false;
            }
        }

        private void OnEnable()
        {
            if (progression != null) progression.ExperienceChanged += Refresh;
            Subscribe();
            displayedExperience = -1;
            Refresh();
        }

        private void OnDisable()
        {
            if (progression != null) progression.ExperienceChanged -= Refresh;
            Unsubscribe();
        }

        public void SetProgressionConnectionEnabled(bool value)
        {
            if (useProgression == value) return;
            useProgression = value;
            if (!runId.IsValid) Refresh();
        }

        public void ShowRunning(CombatRunController source)
        {
            if (source == null || !source.IsRunActive) return;
            if (runId != source.CurrentRunId)
            {
                Unsubscribe();
                runId = source.CurrentRunId;
                runStartExperience = SavedExperience;
                resultExperience = null;
            }
            combat = source;
            Subscribe();
            Refresh();
        }

        public void ShowResult(RunResult result)
        {
            if (!runId.IsValid || result.RunId != runId) return;
            Unsubscribe();
            combat = null;
            // Settlement may have already succeeded. Never add the run to the new saved total.
            // Retain the earned preview while an eligible settlement is waiting for a save retry.
            resultExperience = result.SettlementPolicy == RunSettlementPolicy.Eligible
                ? Math.Max(SavedExperience, AddSaturating(runStartExperience, result.ExperienceEarned))
                : SavedExperience;
            Refresh();
        }

        public void ResetDisplay()
        {
            Unsubscribe();
            combat = null;
            runId = default;
            resultExperience = null;
            Refresh();
        }

        /// <summary>Rebase a live preview after a test cheat changes saved XP.</summary>
        public void RefreshAfterTestExperienceChange()
        {
            if (combat != null && combat.IsRunActive && combat.CurrentRunId == runId)
                runStartExperience = SavedExperience;
            else resultExperience = null;
            Refresh();
        }

        private long SavedExperience => useProgression && progression != null ? progression.ExperiencePoints : 0;

        private void Subscribe()
        {
            if (!isActiveAndEnabled || combat == null || subscribedCombat == combat) return;
            Unsubscribe();
            subscribedCombat = combat;
            subscribedCombat.RewardResolved += OnReward;
        }

        private void Unsubscribe()
        {
            if (subscribedCombat != null) subscribedCombat.RewardResolved -= OnReward;
            subscribedCombat = null;
        }

        private void OnReward(CombatRewardResolved reward)
        {
            if (reward.RunId != runId || reward.Experience <= 0) return;
            // Read the authoritative cumulative amount instead of summing repeatable notifications.
            if (combat != null && combat.CurrentRunId == runId)
                Display(AddSaturating(runStartExperience, combat.ExperienceEarned));
        }

        private void Refresh()
        {
            if (combat != null && combat.IsRunActive && combat.CurrentRunId == runId)
                Display(AddSaturating(runStartExperience, combat.ExperienceEarned));
            else Display(resultExperience ?? SavedExperience);
        }

        private void Display(long total)
        {
            total = Math.Max(0, total);
            if (displayedExperience == total) return;
            displayedExperience = total;
            int level = ProgressionLevelCurve.GetLevel(total);
            bool maximum = level >= ProgressionLevelCurve.MaximumLevel;
            long required = ProgressionLevelCurve.ExperienceForNextLevel(level);
            long current = maximum ? 0 : total - ProgressionLevelCurve.TotalExperienceForLevel(level);
            if (levelText != null) levelText.text = level.ToString(CultureInfo.InvariantCulture);
            if (experienceText != null)
                experienceText.text = maximum ? "MAX" : current.ToString(CultureInfo.InvariantCulture) +
                    " / " + required.ToString(CultureInfo.InvariantCulture);
            if (experienceSlider != null)
                experienceSlider.SetValueWithoutNotify(maximum ? 1 : (float)((double)current / required));
        }

        private static long AddSaturating(long left, long right) => right > long.MaxValue - left ? long.MaxValue : left + right;
    }
}
