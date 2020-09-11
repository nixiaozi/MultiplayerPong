using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.NetCode;
using UnityEngine;

/// <summary>
/// 由于在MonoBehavior模式下没有办法很好的控制Entity所生成的空间（World）尤其是在编辑器模式下。这个暂时废弃不用了
/// </summary>
[Obsolete("由于在MonoBehavior模式下没有办法很好的控制Entity所生成的空间（World）尤其是在编辑器模式下。这个暂时废弃不用了")]
public class PerpareButton : MonoBehaviour
{
    private EntityCommandBufferSystem CommandBufferSystem;
    private EntityQuery query;


    public void DoPrepare(World world)
    {
        Debug.Log("Click to Prepare!");
        // World world = World.All[1];
        // world = World.DefaultGameObjectInjectionWorld;

        var query = world.EntityManager.CreateEntityQuery(new ComponentType[] { typeof(LeoGameStatus), typeof(LeoPlayerGameStatus) });
        var entities = query.ToEntityArray(Allocator.TempJob);
        var oldComs = query.ToComponentDataArray<LeoPlayerGameStatus>(Allocator.TempJob);




        DoPrepareJob doPrepareJob = new DoPrepareJob();
        doPrepareJob.entities = entities;
        doPrepareJob.oldComs = oldComs;
        doPrepareJob.world = world;

        JobHandle handle = doPrepareJob.Schedule();
        handle.Complete();


    }

    public void DoTest()
    {
        Debug.Log("DoTest");
    }

    // Job adding two floating point values together
    // Job 中不能包含像 World这样的引用类型，只能拥有数值类型的数据
    public struct DoPrepareJob : IJob
    {
        public World world;
        public NativeArray<Entity> entities;
        public NativeArray<LeoPlayerGameStatus> oldComs;

        public void Execute()
        {
            var CommandBufferSystem = world.GetExistingSystem<EndSimulationEntityCommandBufferSystem>();

            EntityCommandBuffer commandBuffer
                = CommandBufferSystem.CreateCommandBuffer();

            var entity = entities[0];
            var oldCom = oldComs[0];

            commandBuffer.SetComponent<LeoPlayerGameStatus>(entity,
            new LeoPlayerGameStatus { playerGameStatus = PlayerGameStatus.Ready, playerId = oldCom.playerId });

            var controller = commandBuffer.CreateEntity();
            commandBuffer.AddComponent<ClientGameStatusSendSystemController>(controller); // 打开控制开关使得开始游戏状态更新操作
        }
    }
}

