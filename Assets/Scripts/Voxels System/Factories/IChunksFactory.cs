using System.Collections.Generic;
using UnityEngine;

public interface IChunksFactory
{
    Material Material { get; }
    void InitSeed();
    void GenerateChunksData(List<Vector3> positions);
}
