

using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;

/// <summary>
/// 客户端发送请求为了连接到服务端加入游戏
/// </summary>

[BurstCompile]
public struct GoInGameRequest : IRpcCommand
{
    public void Deserialize(ref DataStreamReader reader)
    {
    }

    public void Serialize(ref DataStreamWriter writer)
    {
    }
    [BurstCompile]
    private static void InvokeExecute(ref RpcExecutor.Parameters parameters)
    {
        RpcExecutor.ExecuteCreateRequestComponent<GoInGameRequest>(ref parameters);
    }

    static PortableFunctionPointer<RpcExecutor.ExecuteDelegate> InvokeExecuteFunctionPointer =
        new PortableFunctionPointer<RpcExecutor.ExecuteDelegate>(InvokeExecute);
    public PortableFunctionPointer<RpcExecutor.ExecuteDelegate> CompileExecute()
    {
        return InvokeExecuteFunctionPointer;
    }
}

// 在 Client World中运行
// The system that makes the RPC request component transfer
public class GoInGameRequestSystem : RpcCommandRequestSystem<GoInGameRequest>
{
}


[UpdateInGroup(typeof(ClientSimulationSystemGroup))]
public class GoInGameClientSystem : SystemBase
{
    protected override void OnUpdate()
    {
        EntityManager entityManager = EntityManager;
        //we addd the with structural changes so that we can add components and the with none to make sure we don't send the request if we already have a connection to the server
        Entities.WithStructuralChanges().WithNone<NetworkStreamInGame>().ForEach((Entity ent, ref NetworkIdComponent id) =>
        {
            //we add a network stream in game component so that this code is only run once
            entityManager.AddComponent<NetworkStreamInGame>(ent);
            //create an entity to hold our request, requests are automatically sent when they are detected on an entity making it really simple to send them.
            var req = entityManager.CreateEntity();
            //add our go in game request
            entityManager.AddComponent<GoInGameRequest>(req);
            //add the rpc request component, this is what tells the sending system to send it
            entityManager.AddComponent<SendRpcCommandRequestComponent>(req);
            //add the entity with the network components as our target.
            entityManager.SetComponentData(req, new SendRpcCommandRequestComponent { TargetConnection = ent });

            // Camera add directly test 
            /*            Camera camera = new Camera();   // 出现为空的错误    
                        camera.enabled = true;
                        GameManager.Instantiate<Camera>(camera);*/


        }).Run();
    }
}