using UnityEngine;

public class DrawRendererBounds : MonoBehaviour
{
    // wont work ok when the chunk mesh is updated
    //private Renderer cachedRenderer;
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