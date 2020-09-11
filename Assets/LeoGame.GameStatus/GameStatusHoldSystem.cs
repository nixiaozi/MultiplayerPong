
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

/// <summary>
/// 控制 ClientGameStatusHoldSystem 的方法
/// </summary>
public struct ClientGameStatusSendSystemController : IComponentData{ }

public struct PlayerIdNotInit : IComponentData { public int value; } // 标识现在并没有初始化PlayerID,暂时先不用这个

[UpdateInGroup(typeof(ClientSimulationSystemGroup))]
public class ClientGameStatusSendSystem : SystemBase
{
    public EntityCommandBufferSystem CommandBufferSystem;
    // private NativeArray<LeoPlayerGameStatus> ThePlayerGameStatus;
    private EntityQuery ThePlayerGameStatus;

    protected override void OnCreate()
    {
        CommandBufferSystem = World.GetExistingSystem<EndSimulationEntityCommandBufferSystem>();
        EntityManager.CreateEntity(typeof(LeoGameStatus), typeof(LeoPlayerGameStatus),typeof(PlayerIdNotInit)); // 创建一个全局可访问的实体
        // ThePlayerGameStatus = GetSingletonEntity<LeoGameStatus>();
        ThePlayerGameStatus = GetEntityQuery(typeof(LeoGameStatus), typeof(LeoPlayerGameStatus));
                                    //.ToComponentDataArray<LeoPlayerGameStatus>(Allocator.TempJob); // 这里产生 NativeArray 被释放一次后就不会再有了

        RequireSingletonForUpdate<ClientGameStatusSendSystemController>(); // 添加控制
        RequireSingletonForUpdate<NetworkIdComponent>();
        // var requiteEntity = GetSingletonEntity<NetworkIdComponent>();
        EntityManager.CreateEntity(typeof(ClientGameStatusSendSystemController));

        /*// 创建游戏状态接收系统，下面的方法应该也可以的
        // World.GetOrCreateSystemsAndLogException(new System.Type[] { typeof(ClientGameStatusReceiveSystem)});
        ClientSimulationSystemGroup simulationSystemGroup = new ClientSimulationSystemGroup();
        simulationSystemGroup.AddSystemToUpdateList(new ClientGameStatusReceiveSystem());
        World.GetOrCreateSystem(typeof(ClientGameStatusReceiveSystem));*/
        
    }

    // 仅发送请求，发送后删除
    protected override void OnUpdate()
    {
        EntityManager.DestroyEntity(GetSingletonEntity<ClientGameStatusSendSystemController>()); // 删除控制标识就不会再次执行

        EntityCommandBuffer.Concurrent commandBuffer
            = CommandBufferSystem.CreateCommandBuffer().ToConcurrent();

        // NativeArray<Entity> getThePlayerGameStatus = new NativeArray<Entity>(new Entity[] { ThePlayerGameStatus }, Allocator.TempJob); // 这是一个值引用
        NativeArray<LeoPlayerGameStatus> getThePlayerGameStatus = ThePlayerGameStatus.ToComponentDataArray<LeoPlayerGameStatus>(Allocator.TempJob);
        // 发送Rpc请求
        var handle1 = Entities
            //.WithDeallocateOnJobCompletion(getThePlayerGameStatus) // 
            .ForEach((Entity entity, int entityInQueryIndex, ref NetworkIdComponent id) =>
            {

                Debug.Log("发送给服务端 LeoPlayerGameStatus 消息！");
                //Entity newEntity = commandBuffer.Instantiate(entityInQueryIndex, getThePlayerGameStatus[0]);

                //commandBuffer.AddComponent<LeoPlayerGameStatus>(entityInQueryIndex, newEntity);
                

                // 定义发送
                Entity newEntity = commandBuffer.CreateEntity(entityInQueryIndex);
                commandBuffer.AddComponent<LeoPlayerGameStatus>(entityInQueryIndex, newEntity, new LeoPlayerGameStatus
                {
                    playerGameStatus = getThePlayerGameStatus[0].playerGameStatus,
                    playerId=id.Value
                });

                commandBuffer.AddComponent<SendRpcCommandRequestComponent>(entityInQueryIndex, newEntity);

                commandBuffer.SetComponent(entityInQueryIndex, newEntity,
                    new SendRpcCommandRequestComponent { TargetConnection = entity });


            }).Schedule(this.Dependency);

        handle1.Complete();

        Dependency = getThePlayerGameStatus.Dispose(handle1);

    }
}

public struct ClientGameStatusReceiveSystemController : IComponentData
{

}


// [DisableAutoCreation]
// [AlwaysUpdateSystem] // error: InvalidOperationException: Cannot require EntityQuery for update on a system with AlwaysSynchronizeSystemAttribute
[UpdateAfter(typeof(ClientGameStatusSendSystem))]
[UpdateInGroup(typeof(ClientSimulationSystemGroup))] // 一定要确认在一个组，上面的才有效
public class ClientGameStatusReceiveSystem : SystemBase
{
    public EntityCommandBufferSystem CommandBufferSystem;
    private Entity ThePlayerGameStatus;
    private EntityQuery entityQueryReceive;

    protected override void OnCreate()
    {
        CommandBufferSystem = World.GetExistingSystem<EndSimulationEntityCommandBufferSystem>();
        ThePlayerGameStatus = GetSingletonEntity<LeoGameStatus>();

        EntityQueryDesc entityQueryDesc = new EntityQueryDesc
        {
            All = new ComponentType[] { typeof(LeoGameStatus), typeof(ReceiveRpcCommandRequestComponent) },
            None = new ComponentType[] { typeof(SendRpcCommandRequestComponent) }
        };

        entityQueryReceive = GetEntityQuery(entityQueryDesc);

        RequireForUpdate(entityQueryReceive); // 需要有才执行
        RequireSingletonForUpdate<ClientGameStatusReceiveSystemController>(); // 添加控制
        EntityManager.CreateEntity(typeof(ClientGameStatusReceiveSystemController));
    }

    protected override void OnUpdate()
    {
        Debug.Log("Do ClientGameStatusReceiveSystem Update");
        EntityCommandBuffer.Concurrent commandBuffer
            = CommandBufferSystem.CreateCommandBuffer().ToConcurrent();
        NativeArray<LeoGameStatus> entitiesComponentReceive = entityQueryReceive.ToComponentDataArray<LeoGameStatus>(Allocator.TempJob); // 直接获取组件
        var handle11 = Entities
            .WithNone<SendRpcCommandRequestComponent>()
            .ForEach(
                (Entity entity,
                int entityInQueryIndex,
                ref LeoPlayerGameStatus leoPlayerGameStatus,
                ref ReceiveRpcCommandRequestComponent reqSrc) =>
                {
                    commandBuffer.DestroyEntity(entityInQueryIndex, entity); // 删除已经完成的Rpc请求
                }).ScheduleParallel(this.Dependency);

        handle11.Complete();

        // 接收并响应Rpc请求返回的数据
        var handle20 = Entities
            .WithAll<LeoPlayerGameStatus>()
            .ForEach(
                (Entity reqEnt,
                int entityInQueryIndex,
                in LeoGameStatus leoGameStatus) =>
                {
                    Debug.Log("获得了从服务端返回的 LeoGameStatus 消息！");
                    commandBuffer.SetComponent<LeoGameStatus>(entityInQueryIndex, reqEnt, entitiesComponentReceive[entitiesComponentReceive.Length-1]); //获取最后的组件值
                    
                    
                
                
                }).ScheduleParallel(this.Dependency);


        handle20.Complete();

        var handle21 = Entities
        .WithNone<SendRpcCommandRequestComponent>()
        .ForEach(
            (Entity reqEnt,
            int entityInQueryIndex,
            ref LeoGameStatus leoGameStatus,
            ref ReceiveRpcCommandRequestComponent reqSrc) =>
        {
            //删除完成的请求
            commandBuffer.DestroyEntity(entityInQueryIndex, reqEnt);
        }).ScheduleParallel(handle20);

        handle21.Complete();


        Dependency = entitiesComponentReceive.Dispose(handle21);  // dispose

        #region Commit For Use Job.WithCode 
        /*        EntityCommandBuffer.Concurrent commandBuffer
            = CommandBufferSystem.CreateCommandBuffer().ToConcurrent();
        NativeArray<LeoPlayerGameStatus> entitiesComponentReceive = entityQueryReceive.ToComponentDataArray<LeoPlayerGameStatus>(Allocator.TempJob); // 直接获取组件
        NativeArray<Entity> entitiesReveive = entityQueryReceive.ToEntityArray(Allocator.TempJob);*/
        /*        Job.WithBurst(FloatMode.Default, FloatPrecision.Standard, false)
                    .WithCode(() =>
                    {
                        // commandBuffer.RemoveComponent<LeoPlayerGameStatus>(0,ThePlayerGameStatus); // 可以直接使用下面的设置组件
                        commandBuffer.SetComponent<LeoPlayerGameStatus>(1, ThePlayerGameStatus, entitiesComponentReceive[entitiesComponentReceive.Length - 1]);

                        for (int i = 0; i < entitiesReveive.Length; i++)
                        {
                            commandBuffer.DestroyEntity(2 + i, entitiesReveive[i]);
                        }


                    }).Schedule(handle2);*/



        /*
                var handle11 = Entities
                    .WithNone<SendRpcCommandRequestComponent>()
                    .ForEach(
                        (Entity entity,
                        int entityInQueryIndex,
                        ref LeoPlayerGameStatus leoPlayerGameStatus,
                        ref ReceiveRpcCommandRequestComponent reqSrc) =>
                    {
                        commandBuffer.DestroyEntity(entityInQueryIndex, entity); // 删除已经完成的Rpc请求
                    }).ScheduleParallel(handle2);
        */
        #endregion



    }
}



/* // 客户端RPC请求发送与接收
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


*/


[UpdateInGroup(typeof(ServerInitializationSystemGroup))]
public class ServerGameStatusReceiveSystem : SystemBase
{
    public EntityCommandBufferSystem CommandBufferSystem;
    private EntityQuery entityQueryReceive;
    // private EntityQuery playerGameStatus;
    protected override void OnCreate()
    {
        // 创建用于存储游戏状态的实体 并进行数据初始化
        var entity = EntityManager.CreateEntity(typeof(LeoGameStatus));

        EntityManager.SetComponentData(entity, new LeoGameStatus
        {
            theGameStatus = TheGameStatus.NoReady
        });

        // 进行请求限制
        EntityQueryDesc entityQueryDesc = new EntityQueryDesc
        {
            All = new ComponentType[] { typeof(LeoPlayerGameStatus), typeof(ReceiveRpcCommandRequestComponent) },
            None = new ComponentType[] { typeof(SendRpcCommandRequestComponent) }
        };

        entityQueryReceive = GetEntityQuery(entityQueryDesc);

        RequireForUpdate(entityQueryReceive); // 需要有才执行

        CommandBufferSystem = World.GetExistingSystem<EndInitializationEntityCommandBufferSystem>();

        /*EntityQueryDesc playerQueryDesc = new EntityQueryDesc
        {
            All = new ComponentType[] { typeof(LeoPlayerGameStatus) },
            None=new ComponentType[] { typeof(SendRpcCommandRequestComponent),typeof(ReceiveRpcCommandRequestComponent) }
        };
        playerGameStatus= GetEntityQuery(playerQueryDesc);*/

    }


    protected override void OnUpdate()
    {
        EntityManager entityManager = EntityManager;

        EntityCommandBuffer commandBuffer
            = CommandBufferSystem.CreateCommandBuffer();
        NativeArray<LeoPlayerGameStatus> leoPlayerStatusReceive 
            = entityQueryReceive.ToComponentDataArray<LeoPlayerGameStatus>(Allocator.TempJob);

        //NativeArray<int> hasDonePlayerIds = new NativeArray<int>(leoPlayerStatusReceive.Length, Allocator.TempJob);

        //NativeArray<int> CountDonePlayerIds = new NativeArray<int>(new int[] { 0 }, Allocator.TempJob);
        /*NativeArray<ReceiveRpcCommandRequestComponent> leoPlayerConnectReveive 
            = entityQueryReceive.ToComponentDataArray<ReceiveRpcCommandRequestComponent>(Allocator.TempJob);*/


        Debug.Log("服务端收到客户端Rpc连接 LeoPlayerGameStatus ");

        // 对已经存在服务端的客户端连接状态，直接修改就好了。对于找不到的就只能在外部修改了
        var handle1=Entities
            .WithoutBurst()
            // .WithStructuralChanges() //  WithStructuralChanges only supper Run()
            .WithNone<SendRpcCommandRequestComponent, ReceiveRpcCommandRequestComponent>()
            .ForEach(
                (Entity reqEnt,
                ref LeoPlayerGameStatus leoPlayerGameStatus) =>
                {
                    int currentPlayerId = leoPlayerGameStatus.playerId;
                    // int PlayerId = EntityManager.GetComponentData<NetworkIdComponent>(reqSrc.SourceConnection).Value;
                    // int currentIndex = 0;
                    if (leoPlayerStatusReceive.Select(s => s.playerId).Contains(currentPlayerId))
                    {
                        commandBuffer.SetComponent<LeoPlayerGameStatus>(reqEnt,
                            leoPlayerStatusReceive.First(s => s.playerId == currentPlayerId));

                        //hasDonePlayerIds[currentIndex] = currentPlayerId;
                        //currentIndex++;
                        //CountDonePlayerIds[0] = currentIndex;
                    }
                    else
                    {
                        // 不存在自然不会更新
                    }

                }).Schedule(Dependency);
        handle1.Complete();

        var handle2 = Entities.ForEach((Entity entity, ref LeoPlayerGameStatus leoPlayerGame, ref ReceiveRpcCommandRequestComponent receive) =>
        {
            commandBuffer.DestroyEntity(entity); // 删除已经使用过的请求对象
        }).Schedule(handle1);
        handle2.Complete();

        var controller = commandBuffer.CreateEntity();
        commandBuffer.AddComponent<ServerGameStatusSendSystemController>(controller); // 添加控制以执行更新游戏状态并同步给客户端

        Dependency = leoPlayerStatusReceive.Dispose(handle2);


        #region 注释添加玩家状态实体的部分，转到初始化时添加
        /*
                var handle2 = Job.WithCode(() =>
                {
                    // 把多余的数据标识为-1 不参与后面的比对
                    if (CountDonePlayerIds[0]!=0)
                    {
                        for (var j = CountDonePlayerIds[0] - 1; j < hasDonePlayerIds.Length; j++)
                        {
                            hasDonePlayerIds[CountDonePlayerIds[0] - 1] = -1;
                        }

                    }

                    // var hasDonePlayerIdsArray = hasDonePlayerIds.ToArray(); // DynamicBuffer to NativeArray
                    for (var i= CountDonePlayerIds[0]; i< leoPlayerStatusReceive.Length; i++)
                    {
                        var thePlayId = leoPlayerStatusReceive[i].playerId;
                        if (!hasDonePlayerIds.Contains(thePlayId))
                        {
                            var the = commandBuffer.CreateEntity();
                            commandBuffer.AddComponent<LeoPlayerGameStatus>(the, leoPlayerStatusReceive[i]);
                        }

                    }

                    var controller  = commandBuffer.CreateEntity();
                    commandBuffer.AddComponent<ServerGameStatusSendSystemController>(controller); // 添加控制以执行更新游戏状态并同步给客户端

                }).Schedule(handle1);

                handle2.Complete();

                Dependency = leoPlayerStatusReceive.Dispose(handle2);  // dispose
                Dependency = hasDonePlayerIds.Dispose(Dependency);
                Dependency = CountDonePlayerIds.Dispose(Dependency);
        */
        #endregion
    }
}

/// <summary>
/// 这个是服务端发送游戏状态给客户端的状态
/// </summary>
public struct ServerGameStatusSendSystemController : IComponentData { }

[UpdateInGroup(typeof(ServerInitializationSystemGroup))]
public class ServerGameStatusSendSystem : SystemBase
{
    public EntityCommandBufferSystem CommandBufferSystem;

    private EntityQuery leoPlayerGameStatusesQuery;

    protected override void OnCreate()
    {
        CommandBufferSystem = World.GetExistingSystem<EndInitializationEntityCommandBufferSystem>();
        EntityQueryDesc entityQueryDesc = new EntityQueryDesc
        {
            All = new ComponentType[] { typeof(LeoPlayerGameStatus) },
            None = new ComponentType[] { typeof(ReceiveRpcCommandRequestComponent),typeof(SendRpcCommandRequestComponent) }
        };

        leoPlayerGameStatusesQuery = GetEntityQuery(entityQueryDesc);
        RequireSingletonForUpdate<ServerGameStatusSendSystemController>();
    }


    protected override void OnUpdate()
    {
        EntityCommandBuffer commandBuffer
               = CommandBufferSystem.CreateCommandBuffer();
        NativeArray<LeoPlayerGameStatus> leoPlayerGameStatuses 
            = leoPlayerGameStatusesQuery.ToComponentDataArray<LeoPlayerGameStatus>(Allocator.TempJob);
        /*NativeArray<ReceiveRpcCommandRequestComponent> leoPlayerConnectReveive  // 由于发送请求需要对于每个人都发所以可以直接通过系统获取
            = leoPlayerGameStatusesQuery.ToComponentDataArray<ReceiveRpcCommandRequestComponent>(Allocator.TempJob);*/
        NativeArray<LeoGameStatus> leoGameStatusesArray = new NativeArray<LeoGameStatus>(new LeoGameStatus[] { new LeoGameStatus { theGameStatus = TheGameStatus.NoReady } }, Allocator.TempJob);

        // 删除标识防止一直执行
        commandBuffer.DestroyEntity(GetSingletonEntity<ServerGameStatusSendSystemController>());

        // 遍历每个玩家的状态进行状态判断
        var handle1 = Entities
            .WithoutBurst()
            .ForEach((Entity entity, in LeoGameStatus gameStatus) =>
            {
                var countPlayer = leoPlayerGameStatuses.Length;

                if (leoPlayerGameStatuses.Count(s=>s.playerGameStatus== PlayerGameStatus.NotReady)== countPlayer)
                {
                    leoGameStatusesArray[0] = new LeoGameStatus { theGameStatus = TheGameStatus.NoReady };
                    //commandBuffer.SetComponent<LeoGameStatus>(entity,new LeoGameStatus { theGameStatus= TheGameStatus.NoReady });
                }
                else if (leoPlayerGameStatuses.Count(s => s.playerGameStatus == PlayerGameStatus.Ready) < countPlayer)
                {
                    leoGameStatusesArray[0] = new LeoGameStatus { theGameStatus = TheGameStatus.PartReady };
                    //commandBuffer.SetComponent<LeoGameStatus>(entity, new LeoGameStatus { theGameStatus = TheGameStatus.PartReady });
                }
                else if(leoPlayerGameStatuses.Count(s => s.playerGameStatus == PlayerGameStatus.Ready) == countPlayer)
                {
                    leoGameStatusesArray[0] = new LeoGameStatus { theGameStatus = TheGameStatus.AllReady };
                    //commandBuffer.SetComponent<LeoGameStatus>(entity, new LeoGameStatus { theGameStatus = TheGameStatus.AllReady });
                }
                else if(leoPlayerGameStatuses.Count(s => s.playerGameStatus == PlayerGameStatus.Playing) == countPlayer)
                {
                    leoGameStatusesArray[0] = new LeoGameStatus { theGameStatus = TheGameStatus.Playing };
                    //commandBuffer.SetComponent<LeoGameStatus>(entity, new LeoGameStatus { theGameStatus = TheGameStatus.Playing });
                }

                commandBuffer.SetComponent<LeoGameStatus>(entity, leoGameStatusesArray[0]);

            }).Schedule(Dependency);

        handle1.Complete();

        //完成游戏状态更新后，同步状态信息给客户端
        var handle2 = Entities
            .ForEach((Entity ent, ref NetworkIdComponent id) =>
            {
                var entity = commandBuffer.CreateEntity();
                commandBuffer.AddComponent<LeoGameStatus>(entity, leoGameStatusesArray[0]);
                commandBuffer.AddComponent<SendRpcCommandRequestComponent>(entity,
                    new SendRpcCommandRequestComponent
                    {
                        TargetConnection = ent
                    });


            }).Schedule(handle1);

        handle2.Complete();


        Dependency = leoPlayerGameStatuses.Dispose(handle2);
        Dependency = leoGameStatusesArray.Dispose(Dependency);


        //Job.WithCode(() =>
        //{
        //    for(var i=0;i< leoPlayerGameStatuses.Length; i++)
        //    {
        //        var entity= commandBuffer.CreateEntity();
        //        commandBuffer.AddComponent<LeoGameStatus>(entity, leoGameStatusesArray[0]);
        //        commandBuffer.AddComponent<SendRpcCommandRequestComponent>(entity,
        //            new SendRpcCommandRequestComponent
        //            {
        //                TargetConnection= leoPlayerConnectReveive[i].SourceConnection
        //            });
        //    }

        //}).Schedule();


    }
}


#region 服务端同时处理发送和接收更新的代码(已注释)
/* // 服务端处理和发送游戏更新状态
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

             // 有与ECS数据存储的格式，同组件的数据存在一起，我们需要利用这个规则，面向对象的方式处理不可取
                EntityManager.AddBuffer<UnReadyPlayerCollection>(entity); // 初始化未准备的玩家列表
                EntityManager.AddBuffer<ReadyPlayerCollection>(entity); // 初始化已准备玩家列表
                EntityManager.AddBuffer<PlayingPlayerCollection>(entity); // 初始化正在游戏中的玩家列表
                EntityManager.AddBuffer<PausedPlayerCollection>(entity); // 初始化已暂停的玩家列表
                EntityManager.AddBuffer<BreakPlayerCollection>(entity); // 初始化连接中断的玩家列表
        


    }

    protected override void OnUpdate()
    {
        EntityManager entityManager = EntityManager;

        EntityCommandBuffer commandBuffer
            = CommandBufferSystem.CreateCommandBuffer();

             // 服务端上有多个对于客户端的连接，所以不能确定是那个客户端，一定要在 ReceiveRpcCommandRequestComponent 组件中寻找
                int ConnectNum = 0;
                if (HasSingleton<NetworkIdComponent>())
                {
                    ConnectNum = GetSingleton<NetworkIdComponent>().Value;
                }
        

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

                //EntityManager.AddSharedComponentData<ThePlayerGameStatus>(entity,
                     new ThePlayerGameStatus 
                     { 
                         playerGameStatus = leoPlayerGameStatus.playerGameStatus ,
                         PlayId= ConnectNum,
                     });


                commandBuffer.DestroyEntity(reqEnt);
                //entityManager.DestroyEntity(reqEnt);
            }).Run();

        // handle1.Complete();

    }
}
*/
#endregion