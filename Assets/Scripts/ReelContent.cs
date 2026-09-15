using UnityEngine;
using TMPro;

// Per-reel content: the username, description and audio-name labels, plus an
// optional custom audio clip that plays while the reel is on screen.
//
// Put this on the Reel root. The label references are optional - if left empty
// they are auto-found by child name ("UserName", "Description", "MusicText"),
// and an AudioSource is added automatically.
[RequireComponent(typeof(AudioSource))]
public class ReelContent : MonoBehaviour
{
    [Header("Content")]
    [SerializeField] private string username = "SuperCoolUser_Name";
    [TextArea(2, 5)]
    [SerializeField] private string description = "";
    [SerializeField] private string audioName = "Original audio";
    [Tooltip("Custom audio that plays while this reel is on screen. Leave empty for none.")]
    [SerializeField] private AudioClip audioClip;

    [Header("Audio playback")]
    [SerializeField] private bool playAudioOnEnable = true;
    [SerializeField] private bool loopAudio = true;
    [Range(0f, 1f)]
    [SerializeField] private float volume = 1f;

    [Header("Label references (optional - auto-found by name if empty)")]
    [SerializeField] private TMP_Text usernameLabel;
    [SerializeField] private TMP_Text descriptionLabel;
    [SerializeField] private TMP_Text audioNameLabel;

    private AudioSource audioSource;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;

        ResolveLabels();
        Apply();
    }

    void OnEnable()
    {
        if (playAudioOnEnable)
        {
            PlayAudio();
        }
    }

    void OnDisable()
    {
        StopAudio();
    }

    // Push all values into the UI and refresh the audio source settings.
    public void Apply()
    {
        if (usernameLabel != null) usernameLabel.text = username;
        if (descriptionLabel != null) descriptionLabel.text = description;
        if (audioNameLabel != null) audioNameLabel.text = audioName;

        if (audioSource != null)
        {
            audioSource.clip = audioClip;
            audioSource.loop = loopAudio;
            audioSource.volume = volume;
        }
    }

    // --- Public API for a feed controller ------------------------------------

    public void SetContent(string user, string desc, string audio, AudioClip clip)
    {
        username = user;
        description = desc;
        audioName = audio;
        audioClip = clip;

        // Note: audio is NOT started here. The feed decides which reel plays
        // (only the top one), via PlayAudio()/StopAudio().
        Apply();
    }

    public void PlayAudio()
    {
        if (audioSource == null || audioClip == null)
        {
            return;
        }
        audioSource.clip = audioClip;
        audioSource.loop = loopAudio;
        audioSource.volume = volume;
        audioSource.Play();
    }

    public void StopAudio()
    {
        if (audioSource != null)
        {
            audioSource.Stop();
        }
    }

    private void ResolveLabels()
    {
        if (usernameLabel == null) usernameLabel = FindLabel("UserName");
        if (descriptionLabel == null) descriptionLabel = FindLabel("Description");
        if (audioNameLabel == null) audioNameLabel = FindLabel("MusicText");
    }

    private TMP_Text FindLabel(string childName)
    {
        Transform[] all = GetComponentsInChildren<Transform>(true);
        foreach (Transform t in all)
        {
            if (t.name == childName)
            {
                TMP_Text label = t.GetComponent<TMP_Text>();
                if (label != null)
                {
                    return label;
                }
            }
        }
        return null;
    }

#if UNITY_EDITOR
    // Live-update the labels in the editor as you type into the fields.
    void OnValidate()
    {
        if (Application.isPlaying)
        {
            return;
        }
        ResolveLabels();
        if (usernameLabel != null) usernameLabel.text = username;
        if (descriptionLabel != null) descriptionLabel.text = description;
        if (audioNameLabel != null) audioNameLabel.text = audioName;
    }
#endif
}
