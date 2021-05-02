using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Collections;

public class RandomSystem : ComponentSystem
{
    public NativeArray<Unity.Mathematics.Random> RandomArray { get; private set; }

    protected override void OnCreate()
    {
        var randomArray = new Unity.Mathematics.Random[JobsUtility.MaxJobThreadCount];
        var seed = new System.Random();

        for (int i = 0; i < JobsUtility.MaxJobThreadCount; i++)
        {
            randomArray[i] = new Unity.Mathematics.Random((uint)seed.Next());
        }

        RandomArray = new NativeArray<Random>(randomArray, Allocator.Persistent);
    }

    protected override void OnDestroy() => RandomArray.Dispose();

    protected override void OnUpdate() { }
}

[UpdateBefore(typeof(MovementSystem))]
public class LookInMoveDirectionSystem : JobComponentSystem
{
    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        JobHandle handle = Entities.ForEach((ref Rotation _rotation, in MovementVelocity _velocity) =>
        {
            _rotation.Value = quaternion.LookRotation(math.normalize(_velocity.Value), new float3 { x = 0, y = 1, z = 0 });

        }).Schedule(inputDeps);

        return handle;
    }
}

[AlwaysSynchronizeSystem, UpdateBefore(typeof(WanderAngleUpdateSystem))]
public class MoveSpeedFluctuation : JobComponentSystem
{
    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var randomArray = World.GetExistingSystem<RandomSystem>().RandomArray;
        float dt = Time.DeltaTime;

        JobHandle handle = Entities.WithNativeDisableParallelForRestriction(randomArray).ForEach((int nativeThreadIndex,
            ref ActiveMoveSpeed _speed,
            in BaseMoveSpeed _baseSpeed,
            in MoveSpeedMaxDecrease _maxDecrease,
            in MoveSpeedMaxIncrease _maxIncrease) =>
        {
            var random = randomArray[nativeThreadIndex];

            float randFluct = random.NextFloat(-_maxDecrease.Value, _maxIncrease.Value) * dt;

            float speed = math.clamp(_baseSpeed.Value + randFluct, _baseSpeed.Value - _maxDecrease.Value, _baseSpeed.Value + _maxIncrease.Value);

            _speed.Value = speed;

            randomArray[nativeThreadIndex] = random;

        }).Schedule(inputDeps);

        handle.Complete();

        return handle;
    }
}