using Unity.Entities;
using Unity.Mathematics;

public struct ActiveMoveSpeed : IComponentData { public float Value { get; set; } }
public struct BaseMoveSpeed : IComponentData { public float Value { get; set; } }

public struct MoveSpeedMaxDecrease : IComponentData { public float Value { get; set; } }
public struct MoveSpeedMaxIncrease : IComponentData { public float Value { get; set; } }

public struct MovementVelocity : IComponentData { public float3 Value { get; set; } }

public struct WanderHorizontalAngle : IComponentData { public float Value { get; set; } }
public struct WanderHorizontalMaxDelta : IComponentData { public float Value { get; set; } }
public struct WanderVerticalAngle : IComponentData { public float Value { get; set; } }
public struct WanderVerticalMaxDelta : IComponentData { public float Value { get; set; } }
public struct WanderSphereDistance : IComponentData { public float Value { get; set; } }
public struct WanderSphereRadius : IComponentData { public float Value { get; set; } }

public struct FlockingViewAngle : IComponentData { public float Value { get; set; } }
public struct FlockingViewDistance : IComponentData { public float Value { get; set; } }
public struct FlockingMinDistance : IComponentData { public float Value { get; set; } }