using System;
using TMPro;
using UnityEngine;
using Zenject;
using VoxelSystem.Managers;
using VoxelSystem.Generators;
using VoxelSystem.Settings;
public class PlayerSettingsGUI : MonoBehaviour, ISettings
{
    [Inject] private readonly IChunksManager _chunksManager;

    [Header("Player settings")]
    [field: SerializeField] private TMP_InputField InputRenderDistance { get; set; }
    [field: SerializeField] private TMP_InputField InputCacheDistance { get; set; }
    [field: SerializeField] private TMP_InputField InputChunksProcessed { get; set; }
    [field: SerializeField] private TMP_InputField InputChunksToLoad { get; set; }
    [field: SerializeField] private TMP_InputField InputTimeToLoadNextChunks { get; set; }
    [field: SerializeField] private TMP_Dropdown MeshGeneratorTypeDropdown { get; set; }
    [field: SerializeField] private bool PrintDebug { get; set; } = false;

    public void OnEnable()
    {
        Load();
    }

    public void Load()
    {
        if (InputRenderDistance == null ||
            InputCacheDistance == null ||
            InputChunksProcessed == null ||
            InputChunksToLoad == null ||
            InputTimeToLoadNextChunks == null ||
            MeshGeneratorTypeDropdown == null)
        {
            Debug.LogError("PlayerSettingsGUI: One or more input fields are not assigned in the inspector.");
            return;
        }

        InputRenderDistance.text = PlayerSettings.RenderDistance.ToString();
        InputCacheDistance.text = PlayerSettings.CacheDistance.ToString();
        InputChunksProcessed.text = PlayerSettings.ChunksProcessed.ToString();
        InputChunksToLoad.text = PlayerSettings.ChunksToLoad.ToString();
        InputTimeToLoadNextChunks.text = PlayerSettings.TimeToLoadNextChunks.ToString();

        MeshGeneratorTypeDropdown.ClearOptions();
        MeshGeneratorTypeDropdown.AddOptions(new System.Collections.Generic.List<string> { "Single-threaded", "Parallel" });
        MeshGeneratorTypeDropdown.RefreshShownValue();

        MeshGeneratorTypeDropdown.value = (int)PlayerSettings.MeshGeneratorType;
    }

    public void Save()
    {
        if (InputRenderDistance == null ||
            InputCacheDistance == null ||
            InputChunksProcessed == null ||
            InputChunksToLoad == null ||
            InputTimeToLoadNextChunks == null ||
            MeshGeneratorTypeDropdown == null)
        {
            Debug.LogError("PlayerSettingsGUI: One or more input fields are not assigned in the inspector.");
            return;
        }

        var valueInt = Convert.ToInt32(InputRenderDistance.text);
        PlayerSettings.RenderDistance = valueInt >= 0 ? valueInt : PlayerSettings.RenderDistance;
        valueInt = Convert.ToInt32(InputCacheDistance.text);
        PlayerSettings.CacheDistance = valueInt >= 0 ? valueInt : PlayerSettings.CacheDistance;
        valueInt = Convert.ToInt32(InputChunksProcessed.text);
        PlayerSettings.ChunksProcessed = valueInt > 0 ? valueInt : PlayerSettings.ChunksProcessed;
        valueInt = Convert.ToInt32(InputChunksToLoad.text);
        PlayerSettings.ChunksToLoad = valueInt > 0 ? valueInt : PlayerSettings.ChunksToLoad;
        var valueFloat = (float)Convert.ToDouble(InputTimeToLoadNextChunks.text);
        PlayerSettings.TimeToLoadNextChunks = valueFloat >= 0 ? valueFloat : PlayerSettings.TimeToLoadNextChunks;
        PlayerSettings.MeshGeneratorType = (MeshGeneratorType)MeshGeneratorTypeDropdown.value;

        WorldSettings.NotifySettingsChanged();

        Load();

        if (!PrintDebug) return;

        Debug.Log($"PlayerSettingsGUI - Settings saved:\n" +
            $"RenderDistance: {PlayerSettings.RenderDistance};\n" +
            $"CacheDistance: {PlayerSettings.CacheDistance};\n" +
            $"ChunksProcessed: {PlayerSettings.ChunksProcessed};\n" +
            $"ChunksToLoad: {PlayerSettings.ChunksToLoad};\n" +
            $"TimeToLoadNextChunks: {PlayerSettings.TimeToLoadNextChunks};\n" +
            $"MeshGeneratorType: {PlayerSettings.MeshGeneratorType}");
    }
}
