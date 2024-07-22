using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Extensions;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;


namespace Unity.Physics.Authoring
{
    public struct ConstantForceData : IComponentData
    {
        public float3 Force;
        public float3 RelativeForce;
        public float3 Torque;
        public float3 RelativeTorque;
    }

    public class ConstantForceAuthoring : MonoBehaviour
    {
        public float3 Force = float3.zero;
        public float3 RelativeForce = float3.zero;
        public float3 Torque = float3.zero;
        public float3 RelativeTorque = float3.zero;

        public bool ExportForces = false;
        public DataFrameAuthoring DataFrame;

        void OnEnable()
        {
            // included so tick box appears in Editor
        }
    }

    public class ConstantForceAuthoringBaker : Baker<ConstantForceAuthoring>
    {
        public override void Bake(ConstantForceAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new ConstantForceData
            {
                Force = authoring.Force,
                RelativeForce = authoring.RelativeForce,
                Torque = authoring.Torque,
                RelativeTorque = authoring.RelativeTorque
            });
        }
    }

    [UpdateInGroup(typeof(BeforePhysicsSystemGroup))]
    partial struct ConstantForceSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Unity.Physics.SimulationSingleton>();
            state.RequireForUpdate<ConstantForceData>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (constantForceData, physicsMass, transform, physicsVelocity)
                in SystemAPI.Query<ConstantForceData, PhysicsMass, LocalToWorld, RefRW<PhysicsVelocity>>())
            {
                var force = float3.zero;
                var relativeTorque = float3.zero;

                if (!constantForceData.Force.Equals(float3.zero))
                    force += constantForceData.Force;

                if (!constantForceData.RelativeForce.Equals(float3.zero))
                    force += math.rotate(transform.Rotation, constantForceData.RelativeForce);

                if (!constantForceData.Torque.Equals(float3.zero))
                    relativeTorque += math.rotate(math.inverse(transform.Rotation), constantForceData.Torque);

                if (!constantForceData.RelativeTorque.Equals(float3.zero))
                    relativeTorque += constantForceData.RelativeTorque;

                var deltaTime = SystemAPI.Time.fixedDeltaTime;
                PhysicsComponentExtensions.ApplyLinearImpulse(ref physicsVelocity.ValueRW, physicsMass, force * deltaTime);
                PhysicsComponentExtensions.ApplyAngularImpulse(ref physicsVelocity.ValueRW, physicsMass, relativeTorque * deltaTime);
            }
        }

        [BurstCompile]
        public partial struct LinearImpulseJob : Unity.Jobs.IJob
        {
            public RefRW<PhysicsVelocity> Velocity;
            public PhysicsMass Mass;
            public float3 Impulse;

            public void Execute()
            {
                PhysicsComponentExtensions.ApplyLinearImpulse(ref Velocity.ValueRW, Mass, Impulse);
            }
        }

        [BurstCompile]
        public partial struct AngularImpulseJob : Unity.Jobs.IJob
        {
            public RefRW<PhysicsVelocity> Velocity;
            public PhysicsMass Mass;
            public float3 Impulse;

            public void Execute()
            {
                PhysicsComponentExtensions.ApplyAngularImpulse(ref Velocity.ValueRW, Mass, Impulse);
            }
        }
    }
}