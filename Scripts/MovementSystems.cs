using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Collections;

public class SteeringSystems : ComponentSystemGroup { }

[UpdateBefore(typeof(WanderSteeringSystem))]
public class WanderAngleUpdateSystem : JobComponentSystem
{
    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var randomArray = World.GetExistingSystem<RandomSystem>().RandomArray;

        JobHandle horizontal = Entities.WithNativeDisableParallelForRestriction(randomArray).ForEach((int nativeThreadIndex, ref WanderHorizontalAngle _angle, in WanderHorizontalMaxDelta _maxDelta) =>
        {
            var random = randomArray[nativeThreadIndex];

            float delta = random.NextFloat(-1.0f, 1.0f) * _maxDelta.Value * math.PI * 2;

            _angle.Value += delta;

            randomArray[nativeThreadIndex] = random;

        }).Schedule(inputDeps);

        horizontal.Complete();

        JobHandle vertical = Entities.WithNativeDisableParallelForRestriction(randomArray).ForEach((int nativeThreadIndex, ref WanderVerticalAngle _angle, in WanderVerticalMaxDelta _maxDelta) =>
        {
            var random = randomArray[nativeThreadIndex];

            float delta = random.NextFloat(-1, 1) * _maxDelta.Value * math.PI * 2;

            _angle.Value += delta;

            randomArray[nativeThreadIndex] = random;

        }).Schedule(horizontal);

        vertical.Complete();

        return vertical;
    }
}

[UpdateInGroup(typeof(SteeringSystems))]
public class WanderSteeringSystem : JobComponentSystem
{
    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        float dt = Time.DeltaTime;
        JobHandle horizontal = Entities.ForEach((ref MovementVelocity _velocity,
            in Translation _position,
            in WanderHorizontalAngle _angle,
            in WanderSphereDistance _distance,
            in WanderSphereRadius _radius) =>
        {
            float3 prevDir = math.normalize(_velocity.Value);

            float3 spherePos = _position.Value + prevDir * (_distance.Value + _radius.Value);
            
            float3 onSphere = spherePos + new float3 { x = math.cos(_angle.Value), y = 0, z = math.sin(_angle.Value) } * _radius.Value;

            float3 v = _velocity.Value;
            v += (onSphere - _position.Value) * dt;

            _velocity.Value = v;

        }).Schedule(inputDeps);
        
        JobHandle vertical = Entities.ForEach((ref MovementVelocity _velocity, 
            in Translation _position,
            in WanderVerticalAngle _angle,
            in WanderSphereDistance _distance, 
            in WanderSphereRadius _radius) =>
        {
            float3 prevDir = math.normalize(_velocity.Value);

            float3 spherePos = _position.Value + prevDir * (_distance.Value + _radius.Value);

            float3 onSphere = spherePos + new float3 { x = 0, y = math.sin(_angle.Value), z = 0  } * _radius.Value;

            float3 v = _velocity.Value;
            v += (onSphere - _position.Value) * dt;
            
            _velocity.Value = v;

        }).Schedule(horizontal);

        return vertical;
    }
}

[UpdateBefore(typeof(MovementSystem)), UpdateAfter(typeof(SteeringSystems))]
public class MaintainSpeedSystem : JobComponentSystem
{
    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        JobHandle job = Entities.ForEach((ref MovementVelocity _velocity, in ActiveMoveSpeed _speed) =>
        {
            float currentSpeed = math.length(_velocity.Value);

            if (currentSpeed - 0.05 < _speed.Value || currentSpeed + 0.05 > _speed.Value)
            {
                _velocity.Value += math.normalize(_velocity.Value) * (_speed.Value - currentSpeed);
            }

        }).Schedule(inputDeps);

        return job;
    }
}

public class OutOfBoundsSteeringSystem : JobComponentSystem
{
    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        float dt = Time.DeltaTime;

        JobHandle job = Entities.ForEach((ref MovementVelocity _velocity, in Translation _pos) =>
        {
            float3 v = _velocity.Value;

            if (_pos.Value.y > -0.3)
            {
                v.y -= ((_pos.Value.y * _pos.Value.y) + 50) * dt;
            }
            else if (_pos.Value.y < -20)
            {
                v.y -= _pos.Value.y * dt;
            }
            else
            {
                if (math.abs(v.y) > 3)
                {
                    v.y -= math.sign(v.y) * (3 - v.y) * dt;
                }
            }

            _velocity.Value = v;

        }).Schedule(inputDeps);

        return job;
    }
}

[UpdateAfter(typeof(SteeringSystems))]
public class MovementSystem : JobComponentSystem
{
    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        float dt = Time.DeltaTime;

        JobHandle job = Entities.ForEach((ref Translation _position, in MovementVelocity _velocity) =>
        {
            _position.Value += _velocity.Value * dt;

        }).Schedule(inputDeps);

        return job;
    }
}


//public class ProximitySystem : JobComponentSystem
//{
//    protected override JobHandle OnUpdate(JobHandle inputDeps)
//    {
//        float dt = Time.DeltaTime;

//        JobHandle job = Entities.ForEach((ref MovementVelocity _vel, in Translation _pos) =>
//        {
//            float3 diff = -_pos.Value;
//            diff.y = 0;

//            float mag = math.length(diff);

//            if (mag > 150)
//            {
//                diff = math.normalize(diff);

//                _vel.Value += diff * ((mag * mag) - 150) * dt;

//            }

//        }).Schedule(inputDeps);

//        return job;
//    }
//}

[UpdateInGroup(typeof(SteeringSystems))]
public class FlockingSystem : ComponentSystemGroup { }

[UpdateInGroup(typeof(FlockingSystem))]
public class CohesionSystem : ComponentSystem
{
    EntityQuery fish;

    protected override void OnCreate()
    {
        fish = GetEntityQuery(typeof(MovementVelocity),
            ComponentType.ReadOnly<Translation>(),
            ComponentType.ReadOnly<FlockingViewAngle>(),
            ComponentType.ReadOnly<FlockingViewDistance>());
    }

    struct CohesionJob : IJobChunk
    {
        public float deltaTime;

        [ReadOnly] public ArchetypeChunkComponentType<Translation> transType;
        [ReadOnly] public ArchetypeChunkComponentType<FlockingViewAngle> angleType;
        [ReadOnly] public ArchetypeChunkComponentType<FlockingViewDistance> distanceType;

        public ArchetypeChunkComponentType<MovementVelocity> velocityType;

        [DeallocateOnJobCompletion, ReadOnly] public NativeArray<Translation> otherTranslations;

        public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
        {
            var positions = chunk.GetNativeArray(transType);
            var velocities = chunk.GetNativeArray(velocityType);
            var viewAngles = chunk.GetNativeArray(angleType);
            var viewDistances = chunk.GetNativeArray(distanceType);

            for (int i = 0; i < chunk.Count; i++)
            {
                Translation pos = positions[i];
                MovementVelocity v = velocities[i];
                FlockingViewDistance viewDist = viewDistances[i];
                FlockingViewAngle viewAngle = viewAngles[i];

                float3 avgPos = float3.zero;
                int numTargets = 0;

                for (int j = 0; j < otherTranslations.Length; j++)
                {
                    Translation other = otherTranslations[j];

                    float3 diff = other.Value - pos.Value;

                    if (math.length(diff) > viewDist.Value)
                    {
                        continue;   // out of range don't check view angle
                    }

                    float dot = math.dot(diff, v.Value);
                    float cos = dot / (math.length(diff) * math.length(v.Value));

                    float ang = math.acos(cos);

                    if (math.abs(ang) <= viewAngle.Value / 2)
                    {
                        avgPos += other.Value;
                        numTargets++;
                    }
                }

                if (numTargets > 0) // only do something if there is something to be done
                {
                    avgPos /= numTargets;

                    float3 diff = avgPos - pos.Value;

                    v.Value += diff * deltaTime;

                    velocities[i] = v;
                }
            }
        }
    }

    protected override void OnUpdate()
    {
        var job = new CohesionJob
        {
            angleType = GetArchetypeChunkComponentType<FlockingViewAngle>(true),
            distanceType = GetArchetypeChunkComponentType<FlockingViewDistance>(true),
            transType = GetArchetypeChunkComponentType<Translation>(true),
            velocityType = GetArchetypeChunkComponentType<MovementVelocity>(false),

            otherTranslations = fish.ToComponentDataArray<Translation>(Allocator.TempJob),

            deltaTime = Time.DeltaTime
        };

        job.Run(fish);
    }
}

[UpdateInGroup(typeof(FlockingSystem))]
public class AlignSystem : ComponentSystem
{
    EntityQuery fish;

    protected override void OnCreate()
    {
        fish = GetEntityQuery(typeof(MovementVelocity),
            ComponentType.ReadOnly<Translation>(),
            ComponentType.ReadOnly<FlockingViewAngle>(),
            ComponentType.ReadOnly<FlockingViewDistance>());
    }

    struct AlignJob : IJobChunk
    {
        public float deltaTime;

        [ReadOnly] public ArchetypeChunkComponentType<Translation> transType;
        [ReadOnly] public ArchetypeChunkComponentType<FlockingViewAngle> angleType;
        [ReadOnly] public ArchetypeChunkComponentType<FlockingViewDistance> distanceType;

        public ArchetypeChunkComponentType<MovementVelocity> velocityType;

        [DeallocateOnJobCompletion, ReadOnly] public NativeArray<Translation> otherTranslations;
        [DeallocateOnJobCompletion, ReadOnly] public NativeArray<MovementVelocity> otherVelocities;

        public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
        {
            var positions = chunk.GetNativeArray(transType);
            var velocities = chunk.GetNativeArray(velocityType);
            var viewAngles = chunk.GetNativeArray(angleType);
            var viewDistances = chunk.GetNativeArray(distanceType);

            for (int i = 0; i < chunk.Count; i++)
            {
                Translation pos = positions[i];
                MovementVelocity v = velocities[i];
                FlockingViewDistance viewDist = viewDistances[i];
                FlockingViewAngle viewAngle = viewAngles[i];

                float3 avgVelocity = float3.zero;
                int numTargets = 0;

                for (int j = 0; j < otherTranslations.Length; j++)
                {
                    Translation other = otherTranslations[j];

                    float3 diff = other.Value - pos.Value;

                    if (math.length(diff) > viewDist.Value)
                    {
                        continue;   // out of range don't check view angle
                    }

                    float dot = math.dot(diff, v.Value);
                    float cos = dot / (math.length(diff) * math.length(v.Value));

                    float ang = math.acos(cos);

                    if (math.abs(ang) <= viewAngle.Value / 2)
                    {
                        avgVelocity += otherVelocities[j].Value;
                        numTargets++;
                    }
                }

                if (numTargets > 0) // only do something if there is something to be done
                {
                    avgVelocity /= numTargets;

                    float3 diff = avgVelocity - v.Value;

                    v.Value += diff * deltaTime;

                    velocities[i] = v;
                }
            }
        }
    }

    protected override void OnUpdate()
    {
        var job = new AlignJob
        {
            angleType = GetArchetypeChunkComponentType<FlockingViewAngle>(true),
            distanceType = GetArchetypeChunkComponentType<FlockingViewDistance>(true),
            transType = GetArchetypeChunkComponentType<Translation>(true),
            velocityType = GetArchetypeChunkComponentType<MovementVelocity>(false),

            otherTranslations = fish.ToComponentDataArray<Translation>(Allocator.TempJob),
            otherVelocities = fish.ToComponentDataArray<MovementVelocity>(Allocator.TempJob),

            deltaTime = Time.DeltaTime
        };

        job.Run(fish);
    }
}

public class SeparationSystem : ComponentSystem
{
    EntityQuery fish;

    protected override void OnCreate()
    {
        fish = GetEntityQuery(typeof(MovementVelocity),
            ComponentType.ReadOnly<Translation>(),
            ComponentType.ReadOnly<FlockingViewAngle>(),
            ComponentType.ReadOnly<FlockingViewDistance>(),
            ComponentType.ReadOnly<FlockingMinDistance>());
    }

    struct SeparateJob : IJobChunk
    {
        public float deltaTime;

        [ReadOnly] public ArchetypeChunkComponentType<Translation> transType;
        [ReadOnly] public ArchetypeChunkComponentType<FlockingViewAngle> angleType;
        [ReadOnly] public ArchetypeChunkComponentType<FlockingMinDistance> minDistType;

        public ArchetypeChunkComponentType<MovementVelocity> velocityType;

        [DeallocateOnJobCompletion, ReadOnly] public NativeArray<Translation> otherTranslations;

        public void Execute(ArchetypeChunk chunk, int chunkIndex, int firstEntityIndex)
        {
            var positions = chunk.GetNativeArray(transType);
            var velocities = chunk.GetNativeArray(velocityType);
            var viewAngles = chunk.GetNativeArray(angleType);
            var minDistances = chunk.GetNativeArray(minDistType);
            
            for (int i = 0; i < chunk.Count; i++)
            {
                Translation pos = positions[i];
                MovementVelocity v = velocities[i];
                FlockingViewAngle viewAngle = viewAngles[i];

                for (int j = 0; j < otherTranslations.Length; j++)
                {
                    Translation other = otherTranslations[j];

                    float3 diff = other.Value - pos.Value;

                    float dist = math.length(diff);

                    if (dist  < minDistances[i].Value)
                    {
                        continue;   // too far away to care
                    }

                    float dot = math.dot(diff, v.Value);
                    float cos = dot / (math.length(diff) * math.length(v.Value));

                    float ang = math.acos(cos);

                    if (math.abs(ang) <= viewAngle.Value / 2)
                    {
                        v.Value -= diff / (dist * dist + 0.01f) * deltaTime;
                        velocities[i] = v;
                    }
                }
            }
        }
    }

    protected override void OnUpdate()
    {
        var job = new SeparateJob()
        {
            angleType = GetArchetypeChunkComponentType<FlockingViewAngle>(true),
            transType = GetArchetypeChunkComponentType<Translation>(true),
            minDistType = GetArchetypeChunkComponentType<FlockingMinDistance>(true),
            velocityType = GetArchetypeChunkComponentType<MovementVelocity>(false),

            otherTranslations = fish.ToComponentDataArray<Translation>(Allocator.TempJob),

            deltaTime = Time.DeltaTime
        };

        job.Run(fish);
    }
}