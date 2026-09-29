using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Builds the cheat/test menu at runtime: one button per minigame (each loads the
// GameScene and opens that minigame immediately, then normal scrolling resumes
// once it finishes), a "scroll mode" button that starts a normal run with dopamine
// drain disabled, and a back button.
//
// Adding a new minigame later is just adding an entry to the Minigames list in the
// Inspector - a matching button appears automatically, no code changes.
public class CheatMenu : MonoBehaviour
{
    [Serializable]
    public struct MinigameOption
    {
        [Tooltip("Text shown on the button.")]
        public string label;

        [Tooltip("The minigame prefab to play (any prefab with a MinigameBase-derived component).")]
        public GameObject prefab;
    }

    [Header("Minigames")]
    [Tooltip("One button is created per entry. Add more here as you add minigames.")]
    [SerializeField] private MinigameOption[] minigames;

    [Header("References")]
    [Tooltip("Parent the spawned buttons go under (e.g. a vertical layout group).")]
    [SerializeField] private Transform buttonContainer;

    [Tooltip("Button prefab to spawn. Should contain a Button and a TMP_Text (in itself or a child) for the label.")]
    [SerializeField] private GameObject buttonPrefab;

    [Header("Extra buttons")]
    [SerializeField] private string scrollModeLabel = "Scroll Mode (no drain)";
    [SerializeField] private string backLabel = "Back";

    [Header("Scenes")]
    [SerializeField] private string gameSceneName = "GameScene";
    [SerializeField] private string mainMenuSceneName = "MainMenuScene";

    private void Start()
    {
        BuildButtons();
    }

    private void BuildButtons()
    {
        if (buttonPrefab == null || buttonContainer == null)
        {
            Debug.LogError("CheatMenu: Button Prefab and Button Container must be assigned.", this);
            return;
        }

        if (minigames != null)
        {
            foreach (MinigameOption option in minigames)
            {
                GameObject prefab = option.prefab; // capture for the closure
                SpawnButton(option.label, () => PlayMinigame(prefab));
            }
        }

        SpawnButton(scrollModeLabel, StartScrollMode);
        SpawnButton(backLabel, () => SceneManager.LoadScene(mainMenuSceneName));
    }

    private void SpawnButton(string label, Action onClick)
    {
        GameObject go = Instantiate(buttonPrefab, buttonContainer, false);

        TMP_Text text = go.GetComponentInChildren<TMP_Text>();
        if (text != null)
        {
            text.text = label;
        }

        Button button = go.GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(() => onClick());
        }
    }

    // Loads the GameScene and asks the MinigameManager there to open this minigame
    // straight away (see MinigameManager.Start / PlaySpecific).
    private void PlayMinigame(GameObject prefab)
    {
        if (prefab == null)
        {
            return;
        }

        CheatState.RequestedMinigame = prefab;
        // Come back to this cheat scene once the minigame finishes.
        CheatState.ReturnToScene = SceneManager.GetActiveScene().name;
        SceneManager.LoadScene(gameSceneName);
    }

    private void StartScrollMode()
    {
        CheatState.NoDopamineDrain = true;
        SceneManager.LoadScene(gameSceneName);
    }
}
