using System;
using TMPro;
using UnityEngine;
using Zenject;

public class PlayerSettingsGUI : MonoBehaviour, ISettings
{
    [Inject] private readonly VoxelSystem.Managers.IChunksManager _chunksManager;
    [Header("Player settings")]
    public TMP_InputField InputRenderDistance;
    public TMP_InputField InputCacheDistance;
    public TMP_InputField InputChunksProcessed;
    public TMP_InputField InputChunksToLoad;
    public TMP_InputField InputTimeToLoadNextChunks;

    public void OnEnable()
    {
        Load();
    }

    public void Load()
    {
        InputRenderDistance.text = PlayerSettings.RenderDistance.ToString();
        InputCacheDistance.text = PlayerSettings.CacheDistance.ToString();
        InputChunksProcessed.text = PlayerSettings.ChunksProcessed.ToString();
        InputChunksToLoad.text = PlayerSettings.ChunksToLoad.ToString();
        InputTimeToLoadNextChunks.text = PlayerSettings.TimeToLoadNextChunks.ToString();
    }

    public void Save()
    {
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

        _chunksManager.GenerateChunksPositionsCheck();
        Load();
    }
}
