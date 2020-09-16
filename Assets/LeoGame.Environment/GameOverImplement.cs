using System.Linq;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using Unity.Transforms;
using UnityEngine;

// 首先在服务端判断游戏是否已经分出胜负
[BurstCompile]
public struct ServerGameOverSystemController : IRpcCommand
{
    public int WinPlayerId;

    public void Deserialize(ref DataStreamReader reader)
    {
        WinPlayerId = reader.ReadInt();
    }

    public void Serialize(ref DataStreamWriter writer)
    {
        writer.WriteInt(WinPlayerId);
    }
    [BurstCompile]
    private static void InvokeExecute(ref RpcExecutor.Parameters parameters)
    {
        RpcExecutor.ExecuteCreateRequestComponent<ServerGameOverSystemController>(ref parameters);
    }

    static PortableFunctionPointer<RpcExecutor.ExecuteDelegate> InvokeExecuteFunctionPointer =
        new PortableFunctionPointer<RpcExecutor.ExecuteDelegate>(InvokeExecute);

    public PortableFunctionPointer<RpcExecutor.ExecuteDelegate> CompileExecute()
    {
        return InvokeExecuteFunctionPointer;
    }
}

public class ServerGameOverSystemControllerRequestSystem: RpcCommandRequestSystem<ServerGameOverSystemController>
{

}


[UpdateInGroup(typeof(ServerSimulationSystemGroup))]//make sure this only runs on the server
public class ServerGameOverSystem : SystemBase
{
    public EntityCommandBufferSystem CommandBufferSystem;
    public EntityQuery GameOverQuery;
    public EntityQuery RpcRequestQuery;
    protected override void OnCreate()
    {
        CommandBufferSystem = World.GetExistingSystem<EndInitializationEntityCommandBufferSystem>();
        GameOverQuery = GetEntityQuery(new EntityQueryDesc 
        { 
            All = new ComponentType[] {typeof(ServerGameOverSystemController) },
            None = new ComponentType[] {typeof(SendRpcCommandRequestComponent),typeof(ReceiveRpcCommandRequestComponent) }
        });

        RpcRequestQuery = GetEntityQuery(new EntityQueryDesc()
        {
            All = new ComponentType[] { typeof(ServerGameOverSystemController), typeof(SendRpcCommandRequestComponent) },
            None = new ComponentType[] { typeof(ReceiveRpcCommandRequestComponent) }
        });


        RequireForUpdate(GameOverQuery);
    }

    protected override void OnUpdate()
    {
        EntityCommandBuffer commandBuffer
                  = CommandBufferSystem.CreateCommandBuffer();

        NativeArray<ServerGameOverSystemController> serverGameOvers 
            = GameOverQuery.ToComponentDataArray<ServerGameOverSystemController>(Allocator.TempJob);
        NativeArray<Entity> serverGameOversEntity = GameOverQuery.ToEntityArray(Allocator.TempJob);

        NativeArray<SendRpcCommandRequestComponent> requestComponents 
            = RpcRequestQuery.ToComponentDataArray<SendRpcCommandRequestComponent>(Allocator.TempJob);

        // 发送游戏结束的状态给客户端
        var handle1 = Entities.ForEach((Entity entity, ref NetworkIdComponent id) =>
        {
            // 必须阻止多次发送重复RPC请求
            // var requestCount = requestComponents.Count(s => s.TargetConnection == entity);
            //if (requestComponents.Count(s => s.TargetConnection == entity) == 0)
            //{
                var ent = commandBuffer.CreateEntity();
                commandBuffer.AddComponent<ServerGameOverSystemController>(ent, serverGameOvers[0]);
                commandBuffer.AddComponent<SendRpcCommandRequestComponent>(ent,
                        new SendRpcCommandRequestComponent
                        {
                            TargetConnection = entity
                        });
            //}

            //for (var i = 0; i < serverGameOversEntity.Length; i++)
            //{
            //    commandBuffer.DestroyEntity(serverGameOversEntity[i]); // 测试看看这种方式是否真的能够删除空间条目，
            //}

        }).Schedule(Dependency);
        handle1.Complete();


        var handle11 = Job.WithCode(() =>
        {
            for(var i=0;i< serverGameOversEntity.Length; i++)
            {
                commandBuffer.DestroyEntity(serverGameOversEntity[i]); // 测试看看这种方式是否真的能够删除空间条目，
            }

        }).Schedule(handle1);
        handle11.Complete();


        var handle2 = Entities.WithNone<SendRpcCommandRequestComponent>()
            .ForEach((Entity entity, ref ServerGameOverSystemController controller, ref ReceiveRpcCommandRequestComponent reqSrc) =>
        {
            //删除完成的请求
            commandBuffer.DestroyEntity(entity); 
            
        }).Schedule(handle11);
        handle2.Complete();

        //commandBuffer.DestroyEntity(GetSingletonEntity<ServerGameOverSystemController>());

        Dependency = serverGameOvers.Dispose(handle2);
        Dependency = serverGameOversEntity.Dispose(Dependency);
        Dependency = requestComponents.Dispose(Dependency);

    }
}


// 客户端收到服务端状态更新后，知道自己是输了还是赢了，并且更新相应状态

public struct ClientCheckGameOverSystemController : IComponentData { }

[UpdateInGroup(typeof(ClientSimulationSystemGroup))]
public class ClientCheckGameOverSystem : SystemBase
{
    GameObject gameOverObject=null; // 难道这里的变量名更别的地方一样也会被覆盖？验证确认
    public EntityCommandBufferSystem CommandBufferSystem;
    private EntityQuery entityQueryReceive;
    protected override void OnCreate()
    {
        CommandBufferSystem = World.GetExistingSystem<EndSimulationEntityCommandBufferSystem>();
        // 会生成一个字符串You Win 或者 You Lost;等待一段时间后把玩家状态恢复为未准备状态
        EntityQueryDesc entityQueryDesc = new EntityQueryDesc
        {
            All = new ComponentType[] { typeof(ServerGameOverSystemController), 
                typeof(ReceiveRpcCommandRequestComponent) },
            None = new ComponentType[] { typeof(SendRpcCommandRequestComponent) }
        };
        entityQueryReceive = GetEntityQuery(entityQueryDesc);

        RequireForUpdate(entityQueryReceive);
        //gameOverObject = Resources.Load<GameObject>("HintPrepare");
        //gameOverObject.GetComponent<TextMesh>().text = "";
        //gameOverObject = GameObject.Instantiate(gameOverObject);
    }

    protected override void OnUpdate()
    {
        if (gameOverObject == null)
        {
            gameOverObject = Resources.Load<GameObject>("HintPrepare");
            gameOverObject.GetComponent<TextMesh>().text = "";
            gameOverObject = GameObject.Instantiate(gameOverObject);
        }

        EntityCommandBuffer commandBuffer
            = CommandBufferSystem.CreateCommandBuffer();
        NativeArray<ServerGameOverSystemController> serverGameOvers
            = entityQueryReceive.ToComponentDataArray<ServerGameOverSystemController>(Allocator.TempJob);

        NativeArray<Entity> serverGameOversEntities = entityQueryReceive.ToEntityArray(Allocator.TempJob);


        Entities.WithoutBurst()
            .ForEach((Entity ent, ref LeoGameStatus gameStatus, ref LeoPlayerGameStatus playerGameStatus) =>
            {
                gameStatus = new LeoGameStatus { theGameStatus = TheGameStatus.Over };
                if (serverGameOvers[0].WinPlayerId == playerGameStatus.playerId)
                {
                    gameOverObject.GetComponent<TextMesh>().text = "You Win!";
                }
                else if (playerGameStatus.playerId <= 1)
                {
                    gameOverObject.GetComponent<TextMesh>().text = "You Lost!";
                }
                else
                {
                    gameOverObject.GetComponent<TextMesh>().text = serverGameOvers[0].WinPlayerId == 0 ? "Left Win!" : "Right Win!";
                }

                
                GameObject.Destroy(gameOverObject, 5); // 删除对象
                // gameOverObject.GetComponent<TextMesh>().text = "";

                // 重新准备
                playerGameStatus = new LeoPlayerGameStatus
                {
                    playerGameStatus = PlayerGameStatus.NotReady,
                    playerId= playerGameStatus.playerId
                };
                // 同步客户端状态修改到服务端,又可以开始新的一局游戏了
                if (GetEntityQuery(new ComponentType[] { typeof(ClientGameStatusSendSystemController) }).CalculateChunkCount() == 0)
                {
                    var entity = commandBuffer.CreateEntity();
                    commandBuffer.AddComponent<ClientGameStatusSendSystemController>(entity);
                }

                if (GetEntityQuery(new ComponentType[] { typeof(PlayerInitSystemController) }).CalculateChunkCount() == 0)
                {
                    var playerInitEntity = commandBuffer.CreateEntity();
                    commandBuffer.AddComponent<PlayerInitSystemController>(playerInitEntity);
                }

                // commandBuffer.DestroyEntity(GetSingletonEntity<ServerGameOverSystemController>());
                //Entities.ForEach((Entity theEntity, in ServerGameOverSystemController controller, in ReceiveRpcCommandRequestComponent receive) =>
                //{
                //    commandBuffer.DestroyEntity(theEntity);
                //}).Run();

                for (var i = 0; i < serverGameOversEntities.Length; i++)
                {
                    commandBuffer.DestroyEntity(serverGameOversEntities[i]);
                }

            }).Run();

        var handle11 = Job.WithCode(() =>
        {
            for (var i = 0; i < serverGameOversEntities.Length; i++)
            {
                commandBuffer.DestroyEntity(serverGameOversEntities[i]);
            }
        }).Schedule(Dependency);
        handle11.Complete();


        var handle2 = Entities
        .WithNone<SendRpcCommandRequestComponent>()
        .ForEach(
            (Entity reqEnt,
            ref ServerGameOverSystemController controller,
            ref ReceiveRpcCommandRequestComponent reqSrc) =>
            {
                //删除完成的请求
                commandBuffer.DestroyEntity( reqEnt);
            }).Schedule(handle11);
        handle2.Complete();

        Dependency = serverGameOvers.Dispose(handle2);
        Dependency = serverGameOversEntities.Dispose(Dependency);


    }
}
