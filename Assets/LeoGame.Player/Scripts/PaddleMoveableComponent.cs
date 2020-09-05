using Unity.Entities;
using Unity.NetCode;
using UnityEngine;


[GenerateAuthoringComponent]
public struct PaddleMoveableComponent : IComponentData
{
    [GhostDefaultField]  
    public int PlayerId;

    // 注释原因：这个是ghost实体组件，组件的值只能在服务端更改
    // public PlayerGameStatus PlayerGameStatus; // 定义用户的当前状态
}
