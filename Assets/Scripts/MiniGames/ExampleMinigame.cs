using System.Collections;
using UnityEngine;
using TMPro;

// Simple example minigame: waits 5 seconds, then finishes and disappears.
// Optionally shows a countdown on a TMP label if one is assigned/found.
public class ExampleMinigame : MinigameBase
{
    [SerializeField] private float duration = 5f;
    [Tooltip("Optional countdown label. Auto-found in children if left empty.")]
    [SerializeField] private TMP_Text countdownLabel;

    public override void StartGame(MinigameContext context)
    {
        if (countdownLabel == null)
        {
            countdownLabel = GetComponentInChildren<TMP_Text>(true);
        }
        StartCoroutine(RunTimer());
    }

    private IEnumerator RunTimer()
    {
        float remaining = duration;
        while (remaining > 0f)
        {
            if (countdownLabel != null)
            {
                countdownLabel.text = Mathf.CeilToInt(remaining).ToString();
            }
            remaining -= Time.deltaTime;
            yield return null;
        }

        // Timer ran out - close the minigame.
        Win();
    }
}
