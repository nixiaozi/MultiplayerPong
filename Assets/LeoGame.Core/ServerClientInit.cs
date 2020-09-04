using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

public class ServerClientInit : MonoBehaviour
{


    private void Start()
    {
        Debug.Log("To Start!");
    }

    private void Awake()
    {
        Debug.Log("To Awake!");
#if UNITY_CLIENT
        ClientServerBootstrap.CreateClientWorld(World.DefaultGameObjectInjectionWorld, "MyClientWorld");
#endif

#if UNITY_SERVER
        ClientServerBootstrap.CreateServerWorld(World.DefaultGameObjectInjectionWorld, "MyServerWorld");

#endif

#if UNITY_EDITOR
        ClientServerBootstrap.CreateServerWorld(World.DefaultGameObjectInjectionWorld, "MyServerWorld");

        //需要生成对应数量的 client
        int numClientWorlds = ClientServerBootstrap.RequestedNumClients; // 客户端
        int totalNumClients = numClientWorlds;

        int numThinClients = ClientServerBootstrap.RequestedNumThinClients; // 轻量级客户端
        totalNumClients += numThinClients;

        for (int i = 0; i < numClientWorlds; ++i)
        {
            ClientServerBootstrap.CreateClientWorld(World.DefaultGameObjectInjectionWorld, "MyClientWorld" + i);
        }

        for (int i = numClientWorlds; i < totalNumClients; ++i)
        {
            var clientWorld = ClientServerBootstrap.CreateClientWorld(World.DefaultGameObjectInjectionWorld, "MyClientWorld" + i);
            clientWorld.EntityManager.CreateEntity(typeof(ThinClientComponent));
        }
#endif

    }

}