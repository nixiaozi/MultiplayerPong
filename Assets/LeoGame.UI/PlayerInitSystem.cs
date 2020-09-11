
using System.Collections;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.NetCode;
using UnityEngine;

public struct PlayerInitSystemController : IComponentData { }


[UpdateInGroup(typeof(ClientSimulationSystemGroup))]
public class PlayerInitSystem : SystemBase
{
    public EntityCommandBufferSystem CommandBufferSystem;

    GameObject gameObject;
    EntityQuery gameStatuQuery;
    // 这个行为主要是在游戏开始前进行不同游戏状态的文字提示，以及游戏即将开始的游戏倒数提示
    protected override void OnCreate()
    {
        CommandBufferSystem = World.GetExistingSystem<EndSimulationEntityCommandBufferSystem>();
        // Load perfabs not form Resources folder https://forum.unity.com/threads/loading-prefabs-from-custom-folder-not-resources.255569/
        gameObject = Resources.Load<GameObject>("HintPrepare");
        gameObject.tag = "ToPrepare";
        gameObject.GetComponent<TextMesh>().text = "";
        gameObject = GameObject.Instantiate(gameObject);
        //var the = GameObject.Instantiate(gameObject); // 直接可以通过这种方式初始化GameObject对象 可以的
        //the.tag = "ToPrepare";

        EntityManager.CreateEntity(typeof(PlayerInitSystemController));
        RequireSingletonForUpdate<PlayerInitSystemController>();
        RequireSingletonForUpdate<NetworkIdComponent>();

        gameStatuQuery = GetEntityQuery(typeof(LeoGameStatus),typeof(LeoPlayerGameStatus));

    }


    

    protected override void OnUpdate()
    {
        EntityCommandBuffer commandBuffer
            = CommandBufferSystem.CreateCommandBuffer();
        //gameObject.GetComponent<PerpareButton>().DoTest();
        var gameStatus = gameStatuQuery.ToComponentDataArray<LeoGameStatus>(Allocator.TempJob);
        var playerGameStatus = gameStatuQuery.ToComponentDataArray<LeoPlayerGameStatus>(Allocator.TempJob);

        JobHandle handle1 = new JobHandle();

        if (playerGameStatus[0].playerGameStatus == PlayerGameStatus.NotReady)
        {
            gameObject.GetComponent<TextMesh>().text = "Click Any Where To Prepare";
            if (Input.GetMouseButtonDown(0))
            {
                Entities.WithoutBurst().ForEach(
                    (Entity ent,ref LeoGameStatus thegameStatus, in LeoPlayerGameStatus playerStatus) => 
                {
                    commandBuffer.SetComponent<LeoPlayerGameStatus>(ent,
                        new LeoPlayerGameStatus
                        {
                            playerGameStatus = PlayerGameStatus.Ready,
                            playerId = playerGameStatus[0].playerId
                        });

                    var entity = commandBuffer.CreateEntity();
                    commandBuffer.AddComponent<ClientGameStatusSendSystemController>(entity);

                }).Run();

                /*handle1 = Job.WithCode(() =>
                  {
                        commandBuffer.SetComponent<LeoPlayerGameStatus>(,
                        new LeoPlayerGameStatus
                        {
                            playerGameStatus = PlayerGameStatus.Ready,
                            playerId = playerGameStatus[0].playerId
                        });

                        var entity = commandBuffer.CreateEntity();
                        commandBuffer.AddComponent<ClientGameStatusSendSystemController>(entity);
                  }).Schedule(Dependency);*/
                
            }
        }
        else if (gameStatus[0].theGameStatus == TheGameStatus.PartReady)
        {
            gameObject.GetComponent<TextMesh>().text = "Wait For Other Player To Prepare";
        }
        else if (gameStatus[0].theGameStatus == TheGameStatus.AllReady)
        {
            gameObject.GetComponent<TextMesh>().text = "Get Ready!";
            Job.WithoutBurst().WithCode(() =>
            {
                commandBuffer.DestroyEntity(GetSingletonEntity<PlayerInitSystemController>()); //控制不要再执行Update
            }).Run();
            
        }


        handle1.Complete();

        Dependency = gameStatus.Dispose(handle1);
        Dependency = playerGameStatus.Dispose(Dependency);


        //gameStatus.Dispose();
        //playerGameStatus.Dispose();
        //player.Dispose();


    }





}





