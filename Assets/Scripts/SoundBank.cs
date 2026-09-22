using UnityEngine;

// One clip in a SoundBank, with a relative weight for random selection.
[System.Serializable]
public class WeightedSound
{
    public AudioClip clip;
    [Tooltip("Relative chance of being picked. Higher = more often.")]
    [Min(0f)] public float weight = 1f;
}

// A set of interchangeable sounds picked at random (weighted) so a repeated
// action doesn't always play the same clip. Assign clips in the inspector and
// call PlayOneShot to fire a random one through an AudioSource.
[System.Serializable]
public class SoundBank
{
    [SerializeField] private WeightedSound[] sounds;

    [Tooltip("Overall volume for sounds from this bank.")]
    [Range(0f, 1f)]
    [SerializeField] private float volume = 1f;

    // Picks a random clip, biased by weight. Null if the bank is empty.
    public AudioClip PickRandom()
    {
        if (sounds == null || sounds.Length == 0)
        {
            return null;
        }

        float total = 0f;
        foreach (WeightedSound s in sounds)
        {
            if (s != null && s.clip != null)
            {
                total += Mathf.Max(0f, s.weight);
            }
        }

        // No usable weights: uniform pick among the valid clips (reservoir sample).
        if (total <= 0f)
        {
            AudioClip chosen = null;
            int seen = 0;
            foreach (WeightedSound s in sounds)
            {
                if (s == null || s.clip == null)
                {
                    continue;
                }
                seen++;
                if (Random.value < 1f / seen)
                {
                    chosen = s.clip;
                }
            }
            return chosen;
        }

        float roll = Random.value * total;
        foreach (WeightedSound s in sounds)
        {
            if (s == null || s.clip == null)
            {
                continue;
            }
            roll -= Mathf.Max(0f, s.weight);
            if (roll <= 0f)
            {
                return s.clip;
            }
        }

        // Floating-point safety net: last valid clip.
        for (int i = sounds.Length - 1; i >= 0; i--)
        {
            if (sounds[i] != null && sounds[i].clip != null)
            {
                return sounds[i].clip;
            }
        }
        return null;
    }

    // Plays a random clip from the bank through the given source (layers over
    // anything already playing). No-op if the source or bank is empty.
    public void PlayOneShot(AudioSource source)
    {
        if (source == null)
        {
            return;
        }
        AudioClip clip = PickRandom();
        if (clip != null)
        {
            source.PlayOneShot(clip, volume);
        }
    }
}
