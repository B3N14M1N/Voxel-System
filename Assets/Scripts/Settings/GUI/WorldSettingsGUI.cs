using System;
using TMPro;
using UnityEngine;

public class WorldSettingsGUI : MonoBehaviour, ISettings
{
    [Header("World settings")]
    public TMP_InputField InputSeed;
    public TMP_InputField InputChunkWidth;
    public TMP_InputField InputChunkHeight;

    public void OnEnable()
    {
        Load();
    }

    public void Load()
    {
        InputSeed.text = WorldSettings.Seed.ToString();
        InputChunkWidth.text = WorldSettings.ChunkWidth.ToString();
        InputChunkHeight.text = WorldSettings.ChunkHeight.ToString();
    }

    public void Save()
    {
        var valueInt = Convert.ToInt32(InputSeed.text);
        if (valueInt != WorldSettings.Seed)
        {
            WorldSettings.Seed = valueInt;
            ChunkFactory.Instance.InitSeed();
        }
        valueInt = Convert.ToInt32(InputChunkWidth.text);
        WorldSettings.ChunkWidth = valueInt > 0 ? valueInt : WorldSettings.ChunkWidth;
        valueInt = Convert.ToInt32(InputChunkHeight.text);
        WorldSettings.ChunkHeight = valueInt > 0 ? valueInt : WorldSettings.ChunkHeight;
        WorldSettings.ChunkBounds = WorldSettings.RecalculatedBounds;
        Load();
    }
}
