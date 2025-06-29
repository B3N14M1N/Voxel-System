using UnityEngine;
using System.IO;
using System;
using VoxelSystem.Factory;
using VoxelSystem.SaveSystem;
using VoxelSystem.Generators;

namespace VoxelSystem.Settings
{
    /// <summary>
    /// Manages the settings for the voxel world, including world path, chunk dimensions, and seed.
    /// This class also handles saving and loading world settings to a file.
    /// </summary>
    public class WorldSettings
    {
        public static WorldSettings Instance { get; private set; }

        public static string WorldsDirectory => Path.Combine(Application.persistentDataPath, "Worlds");
        public string WorldPath { get; private set; }
        public string PersistentWorldPath => Path.Combine(WorldsDirectory, WorldPath);
        public string PersistentChunksPath => Path.Combine(PersistentWorldPath, "Chunks");

        public static event Action OnWorldSettingsChanged;
        public static event Action OnWorldChanged;

        public static bool HasDebugging { get; set; } = true;

        /// <summary>
        /// Notify subscribers that the world settings have changed.
        /// This will recalculate the chunk bounds and save the settings.
        /// </summary>
        public static void NotifySettingsChanged()
        {
            //ChunkSaveSystem.DeleteAllChunkSaves();
            ChunkBounds = RecalculatedBounds;
            OnWorldSettingsChanged?.Invoke();
            Instance?.SaveWorldSettings();

            // Reinitialize the ChunkSaveSystem to ensure semaphores are in a good state
            ChunkSaveSystem.Initialize();
        }

        /// <summary>
        /// Notify subscribers that the world has changed.
        /// This will recalculate the chunk bounds and reset the chunks.
        /// /// </summary>
        public static void NotifyWorldChanged()
        {
            OnWorldChanged?.Invoke();
            Instance?.SaveWorldSettings();
            ChunkBounds = RecalculatedBounds;
            
            // Reinitialize the ChunkSaveSystem to ensure semaphores are in a good state
            ChunkSaveSystem.Initialize();
        }

        public static void ResetCurrentWorld()
        {
            OnWorldChanged?.Invoke();
            Instance?.SaveWorldSettings();
            ChunkBounds = RecalculatedBounds;
            // Reinitialize the ChunkSaveSystem to ensure semaphores are in a good state
            ChunkSaveSystem.Initialize();
        }
        public static int Seed = 0;

        public static int ChunkWidth = 32;

        public static int ChunkHeight = 255;

        public static Bounds RecalculatedBounds
        {
            get
            {
                return new Bounds(new Vector3(ChunkWidth, ChunkHeight, ChunkWidth) * 0.5f, new Vector3(ChunkWidth, ChunkHeight, ChunkWidth));
            }
        }

        public static Bounds ChunkBounds = RecalculatedBounds;
        public static int RenderedVoxelsInChunk => ChunkWidth * ChunkWidth * ChunkHeight;

        public static bool ChunksInRange(Vector3 center, Vector3 position, int range)
        {
            return position.x <= center.x + range &&
                position.x >= center.x - range &&
                position.z <= center.z + range &&
                position.z >= center.z - range;
        }

        public static Vector3 ChunkPositionFromPosition(Vector3 pos)
        {
            pos /= ChunkWidth;
            pos.x = Mathf.FloorToInt(pos.x);
            pos.y = 0;
            pos.z = Mathf.FloorToInt(pos.z);
            return pos;
        }

        public void InitializeWorld(string worldPath)
        {
            Instance = this;
            WorldPath = worldPath;
            ChunkJob.ResetProcessed();

            // Create worlds directory if it doesn't exist
            if (!Directory.Exists(WorldsDirectory))
            {
                Directory.CreateDirectory(WorldsDirectory);
            }

            // Create world directory if it doesn't exist
            if (!Directory.Exists(PersistentWorldPath))
            {
                Directory.CreateDirectory(PersistentWorldPath);
            }

            // Create chunks subdirectory if it doesn't exist
            if (!Directory.Exists(PersistentChunksPath))
            {
                Directory.CreateDirectory(PersistentChunksPath);
            }

            LoadWorldSettings();
            NotifyWorldChanged();
        }

        public void CreateNewWorld(string worldPath)
        {
            NotifyWorldChanged();
            InitializeWorld(worldPath);
            SaveWorldSettings();
        }

        public void SaveWorldSettings()
        {
            if (string.IsNullOrEmpty(WorldPath)) return;

            string settingsPath = Path.Combine(PersistentWorldPath, "settings.txt");
            string settingsContent = $"Seed={Seed}\nChunkWidth={ChunkWidth}\nChunkHeight={ChunkHeight}";
            File.WriteAllText(settingsPath, settingsContent);
            if (HasDebugging)
                Debug.Log($"World settings saved: {settingsPath}");
        }

        private void LoadWorldSettings()
        {
            string settingsPath = Path.Combine(PersistentWorldPath, "settings.txt");
            if (HasDebugging)
                Debug.Log($"Loading world settings from {PersistentWorldPath}");

            // Check if settings file exists
            if (!File.Exists(settingsPath))
            {
                // If settings file doesn't exist, save current settings
                SaveWorldSettings();
                return;
            }

            string[] lines = File.ReadAllLines(settingsPath);
            foreach (string line in lines)
            {
                string[] parts = line.Split('=');
                if (parts.Length == 2)
                {
                    string key = parts[0].Trim();
                    string value = parts[1].Trim();

                    switch (key)
                    {
                        case "Seed":
                            if (int.TryParse(value, out int seed))
                                Seed = seed;
                            break;
                        case "ChunkWidth":
                            if (int.TryParse(value, out int width))
                                ChunkWidth = width;
                            break;
                        case "ChunkHeight":
                            if (int.TryParse(value, out int height))
                                ChunkHeight = height;
                            break;
                    }
                }
            }

            ChunkFactory.Instance.InitSeed();
            ChunkBounds = RecalculatedBounds;
        }
    }
}