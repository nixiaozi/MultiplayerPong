using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

[UpdateInGroup(typeof(ServerSimulationSystemGroup))]//make sure this only runs on the server
public class GoInGameServerSystem : SystemBase
{
    protected override void OnUpdate()
    {
        EntityManager entityManager = EntityManager;
        //the with none is to make sure that we don't run this system on rpcs that we are sending, only on ones that we are receiving. 
        Entities
            .WithStructuralChanges()
            .WithNone<SendRpcCommandRequestComponent>().ForEach(
            (Entity reqEnt, // reqEnt 当前迭代的实体
                ref GoInGameRequest req, // req 当前迭代实体的 GoInGameRequest 组件
                ref ReceiveRpcCommandRequestComponent reqSrc
                ) =>
            {
                //we add a network connection to the component on our side
                entityManager.AddComponent<NetworkStreamInGame>(reqSrc.SourceConnection);
                UnityEngine.Debug.Log(System.String.Format("Server setting connection {0} to in game", 
                    EntityManager.GetComponentData<NetworkIdComponent>(reqSrc.SourceConnection).Value));


                // var ghostCollection = GetSingleton<GhostPrefabCollectionComponent>();

                EntityQuery query = GetEntityQuery(typeof(GhostPrefabCollectionComponent));
                NativeArray<GhostPrefabCollectionComponent> GhostPrefabs = query.ToComponentDataArray<GhostPrefabCollectionComponent>(Allocator.Temp);
                var ghostCollection = GhostPrefabs[0];

                var ghostId = MultiplayerPongGhostSerializerCollection.FindGhostType<PaddleTheSideSnapshotData>(); // TheSide

                var prefab = EntityManager.GetBuffer<GhostPrefabBuffer>(ghostCollection.serverPrefabs)[ghostId].Value;

                // spawn in our player 
                var player = entityManager.Instantiate(prefab);

                entityManager.SetComponentData(player,
                    new PaddleMoveableComponent
                    {
                        PlayerId = entityManager.GetComponentData<NetworkIdComponent>(reqSrc.SourceConnection).Value
                    });



                //var ghostCollection = GetSingleton<GhostPrefabCollectionComponent>();
                ////if you get errors in this code make sure you have generated the ghost collection and ghost code using their authoring components and that the names are set correctly when you do so.
                //var ghostId = NetCubeGhostSerializerCollection.FindGhostType<CubeSnapshotData>();
                //var prefab = EntityManager.GetBuffer<GhostPrefabBuffer>(ghostCollection.serverPrefabs)[ghostId].Value;
                ////spawn in our player
                //var player = entityManager.Instantiate(prefab);

                //entityManager.SetComponentData(player, new MovableCubeComponent { PlayerId = entityManager.GetComponentData<NetworkIdComponent>(reqSrc.SourceConnection).Value });
                ////if you are copy pasting this article in order don't worry if you get an error about cube input not existing as we are going to code that next, this just adds the buffer so that we can receive input from the player. If you want to test your code at this point just comment it out
                //entityManager.AddBuffer<CubeInput>(player);

                //entityManager.SetComponentData(reqSrc.SourceConnection, new CommandTargetComponent { targetEntity = player });

                entityManager.DestroyEntity(reqEnt);
                //Debug.Log("Spawned Player");
            }).Run();
    }
}