using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(ServerSimulationSystemGroup))]//make sure this only runs on the server
public class GoInGameServerSystem : SystemBase
{
    protected override void OnUpdate()
    {
        EntityManager entityManager = EntityManager;
        //the with none is to make sure that we don't run this system on rpcs that we are sending, only on ones that we are receiving. 
        Entities
            .WithStructuralChanges()
            .WithNone<SendRpcCommandRequestComponent>().ForEach(
            (Entity reqEnt, // reqEnt 当前迭代的实体
                ref GoInGameRequest req, // req 当前迭代实体的 GoInGameRequest 组件
                ref ReceiveRpcCommandRequestComponent reqSrc
                ) =>
            {
                Debug.Log("GoInGameRequest Version:"+req.Version); // 获取当前连接的客户端版本号

            //we add a network connection to the component on our side
            entityManager.AddComponent<NetworkStreamInGame>(reqSrc.SourceConnection);
            UnityEngine.Debug.Log(System.String.Format("Server setting connection {0} to in game",
                EntityManager.GetComponentData<NetworkIdComponent>(reqSrc.SourceConnection).Value));

                int ConnectNum = EntityManager.GetComponentData<NetworkIdComponent>(reqSrc.SourceConnection).Value; // ConnectNum 从1开始

                // 处理连接玩家大于2的情况
                if (ConnectNum>2)
                {
                    return;
                }

                // var ghostCollection = GetSingleton<GhostPrefabCollectionComponent>();

                EntityQuery query = GetEntityQuery(typeof(GhostPrefabCollectionComponent));
                NativeArray<GhostPrefabCollectionComponent> GhostPrefabs = query.ToComponentDataArray<GhostPrefabCollectionComponent>(Allocator.Temp);
                var ghostCollection = GhostPrefabs[0]; // 三行代码等效于 var ghostCollection = GetSingleton<GhostPrefabCollectionComponent>();

                // var ghostId = MultiplayerPongGhostSerializerCollection.FindGhostType<PaddleTheSideSnapshotData>(); // 这个只会返回0和-1；或者是返回ghost在ghostCollection 中的编号
                var ghostId = 0;

                if (ConnectNum <=2)
                {
                    ghostId = ConnectNum - 1;
                }


                var prefab = EntityManager.GetBuffer<GhostPrefabBuffer>(ghostCollection.serverPrefabs)[ghostId].Value;


                // spawn in our player 
                var player = entityManager.Instantiate(prefab);

                entityManager.SetComponentData(player,
                    new PaddleMoveableComponent
                    {
                        PlayerId = ConnectNum
                    });

                //entityManager.AddComponent<GhostConnectionPosition>(player);
                //entityManager.SetComponentData(player, new GhostConnectionPosition
                //{
                //    Position = new float3(-10f + 10f * ConnectNum, 0f, 0f)
                //});

                // entityManager.HasComponent<Transform>(player).position = new float3(-10f + 10f * ConnectNum, 0f, 0f);
                //var test = entityManager.GetComponentData<LocalToWorld>(player); // 这个组件是只读的，我们需要使用Translation 来控制对象的初始位置
                //var testComponent= entityManager.GetComponentTypes(player);
                //var test1= entityManager.GetComponentData<Translation>(player);
/*              // 实际并不需要重新定义玩家实体位置
                entityManager.SetComponentData(player, new Translation
                {
                    Value = new float3(-15f + (5f * ConnectNum), 0f, 0f)
                });
*/
                // 添加初始化的玩家游戏状态实体
                var playerStatus = entityManager.CreateEntity(typeof(LeoPlayerGameStatus));

                entityManager.SetComponentData(playerStatus,
                    new LeoPlayerGameStatus
                    {
                        playerGameStatus = PlayerGameStatus.NotReady,
                        playerId = ConnectNum,
                    });



                /*
                                entityManager.SetComponentData(player, new GhostConnectionPosition // 这个需要在客户端添加
                                {
                                    Position = new float3(-10f+10f*ConnectNum, 0f, 0f)
                                });

                */

                //var ghostCollection = GetSingleton<GhostPrefabCollectionComponent>();
                ////if you get errors in this code make sure you have generated the ghost collection and ghost code using their authoring components and that the names are set correctly when you do so.
                //var ghostId = NetCubeGhostSerializerCollection.FindGhostType<CubeSnapshotData>();
                //var prefab = EntityManager.GetBuffer<GhostPrefabBuffer>(ghostCollection.serverPrefabs)[ghostId].Value;
                ////spawn in our player
                //var player = entityManager.Instantiate(prefab);

                //entityManager.SetComponentData(player, new MovableCubeComponent { PlayerId = entityManager.GetComponentData<NetworkIdComponent>(reqSrc.SourceConnection).Value });
                ////if you are copy pasting this article in order don't worry if you get an error about cube input not existing as we are going to code that next, this just adds the buffer so that we can receive input from the player. If you want to test your code at this point just comment it out
                //entityManager.AddBuffer<CubeInput>(player);

                //entityManager.SetComponentData(reqSrc.SourceConnection, new CommandTargetComponent { targetEntity = player });

                // 添加实体组件用于接收客户端输入的操作
                entityManager.AddBuffer<PaddleInput>(player);
                entityManager.SetComponentData(reqSrc.SourceConnection, new CommandTargetComponent { targetEntity = player });


                entityManager.DestroyEntity(reqEnt);
                //Debug.Log("Spawned Player");
            }).Run();
    }
}