
using Unity.Entities;
using Unity.NetCode;

// [DisableAutoCreation]  这个需要自动运行，直接在开始就把游戏状态的控制加入
[AlwaysUpdateSystem]
[UpdateInGroup(typeof(ServerInitializationSystemGroup))]
public class ServerGameStatusHoldSystem : SystemBase
{
    protected override void OnCreate()
    {
        // 创建用于存储游戏状态的实体 并进行数据初始化
        var entity=EntityManager.CreateEntity(typeof(LeoGameStatus));

        EntityManager.SetComponentData(entity, new LeoGameStatus
        {
            TheGameStatus = TheGameStatus.NoReady
        });


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
        



    }
}

