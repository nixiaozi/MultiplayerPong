using Unity.Entities;
using Unity.NetCode;
using UnityEngine;


[GenerateAuthoringComponent]
public struct PaddleMoveableComponent : IComponentData
{
    [GhostDefaultField]  
    public int PlayerId;
}
