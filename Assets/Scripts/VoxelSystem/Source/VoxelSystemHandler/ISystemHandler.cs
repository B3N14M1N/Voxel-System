using UnityEngine;
namespace VoxelSystem.VoxelSystemHandler
{
	/// <summary>
	/// 
	/// </summary>
	public interface ISystemHandler
	{
		void UpdateChunks(Vector3 position);

		void UpdateVoxel(Vector3 position, int voxelData);
	}
}