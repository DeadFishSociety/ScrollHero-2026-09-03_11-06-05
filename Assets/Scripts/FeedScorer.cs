using System;
using UnityEngine;

// Awards score for what the player does in the feed. Add this to the Feed object
// (next to ReelFeedController and MinigameManager). It listens for reel swipes,
// likes and finished minigames, then hands the points to the ScoreManager, which
// shows the total in the score text field on the main scene.
//
// This is the one place scoring rules live: tune the point values here per
// minigame, per swipe and per like without touching the feed or minigame code.
[RequireComponent(typeof(ReelFeedController))]
public class FeedScorer : MonoBehaviour
{
    // Score for one kind of minigame, matched by its prefab.
    [Serializable]
    public class MinigameScore
    {
        [Tooltip("The minigame prefab - the same one listed on the MinigameManager.")]
        public GameObject prefab;

        [Tooltip("Points awarded when this minigame is won.")]
        public int wonScore = 100;

        [Tooltip("Points awarded (or, if negative, lost) when this minigame is lost.")]
        public int lostScore = 0;
    }

    [Header("Score target")]
    [Tooltip("The ScoreManager that stores and displays the score. Leave empty to use the scene's ScoreManager automatically.")]
    [SerializeField] private ScoreManager scoreManager;

    [Header("Reel actions")]
    [Tooltip("Points awarded each time a reel is swiped away.")]
    [SerializeField] private int swipeScore = 5;

    [Tooltip("Points awarded when a reel is liked.")]
    [SerializeField] private int likeScore = 10;

    [Header("Minigames")]
    [Tooltip("Points per minigame, keyed by prefab. A minigame not listed here awards nothing.")]
    [SerializeField] private MinigameScore[] minigameScores;

    private ReelFeedController feed;
    private MinigameManager minigames;

    private void Awake()
    {
        feed = GetComponent<ReelFeedController>();
        minigames = GetComponent<MinigameManager>();
    }

    private void OnEnable()
    {
        if (feed != null)
        {
            feed.ScrollCommitted += OnReelSwiped;
        }
        if (minigames != null)
        {
            minigames.MinigameFinished += OnMinigameFinished;
        }
        ReelLike.AnyReelLiked += OnReelLiked;
    }

    private void OnDisable()
    {
        if (feed != null)
        {
            feed.ScrollCommitted -= OnReelSwiped;
        }
        if (minigames != null)
        {
            minigames.MinigameFinished -= OnMinigameFinished;
        }
        ReelLike.AnyReelLiked -= OnReelLiked;
    }

    // Fires once per reel advanced (including each reel crossed on a fast flick).
    private void OnReelSwiped()
    {
        Award(swipeScore);
    }

    private void OnReelLiked()
    {
        Award(likeScore);
    }

    private void OnMinigameFinished(GameObject prefab, MinigameOutcome outcome)
    {
        if (minigameScores == null)
        {
            return;
        }
        for (int i = 0; i < minigameScores.Length; i++)
        {
            MinigameScore entry = minigameScores[i];
            if (entry != null && entry.prefab == prefab)
            {
                Award(outcome == MinigameOutcome.Won ? entry.wonScore : entry.lostScore);
                return;
            }
        }
    }

    private void Award(int amount)
    {
        if (amount == 0)
        {
            return;
        }

        ScoreManager target = scoreManager != null ? scoreManager : ScoreManager.Instance;
        if (target != null)
        {
            target.Add(amount);
        }
        else
        {
            Debug.LogWarning("FeedScorer: no ScoreManager found to receive score.", this);
        }
    }
}
