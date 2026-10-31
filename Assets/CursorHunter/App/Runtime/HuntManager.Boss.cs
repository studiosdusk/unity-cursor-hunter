using System;
using CursorHunter.Data;
using CursorHunter.Contracts;
using UnityEngine;

namespace CursorHunter.App
{
    public sealed partial class HuntManager
    {
        [Header("Boss challenge")]
        [SerializeField] private BossDefinition[] bosses = Array.Empty<BossDefinition>();
        [SerializeField] private BossHealthPresenter bossHealth;
        private bool _lastRunWasBoss, _lastBossSucceeded;
        private int _lastBossStage, _sessionHighestBossStage;
        private RunResult _pendingBossSettlement;
        public bool IsBossDeathPlaying => monsterSpawner != null && monsterSpawner.IsBossDeathPlaying;
        public int HighestBossStageCleared => connectTraitScreenToCombat && progressionController != null
            ? progressionController.HighestBossStageCleared : _sessionHighestBossStage;

        public void BeginBossRun()
        {
            ResolveReferences();
            BeginBossStage(Math.Min(BossDefinition.StageCount, HighestBossStageCleared + 1));
        }

        /// <summary>Button OnClick: reset saved boss progress so the next challenge starts at stage one.</summary>
        public void ResetBossProgress()
        {
            ResolveReferences();
            if (connectTraitScreenToCombat && progressionController == null)
            {
                Debug.LogWarning("보스 진행도를 초기화할 특성 저장 시스템을 찾지 못했습니다.", this);
                return;
            }
            // Finish an already earned payout before resetting its stage record.
            if (!TrySavePendingBossReward() ||
                progressionController != null && !progressionController.TryResetBossProgress())
            {
                Debug.LogWarning("보스 진행도 초기화를 저장하지 못했습니다. 다시 시도해 주세요.", this);
                return;
            }
            if (_lastRunWasBoss)
            {
                ResetPrototypeRun();
                if (uiRootController != null && uiRootController.CurrentScreen == UiScreenId.Combat)
                    uiRootController.ShowMainMenu();
            }
            _sessionHighestBossStage = 0;
            _lastBossStage = 0;
            _lastRunWasBoss = false;
            _lastBossSucceeded = false;
            _pendingBossSettlement = default;
        }

        public void BeginBossStage(int stage)
        {
            ResolveReferences();
            if (stage < 1 || stage > BossDefinition.StageCount) return;
#if UNITY_EDITOR
            if (!HasCombatBalanceSettings && stage > HighestBossStageCleared + 1) return;
#else
            if (stage > HighestBossStageCleared + 1) return;
#endif
            if (bosses == null || bosses.Length == 0) bosses = Resources.LoadAll<BossDefinition>("BossDefinitions");
            foreach (BossDefinition boss in bosses)
                if (boss != null && boss.Stage == stage && boss.IsValid)
                {
                    BeginRun(null, boss);
                    return;
                }
            Debug.LogError($"Boss stage {stage} has no valid definition.", this);
        }

        public void ContinueFromResult()
        {
            if (!TrySavePendingBossReward()) return;
            if (!_lastRunWasBoss || !_lastBossSucceeded) { ReplayLastRun(); return; }
            if (_lastBossStage < BossDefinition.StageCount) BeginBossStage(_lastBossStage + 1);
            else BeginPrototypeRun();
        }

        public void OpenUpgrades()
        {
            if (!TrySavePendingBossReward()) return;
            ResetPrototypeRun();
            testPanelToggleController?.HideTestPanel();
            uiRootController?.ShowTrait();
        }

        public bool TrySavePendingBossReward()
        {
            if (!_pendingBossSettlement.RunId.IsValid) return true;
            SettleRunRewards(_pendingBossSettlement);
            return !_pendingBossSettlement.RunId.IsValid;
        }
    }
}
