using System;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace VoxelSystem.Data.Blocks
{
    /// <summary>
    /// Texture size options for block textures in the voxel system.
    /// </summary>
    public enum TextureSize
    {
        Size8x8 = 8,
        Size16x16 = 16,
        Size32x32 = 32,
        Size64x64 = 64,
        Size128x128 = 128
    }

    /// <summary>
    /// Manages the collection of block types and their textures in the voxel system.
    /// </summary>
    [CreateAssetMenu(fileName = "Blocks", menuName = "Voxel System/Blocks Catalogue", order = 0)]
    public class BlocksCatalogue : ScriptableObject, IDisposable
    {
        private TextureSize TextureSize { get; set; } = TextureSize.Size16x16;
        [field: SerializeField] private List<Material> Materials { get; set; }
        [field: SerializeField] private List<Block> Blocks { get; set; }
        [field: SerializeField, Tooltip("Size of texture in pixels (both width and height)")] 

        public NativeParallelHashMap<int, int> TextureMapping { get; private set; }

        /// <summary>
        /// Generates a texture atlas from the block textures and returns a mapping of voxel types to texture indices.
        /// </summary>
        /// <returns>A mapping of voxel type IDs to texture indices</returns>
        public NativeParallelHashMap<int, int> GenerateAtlas()
        {
            int blockCount = Blocks.Count;
            int textureSize = (int)TextureSize; // Using configurable texture size

            Texture2DArray textureArray = new(textureSize, textureSize, blockCount * 2, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            NativeParallelHashMap<int, int> textureMapping = new(Blocks.Count, Allocator.Persistent);

            var placeholder = CreateBlankTexture(textureSize);
            for (int i = 0; i < blockCount; i++)
            {
                Block block = Blocks[i];
                Texture2D topTexture = block.TopTexture ? ConvertToLinear(block.TopTexture, textureSize) : placeholder;
                Texture2D sideTexture = block.SideTexture ? ConvertToLinear(block.SideTexture, textureSize) : topTexture;

                textureArray.SetPixels32(topTexture.GetPixels32(), i * 2);
                textureArray.SetPixels32(sideTexture.GetPixels32(), i * 2 + 1);

                textureMapping[(int)block.VoxelType] = i;
            }

            textureArray.Apply();
            foreach (var material in Materials)
            {
                material.SetTexture("_Texture2D_Array", textureArray);
            }

            SaveAtlas(textureArray);

            TextureMapping = textureMapping;

            return textureMapping;
        }

        /// <summary>
        /// Converts a texture to linear format and resizes it to the target size.
        /// </summary>
        /// <param name="texture">The source texture</param>
        /// <param name="size">The target size of the texture</param>
        /// <returns>A new texture in linear format with the specified size</returns>
        private Texture2D ConvertToLinear(Texture2D texture, int size)
        {
            // Create a new texture with the target size
            Texture2D linearTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            linearTexture.filterMode = FilterMode.Point;
            linearTexture.wrapMode = TextureWrapMode.Clamp;

            if (texture.width == size && texture.height == size)
            {
                // If the texture is already the correct size, just copy the pixels
                linearTexture.SetPixels32(texture.GetPixels32());
            }
            else
            {
                // Create a temporary RenderTexture for resizing
                RenderTexture rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32);
                rt.filterMode = FilterMode.Point;

                // Copy the source texture to the temporary RenderTexture with resizing
                RenderTexture.active = rt;
                Graphics.Blit(texture, rt);

                // Read the resized texture data back
                linearTexture.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                
                // Clean up
                RenderTexture.active = null;
                RenderTexture.ReleaseTemporary(rt);
            }

            linearTexture.Apply();
            return linearTexture;
        }

        /// <summary>
        /// Creates a transparent texture of the specified size.
        /// </summary>
        /// <param name="size">The size of the texture in pixels</param>
        /// <returns>A new blank transparent texture</returns>
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

        /// <summary>
        /// Saves the texture array as an asset in the project.
        /// </summary>
        /// <param name="textureArray">The texture array to save</param>
        private void SaveAtlas(Texture2DArray textureArray)
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

        /// <summary>
        /// Releases allocated native resources.
        /// </summary>
        public void Dispose()
        {
            Debug.Log("[Blocks Catalogue] Disposing of Texture Mapping");
            if (TextureMapping.IsCreated) TextureMapping.Dispose();
        }
    }
}