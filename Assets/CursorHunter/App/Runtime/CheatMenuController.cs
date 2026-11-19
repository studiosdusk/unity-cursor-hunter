using System;
using System.Collections.Generic;
using CursorHunter.Progression;
using CursorHunter.Progression.ResearchPrototype;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CursorHunter.App
{
    [Serializable]
    public sealed class CheatButtonBinding
    {
        [Tooltip("CheatTable의 ID와 일치해야 합니다.")] public string id;
        public Button button;
    }

    /// <summary>Always-active listener and action bindings for the scene-authored cheat menu.</summary>
    [DisallowMultipleComponent]
    public sealed class CheatMenuController : MonoBehaviour
    {
        [SerializeField] private GameObject cheatRoot;
        [SerializeField] private CheatTable table;
        [SerializeField] private List<CheatButtonBinding> buttonBindings = new List<CheatButtonBinding>();
        [SerializeField] private GameObject idleStatus, successStatus, failureStatus, maximumLevelStatus;
        [SerializeField] private HuntManager hunt;
        [SerializeField] private TraitScreenController progression;
        [SerializeField] private ResearchTestMap researchMap;

        private readonly List<(Button button, UnityAction action)> listeners = new List<(Button, UnityAction)>();
        private bool bound, restarting;

        private void Awake()
        {
            if (cheatRoot != null) cheatRoot.SetActive(false);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.equalsKey.wasPressedThisFrame && !keyboard.shiftKey.isPressed &&
                !IsEditingText()) Toggle();
        }

        private void OnDestroy()
        {
            foreach (var listener in listeners)
                if (listener.button != null) listener.button.onClick.RemoveListener(listener.action);
        }

        public void Toggle()
        {
            if (cheatRoot == null || restarting) return;
            if (!cheatRoot.activeSelf)
            {
                if (!bound && !BindButtons()) return;
                SetStatus(null);
                cheatRoot.transform.SetAsLastSibling();
                cheatRoot.SetActive(true);
            }
            else cheatRoot.SetActive(false);
        }

        private static bool IsEditingText()
        {
            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected == null) return false;
            var tmp = selected.GetComponent<TMP_InputField>();
            var input = selected.GetComponent<InputField>();
            return (tmp != null && tmp.isFocused) || (input != null && input.isFocused);
        }

        private bool BindButtons()
        {
            if (table == null || table.Entries == null || buttonBindings == null)
            {
                Debug.LogError("Cheat menu needs its table and scene button bindings.", this);
                return false;
            }
            var rows = new Dictionary<string, CheatRow>(StringComparer.Ordinal);
            var invalidIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (CheatRow row in table.Entries)
            {
                if (row == null || string.IsNullOrWhiteSpace(row.id))
                {
                    Debug.LogWarning("Cheat table contains an empty row ID.", table);
                    continue;
                }
                if (invalidIds.Contains(row.id)) continue;
                if (rows.ContainsKey(row.id))
                {
                    rows.Remove(row.id);
                    invalidIds.Add(row.id);
                    Debug.LogWarning("Cheat table contains duplicate ID: " + row.id, table);
                    continue;
                }
                rows.Add(row.id, row);
            }

            var seenButtons = new HashSet<Button>();
            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (CheatButtonBinding binding in buttonBindings)
            {
                if (binding == null || binding.button == null || string.IsNullOrWhiteSpace(binding.id))
                {
                    Debug.LogWarning("Cheat menu has an incomplete scene button binding.", this);
                    continue;
                }
                if (!seenButtons.Add(binding.button) || !seenIds.Add(binding.id))
                {
                    Debug.LogWarning("Cheat menu has a duplicate button or ID: " + binding.id, this);
                    continue;
                }
                bool available = rows.TryGetValue(binding.id, out CheatRow row) && row.enabled;
                binding.button.gameObject.SetActive(available);
                if (!available) continue;
                UnityAction listener = () => Execute(row);
                binding.button.onClick.AddListener(listener);
                listeners.Add((binding.button, listener));
            }
            foreach (CheatRow row in rows.Values)
                if (row.enabled && !seenIds.Contains(row.id))
                    Debug.LogWarning("Cheat table row has no scene button: " + row.id, this);
            bound = true;
            return true;
        }

        private void Execute(CheatRow row)
        {
            if (restarting) return;
            bool success = false;
            try
            {
                if (hunt != null && progression != null)
                {
                    if (progression.IsProfileLoadBlocked && row.action != CheatAction.ResetAllProgress)
                    {
                        SetStatus(false);
                        return;
                    }
                    if (row.action == CheatAction.ResetTraitCards)
                        success = hunt.TryResetTraitCardProgress();
                    else if (hunt.TrySavePendingRunRewards())
                    {
                        switch (row.action)
                        {
                            case CheatAction.ResetResearch:
                                success = researchMap != null && researchMap.progression == progression && researchMap.TryResetResearchProgress();
                                break;
                            case CheatAction.ResetRelics: success = progression.TryResetRelicsForTesting(); break;
                            case CheatAction.AddAllCurrencies: success = progression.TryChangeCurrenciesForTesting(row.amount, false); break;
                            case CheatAction.ClearAllCurrencies: success = progression.TryChangeCurrenciesForTesting(0, true); break;
                            case CheatAction.ResetAllProgress: success = RestartWithFreshProfile(); break;
                            case CheatAction.IncreaseUserLevel:
                                if (ProgressionLevelCurve.GetLevel(progression.ExperiencePoints) >= ProgressionLevelCurve.MaximumLevel)
                                {
                                    SetStatus(false, true);
                                    return;
                                }
                                success = progression.TryIncreaseUserLevelForTesting();
                                if (success) hunt.RefreshExperienceAfterTestChange();
                                break;
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
            SetStatus(success);
        }

        private bool RestartWithFreshProfile()
        {
            int sceneIndex = SceneUtility.GetBuildIndexByScenePath(gameObject.scene.path);
            if (sceneIndex < 0 || !progression.TryResetAllProgressForTesting()) return false;

            restarting = true;
            hunt.ResetPrototypeRun();
            cheatRoot.SetActive(false);
            SceneManager.LoadScene(sceneIndex, LoadSceneMode.Single);
            return true;
        }

        private void SetStatus(bool? success, bool maximumLevel = false)
        {
            if (idleStatus != null) idleStatus.SetActive(!success.HasValue && !maximumLevel);
            if (successStatus != null) successStatus.SetActive(success == true && !maximumLevel);
            if (failureStatus != null) failureStatus.SetActive(success == false && !maximumLevel);
            if (maximumLevelStatus != null) maximumLevelStatus.SetActive(maximumLevel);
        }
    }
}
