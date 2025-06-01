using TMPro;
using UnityEngine;
using VoxelSystem.Factory;
using VoxelSystem.Settings;
using Zenject;

public class WorldSettingsGUI : MonoBehaviour, ISettings
{
    [Header("World settings")]
    public TMP_InputField InputSeed;
    public TMP_InputField InputChunkWidth;
    public TMP_InputField InputChunkHeight;
    public TMP_InputField InputWorldPath;

    [Inject]
    private WorldSettings _worldSettings;

    public void OnEnable()
    {
        Load();
    }

    /// </inheritdoc>
    public void Load()
    {
        if (!ValidateComponents()) return;

        InputSeed.text = WorldSettings.Seed.ToString();
        InputChunkWidth.text = WorldSettings.ChunkWidth.ToString();
        InputChunkHeight.text = WorldSettings.ChunkHeight.ToString();
        InputWorldPath.text = _worldSettings?.WorldPath ?? "";
    }

    /// </inheritdoc>
    public void Save()
    {
        if (!ValidateComponents()) return;

        // Handle world path change
        if (!string.IsNullOrEmpty(InputWorldPath.text) && InputWorldPath.text != _worldSettings?.WorldPath)
        {
            _worldSettings?.CreateNewWorld(InputWorldPath.text);
            Load();
            return;
        }

        bool settingsChanged = false;
        bool seedChanged = false;

        int seed = int.TryParse(InputSeed.text, out int valueInt) ? valueInt : InputSeed.text.GetHashCode();

        if (WorldSettings.Seed != seed)
        {
            WorldSettings.Seed = seed;
            ChunkFactory.Instance.InitSeed();
            settingsChanged = true;
            seedChanged = true;
        }
        
        // Handle other input fields
        if (int.TryParse(InputChunkWidth.text, out valueInt))
        {
            int newWidth = valueInt > 0 ? valueInt : WorldSettings.ChunkWidth;
            if (newWidth != WorldSettings.ChunkWidth)
            {
                WorldSettings.ChunkWidth = newWidth;
                settingsChanged = true;
            }
        }

        if (int.TryParse(InputChunkHeight.text, out valueInt))
        {
            int newHeight = valueInt > 0 ? valueInt : WorldSettings.ChunkHeight;
            if (newHeight != WorldSettings.ChunkHeight)
            {
                WorldSettings.ChunkHeight = newHeight;
                settingsChanged = true;
            }
        }

        Load();
        if (seedChanged)
        {
            WorldSettings.NotifyWorldChanged();
            return;
        }

        if (settingsChanged)
        {
            WorldSettings.NotifySettingsChanged();
        }
    }

    /// <summary>
    /// Validates the input fields to ensure they are not null.
    /// </summary>
    private bool ValidateComponents()
    {
        if (InputSeed == null || InputChunkWidth == null || InputChunkHeight == null || InputWorldPath == null)
        {
            Debug.LogError("WorldSettingsGUI: One or more input fields are not assigned in the inspector.");
            return false;
        }

        return true;
    }
}
