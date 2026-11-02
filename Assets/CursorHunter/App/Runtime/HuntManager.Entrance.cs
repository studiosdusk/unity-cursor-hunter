using System.Collections;
using CursorHunter.Contracts;
using UnityEngine;

namespace CursorHunter.App
{
    public sealed partial class HuntManager
    {
        [Header("Stage entrance")]
        [SerializeField] private bool playEntranceSequence;
        [SerializeField] private BattleResultPresentation battlePresentation;
        private Coroutine _entranceCoroutine;
        public bool IsPlayingEntrance => combatRunController != null && combatRunController.IsEntering;

        private void StartEntrancePresentation()
        {
            if (!IsPlayingEntrance) return;
            _entranceCoroutine = StartCoroutine(PlayEntrance(_runCoordinator.Session.Request.RunId));
        }

        private IEnumerator PlayEntrance(RunId runId)
        {
            // Render an empty field before advancing any of the initial popups.
            yield return null;
            while (OwnsEntrance(runId) && !monsterSpawner.IsInitialEntranceComplete)
            {
                if (!ShouldPauseForInspection()) monsterSpawner.AdvanceInitialEntrance(Time.deltaTime);
                yield return null;
            }
            if (!OwnsEntrance(runId)) yield break;
            battlePresentation.ShowStart(runId, ShouldPauseForInspection());
            while (OwnsEntrance(runId) && battlePresentation.IsShowingStart)
            {
                battlePresentation.SetStartPaused(ShouldPauseForInspection());
                yield return null;
            }
            while (OwnsEntrance(runId) && (ShouldPauseForInspection() || Time.timeScale <= 0f))
                yield return null;
            if (OwnsEntrance(runId) && _runCoordinator.FinishEntrance())
            {
                RefreshRetireButton();
                runHud?.ShowRunning(combatRunController, monsterSpawner);
            }
            _entranceCoroutine = null;
        }

        private bool OwnsEntrance(RunId runId) => _runCoordinator != null &&
            _runCoordinator.State == RunState.Starting && IsPlayingEntrance &&
            _runCoordinator.Session.Request.RunId == runId;

        private void CancelEntrancePresentation()
        {
            if (_entranceCoroutine != null) StopCoroutine(_entranceCoroutine);
            _entranceCoroutine = null;
            if (battlePresentation != null && battlePresentation.IsShowingStart) battlePresentation.Hide();
        }

        private void HandleEntranceScreenChanged(UiScreenId previous, UiScreenId current)
        {
            if (current != UiScreenId.Combat && IsPlayingEntrance) ResetPrototypeRun();
        }
    }
}
