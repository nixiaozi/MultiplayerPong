using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;


[BurstCompile]
public struct LeoGameStatus : IRpcCommand
{
    public TheGameStatus theGameStatus;

    public void Deserialize(ref DataStreamReader reader)
    {
        theGameStatus = (TheGameStatus)reader.ReadInt();
    }

    public void Serialize(ref DataStreamWriter writer)
    {
        writer.WriteInt((int)theGameStatus);
    }
    [BurstCompile]
    private static void InvokeExecute(ref RpcExecutor.Parameters parameters)
    {
        RpcExecutor.ExecuteCreateRequestComponent<LeoGameStatus>(ref parameters); 
    }

    static PortableFunctionPointer<RpcExecutor.ExecuteDelegate> InvokeExecuteFunctionPointer =
        new PortableFunctionPointer<RpcExecutor.ExecuteDelegate>(InvokeExecute);

    public PortableFunctionPointer<RpcExecutor.ExecuteDelegate> CompileExecute()
    {
        return InvokeExecuteFunctionPointer;
    }
}

public class LeoGameStatusRequestSystem : RpcCommandRequestSystem<LeoGameStatus>
{
}


// ArgumentException: Component LeoPlayerGameStatus can only implement one of IComponentData, ISharedComponentData and IBufferElementData
[BurstCompile]
public struct LeoPlayerGameStatus : IRpcCommand // ISharedComponentData, IRpcCommand // 这里限定为共享组件可能会有问题 确实有问题
{
    public PlayerGameStatus playerGameStatus;
    public int playerId;

    public void Deserialize(ref DataStreamReader reader)
    {
        playerGameStatus = (PlayerGameStatus)reader.ReadInt();
        playerId = reader.ReadInt();
    }

    public void Serialize(ref DataStreamWriter writer)
    {
        writer.WriteInt((int)playerGameStatus);
        writer.WriteInt(playerId);
    }
    [BurstCompile]
    private static void InvokeExecute(ref RpcExecutor.Parameters parameters)
    {
        RpcExecutor.ExecuteCreateRequestComponent<LeoPlayerGameStatus>(ref parameters);
    }

    static PortableFunctionPointer<RpcExecutor.ExecuteDelegate> InvokeExecuteFunctionPointer =
        new PortableFunctionPointer<RpcExecutor.ExecuteDelegate>(InvokeExecute);

    public PortableFunctionPointer<RpcExecutor.ExecuteDelegate> CompileExecute()
    {
        return InvokeExecuteFunctionPointer;
    }

}

public class LeoPlayerGameStatusRequestSystem : RpcCommandRequestSystem<LeoPlayerGameStatus>
{
}



/* // 注意ECS数据存储的特性以及它的优点进行判断如何编码

/// <summary>
/// 未准备的玩家连接号
/// </summary>
public struct UnReadyPlayerCollection : IBufferElementData
{
    // Actual value each buffer element will store.
    public int Value;

    // The following implicit conversions are optional, but can be convenient.
    public static implicit operator int(UnReadyPlayerCollection e)
    {
        return e.Value;
    }

    public static implicit operator UnReadyPlayerCollection(int e)
    {
        return new UnReadyPlayerCollection { Value = e };
    }
}

/// <summary>
/// 已准备好的玩家列表
/// </summary>
public struct ReadyPlayerCollection : IBufferElementData
{
    // Actual value each buffer element will store.
    public int Value;

    // The following implicit conversions are optional, but can be convenient.
    public static implicit operator int(ReadyPlayerCollection e)
    {
        return e.Value;
    }

    public static implicit operator ReadyPlayerCollection(int e)
    {
        return new ReadyPlayerCollection { Value = e };
    }
}

/// <summary>
/// 正在游戏中的玩家列表
/// </summary>
public struct PlayingPlayerCollection : IBufferElementData
{
    // Actual value each buffer element will store.
    public int Value;

    // The following implicit conversions are optional, but can be convenient.
    public static implicit operator int(PlayingPlayerCollection e)
    {
        return e.Value;
    }

    public static implicit operator PlayingPlayerCollection(int e)
    {
        return new PlayingPlayerCollection { Value = e };
    }
}

/// <summary>
/// 暂停中的玩家列表
/// </summary>
public struct PausedPlayerCollection : IBufferElementData
{
    // Actual value each buffer element will store.
    public int Value;

    // The following implicit conversions are optional, but can be convenient.
    public static implicit operator int(PausedPlayerCollection e)
    {
        return e.Value;
    }

    public static implicit operator PausedPlayerCollection(int e)
    {
        return new PausedPlayerCollection { Value = e };
    }
}

/// <summary>
/// 连接中断的玩家列表
/// </summary>
public struct BreakPlayerCollection : IBufferElementData
{
    // Actual value each buffer element will store.
    public int Value;

    // The following implicit conversions are optional, but can be convenient.
    public static implicit operator int(BreakPlayerCollection e)
    {
        return e.Value;
    }

    public static implicit operator BreakPlayerCollection(int e)
    {
        return new BreakPlayerCollection { Value = e };
    }
}

*/


/// <summary>
/// 定义多人游戏可能的状态
/// </summary>
public enum TheGameStatus
{
    /// <summary>
    /// 没有人准备
    /// </summary>
    NoReady,
    /// <summary>
    /// 部分人准备好
    /// </summary>
    PartReady,
    /// <summary>
    /// 所有人都准备好
    /// </summary>
    AllReady,
    /// <summary>
    /// 游戏中
    /// </summary>
    Playing,
    /// <summary>
    /// 准备暂停
    /// </summary>
    PrePause,
    /// <summary>
    /// 游戏暂停
    /// </summary>
    Paused,
    /// <summary>
    /// 游戏结束
    /// </summary>
    Over,
    /// <summary>
    /// 连接中断
    /// </summary>
    Break,


}

/// <summary>
/// 定义游戏玩家的游戏状态
/// </summary>
public enum PlayerGameStatus
{
    /// <summary>
    /// 未准备
    /// </summary>
    NotReady,
    /// <summary>
    /// 已准备
    /// </summary>
    Ready,
    /// <summary>
    /// 游戏中
    /// </summary>
    Playing,
    /// <summary>
    /// 暂停中
    /// </summary>
    Pause,
    /// <summary>
    /// 连接中断
    /// </summary>
    Break,
}


//public struct ThePlayerGameStatus : ISharedComponentData
//{
//    public PlayerGameStatus playerGameStatusNum;

//    // public int PlayId;
//}