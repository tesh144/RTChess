using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MusicTrigger : MonoBehaviour
{
    public AudioClip clipOnEnable;
    // Start is called before the first frame update
    void Start()
    {
        if (MusicSystem.instance != null) MusicSystem.instance.StartMusic();
    }

    private void OnEnable()
    {
        if (MusicSystem.instance != null) MusicSystem.instance.source.PlayOneShot(clipOnEnable);
    }
}
