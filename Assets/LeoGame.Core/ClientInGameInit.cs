

using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Networking.Transport;
using UnityEngine;

/// <summary>
/// 客户端发送请求为了连接到服务端加入游戏
/// </summary>

[BurstCompile]
public struct GoInGameRequest : IRpcCommand // 这是服务端与客户端共用的代码，所有序列化和反序列化虽然在不同机器上执行，但是一定要有
{
    public NativeString64 Version;
    public int TestInt;

    public void Deserialize(ref DataStreamReader reader)
    {
        Version = reader.ReadString();  // 需要与下面的成对出现 把组件值传递给
        TestInt = reader.ReadInt();
    }

    public void Serialize(ref DataStreamWriter writer)
    {
        writer.WriteString(Version); // 需要与上面的成对出现
        writer.WriteInt(TestInt);
    }
    [BurstCompile]
    private static void InvokeExecute(ref RpcExecutor.Parameters parameters)
    {
        RpcExecutor.ExecuteCreateRequestComponent<GoInGameRequest>(ref parameters); // Rpc 需要添加 GoInGameRequest 这个组件才能运行
    }

    // 定义了一个静态 struct InvokeExecuteFunctionPointer<T>  现在的 T 是 RpcExecutor.ExecuteDelegate 类型的委托
    // 而 InvokeExecute 就是 RpcExecutor.ExecuteDelegate 类型的委托
    static PortableFunctionPointer<RpcExecutor.ExecuteDelegate> InvokeExecuteFunctionPointer =
        new PortableFunctionPointer<RpcExecutor.ExecuteDelegate>(InvokeExecute);

    // 而 PortableFunctionPointer<RpcExecutor.ExecuteDelegate> CompileExecute() 是为了实现 IRpcCommand 接口
    public PortableFunctionPointer<RpcExecutor.ExecuteDelegate> CompileExecute()
    {
        return InvokeExecuteFunctionPointer;
    }
}

// 在 Client World中运行
// The system that makes the RPC request component transfer
// 这个继承了 RpcCommandRequestSystem<TActionRequest> 用来执行 GoInGameRequest 命令
// 这个system会寻找拥有 SendRpcCommandRequestComponent 和 TActionRequest（GoInGameRequest） 组件的实体，并执行GoInGameRequest中定义的命令
public class GoInGameRequestSystem : RpcCommandRequestSystem<GoInGameRequest>
{
}


[UpdateInGroup(typeof(ClientSimulationSystemGroup))]
public class GoInGameClientSystem : SystemBase
{
    public EntityCommandBufferSystem CommandBufferSystem;

    protected override void OnCreate()
    {
        CommandBufferSystem = World.GetExistingSystem<EndSimulationEntityCommandBufferSystem>();
        RequireSingletonForUpdate<NetworkIdComponent>();
    }

    protected override void OnUpdate()
    {
        //int playerId = 0; // 直接使用这个好像是不行的。
        // NativeArray<int> arrayPlayerId = new NativeArray<int>(new int[] { 0 }, Allocator.TempJob);
        EntityManager entityManager = EntityManager;
        EntityCommandBuffer commandBuffer
            = CommandBufferSystem.CreateCommandBuffer();
        //we addd the with structural changes so that we can add components and the with none to make sure we don't send the request if we already have a connection to the server
        // WithStructuralChanges()：在主线程以关闭Burst的方式运行，这时候可以做一些structural changes的操作。建议使用EntityCommandBuffer来代替这种用法。
        Entities.WithoutBurst()//.WithStructuralChanges()
            .WithNone<NetworkStreamInGame>().ForEach((Entity ent, ref NetworkIdComponent id) =>
        {
            //we add a network stream in game component so that this code is only run once
            commandBuffer.AddComponent<NetworkStreamInGame>(ent);
            // RPC 请求完成之后会自动删除。。。
            //create an entity to hold our request, requests are automatically sent when they are detected on an entity making it really simple to send them.
            var req = commandBuffer.CreateEntity();
            //add our go in game request
            commandBuffer.AddComponent<GoInGameRequest>(req);
            //add the rpc request component, this is what tells the sending system to send it
            commandBuffer.AddComponent<SendRpcCommandRequestComponent>(req);
            //add the entity with the network components as our target.
            commandBuffer.SetComponent<SendRpcCommandRequestComponent>(req, new SendRpcCommandRequestComponent { TargetConnection = ent });

            // 为每个生成的玩家对象指定自定义位置==> 这个只是进行距离重要性计算的。
            commandBuffer.AddComponent<GhostConnectionPosition>(req);
            commandBuffer.SetComponent(req, new GhostConnectionPosition
            {
                Position = new float3(-10f + 10f * id.Value, 0f, 0f)
            });


            commandBuffer.SetComponent(req, new GoInGameRequest { TestInt = 90, Version = "preview-0.0.1" }); // 添加自定义的 Version

            // 带有 SendRpcCommandRequestComponent 和 GoInGameRequest 的组件会被 RpcCommandRequestSystem<TActionRequest> 执行
            // 创建的实体命令应该在操作完成后会被删除

            // Camera add directly test 
            /*            Camera camera = new Camera();   // 出现为空的错误    
                        camera.enabled = true;
                        GameManager.Instantiate<Camera>(camera);*/
            // arrayPlayerId[0] = id.Value;
            var playerStatus = GetSingletonEntity<PlayerIdNotInit>();
            commandBuffer.SetComponent<LeoPlayerGameStatus>(playerStatus, new LeoPlayerGameStatus
            {
                playerGameStatus = PlayerGameStatus.NotReady,
                playerId = id.Value
            });
            commandBuffer.RemoveComponent<PlayerIdNotInit>(playerStatus);

            // 解决观战玩家也可以发送自己的游戏状态导致游戏状态混乱的问题
            // 只有连接的前两个可以进行游戏
            if(id.Value>0 && id.Value <= 2)
            {
                var playInitEntity = commandBuffer.CreateEntity();
                commandBuffer.AddComponent<PlayerInitSystemController>(playInitEntity);
            }



        }).Run();

        #region 修改这段代码通过直接在前面就修改玩家状态的值就好了
        /*
                handle1.Complete();
                //获取并保存玩家ID
                var handle2 = Entities
                    .ForEach((Entity entity,ref LeoGameStatus gameStatus,in PlayerIdNotInit player, in LeoPlayerGameStatus playerStatus) =>
                    {
                        LeoPlayerGameStatus editCom = new LeoPlayerGameStatus
                        {
                            playerGameStatus = playerStatus.playerGameStatus,
                            playerId = arrayPlayerId[0]
                        };
                        commandBuffer.SetComponent<LeoPlayerGameStatus>(entity, editCom); // 修改真实值
                        commandBuffer.RemoveComponent<PlayerIdNotInit>(entity); // 删除控制标识
                    }).Schedule(handle1);

                handle2.Complete();
                Dependency = arrayPlayerId.Dispose(handle2);


                Debug.Log("Execute to That=>ClientInGameInit3");*/
        #endregion
    }
}