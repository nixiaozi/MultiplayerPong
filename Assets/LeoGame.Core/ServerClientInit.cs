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
        var clientWorld =ClientServerBootstrap.CreateClientWorld(World.DefaultGameObjectInjectionWorld, "MyClientWorld");
        // clientWorld.EntityManager.CreateEntity(typeof(LeoGameStatus), typeof(LeoPlayerGameStatus)); // 创建一个全局可访问的实体
#endif

#if UNITY_SERVER
        ClientServerBootstrap.CreateServerWorld(World.DefaultGameObjectInjectionWorld, "MyServerWorld");

#endif

#if UNITY_EDITOR

        var theServerWorld = ClientServerBootstrap.CreateServerWorld(World.DefaultGameObjectInjectionWorld, "MyServerWorld");

        //需要生成对应数量的 client
        int numClientWorlds = ClientServerBootstrap.RequestedNumClients; // 客户端
        int totalNumClients = numClientWorlds;

        int numThinClients = ClientServerBootstrap.RequestedNumThinClients; // 轻量级客户端
        totalNumClients += numThinClients;

        for (int i = 0; i < numClientWorlds; ++i)
        {
            var clientWorld = ClientServerBootstrap.CreateClientWorld(World.DefaultGameObjectInjectionWorld, "MyClientWorld" + i);
            // clientWorld.EntityManager.CreateEntity(typeof(LeoGameStatus), typeof(LeoPlayerGameStatus)); // 创建一个全局可访问的实体
        }

        for (int i = numClientWorlds; i < totalNumClients; ++i)
        {
            var clientWorld = ClientServerBootstrap.CreateClientWorld(World.DefaultGameObjectInjectionWorld, "MyClientWorld" + i);
            clientWorld.EntityManager.CreateEntity(typeof(ThinClientComponent));
            // clientWorld.EntityManager.CreateEntity(typeof(LeoGameStatus), typeof(LeoPlayerGameStatus)); // 创建一个全局可访问的实体
        }
#endif

    }

}