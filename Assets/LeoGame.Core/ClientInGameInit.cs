

using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Networking.Transport;

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
    protected override void OnUpdate()
    {
        EntityManager entityManager = EntityManager;
        //we addd the with structural changes so that we can add components and the with none to make sure we don't send the request if we already have a connection to the server
        // WithStructuralChanges()：在主线程以关闭Burst的方式运行，这时候可以做一些structural changes的操作。建议使用EntityCommandBuffer来代替这种用法。
        Entities.WithStructuralChanges().WithNone<NetworkStreamInGame>().ForEach((Entity ent, ref NetworkIdComponent id) =>
        {
            //we add a network stream in game component so that this code is only run once
            entityManager.AddComponent<NetworkStreamInGame>(ent);
            // RPC 请求完成之后会自动删除。。。
            //create an entity to hold our request, requests are automatically sent when they are detected on an entity making it really simple to send them.
            var req = entityManager.CreateEntity();
            //add our go in game request
            entityManager.AddComponent<GoInGameRequest>(req);
            //add the rpc request component, this is what tells the sending system to send it
            entityManager.AddComponent<SendRpcCommandRequestComponent>(req);
            //add the entity with the network components as our target.
            entityManager.SetComponentData(req, new SendRpcCommandRequestComponent { TargetConnection = ent });

            // 为每个生成的玩家对象指定自定义位置==> 这个只是进行距离重要性计算的。
            entityManager.AddComponent<GhostConnectionPosition>(req);
            entityManager.SetComponentData(req, new GhostConnectionPosition
            {
                Position = new float3(-10f + 10f * id.Value, 0f, 0f)
            });


            EntityManager.SetComponentData(req, new GoInGameRequest { TestInt = 90,Version="preview-0.0.1" }); // 添加自定义的 Version

            // 带有 SendRpcCommandRequestComponent 和 GoInGameRequest 的组件会被 RpcCommandRequestSystem<TActionRequest> 执行
            // 创建的实体命令应该在操作完成后会被删除

            // Camera add directly test 
            /*            Camera camera = new Camera();   // 出现为空的错误    
                        camera.enabled = true;
                        GameManager.Instantiate<Camera>(camera);*/


        }).Run();


    }
}