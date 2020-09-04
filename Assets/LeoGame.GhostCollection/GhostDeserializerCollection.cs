using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Networking.Transport;
using Unity.NetCode;

public struct MultiplayerPongGhostDeserializerCollection : IGhostDeserializerCollection
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public string[] CreateSerializerNameList()
    {
        var arr = new string[]
        {
            "PaddleTheSideGhostSerializer",
        };
        return arr;
    }

    public int Length => 1;
#endif
    public void Initialize(World world)
    {
        var curPaddleTheSideGhostSpawnSystem = world.GetOrCreateSystem<PaddleTheSideGhostSpawnSystem>();
        m_PaddleTheSideSnapshotDataNewGhostIds = curPaddleTheSideGhostSpawnSystem.NewGhostIds;
        m_PaddleTheSideSnapshotDataNewGhosts = curPaddleTheSideGhostSpawnSystem.NewGhosts;
        curPaddleTheSideGhostSpawnSystem.GhostType = 0;
    }

    public void BeginDeserialize(JobComponentSystem system)
    {
        m_PaddleTheSideSnapshotDataFromEntity = system.GetBufferFromEntity<PaddleTheSideSnapshotData>();
    }
    public bool Deserialize(int serializer, Entity entity, uint snapshot, uint baseline, uint baseline2, uint baseline3,
        ref DataStreamReader reader, NetworkCompressionModel compressionModel)
    {
        switch (serializer)
        {
            case 0:
                return GhostReceiveSystem<MultiplayerPongGhostDeserializerCollection>.InvokeDeserialize(m_PaddleTheSideSnapshotDataFromEntity, entity, snapshot, baseline, baseline2,
                baseline3, ref reader, compressionModel);
            default:
                throw new ArgumentException("Invalid serializer type");
        }
    }
    public void Spawn(int serializer, int ghostId, uint snapshot, ref DataStreamReader reader,
        NetworkCompressionModel compressionModel)
    {
        switch (serializer)
        {
            case 0:
                m_PaddleTheSideSnapshotDataNewGhostIds.Add(ghostId);
                m_PaddleTheSideSnapshotDataNewGhosts.Add(GhostReceiveSystem<MultiplayerPongGhostDeserializerCollection>.InvokeSpawn<PaddleTheSideSnapshotData>(snapshot, ref reader, compressionModel));
                break;
            default:
                throw new ArgumentException("Invalid serializer type");
        }
    }

    private BufferFromEntity<PaddleTheSideSnapshotData> m_PaddleTheSideSnapshotDataFromEntity;
    private NativeList<int> m_PaddleTheSideSnapshotDataNewGhostIds;
    private NativeList<PaddleTheSideSnapshotData> m_PaddleTheSideSnapshotDataNewGhosts;
}
public struct EnableMultiplayerPongGhostReceiveSystemComponent : IComponentData
{}
public class MultiplayerPongGhostReceiveSystem : GhostReceiveSystem<MultiplayerPongGhostDeserializerCollection>
{
    protected override void OnCreate()
    {
        base.OnCreate();
        RequireSingletonForUpdate<EnableMultiplayerPongGhostReceiveSystemComponent>();
    }
}
