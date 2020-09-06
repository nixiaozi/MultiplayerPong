
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

[AlwaysUpdateSystem]
[UpdateInGroup(typeof(ClientInitializationSystemGroup))]

public class ClientGameStatusHoldSystem : SystemBase
{
    public EntityCommandBufferSystem CommandBufferSystem;
    private Entity ThePlayerGameStatus;

    protected override void OnCreate()
    {
        CommandBufferSystem = World.GetExistingSystem<EndInitializationEntityCommandBufferSystem>();


        //CommandBufferSystem.EntityManager.CreateEntity(typeof(LeoGameStatus), typeof(LeoPlayerGameStatus));
        ThePlayerGameStatus = EntityManager.CreateEntity(typeof(LeoGameStatus), typeof(LeoPlayerGameStatus)); // 创建一个全局可访问的实体


        SetSingleton<LeoGameStatus>(new LeoGameStatus { theGameStatus = TheGameStatus.NoReady }); // 直接修改单例实体组件的值



        //CommandBufferSystem.SetSingleton<LeoGameStatus>(new LeoGameStatus { theGameStatus = TheGameStatus.NoReady });
        //CommandBufferSystem.SetSingleton<LeoPlayerGameStatus>(new LeoPlayerGameStatus { playerGameStatus = PlayerGameStatus.NotReady });
    }

    protected override void OnUpdate()
    {
        EntityCommandBuffer.Concurrent commandBuffer
            = CommandBufferSystem.CreateCommandBuffer().ToConcurrent();

        // 只能使用 NativeArray<T> 的方式传递值给 Burst 编译器执行
        NativeArray<Entity> getThePlayerGameStatus =new NativeArray<Entity>(new Entity[] { ThePlayerGameStatus },Allocator.TempJob) ;

        // 发送Rpc请求
        var handle1 = Entities
            //.WithDeallocateOnJobCompletion(getThePlayerGameStatus) // 
            .ForEach((Entity entity, int entityInQueryIndex, ref NetworkIdComponent id) =>
        {

            Debug.Log("发送给服务端 LeoPlayerGameStatus 消息！");
            Entity newEntity = commandBuffer.Instantiate(entityInQueryIndex, getThePlayerGameStatus[0]);

            commandBuffer.AddComponent<LeoPlayerGameStatus>(entityInQueryIndex,newEntity);

            commandBuffer.AddComponent<SendRpcCommandRequestComponent>(entityInQueryIndex, newEntity);

            commandBuffer.SetComponent(entityInQueryIndex, newEntity,
                new SendRpcCommandRequestComponent { TargetConnection = entity });

            

        }).ScheduleParallel(this.Dependency);

        var handle11 = Entities
            .WithNone<SendRpcCommandRequestComponent>()
            .ForEach(
                (Entity entity, 
                int entityInQueryIndex,
                ref LeoPlayerGameStatus leoPlayerGameStatus,
                ref ReceiveRpcCommandRequestComponent reqSrc) => 
            {
                commandBuffer.DestroyEntity(entityInQueryIndex, entity); // 删除已经完成的Rpc请求
            }).ScheduleParallel(handle1);

        // handle1.Complete();  已经添加了依赖链，不需要显式的指定 handle1 必须完成

        // 接收Rpc请求数据
        var handle2 = Entities
            .WithNone<SendRpcCommandRequestComponent>()
            .ForEach(
                (Entity reqEnt, 
                int entityInQueryIndex, 
                ref LeoGameStatus leoGameStatus, 
                ref ReceiveRpcCommandRequestComponent reqSrc) =>
             {
                 Debug.Log("获得了从服务端返回的 LeoGameStatus 消息！");

                commandBuffer.DestroyEntity(entityInQueryIndex,reqEnt);
             }).ScheduleParallel(handle11);

        handle2.Complete();

        Dependency = getThePlayerGameStatus.Dispose(handle2); // Dispose getThePlayerGameStatus
    }

}





// [DisableAutoCreation]  这个需要自动运行，直接在开始就把游戏状态的控制加入
[AlwaysUpdateSystem]
[UpdateInGroup(typeof(ServerInitializationSystemGroup))]
public class ServerGameStatusHoldSystem : SystemBase
{
    public EntityCommandBufferSystem CommandBufferSystem;

    protected override void OnCreate()
    {
        // 创建用于存储游戏状态的实体 并进行数据初始化
        var entity=EntityManager.CreateEntity(typeof(LeoGameStatus));

        EntityManager.SetComponentData(entity, new LeoGameStatus
        {
            theGameStatus = TheGameStatus.NoReady
        });

        CommandBufferSystem = World.GetExistingSystem<EndInitializationEntityCommandBufferSystem>();

        /*      // 有与ECS数据存储的格式，同组件的数据存在一起，我们需要利用这个规则，面向对象的方式处理不可取
                EntityManager.AddBuffer<UnReadyPlayerCollection>(entity); // 初始化未准备的玩家列表
                EntityManager.AddBuffer<ReadyPlayerCollection>(entity); // 初始化已准备玩家列表
                EntityManager.AddBuffer<PlayingPlayerCollection>(entity); // 初始化正在游戏中的玩家列表
                EntityManager.AddBuffer<PausedPlayerCollection>(entity); // 初始化已暂停的玩家列表
                EntityManager.AddBuffer<BreakPlayerCollection>(entity); // 初始化连接中断的玩家列表
        */


    }

    protected override void OnUpdate()
    {
        EntityManager entityManager = EntityManager;

        EntityCommandBuffer commandBuffer
            = CommandBufferSystem.CreateCommandBuffer();

        /*      // 服务端上有多个对于客户端的连接，所以不能确定是那个客户端，一定要在 ReceiveRpcCommandRequestComponent 组件中寻找
                int ConnectNum = 0;
                if (HasSingleton<NetworkIdComponent>())
                {
                    ConnectNum = GetSingleton<NetworkIdComponent>().Value;
                }
        */

        // 服务端的Rpc请求处理需要知道每个请求的对应客户端好像只能使用 
        // EntityManager.GetComponentData<NetworkIdComponent>(reqSrc.SourceConnection).Value;
        Entities
            .WithStructuralChanges()  
            .WithNone<SendRpcCommandRequestComponent>()
            .ForEach(
                (Entity reqEnt,
                ref LeoPlayerGameStatus leoPlayerGameStatus,
                ref ReceiveRpcCommandRequestComponent reqSrc) =>
            {
                // int ConnectNum = GetSingleton<NetworkIdComponent>().Value;
                int ConnectNum = EntityManager.GetComponentData<NetworkIdComponent>(reqSrc.SourceConnection).Value;
                Debug.Log("服务端收到客户端Rpc连接 LeoPlayerGameStatus ");

                

                var entity= commandBuffer.CreateEntity();

                // 这里使用AddSharedComponent会有问题？？ 可能是不能确认 共享组件的值是否相等
                commandBuffer.AddComponent<LeoPlayerGameStatus>(entity, new LeoPlayerGameStatus
                {
                     playerGameStatus = leoPlayerGameStatus.playerGameStatus,
                     playerId=ConnectNum
                });

                commandBuffer.AddComponent<LeoGameStatus>(entity, new LeoGameStatus { theGameStatus = TheGameStatus.AllReady });

                commandBuffer.AddComponent<SendRpcCommandRequestComponent>(entity, new SendRpcCommandRequestComponent
                {
                    TargetConnection = reqSrc.SourceConnection
                });

                /*EntityManager.AddSharedComponentData<ThePlayerGameStatus>(entity,
                     new ThePlayerGameStatus 
                     { 
                         playerGameStatus = leoPlayerGameStatus.playerGameStatus ,
                         PlayId= ConnectNum,
                     });*/


                commandBuffer.DestroyEntity(reqEnt);
                /*entityManager.DestroyEntity(reqEnt);*/
            }).Run();

        // handle1.Complete();

    }
}

