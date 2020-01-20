using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/**********************************************************************
 *                                                                    *
 * Simple script disabling randomly the section AreneV1 or AreneV2    *
 * of the level on startup                                            *
 *                                                                    *
 * WARNING : Because we're using GameObject.Find(), both Arene must   *
 * be set as active in the editor otherwise we won't be able to       *
 * find() them                                                        *
 *                                                                    *
 **********************************************************************/


public class MainGameSceneLoaderScript : MonoBehaviour
{

	// Use this for initialization
    void Start ()
    {
        if (Random.value > 0.5f)
            // Turn off AreneV1
            GameObject.Find("AreneV1").SetActive(false);
        else
            // Turn off  AreneV2
            GameObject.Find("AreneV2").SetActive(false);
	}
}
