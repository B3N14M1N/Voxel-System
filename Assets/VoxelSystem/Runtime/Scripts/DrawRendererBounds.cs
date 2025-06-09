using UnityEngine;

namespace VoxelSystem.Utils
{
    /// <summary>
    /// Draws the bounding box of a renderer when the GameObject is selected in the editor.
    /// </summary>
    public class DrawRendererBounds : MonoBehaviour
    {
        // wont work ok when the chunk mesh is updated
        //private Renderer cachedRenderer;

        /// <summary>
        /// Draws a wire cube representing the bounds of the attached renderer.
        /// </summary>
        public void OnDrawGizmosSelected()
        {
            var cachedRenderer = GetComponent<Renderer>();
            if (cachedRenderer == null)
                return;

            var bounds = cachedRenderer.bounds;
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.color = Color.black;
            Gizmos.DrawWireCube(bounds.center, bounds.extents * 2);
        }
    }
}