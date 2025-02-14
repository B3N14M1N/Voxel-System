using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace VoxelSystem.Data.Blocks
{
    /// <summary>
    /// 
    /// </summary>
    [CreateAssetMenu(fileName = "Blocks", menuName = "Voxel System/Blocks Catalogue", order = 0)]
    public class BlocksCatalogue : ScriptableObject
    {
        [field: SerializeField] private List<Material> Materials { get; set; }
        [field: SerializeField] private List<Block> Blocks { get; set; }

        public Dictionary<VoxelType, int> GenerateAtlas()
        {
            int blockCount = Blocks.Count;
            int textureSize = 16; // Assuming all textures are 16x16

            Texture2DArray textureArray = new(textureSize, textureSize, blockCount * 2, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            Dictionary<VoxelType, int> textureMapping = new Dictionary<VoxelType, int>();

            var placeholder = CreateBlankTexture(textureSize);
            for (int i = 0; i < blockCount; i++)
            {
                Block block = Blocks[i];
                Texture2D topTexture = block.TopTexture ? ConvertToLinear(block.TopTexture, textureSize) : placeholder;
                Texture2D sideTexture = block.SideTexture ? ConvertToLinear(block.SideTexture, textureSize) : topTexture;

                textureArray.SetPixels32(topTexture.GetPixels32(), i * 2);
                textureArray.SetPixels32(sideTexture.GetPixels32(), i * 2 + 1);

                textureMapping[block.VoxelType] = i;
            }

            textureArray.Apply();
            foreach (var material in Materials)
            {
                material.SetTexture("_Texture2D_Array", textureArray);
            }

            SaveAtlas(textureArray);

            return textureMapping;
        }

        private Texture2D ConvertToLinear(Texture2D texture, int size)
        {
            Texture2D linearTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            linearTexture.filterMode = FilterMode.Point;
            linearTexture.wrapMode = TextureWrapMode.Clamp;

            Color32[] pixels = texture.GetPixels32();
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = pixels[i];
            }
            linearTexture.SetPixels32(pixels);
            linearTexture.Apply();
            return linearTexture;
        }

        private Texture2D CreateBlankTexture(int size)
        {
            Texture2D blankTexture = new(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            Color32[] pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(0, 0, 0, 0);
            }
            blankTexture.SetPixels32(pixels);
            blankTexture.Apply();
            return blankTexture;
        }

        public void SaveAtlas(Texture2DArray textureArray)
        {
#if UNITY_EDITOR
            string directoryPath = "Assets/VoxelSystem/Resources/Textures/Blocks/Atlas";
            string assetPath = Path.Combine(directoryPath, "Atlas.asset");

            // Ensure the directory exists
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            // Delete existing asset to avoid conflicts
            if (AssetDatabase.LoadAssetAtPath<Texture2DArray>(assetPath) != null)
            {
                AssetDatabase.DeleteAsset(assetPath);
            }

            // Save the Texture2DArray as a Unity asset
            AssetDatabase.CreateAsset(textureArray, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Saved texture atlas at: {assetPath}");
#endif
        }
    }
}