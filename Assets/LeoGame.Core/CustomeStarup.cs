using Unity.Entities;
using Unity.NetCode;

public class CustomeStarup : ClientServerBootstrap
{
    public override bool Initialize(string defaultWorldName)
    {
        var systems = DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default);
        GenerateSystemLists(systems);

        World world = new World(defaultWorldName);
        World.DefaultGameObjectInjectionWorld = world;

        DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, ExplicitDefaultWorldSystems);

        // 添加自定义的SystemGroup

        

        ScriptBehaviourUpdateOrder.UpdatePlayerLoop(world);
        return true;
    }
}