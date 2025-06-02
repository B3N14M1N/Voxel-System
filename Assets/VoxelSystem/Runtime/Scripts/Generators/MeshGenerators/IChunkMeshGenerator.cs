using VoxelSystem.Data.GenerationFlags;

namespace VoxelSystem.Generators
{
    /// <summary>
    /// Interface for chunk mesh generators that provide a common API for different mesh generation strategies.
    /// </summary>
    public interface IChunkMeshGenerator
    {
        /// <summary>
        /// Gets the generation data describing what is being generated.
        /// </summary>
        GenerationData GenerationData { get; }
        
        /// <summary>
        /// Whether the mesh generation job has completed.
        /// </summary>
        bool IsComplete { get; }
        
        /// <summary>
        /// Completes the mesh generation job and applies the mesh to the chunk.
        /// </summary>
        /// <returns>Updated generation data with new flags</returns>
        GenerationData Complete();
        
        /// <summary>
        /// Releases resources used by this generator.
        /// </summary>
        void Dispose();
    }
}