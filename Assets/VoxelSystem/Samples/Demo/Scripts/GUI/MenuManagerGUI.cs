using System;
using UnityEditor;
using UnityEngine;

[Serializable]
public class MenuManagerGUI : MonoBehaviour
{

    [Header("Screen")]
    public KeyCode screenWindowKeyCode = KeyCode.F11;
    [Header("Stats")]
    public KeyCode statsKeyCode = KeyCode.F3;
    public GameObject StatsPannel;

    [Header("Menu")]
    public KeyCode MenyKeyCode = KeyCode.Escape;
    public GameObject MenuPannel;
    public void Start()
    {
        //Cursor.visible = false;
        Cursor.visible = true;
    }
    public void Update()
    {
        if (Input.GetKeyDown(statsKeyCode))
        {
            StatsPannel.SetActive(!StatsPannel.activeSelf);
        }

        if (Input.GetKeyDown(MenyKeyCode))
        {
            MenuPannel.SetActive(!MenuPannel.activeSelf);
        }
        if (Input.GetKeyDown(screenWindowKeyCode))
        {
            Screen.fullScreen = !Screen.fullScreen;
        }
    }

    public void Quit()
    {
#if UNITY_EDITOR
        EditorUtility.UnloadUnusedAssetsImmediate();
        GC.Collect();
#endif
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_WEBPLAYER
        Application.OpenURL(webplayerQuitURL);
#else
        Application.Quit();
#endif
    }
}
