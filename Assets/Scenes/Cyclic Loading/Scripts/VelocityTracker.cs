using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using UnityEngine;

public class VelocityTracker : MonoBehaviour {
    public float3 previousVelocity = float3.zero;
    public float3 previousDeltaTime = float3.zero;
}

public struct VelocityTrackerData : IComponentData {
    public float3 PreviousVelocity;
    public float3 PreviousDeltaTime;
}

[BurstCompile]
internal class VelocityTrackerBaker : Baker<VelocityTracker> {
    public override void Bake(VelocityTracker authoring) {
        Entity entity = GetEntity(TransformUsageFlags.Dynamic);
        AddComponent(entity,
            new VelocityTrackerData {
                PreviousVelocity = authoring.previousVelocity,
                PreviousDeltaTime = authoring.previousDeltaTime
            });
    }
}

[BurstCompile]
[UpdateInGroup(typeof(BeforePhysicsSystemGroup))]
public partial class VelocityTrackerSystem : SystemBase {
    protected override void OnUpdate() {
        Entities
            .WithAll<VelocityTrackerData>()
            .ForEach((ref VelocityTrackerData data, ref PhysicsVelocity velocity) => {
                data.PreviousVelocity = velocity.Linear;
                data.PreviousDeltaTime = SystemAPI.Time.DeltaTime;
            })
            .Schedule();
    }
}

[BurstCompile]
[UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
public partial struct AccelerationSystem : ISystem {
    public void OnCreate(ref SystemState state) {
        state.RequireForUpdate<SimulationSingleton>();
    }

    public void OnUpdate(ref SystemState state) {
        var deltaTime = SystemAPI.Time.DeltaTime;

        foreach (var (physicsVelocity, previousVelocity, physicsBody) in
                 SystemAPI.Query<RefRO<PhysicsVelocity>, RefRW<VelocityTrackerData>, RefRO<PhysicsMass>>()) {
            var currentVelocity = physicsVelocity.ValueRO.Linear;
            var acceleration = (currentVelocity - previousVelocity.ValueRO.PreviousVelocity) / previousVelocity.ValueRO.PreviousDeltaTime;
            var force = (1.0f / physicsBody.ValueRO.InverseMass) * acceleration;

            Debug.Log($"acceleration: {signedMagnitude(acceleration)} m/s^2 \u21d4 {acceleration}\nforce:        {signedMagnitude(force)} N \u21d4 {force}\n");
        }

        return;

        float magnitude(float3 input) => new Vector3(input.x, input.y, input.z).magnitude;
        float sign(float3 input) => math.sign(input.x);
        float signedMagnitude(float3 input) => sign(input) * magnitude(input);
    }
}
