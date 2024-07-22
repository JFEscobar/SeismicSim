using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Extensions;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;

namespace Unity.Physics.Authoring
{
    public class RampForceAuthoring : ExportableData
    {
        public bool ApplyForcesLocally = true;
        public float TargetTime;
        public float3 ForceTarget;
        public float3 TorqueTarget;

        void OnEnable()
        {
            // included so tick box appears in Editor
        }
    }
    public struct RampForceData : IComponentData
    {
        public float3 ForceTarget;
        public float3 TorqueTarget;
        public float TargetTime;
        public bool ApplyLocally;
    }

    public struct RampForceCaptorData : IComponentData
    {
        public Entity Target;
    }

    public class RampForceAuthoringBaker : Baker<RampForceAuthoring>
    {
        public override void Bake(RampForceAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new RampForceData
            {
                ForceTarget = authoring.ForceTarget,
                TorqueTarget = authoring.TorqueTarget,
                TargetTime = authoring.TargetTime,
                ApplyLocally = authoring.ApplyForcesLocally
            });

            if (authoring.ExportData)
            {
                var captorEntity = CreateAdditionalEntity(TransformUsageFlags.None);
                var dataFrameEntity = GetEntity(authoring.DataFrame, TransformUsageFlags.None);

                AddComponent(captorEntity, new RampForceCaptorData { Target = entity });

                AddBuffer<ColumnInfo>(captorEntity);

                var columnBakingBuffer = AddBuffer<DataFrameColumnBaking>(captorEntity);
                columnBakingBuffer.Add(new DataFrameColumnBaking(dataFrameEntity, "xForce - " + authoring.name));
                columnBakingBuffer.Add(new DataFrameColumnBaking(dataFrameEntity, "yForce - " + authoring.name));
                columnBakingBuffer.Add(new DataFrameColumnBaking(dataFrameEntity, "zForce - " + authoring.name));
                columnBakingBuffer.Add(new DataFrameColumnBaking(dataFrameEntity, "xTorque - " + authoring.name));
                columnBakingBuffer.Add(new DataFrameColumnBaking(dataFrameEntity, "yTorque - " + authoring.name));
                columnBakingBuffer.Add(new DataFrameColumnBaking(dataFrameEntity, "zTorque - " + authoring.name));
            }
        }
    }

    [UpdateInGroup(typeof(BeforePhysicsSystemGroup))]
    partial struct RampForceSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Unity.Physics.SimulationSingleton>();
            state.RequireForUpdate<RampForceData>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (rampForceData, physicsMass, transform, physicsVelocity)
                in SystemAPI.Query<RampForceData, PhysicsMass, LocalToWorld, RefRW<PhysicsVelocity>>())
            {
                var deltaTime = SystemAPI.Time.fixedDeltaTime;
                float3 force, relativeTorque;

                if (rampForceData.ApplyLocally)
                {
                    force = math.rotate(transform.Rotation, rampForceData.ForceTarget);
                    relativeTorque = rampForceData.TorqueTarget;
                }
                else
                {
                    force = rampForceData.ForceTarget;
                    relativeTorque = math.rotate(math.inverse(transform.Rotation), rampForceData.TorqueTarget);
                }

                var time = SystemAPI.Time.ElapsedTime;
                if (time < rampForceData.TargetTime)
                {
                    float slope = (float)time / rampForceData.TargetTime;
                    force *= slope;
                    relativeTorque *= slope;
                }
                var linearImpulse = force * deltaTime;
                PhysicsComponentExtensions.ApplyLinearImpulse(ref physicsVelocity.ValueRW, physicsMass, linearImpulse);
                var relativeAngularImpulse = relativeTorque * deltaTime;
                PhysicsComponentExtensions.ApplyAngularImpulse(ref physicsVelocity.ValueRW, physicsMass, relativeAngularImpulse);
            }
        }
    }

    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    partial struct RampForceCaptureSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Unity.Physics.SimulationSingleton>();
            state.RequireForUpdate<RampForceData>();
            state.RequireForUpdate<ColumnInfo>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            int frame = Time.frameCount;
            var commandBuffer = SystemAPI.GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (rampForceCaptorData, columns)
                in SystemAPI.Query<RampForceCaptorData, DynamicBuffer<ColumnInfo>>())
            {
                var targetEntity = rampForceCaptorData.Target;

                var transform = SystemAPI.GetComponent<LocalToWorld>(targetEntity);
                var rampForceData = SystemAPI.GetComponent<RampForceData>(targetEntity);

                float3 force, relativeTorque;

                if (rampForceData.ApplyLocally)
                {
                    force = math.rotate(transform.Rotation, rampForceData.ForceTarget);
                    relativeTorque = rampForceData.TorqueTarget;
                }
                else
                {
                    force = rampForceData.ForceTarget;
                    relativeTorque = math.rotate(math.inverse(transform.Rotation), rampForceData.TorqueTarget);
                }

                var time = SystemAPI.Time.ElapsedTime;
                if (time < rampForceData.TargetTime)
                {
                    float slope = (float)time / rampForceData.TargetTime;
                    force *= slope;
                    relativeTorque *= slope;
                }

                commandBuffer.AppendToBuffer(columns[0].DataFrameEntity, new CaptureElement(frame, columns[0].Index, force.x));
                commandBuffer.AppendToBuffer(columns[1].DataFrameEntity, new CaptureElement(frame, columns[1].Index, force.y));
                commandBuffer.AppendToBuffer(columns[2].DataFrameEntity, new CaptureElement(frame, columns[2].Index, force.z));
                commandBuffer.AppendToBuffer(columns[3].DataFrameEntity, new CaptureElement(frame, columns[3].Index, relativeTorque.x));
                commandBuffer.AppendToBuffer(columns[4].DataFrameEntity, new CaptureElement(frame, columns[4].Index, relativeTorque.y));
                commandBuffer.AppendToBuffer(columns[5].DataFrameEntity, new CaptureElement(frame, columns[5].Index, relativeTorque.z));
            }
        }
    }
}
