
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Networking.Transport;
using Unity.Transforms;
using UnityEngine;
using Unity.Physics.Extensions;
using Unity.Physics;

public struct PaddleInput: ICommandData<PaddleInput>
{
    public uint Tick => tick;
    public uint tick;
    public float horizontal;
    public float vertical;

    public void Deserialize(uint tick, ref DataStreamReader reader)
    {
        this.tick = tick;
        horizontal = reader.ReadInt();
        vertical = reader.ReadInt();
    }

    public void Serialize(ref DataStreamWriter writer)
    {
        writer.WriteFloat(horizontal);
        writer.WriteFloat(vertical);
    }

    public void Deserialize(uint tick, ref DataStreamReader reader, PaddleInput baseline,
        NetworkCompressionModel compressionModel)
    {
        Deserialize(tick, ref reader);
    }

    public void Serialize(ref DataStreamWriter writer, PaddleInput baseline, NetworkCompressionModel compressionModel)
    {
        Serialize(ref writer);
    }



}

public class PaddleSendCommandSystem : CommandSendSystem<PaddleInput>
{
}
public class PaddleReceiveCommandSystem : CommandReceiveSystem<PaddleInput>
{
}


[UpdateInGroup(typeof(ClientSimulationSystemGroup))]
public class PaddlePlayerInput : SystemBase
{
    private FixedJoystick joystick;
    protected override void OnCreate()
    {
        RequireSingletonForUpdate<NetworkIdComponent>(); // 创建一个组件对象准备传输数据
        RequireSingletonForUpdate<EnableMultiplayerPongGhostReceiveSystemComponent>(); // 创建一个组件对象准备接收对象

        // 这个是强耦合的方式生成并获取游戏对象
        var gameobject = Resources.Load<Canvas>("Canvas");
        gameobject = GameObject.Instantiate<Canvas>(gameobject);

        joystick = Resources.Load<FixedJoystick>("Fixed Joystick");
        joystick = GameObject.Instantiate<FixedJoystick>(joystick);
        joystick.transform.SetParent(gameobject.transform);

    }

    

    protected override void OnUpdate()
    {
        //if(joystick.Horizontal!=0f && joystick.Vertical != 0f)
        //{
            Debug.Log("Do Joystick!");
            var localInput = GetSingleton<CommandTargetComponent>().targetEntity;
            var entityManager = EntityManager;
            //if the singleton is not set it means that we need to setup our cube
            if (localInput == Entity.Null)
            {
                //find the cube that we control
                var localPlayerId = GetSingleton<NetworkIdComponent>().Value;
                Entities
                    .WithStructuralChanges() // SystemBase 才会有这个扩展
                    .WithNone<PaddleInput>()
                    .ForEach((Entity ent, ref PaddleMoveableComponent cube) =>
                    {
                        if (cube.PlayerId == localPlayerId)
                        {
                        //add our input buffer and set the singleton to our cube
                        UnityEngine.Debug.Log("Added input buffer");
                            entityManager.AddBuffer<PaddleInput>(ent);
                            entityManager.SetComponentData(GetSingletonEntity<CommandTargetComponent>(), new CommandTargetComponent { targetEntity = ent });
                        }
                    }).Run();
                return;
            }
            var input = default(PaddleInput);
            //set our tick so we can roll it back
            input.tick = World.GetExistingSystem<ClientSimulationSystemGroup>().ServerTick;

            if (joystick.Horizontal != 0f)
            {
                input.horizontal = joystick.Horizontal;
            }
            if (joystick.Vertical != 0f)
            {
                input.vertical = joystick.Vertical;
            }
            //add our input to the buffer
            var inputBuffer = EntityManager.GetBuffer<PaddleInput>(localInput);
            inputBuffer.AddCommandData(input);
        //}
    }
}



[AlwaysUpdateSystem]
[UpdateInGroup(typeof(GhostPredictionSystemGroup))]
public class MovePaddleSystem : SystemBase
{
    // public float moveSpeed = 1.0f;

    protected override void OnCreate()
    {
        Debug.Log("MovePaddleSystem Created!");
    }


    protected override void OnUpdate()
    {
        var group = World.GetExistingSystem<GhostPredictionSystemGroup>();
        //get the current tick so that we can get the right input
        var tick = group.PredictingTick;
        var deltaTime = Time.DeltaTime;
        // var theSpeed = new NativeArray<float>(new float[] { moveSpeed }, Allocator.TempJob);
        //var handle1 = 
        Entities
            .ForEach((DynamicBuffer<PaddleInput> inputBuffer,ref PhysicsVelocity pv, 
            ref Translation trans, ref PredictedGhostComponent prediction,ref PhysicsMass mass) =>
        {
            if (!GhostPredictionSystemGroup.ShouldPredict(tick, prediction))
                return;
            PaddleInput input;
            //we get the input at the tick we are predicting
            inputBuffer.GetDataAtTick(tick, out input);
            //trans.Value.x += deltaTime * theSpeed[0] * (1 / input.horizontal);
            //trans.Value.y += deltaTime * theSpeed[0] * (1 / input.horizontal);

            if (trans.Value.x > 0) // 在右边，
            {
                trans.Value.x = input.horizontal > 0 ? trans.Value.x + deltaTime * 2f : trans.Value.x - deltaTime * 2f;
                if (trans.Value.x > 8.5f)
                    trans.Value.x = 8.5f;
                if (trans.Value.x < 0.3f)
                    trans.Value.x = 0.3f;
            }

            if (trans.Value.x < 0) // 在左边
            {
                trans.Value.x = input.horizontal > 0 ? trans.Value.x + deltaTime * 2f : trans.Value.x - deltaTime * 2f;
                if (trans.Value.x > -0.3f)
                    trans.Value.x = -0.3f;
                if (trans.Value.x < -8.5f)
                    trans.Value.x = -8.5f;
            }


            //if (input.horizontal > 0)
            //    trans.Value.x += deltaTime * 2f;
            //if (input.horizontal < 0)
            //    trans.Value.x -= deltaTime * 2f;


            if (input.vertical > 0)
                trans.Value.y += deltaTime * 2f;
            if (input.vertical < 0)
                trans.Value.y -= deltaTime * 2f;

            // 修正y轴的值
            if (trans.Value.y < -3.2f)
                trans.Value.y = -3.2f;

            if (trans.Value.y > 3.2f)
                trans.Value.y = 3.2f;



            /* ComponentExtensions.ApplyImpulse((
                 )
                 PhysicsVelocity*/

                // pv.ApplyLinearImpulse(1, new float3(input.horizontal, input.vertical, 0));

                /*  
                float3 playerInput = new float3(input.horizontal, input.vertical, 0f); // 这个版本的也不行
                trans.Value += playerInput * 2f;
                */
                //if(input.horizontal!=0f)
                //    trans.Value.x += deltaTime * 0.001f * (input.horizontal);
                //if(input.vertical!=0f)
                //    trans.Value.y += deltaTime * 0.001f * (input.vertical);
        }).Run();//.Schedule(Dependency);

        //handle1.Complete();

        //Dependency = theSpeed.Dispose(handle1);


    }

}

