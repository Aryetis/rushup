using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine;

public class LevelManager : MonoBehaviour
{
    public void loadLevel(string name)
    {
        SceneManager.LoadScene(name);
    }

    public void quit()
    {
        Application.Quit();
    }

    void Awake()
    {
        // Don't destroy this object during LoadScene()
        DontDestroyOnLoad(gameObject);
    }
}
