using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace VoxelSystem.Data.Blocks
{
    /// <summary>
    /// 
    /// </summary>
    [CreateAssetMenu(fileName = "Blocks", menuName = "Voxel System/Blocks Catalogue", order = 0)]
    public class BlocksCatalogue : ScriptableObject
    {
        [field: SerializeField] private Material Material { get; set; }
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

            var placeholder = CreateWhiteTexture(textureSize);
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
            Material.SetTexture("_Texture2D_Array", textureArray);
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

        private Texture2D CreateWhiteTexture(int size)
        {
            Texture2D whiteTexture = new(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            Color32[] pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.white;
            }
            whiteTexture.SetPixels32(pixels);
            whiteTexture.Apply();
            return whiteTexture;
        }
    }
}