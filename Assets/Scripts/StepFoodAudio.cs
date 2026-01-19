using System;
using UnityEngine;

public class StepFoodAudio : MonoBehaviour
{
    [Header("Music")]
    public AudioSource musicSource;
    public AudioClip backgroundMusic;
    
    [Header("SFX")]
    public AudioSource sfxSource;
    public AudioClip stepClip;
    public AudioClip ingredientClearClip;
    public AudioClip cakeReadyClip;

    private void Awake()
    {
        if (musicSource && backgroundMusic)
        {
            musicSource.clip = backgroundMusic;
            musicSource.loop = true;
            if (!musicSource.isPlaying)
            {
                musicSource.Play();
            }
        }
    }

    public void PlayStep()
    {
        if (sfxSource && stepClip)
        {
            sfxSource.PlayOneShot(stepClip);
        }
    }
    
    public void PlayIngredientClear()
    {
        if (sfxSource && ingredientClearClip)
        {
            sfxSource.PlayOneShot(ingredientClearClip);
        }
    }
    
    public void PlayCakeReady()
    {
        if (sfxSource && cakeReadyClip)
        {
            sfxSource.PlayOneShot(cakeReadyClip);
        }
    }

    public float GetPourDuration()
    {
        return ingredientClearClip ? ingredientClearClip.length : 0;
    }
}
