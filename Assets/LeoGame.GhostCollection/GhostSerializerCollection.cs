using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Networking.Transport;
using Unity.NetCode;

public struct MultiplayerPongGhostSerializerCollection : IGhostSerializerCollection
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public string[] CreateSerializerNameList()
    {
        var arr = new string[]
        {
            "PaddleTheSideGhostSerializer",
            "PaddleOtherSideGhostSerializer",
            "SphereGhostSerializer",
        };
        return arr;
    }

    public int Length => 3;
#endif
    public static int FindGhostType<T>()
        where T : struct, ISnapshotData<T>
    {
        if (typeof(T) == typeof(PaddleTheSideSnapshotData))
            return 0;
        if (typeof(T) == typeof(PaddleOtherSideSnapshotData))
            return 1;
        if (typeof(T) == typeof(SphereSnapshotData))
            return 2;
        return -1;
    }

    public void BeginSerialize(ComponentSystemBase system)
    {
        m_PaddleTheSideGhostSerializer.BeginSerialize(system);
        m_PaddleOtherSideGhostSerializer.BeginSerialize(system);
        m_SphereGhostSerializer.BeginSerialize(system);
    }

    public int CalculateImportance(int serializer, ArchetypeChunk chunk)
    {
        switch (serializer)
        {
            case 0:
                return m_PaddleTheSideGhostSerializer.CalculateImportance(chunk);
            case 1:
                return m_PaddleOtherSideGhostSerializer.CalculateImportance(chunk);
            case 2:
                return m_SphereGhostSerializer.CalculateImportance(chunk);
        }

        throw new ArgumentException("Invalid serializer type");
    }

    public int GetSnapshotSize(int serializer)
    {
        switch (serializer)
        {
            case 0:
                return m_PaddleTheSideGhostSerializer.SnapshotSize;
            case 1:
                return m_PaddleOtherSideGhostSerializer.SnapshotSize;
            case 2:
                return m_SphereGhostSerializer.SnapshotSize;
        }

        throw new ArgumentException("Invalid serializer type");
    }

    public int Serialize(ref DataStreamWriter dataStream, SerializeData data)
    {
        switch (data.ghostType)
        {
            case 0:
            {
                return GhostSendSystem<MultiplayerPongGhostSerializerCollection>.InvokeSerialize<PaddleTheSideGhostSerializer, PaddleTheSideSnapshotData>(m_PaddleTheSideGhostSerializer, ref dataStream, data);
            }
            case 1:
            {
                return GhostSendSystem<MultiplayerPongGhostSerializerCollection>.InvokeSerialize<PaddleOtherSideGhostSerializer, PaddleOtherSideSnapshotData>(m_PaddleOtherSideGhostSerializer, ref dataStream, data);
            }
            case 2:
            {
                return GhostSendSystem<MultiplayerPongGhostSerializerCollection>.InvokeSerialize<SphereGhostSerializer, SphereSnapshotData>(m_SphereGhostSerializer, ref dataStream, data);
            }
            default:
                throw new ArgumentException("Invalid serializer type");
        }
    }
    private PaddleTheSideGhostSerializer m_PaddleTheSideGhostSerializer;
    private PaddleOtherSideGhostSerializer m_PaddleOtherSideGhostSerializer;
    private SphereGhostSerializer m_SphereGhostSerializer;
}

public struct EnableMultiplayerPongGhostSendSystemComponent : IComponentData
{}
public class MultiplayerPongGhostSendSystem : GhostSendSystem<MultiplayerPongGhostSerializerCollection>
{
    protected override void OnCreate()
    {
        base.OnCreate();
        RequireSingletonForUpdate<EnableMultiplayerPongGhostSendSystemComponent>();
    }

    public override bool IsEnabled()
    {
        return HasSingleton<EnableMultiplayerPongGhostSendSystemComponent>();
    }
}
