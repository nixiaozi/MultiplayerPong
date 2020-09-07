using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;

/// <summary>
/// 游戏准备阶段，分别初始化服务端和客户端的端口监听和套接层
/// </summary>
[UpdateInWorld(UpdateInWorld.TargetWorld.Default)]
public class GameInit : SystemBase
{
    // Singleton component to trigger connections once from a control system
    struct InitGameComponent : IComponentData
    {
    }

    protected override void OnCreate()
    {
        RequireSingletonForUpdate<InitGameComponent>(); // 定义了 Update 需要有 InitGameComponent 这个组件才能运行
        // Create singleton, require singleton for update so system runs once
        EntityManager.CreateEntity(typeof(InitGameComponent));
    }

    protected override void OnUpdate()
    {
        // Destroy singleton to prevent system from running again
        EntityManager.DestroyEntity(GetSingletonEntity<InitGameComponent>()); // OnCreate 定义了需要有 InitGameComponent 这个组件才能运行，这里删除后就不会再运行
        foreach (var world in World.All)
        {
            var network = world.GetExistingSystem<NetworkStreamReceiveSystem>();
            if (world.GetExistingSystem<ClientSimulationSystemGroup>() != null)
            {
                //to chose an IP instead of just always connecting to localhost uncomment the next line and delete the other two lines of code
                //NetworkEndPoint.TryParse("IP address here", 7979, out NetworkEndPoint ep);
                NetworkEndPoint ep = NetworkEndPoint.LoopbackIpv4;
                ep.Port = 7979;

                network.Connect(ep);
            }
            else if (world.GetExistingSystem<ServerSimulationSystemGroup>() != null)
            {
                // Server world automatically listens for connections from any host
                NetworkEndPoint ep = NetworkEndPoint.AnyIpv4;
                ep.Port = 7979;
                network.Listen(ep);

                
            }
        }
    }


}