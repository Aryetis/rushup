using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MusicHolderBehavior : MonoBehaviour
{
    void Awake()
    {
        // Don't destroy this object during LoadScene()
        DontDestroyOnLoad(gameObject);
    }
}
