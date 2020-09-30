using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 游戏准备阶段，分别初始化服务端和客户端的端口监听和套接层
/// </summary>
[UpdateInWorld(UpdateInWorld.TargetWorld.Default)]
public class GameInit : SystemBase
{
    // Singleton component to trigger connections once from a control system
    struct InitGameComponent : IComponentData
    {
    }

    struct InitServerListen : IComponentData { }

    protected override void OnCreate()
    {
        RequireSingletonForUpdate<InitGameComponent>(); // 定义了 Update 需要有 InitGameComponent 这个组件才能运行
        // Create singleton, require singleton for update so system runs once
        EntityManager.CreateEntity(typeof(InitGameComponent));
        //EntityManager.CreateEntity(typeof(InitServerListen));
    }


    private string GetServerIp()
    {
        var url = "";
        // 定义获取服务端IP地址的链接。
        #region UNITY_CLIENT
        url = "http://gameapi.betyfalsh.com:6162/Default/GetGameServerIp";
        #endregion

        #region UNITY_EDITOR
        url = "http://gameapi.betyfalsh.com:6162/Default/GetGameServerIp";
        // url = "http://192.168.0.101:6162/Default/GetGameServerIp";
        #endregion


        List<IMultipartFormSection> formData = new List<IMultipartFormSection>();
        UnityWebRequest www = UnityWebRequest.Post(url, formData);
        www.SendWebRequest();
        
        
        if (www.isNetworkError || www.isHttpError)
        {
            return null;
        }
        else
        {
            while (!www.isDone)
            {
                Thread.Sleep(200);
            }
            //Debug.Log("Form upload complete!");
            var result = JsonUtility.FromJson<HttpResult<string>>(www.downloadHandler.text);
            if (result != null && result.success)
            {
                return result.data;
                /*var ip = result.data;
                NetworkEndPoint ep = new NetworkEndPoint();
                NetworkEndPoint.TryParse(ip, 7979, out ep);
                network.Connect(ep); // 网络连接*/
            }
            else
            {
                return null;
                //if (GetEntityQuery(new ComponentType[] { typeof(InitGameComponent) }).CalculateChunkCount() == 0)
                //    EntityManager.CreateEntity(typeof(InitGameComponent));
            }
        }

        
        /*if (www.isNetworkError || www.isHttpError)
        {
            if (GetEntityQuery(new ComponentType[] { typeof(InitGameComponent) }).CalculateChunkCount() == 0)
                EntityManager.CreateEntity(typeof(InitGameComponent));
        }
        else
        {
            //Debug.Log("Form upload complete!");
            var result = JsonUtility.FromJson<HttpResult<string>>(www.downloadHandler.text);
            if (result != null && result.success)
            {
                var ip = result.data;
                NetworkEndPoint ep = new NetworkEndPoint();
                NetworkEndPoint.TryParse(ip, 7979, out ep);
                network.Connect(ep); // 网络连接
            }
            else
            {
                if (GetEntityQuery(new ComponentType[] { typeof(InitGameComponent) }).CalculateChunkCount() == 0)
                    EntityManager.CreateEntity(typeof(InitGameComponent));
            }
        }*/ 
    }


    protected override void OnUpdate()
    {
        // Destroy singleton to prevent system from running again
        EntityManager.DestroyEntity(GetSingletonEntity<InitGameComponent>()); // OnCreate 定义了需要有 InitGameComponent 这个组件才能运行，这里删除后就不会再运行
        foreach (var world in World.All)
        {
            var network = world.GetExistingSystem<NetworkStreamReceiveSystem>();
            if (world.GetExistingSystem<ClientSimulationSystemGroup>() != null)
            {
                //to chose an IP instead of just always connecting to localhost uncomment the next line and delete the other two lines of code
                //NetworkEndPoint.TryParse("IP address here", 7979, out NetworkEndPoint ep);
                //NetworkEndPoint ep = NetworkEndPoint.LoopbackIpv4;
                //ep.Port = 7979;
                #region UNITY_CLIENT || UNITY_EDITOR
                var ip = GetServerIp();
                if (String.IsNullOrEmpty(ip))
                {
                    Debug.LogError("获取服务端IP地址失败！");
                }

                NetworkEndPoint ep = new NetworkEndPoint();
                NetworkEndPoint.TryParse(ip, 7979, out ep);
                network.Connect(ep); // 网络连接
                #endregion
                // network.Connect(ep);
            }
            else if (world.GetExistingSystem<ServerSimulationSystemGroup>() != null)
            {
                // Server world automatically listens for connections from any host
                NetworkEndPoint ep = NetworkEndPoint.AnyIpv4;
                ep.Port = 7979;
                network.Listen(ep);

                // network.Listen(ep);
                //if(!network.Driver.IsCreated)
                //    network.Listen(ep);

                //if(network.Driver.Bind(ep)!=0)
                //    network.Listen(ep);

                //if (GetEntityQuery(new ComponentType[] { typeof(InitServerListen) }).CalculateChunkCount() == 0)
                //{
                //    NetworkEndPoint ep = NetworkEndPoint.AnyIpv4;
                //    ep.Port = 7979;
                //    network.Listen(ep);
                //    EntityManager.DestroyEntity(GetSingletonEntity<InitServerListen>());
                //}

            }
        }
    }


}

[Serializable]
public class HttpResult<T>
{
    public string controlFuncDefine;

    public Guid opreator;

    public bool success;

    public int dataCount;

    public T data;

    public int pageSize;

    public int pageIndex;

    public string displayMessage;

    public string redirectUrl;
}