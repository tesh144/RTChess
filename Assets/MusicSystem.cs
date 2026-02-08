using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MusicSystem : MonoBehaviour
{
    public static MusicSystem instance;

    public AudioSource source;
    // Start is called before the first frame update

    private void Awake()
    {
        instance = this;
    }

    public void StartMusic()
    {
        if (!source.isPlaying) source.Play();
    }
}
