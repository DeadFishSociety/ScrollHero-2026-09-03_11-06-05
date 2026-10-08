using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Drives a single TopUI Image through spritesheet animations chosen by the player's
// current life count. The whole state is the life count, so:
//
//   life 1 = despair,  life 2 = normal,  life 3 = overdrive (golden)
//
// (Overdrive lifts the player to max lives, the last life is 1 - so the life count
// alone tells us which look to show. There is no separate despair/overdrive state.)
//
// Each life count has a looping idle animation and, optionally, a one-shot
// transition animation played when the player ENTERS that life count, before its
// loop takes over. Only the life counts you give a transition animate one; any
// other change (e.g. dropping to the normal life, or a count with no transition
// assigned) switches straight to the loop with no transition - per the rule: if a
// change isn't covered by a transition, don't play one.
//
// Self-wires off DopamineManager.OnLivesChanged, like the rest of the TopUI. Put it
// on an Image in the TopUI prefab.
[RequireComponent(typeof(Image))]
public class TopUIStateAnimator : MonoBehaviour
{
    [Serializable]
    public class Clip
    {
        [Tooltip("Frames played in order. Empty = nothing to play (treated as 'no animation / no rule').")]
        public Sprite[] frames;

        [Tooltip("Playback speed for this clip, in FPS. 0 = use the component's Default Fps.")]
        [Min(0f)]
        public float fps = 0f;

        public bool HasFrames => frames != null && frames.Length > 0;
    }

    [Header("Playback")]
    [Tooltip("Default playback speed (FPS) for clips that don't set their own.")]
    [SerializeField] private float defaultFps = 12f;

    [Tooltip("Use unscaled time so animations keep running while the game is paused / time-scaled.")]
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Idle loops (indexed by life count: element [i] = i lives)")]
    [Tooltip("Looping idle animation per life count. Element [i] plays while the player has i lives. For 3 lives, fill 0..3 (1 = despair, 2 = normal, 3 = overdrive).")]
    [SerializeField] private Clip[] lifeLoops;

    [Header("Enter transitions (indexed by life count, optional)")]
    [Tooltip("One-shot animation played when ENTERING that life count, before its idle loop. Element [i] plays on entering i lives. Leave an element empty for no transition (e.g. the normal life).")]
    [SerializeField] private Clip[] lifeTransitions;

    private Image image;
    private Coroutine routine;

    // Latest known life count. Starts at -1 so the first OnLivesChanged always applies.
    private int lives = -1;

    private void Awake()
    {
        image = GetComponent<Image>();
    }

    private void OnEnable()
    {
        DopamineManager.OnLivesChanged += OnLivesChanged;
    }

    private void OnDisable()
    {
        DopamineManager.OnLivesChanged -= OnLivesChanged;
    }

    private void OnLivesChanged(int newLives, int maxLives)
    {
        if (newLives == lives)
        {
            return;
        }
        lives = newLives;

        Clip loop = LoopFor(lives);
        Clip transition = TransitionFor(lives);

        if (transition != null && transition.HasFrames)
        {
            // This life count is covered by a transition: play it once, then loop.
            PlayTransitionThenLoop(transition, loop);
        }
        else
        {
            // Not covered by a transition: switch straight to the idle loop.
            PlayLoop(loop);
        }
    }

    private Clip LoopFor(int lifeCount)
    {
        if (lifeLoops == null || lifeLoops.Length == 0)
        {
            return null;
        }
        int index = Mathf.Clamp(lifeCount, 0, lifeLoops.Length - 1);
        return lifeLoops[index];
    }

    private Clip TransitionFor(int lifeCount)
    {
        if (lifeTransitions == null || lifeCount < 0 || lifeCount >= lifeTransitions.Length)
        {
            return null;
        }
        return lifeTransitions[lifeCount];
    }

    // --- Playback ------------------------------------------------------------

    private void PlayTransitionThenLoop(Clip transition, Clip loopClip)
    {
        StopRoutine();
        routine = StartCoroutine(TransitionThenLoop(transition, loopClip));
    }

    private void PlayLoop(Clip loopClip)
    {
        StopRoutine();
        if (loopClip == null || !loopClip.HasFrames)
        {
            return; // nothing configured for this life - leave the current frame as-is
        }
        routine = StartCoroutine(Loop(loopClip));
    }

    private IEnumerator TransitionThenLoop(Clip transition, Clip loopClip)
    {
        yield return PlayOnce(transition);

        if (loopClip != null && loopClip.HasFrames)
        {
            yield return Loop(loopClip);
        }
        else
        {
            routine = null; // no idle loop configured - hold the transition's last frame
        }
    }

    private IEnumerator PlayOnce(Clip clip)
    {
        float frameDuration = FrameDuration(clip);
        for (int i = 0; i < clip.frames.Length; i++)
        {
            image.sprite = clip.frames[i];
            yield return WaitFrame(frameDuration);
        }
    }

    private IEnumerator Loop(Clip clip)
    {
        float frameDuration = FrameDuration(clip);
        int i = 0;
        while (true)
        {
            image.sprite = clip.frames[i];
            i = (i + 1) % clip.frames.Length;
            yield return WaitFrame(frameDuration);
        }
    }

    private float FrameDuration(Clip clip)
    {
        float fps = clip.fps > 0f ? clip.fps : defaultFps;
        return 1f / Mathf.Max(1f, fps);
    }

    private object WaitFrame(float seconds)
    {
        if (useUnscaledTime)
        {
            return new WaitForSecondsRealtime(seconds);
        }
        return new WaitForSeconds(seconds);
    }

    private void StopRoutine()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
    }
}
