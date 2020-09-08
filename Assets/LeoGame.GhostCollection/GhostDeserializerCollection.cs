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
            "PaddleOtherSideGhostSerializer",
            "SphereGhostSerializer",
        };
        return arr;
    }

    public int Length => 3;
#endif
    public void Initialize(World world)
    {
        var curPaddleTheSideGhostSpawnSystem = world.GetOrCreateSystem<PaddleTheSideGhostSpawnSystem>();
        m_PaddleTheSideSnapshotDataNewGhostIds = curPaddleTheSideGhostSpawnSystem.NewGhostIds;
        m_PaddleTheSideSnapshotDataNewGhosts = curPaddleTheSideGhostSpawnSystem.NewGhosts;
        curPaddleTheSideGhostSpawnSystem.GhostType = 0;
        var curPaddleOtherSideGhostSpawnSystem = world.GetOrCreateSystem<PaddleOtherSideGhostSpawnSystem>();
        m_PaddleOtherSideSnapshotDataNewGhostIds = curPaddleOtherSideGhostSpawnSystem.NewGhostIds;
        m_PaddleOtherSideSnapshotDataNewGhosts = curPaddleOtherSideGhostSpawnSystem.NewGhosts;
        curPaddleOtherSideGhostSpawnSystem.GhostType = 1;
        var curSphereGhostSpawnSystem = world.GetOrCreateSystem<SphereGhostSpawnSystem>();
        m_SphereSnapshotDataNewGhostIds = curSphereGhostSpawnSystem.NewGhostIds;
        m_SphereSnapshotDataNewGhosts = curSphereGhostSpawnSystem.NewGhosts;
        curSphereGhostSpawnSystem.GhostType = 2;
    }

    public void BeginDeserialize(JobComponentSystem system)
    {
        m_PaddleTheSideSnapshotDataFromEntity = system.GetBufferFromEntity<PaddleTheSideSnapshotData>();
        m_PaddleOtherSideSnapshotDataFromEntity = system.GetBufferFromEntity<PaddleOtherSideSnapshotData>();
        m_SphereSnapshotDataFromEntity = system.GetBufferFromEntity<SphereSnapshotData>();
    }
    public bool Deserialize(int serializer, Entity entity, uint snapshot, uint baseline, uint baseline2, uint baseline3,
        ref DataStreamReader reader, NetworkCompressionModel compressionModel)
    {
        switch (serializer)
        {
            case 0:
                return GhostReceiveSystem<MultiplayerPongGhostDeserializerCollection>.InvokeDeserialize(m_PaddleTheSideSnapshotDataFromEntity, entity, snapshot, baseline, baseline2,
                baseline3, ref reader, compressionModel);
            case 1:
                return GhostReceiveSystem<MultiplayerPongGhostDeserializerCollection>.InvokeDeserialize(m_PaddleOtherSideSnapshotDataFromEntity, entity, snapshot, baseline, baseline2,
                baseline3, ref reader, compressionModel);
            case 2:
                return GhostReceiveSystem<MultiplayerPongGhostDeserializerCollection>.InvokeDeserialize(m_SphereSnapshotDataFromEntity, entity, snapshot, baseline, baseline2,
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
            case 1:
                m_PaddleOtherSideSnapshotDataNewGhostIds.Add(ghostId);
                m_PaddleOtherSideSnapshotDataNewGhosts.Add(GhostReceiveSystem<MultiplayerPongGhostDeserializerCollection>.InvokeSpawn<PaddleOtherSideSnapshotData>(snapshot, ref reader, compressionModel));
                break;
            case 2:
                m_SphereSnapshotDataNewGhostIds.Add(ghostId);
                m_SphereSnapshotDataNewGhosts.Add(GhostReceiveSystem<MultiplayerPongGhostDeserializerCollection>.InvokeSpawn<SphereSnapshotData>(snapshot, ref reader, compressionModel));
                break;
            default:
                throw new ArgumentException("Invalid serializer type");
        }
    }

    private BufferFromEntity<PaddleTheSideSnapshotData> m_PaddleTheSideSnapshotDataFromEntity;
    private NativeList<int> m_PaddleTheSideSnapshotDataNewGhostIds;
    private NativeList<PaddleTheSideSnapshotData> m_PaddleTheSideSnapshotDataNewGhosts;
    private BufferFromEntity<PaddleOtherSideSnapshotData> m_PaddleOtherSideSnapshotDataFromEntity;
    private NativeList<int> m_PaddleOtherSideSnapshotDataNewGhostIds;
    private NativeList<PaddleOtherSideSnapshotData> m_PaddleOtherSideSnapshotDataNewGhosts;
    private BufferFromEntity<SphereSnapshotData> m_SphereSnapshotDataFromEntity;
    private NativeList<int> m_SphereSnapshotDataNewGhostIds;
    private NativeList<SphereSnapshotData> m_SphereSnapshotDataNewGhosts;
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
