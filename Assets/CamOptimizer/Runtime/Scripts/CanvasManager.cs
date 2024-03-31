using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class CanvasManager : MonoBehaviour
{
    public TextMeshProUGUI pause_txt;
    public GameObject pause_button;

    void Update()
    {
        if (transform.GetChild(transform.childCount-1) != pause_button.transform)
        {
            pause_button.transform.SetSiblingIndex(transform.childCount - 1);
            Debug.Log("Change button to the last child");
        }
    }

    public void Pause()
    {
        GA_optimizer.is_pause = !GA_optimizer.is_pause;
        if ( GA_optimizer.is_pause )
        {
            pause_txt.text = "click to resume";
        }
        else
        {
            pause_txt.text = "click to pause";
        }
    }

}
