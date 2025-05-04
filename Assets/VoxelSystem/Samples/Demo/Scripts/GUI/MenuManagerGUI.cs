using System;
using UnityEditor;
using UnityEngine;
using Zenject;

[Serializable]
public class MenuManagerGUI : MonoBehaviour
{
    [Inject] private readonly PlayerStateManager _stateManager;

    [Header("Screen")]
    [field: SerializeField] private KeyCode screenWindowKeyCode { get; set; } = KeyCode.F11;
    [Header("Stats")]
    [field: SerializeField] private KeyCode statsKeyCode { get; set; } = KeyCode.F3;
    [field: SerializeField] private GameObject StatsPannel { get; set; }
    private bool _isStatsPannelActive = false;

    [Header("Menu")]
    [field: SerializeField] private KeyCode MenyKeyCode { get; set; } = KeyCode.Escape;
    [field: SerializeField] private GameObject MenuPannel { get; set; }
    [field: SerializeField] private GameObject BlockSelectorPannel { get; set; }

    private bool _isCursorVisible = true;
    private bool _isCursorLocked = false;

    public void Start()
    {
        Cursor.visible = _isCursorVisible;
        Cursor.lockState = _isCursorLocked ? CursorLockMode.Locked : CursorLockMode.None;
    }

    public void Update()
    {
        if (Input.GetKeyDown(statsKeyCode))
        {
            StatsPannel.SetActive(!StatsPannel.activeSelf);
        }

        if (Input.GetKeyDown(MenyKeyCode))
        {
            UpdateSettingsPannel();
        }

        if (Input.GetKeyDown(screenWindowKeyCode))
        {
            Screen.fullScreen = !Screen.fullScreen;
        }
    }

    public void UpdateSettingsPannel()
    {
        bool isActivating = !MenuPannel.activeSelf;

        if (isActivating)
        {
            _isStatsPannelActive = StatsPannel.activeSelf;
            StatsPannel.SetActive(false);
            _isCursorVisible = Cursor.visible;
            _isCursorLocked = Cursor.lockState == CursorLockMode.Locked;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            StatsPannel.SetActive(_isStatsPannelActive);
            Cursor.lockState = _isCursorLocked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = _isCursorVisible;
        }

        _stateManager.SetControlState(!isActivating);
        BlockSelectorPannel.SetActive(!isActivating);
        MenuPannel.SetActive(isActivating);
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
