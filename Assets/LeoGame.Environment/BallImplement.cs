
using System.Threading;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

public struct BallSpawnerSystemController : IComponentData { }
public struct ThePong : IComponentData { }

// [DisableAutoCreation]
[UpdateInGroup(typeof(ServerSimulationSystemGroup))]//make sure this only runs on the server
public class BallSpawnerSystem:SystemBase
{
    protected override void OnCreate()
    {
        RequireSingletonForUpdate<BallSpawnerSystemController>();
    }

    

    protected override void OnUpdate()
    {
        EntityManager.DestroyEntity(GetSingletonEntity<BallSpawnerSystemController>());
        // 如果球出界了就会执行GameOver的System操作
        // 在创建时先实现实体，然后等待一秒后去掉GetReady并且给球一个速度
        Job.WithoutBurst().WithCode(() =>
        {
            var ghostCollection = GetSingleton<GhostPrefabCollectionComponent>();
            var prefab = EntityManager.GetBuffer<GhostPrefabBuffer>(ghostCollection.serverPrefabs)[2].Value; // 初始化球
            var ball = EntityManager.Instantiate(prefab);
            EntityManager.AddComponent<ThePong>(ball); // 添加标识，方便以后删除
            EntityManager.SetComponentData<Translation>(ball, new Translation { Value = new float3(0f, 0f, 0f) });
            Thread.Sleep(2000);
            EntityManager.SetComponentData(ball, new PhysicsVelocity
            {
                Linear = new float3(math.radians(10f), math.radians(10f), 0f),
                Angular = new float3(0f, 0f, 0f)
            });

            EntityManager.CreateEntity(typeof(BallCheckSystemController));
        }).Run();


    }

}


public struct BallCheckSystemController : IComponentData { }

[UpdateInGroup(typeof(ServerSimulationSystemGroup))]
public class BallCheckSystem : SystemBase
{

    protected override void OnCreate()
    {
        RequireSingletonForUpdate<BallCheckSystemController>();
    }


    protected override void OnUpdate()
    {
        Entities.WithoutBurst().WithStructuralChanges() // 为了快速实现直接修改结构
            .ForEach((Entity ent, ref ThePong thePong, ref Translation translation,ref PhysicsVelocity velocity,ref Rotation rotation) =>
        {
            // 对于Z轴方向的线速度分量要进行控制
            if (velocity.Linear.z != 0f)
            {
                velocity.Linear.z = 0f;
                translation.Value.z = 0f;
            }

            if (translation.Value.x > 9f || translation.Value.x < -9f)
            {
                // 修改服务端的游戏状态为 游戏结束
                EntityManager.SetComponentData<LeoGameStatus>(GetSingletonEntity<LeoGameStatus>(),
                    new LeoGameStatus { theGameStatus = TheGameStatus.Over });

                // 下一步服务端同步客户端信息
                if (GetEntityQuery(new ComponentType[] {typeof(ServerGameOverSystemController) }).CalculateChunkCount()==0) // 防止网络状态不佳，出现生成多个对象的问题
                {
                    var tEnt = EntityManager.CreateEntity(typeof(ServerGameOverSystemController));
                    EntityManager.SetComponentData<ServerGameOverSystemController>(tEnt, new ServerGameOverSystemController
                    {
                        WinPlayerId = translation.Value.x < 0 ? 2 : 1
                    });
                }

                // 控制不要再检查球的状态了
                EntityManager.DestroyEntity(GetSingletonEntity<BallCheckSystemController>());
                EntityManager.DestroyEntity(ent); //同时清除已经存在的球
            }

        }).Run();

    }
}