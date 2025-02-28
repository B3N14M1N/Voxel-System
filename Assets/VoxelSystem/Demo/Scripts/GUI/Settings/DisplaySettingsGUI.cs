using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DisplaySettings : MonoBehaviour, ISettings
{
    [Header("FPS settings")]
    public int maxFps = 30;
    public Slider Slider;
    public TMP_InputField InputMaxFPS;

    public void Awake()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = maxFps;
        InputMaxFPS.text = maxFps.ToString();
        Slider.value = maxFps;

        Slider.onValueChanged.AddListener(delegate { SliderValueChangeCheck(); });
        InputMaxFPS.onSubmit.AddListener(delegate { InputFPSValueChangeCheck(); });
    }
    public void Start()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = maxFps;
        InputMaxFPS.text = maxFps.ToString();
        Slider.value = maxFps;
    }
    public void OnEnable()
    {
        Load();
    }

    public void Load()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = maxFps;
        InputMaxFPS.text = maxFps.ToString();
        Slider.value = maxFps;
    }

    public void Save()
    {
        Slider.value = maxFps;
    }

    private void SliderValueChangeCheck()
    {
        if (Slider.value != maxFps)
        {
            maxFps = (int)Slider.value;
            Application.targetFrameRate = maxFps;
            InputMaxFPS.text = maxFps.ToString();
        }
    }

    private void InputFPSValueChangeCheck()
    {
        if (!string.IsNullOrEmpty(InputMaxFPS.text))
        {
            var newMax = int.Parse(InputMaxFPS.text);
            maxFps = newMax >= Slider.minValue ? newMax : maxFps;
            Slider.value = maxFps;
            Application.targetFrameRate = maxFps;

        }
        InputMaxFPS.text = maxFps.ToString();
    }

}
