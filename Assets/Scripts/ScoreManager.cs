using System;
using TMPro;
using UnityEngine;

// Holds the running score and shows it in the score text field on the main
// scene. Put this on the object that owns the score label (or any persistent
// scene object) and drag the TMP_Text into Score Label.
//
// It is deliberately a plain store + display: the FeedScorer (on the Feed) is
// what decides when and how many points to add, keeping scoring rules out of
// here. Mirrors DopamineManager's static-event style so other UI can react.
public class ScoreManager : MonoBehaviour
{
    // Convenience access for the FeedScorer when no explicit reference is wired.
    public static ScoreManager Instance { get; private set; }

    // Raised whenever the score changes, with the new total.
    public static event Action<int> OnScoreChanged;

    [Header("Display")]
    [Tooltip("The score text field on the main scene. Updated whenever the score changes.")]
    [SerializeField] private TMP_Text scoreLabel;

    [Tooltip("How the score is formatted into the label. {0} is the score value.")]
    [SerializeField] private string labelFormat = "{0}";

    [Header("State")]
    [Tooltip("Starting score.")]
    [SerializeField] private int score;

    public int Score => score;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // Keep the first one; a second manager would fight over the label.
            Debug.LogWarning("ScoreManager: another instance already exists.", this);
        }
        else
        {
            Instance = this;
        }
    }

    private void Start()
    {
        Refresh();
        OnScoreChanged?.Invoke(score);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // Adds (or, with a negative amount, subtracts) points.
    public void Add(int amount)
    {
        if (amount == 0)
        {
            return;
        }
        SetScore(score + amount);
    }

    // Sets the score directly (e.g. to reset it between runs).
    public void SetScore(int value)
    {
        score = value;
        Refresh();
        OnScoreChanged?.Invoke(score);
    }

    private void Refresh()
    {
        if (scoreLabel != null)
        {
            scoreLabel.text = string.Format(labelFormat, score);
        }
    }
}
