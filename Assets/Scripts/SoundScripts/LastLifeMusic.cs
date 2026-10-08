using UnityEngine;

// Plays a separate soundtrack while the player is on their last life, replacing
// the normal background music (it stops while the last-life track plays). Returns
// to the theme once they're no longer on the last life. Leaves the music alone
// during overdrive, since the OverdriveController owns it then.
//
// Put this on a live GameObject (e.g. the Feed object) and assign the clip.
public class LastLifeMusic : MonoBehaviour
{
    [Tooltip("Soundtrack played while on the last life, in place of the theme. Leave empty for none.")]
    [SerializeField] private AudioClip lastLifeMusic;

    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.6f;

    [Tooltip("The MusicPlayer to override. Auto-found if empty.")]
    [SerializeField] private MusicPlayer musicPlayer;

    // True while our override is the one currently in effect.
    private bool active;

    private void Awake()
    {
        if (musicPlayer == null)
        {
            musicPlayer = FindFirstObjectByType<MusicPlayer>(FindObjectsInactive.Include);
        }
    }

    private void OnEnable()
    {
        DopamineManager.OnLivesChanged += OnLivesChanged;
    }

    private void OnDisable()
    {
        DopamineManager.OnLivesChanged -= OnLivesChanged;
        if (active && musicPlayer != null)
        {
            musicPlayer.StopOverride();
            active = false;
        }
    }

    private void OnLivesChanged(int lives, int maxLives)
    {
        if (musicPlayer == null || lastLifeMusic == null)
        {
            return;
        }

        // During overdrive the OverdriveController owns the music - don't fight it.
        if (OverdriveController.IsActive)
        {
            return;
        }

        bool wantLastLife = lives <= 1; // last life (1) through game over (0)
        if (wantLastLife && !active)
        {
            musicPlayer.PlayOverride(lastLifeMusic, volume);
            active = true;
        }
        else if (!wantLastLife && active)
        {
            musicPlayer.StopOverride();
            active = false;
        }
    }
}
