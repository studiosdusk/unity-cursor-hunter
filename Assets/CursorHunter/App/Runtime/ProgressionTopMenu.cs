using CursorHunter.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace CursorHunter.App
{
    /// <summary>Connects the authored top menu to the research map and existing collections.</summary>
    public sealed class ProgressionTopMenu : MonoBehaviour
    {
        [SerializeField] private AppUiRootController app;
        [SerializeField] private TraitScreenController progression;
        [SerializeField] private GameObject topMenu, bottomMenu;
        [SerializeField] private GameObject researchMap;
        [SerializeField] private Button researchButton, relicButton, talentButton, fieldButton;
        [SerializeField] private Button stageActionButton, battleActionButton;
        [SerializeField] private Button bossButton, huntButton, bottomStageButton, bottomBattleButton;
        [SerializeField] private HuntManager huntManager;
        [SerializeField] private Sprite selectedSprite, normalSprite;
        private int selected;

        private void Awake()
        {
            if (researchButton != null) researchButton.onClick.AddListener(ShowResearch);
            if (relicButton != null) relicButton.onClick.AddListener(ShowRelics);
            if (talentButton != null) talentButton.onClick.AddListener(ShowTalents);
            if (fieldButton != null) fieldButton.onClick.AddListener(ShowFields);
            if (stageActionButton != null) stageActionButton.onClick.AddListener(ShowFields);
            if (battleActionButton != null) battleActionButton.onClick.AddListener(StartBattle);
            if (bossButton != null) bossButton.onClick.AddListener(StartBoss);
            if (huntButton != null) huntButton.onClick.AddListener(StartBattle);
            if (bottomStageButton != null) bottomStageButton.onClick.AddListener(ShowFields);
            if (bottomBattleButton != null) bottomBattleButton.onClick.AddListener(StartBattle);
        }
        private void OnEnable()
        {
            if (app != null) app.ScreenChanged += OnScreenChanged;
        }
        private void Start() => ApplyScreen(app == null || app.CurrentScreen == UiScreenId.Trait);
        private void OnDisable()
        {
            if (app != null) app.ScreenChanged -= OnScreenChanged;
        }
        private void OnDestroy()
        {
            if (researchButton != null) researchButton.onClick.RemoveListener(ShowResearch);
            if (relicButton != null) relicButton.onClick.RemoveListener(ShowRelics);
            if (talentButton != null) talentButton.onClick.RemoveListener(ShowTalents);
            if (fieldButton != null) fieldButton.onClick.RemoveListener(ShowFields);
            if (stageActionButton != null) stageActionButton.onClick.RemoveListener(ShowFields);
            if (battleActionButton != null) battleActionButton.onClick.RemoveListener(StartBattle);
            if (bossButton != null) bossButton.onClick.RemoveListener(StartBoss);
            if (huntButton != null) huntButton.onClick.RemoveListener(StartBattle);
            if (bottomStageButton != null) bottomStageButton.onClick.RemoveListener(ShowFields);
            if (bottomBattleButton != null) bottomBattleButton.onClick.RemoveListener(StartBattle);
        }
        public void ShowResearch() => Select(0);
        public void ShowRelics() => Select(1);
        public void ShowTalents() => Select(2);
        public void ShowFields() => Select(3);
        public void StartBattle() { if (huntManager != null) huntManager.BeginPrototypeRun(); }
        public void StartBoss() { if (huntManager != null) huntManager.BeginBossRun(); }
        private void Select(int section)
        {
            selected = section;
            ApplyScreen(app == null || app.CurrentScreen == UiScreenId.Trait);
        }
        private void OnScreenChanged(UiScreenId previous, UiScreenId current) => ApplyScreen(current == UiScreenId.Trait);
        private void ApplyScreen(bool visible)
        {
            if (topMenu != null) topMenu.SetActive(visible);
            if (bottomMenu != null) bottomMenu.SetActive(visible);
            if (researchMap != null) researchMap.SetActive(visible && selected == 0);
            if (visible && progression != null) progression.SetExternalMenuSection(selected);
            SetSelected(researchButton, selected == 0);
            SetSelected(relicButton, selected == 1);
            SetSelected(talentButton, selected == 2);
            SetSelected(fieldButton, selected == 3);
        }
        private void SetSelected(Button button, bool isSelected)
        {
            if (button == null || button.image == null) return;
            Sprite sprite = isSelected ? selectedSprite : normalSprite;
            if (sprite != null) button.image.sprite = sprite;
        }
    }
}
