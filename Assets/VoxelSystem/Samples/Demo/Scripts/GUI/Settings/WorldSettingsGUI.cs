using System;
using TMPro;
using UnityEngine;
using VoxelSystem.Factory;

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

    /// </inheritdoc>
    public void Load()
    {
        if(!ValidateComponents()) return;

        InputSeed.text = WorldSettings.Seed.ToString();
        InputChunkWidth.text = WorldSettings.ChunkWidth.ToString();
        InputChunkHeight.text = WorldSettings.ChunkHeight.ToString();
    }

    /// </inheritdoc>
    public void Save()
    {
        if(!ValidateComponents()) return;

        // Try to convert the seed input to an integer
        if (int.TryParse(InputSeed.text, out int valueInt))
        {
            // Successfully converted to int, use the value directly
            if (valueInt != WorldSettings.Seed)
            {
                WorldSettings.Seed = valueInt;
                ChunkFactory.Instance.InitSeed();
            }
        }
        else
        {
            // Input is not a valid integer, use string hash code as seed
            int hashCode = InputSeed.text.GetHashCode();
            if (hashCode != WorldSettings.Seed)
            {
                WorldSettings.Seed = hashCode;
                ChunkFactory.Instance.InitSeed();
            }
        }

        // Handle other input fields
        if (int.TryParse(InputChunkWidth.text, out valueInt))
        {
            WorldSettings.ChunkWidth = valueInt > 0 ? valueInt : WorldSettings.ChunkWidth;
        }
        
        if (int.TryParse(InputChunkHeight.text, out valueInt))
        {
            WorldSettings.ChunkHeight = valueInt > 0 ? valueInt : WorldSettings.ChunkHeight;
        }
        
        WorldSettings.ChunkBounds = WorldSettings.RecalculatedBounds;
        Load();
    }

    /// <summary>
    /// Validates the input fields to ensure they are not null.
    /// </summary>
    private bool ValidateComponents()
    {
        if (InputSeed == null || InputChunkWidth == null || InputChunkHeight == null)
        {
            Debug.LogError("WorldSettingsGUI: One or more input fields are not assigned in the inspector.");
            return false;
        }
        
        return true;
    }
}
