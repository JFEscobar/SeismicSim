using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;
using static UnityEngine.EventSystems.EventTrigger;

namespace Unity.Physics.Authoring
{
    [RequireComponent(typeof(PhysicsShapeAuthoring))]
    public class MaterialParameterJoint : BaseJoint
    {
        [Tooltip("If checked, PositionLocal will snap to match PositionInConnectedEntity")]
        public bool AutoSetConnected = true;
        public float3 LocalCenter;        // This should be set to the center of the contact area
        public float3 PositionInConnectedEntity;
        public quaternion OrientationLocal = quaternion.identity;
        public quaternion OrientationInConnectedEntity = quaternion.identity;

        public float CompressiveStrength; // Material strength in the -LocalNormal direction (MPa)
        public float TensileStrength;     // Material strength in the LocalNormal direction (MPa)
        public float CohesiveStrength;    // Material cohesive strength (MPa)

        public float ContactAreaSize1;    // Size of the contact area in the direction of LocalTangent1
        public float ContactAreaSize2;    // Size of the contact area in the direction of LocalTangent2


        [Tooltip("If checked, the basis will snap to a normalized orthogonal basis")]
        public bool AutoSetOrientation = false;
        // Direction in which the material strength specified. Set these in order.
        public float3 LocalNormal;
        public float3 LocalTangent1;
        public float3 LocalTangent2;

        public bool ExportData = false;
        public DataFrameAuthoring DataFrame;

        void OnEnable()
        {
            // included so tick box appears in Editor
        }
        public void UpdateAuto()
        {
            if (AutoSetConnected)
            {
                RigidTransform bFromA = math.mul(math.inverse(worldFromB), worldFromA);
                PositionInConnectedEntity = math.transform(bFromA, LocalCenter);
                OrientationInConnectedEntity = math.mul(bFromA.rot, OrientationLocal);
            }
            {
                OrientationLocal = math.normalize(OrientationLocal);
                OrientationInConnectedEntity = math.normalize(OrientationInConnectedEntity);
            }
            {
                MaxImpulse = 0.0f;
            }
            if (AutoSetOrientation && !LocalNormal.Equals(float3.zero) && !LocalTangent1.Equals(float3.zero))
            {
                LocalTangent2 = math.normalize(math.cross(LocalNormal, LocalTangent1));

                LocalNormal = math.normalize(LocalNormal);
                if (!LocalTangent1.Equals(float3.zero))
                {
                    LocalTangent1 = math.normalize(math.cross(LocalTangent2, LocalNormal));
                }
            }
        }
    }

    public struct MaterialParameterJointData : IComponentData
    {
        public float CompressiveStrength;
        public float TensileStrength;
        public float CohesiveStrength;
        public float FrictionCoeff;

        public float2 ContactAreaSize;
        public BodyFrame BodyAFromJoint;
    }

    public struct AngularImpulseEventCapture : IComponentData
    {
        public float3 Value;
    }

    public struct LinearImpulseEventCapture : IComponentData
    {
        public float3 Value;
    }

    public struct JointForcesCaptorData : IComponentData
    {
        public Entity Target;
    }

    class MaterialParameterJointBaker : JointBaker<MaterialParameterJoint>
    {
        Material ProduceMaterial(PhysicsShapeAuthoring shape)
        {
            var materialTemplate = shape.MaterialTemplate;
            if (materialTemplate != null)
                DependsOn(materialTemplate);
            return shape.GetMaterial();
        }

        public override void Bake(MaterialParameterJoint authoring)
        {
            authoring.UpdateAuto();

            var materialA = ProduceMaterial(authoring.GetComponent<PhysicsShapeAuthoring>());
            var materialB = ProduceMaterial(authoring.ConnectedBody.GetComponentInParent<PhysicsShapeAuthoring>());

            var frictionCoefficient = Material.GetCombinedFriction(materialA, materialB);

            var physicsJoint = PhysicsJoint.CreateFixed(
                new RigidTransform(authoring.OrientationLocal, authoring.LocalCenter),
                new RigidTransform(authoring.OrientationInConnectedEntity, authoring.PositionInConnectedEntity)
            );
            physicsJoint.SetImpulseEventThresholdAllConstraints(authoring.MaxImpulse);

            var constraintBodyPair = GetConstrainedBodyPair(authoring);
            uint worldIndex = GetWorldIndexFromBaseJoint(authoring);
            var jointEntity = CreateJointEntity(worldIndex, constraintBodyPair, physicsJoint);

            AddComponent(jointEntity, new MaterialParameterJointData
            {
                CompressiveStrength = authoring.CompressiveStrength * 1000000.0f,
                TensileStrength = authoring.TensileStrength * 1000000.0f,
                CohesiveStrength = authoring.CohesiveStrength * 1000000.0f,
                FrictionCoeff = frictionCoefficient,
                ContactAreaSize = {
                    x = authoring.ContactAreaSize1,
                    y = authoring.ContactAreaSize2
                },
                BodyAFromJoint = {
                    Position = authoring.LocalCenter ,
                    Axis = authoring.LocalNormal,
                    PerpendicularAxis = authoring.LocalTangent1
                }
            });

            AddComponent(jointEntity, new AngularImpulseEventCapture { Value = 0.0f });
            AddComponent(jointEntity, new LinearImpulseEventCapture { Value = 0.0f });

            if (authoring.ExportData)
            {
                var captorEntity = CreateAdditionalEntity(TransformUsageFlags.None);
                var dataFrameEntity = GetEntity(authoring.DataFrame, TransformUsageFlags.None);

                AddComponent(captorEntity, new JointForcesCaptorData { Target = jointEntity });

                AddBuffer<ColumnInfo>(captorEntity);

                var columnBakingBuffer = AddBuffer<DataFrameColumnBaking>(captorEntity);
                columnBakingBuffer.Add(new DataFrameColumnBaking(dataFrameEntity, "xForce - Constraint" + authoring.name + authoring.ConnectedBody.name));
                columnBakingBuffer.Add(new DataFrameColumnBaking(dataFrameEntity, "yForce - Constraint" + authoring.name + authoring.ConnectedBody.name));
                columnBakingBuffer.Add(new DataFrameColumnBaking(dataFrameEntity, "zForce - Constraint" + authoring.name + authoring.ConnectedBody.name));
                columnBakingBuffer.Add(new DataFrameColumnBaking(dataFrameEntity, "xTorque - Constraint" + authoring.name + authoring.ConnectedBody.name));
                columnBakingBuffer.Add(new DataFrameColumnBaking(dataFrameEntity, "yTorque - Constraint" + authoring.name + authoring.ConnectedBody.name));
                columnBakingBuffer.Add(new DataFrameColumnBaking(dataFrameEntity, "zTorque - Constraint" + authoring.name + authoring.ConnectedBody.name));
            }
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
    public partial struct ImpulseEventCaptureSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Unity.Physics.SimulationSingleton>();
            state.RequireForUpdate<LinearImpulseEventCapture>();
            state.RequireForUpdate<AngularImpulseEventCapture>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var capture in SystemAPI.Query<RefRW<LinearImpulseEventCapture>>())
            {
                capture.ValueRW.Value = 0.0f;
            }
            foreach (var capture in SystemAPI.Query<RefRW<AngularImpulseEventCapture>>())
            {
                capture.ValueRW.Value = 0.0f;
            }

            var commandBuffer = SystemAPI.GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            state.Dependency = new CollectJointsJob
            {
                CommandBuffer = commandBuffer,
                AngularImpulseEventCaptureData = SystemAPI.GetComponentLookup<AngularImpulseEventCapture>(),
                LinearImpulseEventCaptureData = SystemAPI.GetComponentLookup<LinearImpulseEventCapture>()
            }.Schedule(SystemAPI.GetSingleton<SimulationSingleton>(), state.Dependency);
        }

        [BurstCompile]
        public partial struct CollectJointsJob : Unity.Physics.IImpulseEventsJobBase
        {
            public EntityCommandBuffer CommandBuffer;
            public ComponentLookup<AngularImpulseEventCapture> AngularImpulseEventCaptureData;
            public ComponentLookup<LinearImpulseEventCapture> LinearImpulseEventCaptureData;

            [BurstCompile]
            public void Execute(ImpulseEvent impulseEvent)
            {
                var jointEntity = impulseEvent.JointEntity;

                if (LinearImpulseEventCaptureData.HasComponent(jointEntity) && impulseEvent.Type == ConstraintType.Linear)
                {
                    var impulseCapure = new LinearImpulseEventCapture { Value = impulseEvent.Impulse };
                    CommandBuffer.SetComponent<LinearImpulseEventCapture>(jointEntity, impulseCapure);
                }
                else if (AngularImpulseEventCaptureData.HasComponent(jointEntity) && impulseEvent.Type ==ConstraintType.Angular)
                {
                    var impulseCapure = new AngularImpulseEventCapture { Value = impulseEvent.Impulse };
                    CommandBuffer.SetComponent<AngularImpulseEventCapture>(jointEntity, impulseCapure);
                }
            }
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(FixedStepSimulationSystemGroup))]
    public partial struct ParameterizedJointCheckSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Unity.Physics.SimulationSingleton>();
            state.RequireForUpdate<AngularImpulseEventCapture>();
            state.RequireForUpdate<LinearImpulseEventCapture>();
            state.RequireForUpdate<MaterialParameterJointData>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            var deltaTime = Time.fixedDeltaTime;
            var invDeltaTime = 1f / deltaTime;
            foreach (var (linearImpulseEvent, angularImpulseEvent, parameterizedJoint, entity) in
                SystemAPI.Query<LinearImpulseEventCapture, AngularImpulseEventCapture, 
                MaterialParameterJointData>().WithEntityAccess())
            {
                var BodyAFromJoint = parameterizedJoint.BodyAFromJoint;
                var linearImpulse = linearImpulseEvent.Value;
                var angularImpulse = angularImpulseEvent.Value;

                float3 positionA = BodyAFromJoint.Position;
                float3x3 rotationA = new float3x3(BodyAFromJoint.Axis, BodyAFromJoint.PerpendicularAxis, math.cross(BodyAFromJoint.Axis, BodyAFromJoint.PerpendicularAxis));
                float3x3 rotationA_T = math.transpose(rotationA);

                var forces = math.mul(rotationA_T, linearImpulse) * invDeltaTime;

                float area = parameterizedJoint.ContactAreaSize.x * parameterizedJoint.ContactAreaSize.y;
                if (forces.x < -parameterizedJoint.CompressiveStrength * area
                    || forces.x > parameterizedJoint.TensileStrength * area)
                {
                    commandBuffer.DestroyEntity(entity);
                    continue;
                }

                var friction = forces.x > 0f ? parameterizedJoint.FrictionCoeff * forces.x : 0f;
                if (math.length(forces.yz) > (parameterizedJoint.CohesiveStrength * area + friction))
                {
                    commandBuffer.DestroyEntity(entity);
                    continue;
                }

                var moment = (math.mul(-rotationA_T, math.cross(positionA, linearImpulse))
                    + math.mul(rotationA_T, angularImpulse)) * invDeltaTime;

                float bendingLimitConstant = parameterizedJoint.ContactAreaSize.x * parameterizedJoint.ContactAreaSize.y / 6f * parameterizedJoint.TensileStrength;
                if (math.abs(moment.y) > bendingLimitConstant * parameterizedJoint.ContactAreaSize.y
                    || math.abs(moment.z) > bendingLimitConstant * parameterizedJoint.ContactAreaSize.x)
                {
                    commandBuffer.DestroyEntity(entity);
                    continue;
                }

                bendingLimitConstant = parameterizedJoint.ContactAreaSize.x * parameterizedJoint.ContactAreaSize.y / 6f * parameterizedJoint.CompressiveStrength;
                if (math.abs(moment.y) > bendingLimitConstant * parameterizedJoint.ContactAreaSize.y
                    || math.abs(moment.z) > bendingLimitConstant * parameterizedJoint.ContactAreaSize.x)
                {
                    commandBuffer.DestroyEntity(entity);
                    continue;
                }

                var compressiveStrengthResidue = (forces.x < 0f) ? parameterizedJoint.CompressiveStrength: parameterizedJoint.TensileStrength + forces.x;
            }
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(FixedStepSimulationSystemGroup))]
    partial struct JointForceCaptureSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Unity.Physics.SimulationSingleton>();
            state.RequireForUpdate<AngularImpulseEventCapture>();
            state.RequireForUpdate<LinearImpulseEventCapture>();
            state.RequireForUpdate<JointForcesCaptorData>();
            state.RequireForUpdate<ColumnInfo>();   
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            int frame = Time.frameCount;
            var commandBuffer = SystemAPI.GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
            var invDeltaTime = 1f / Time.fixedDeltaTime;

            foreach (var (jointForceCaptorData, columns)
                in SystemAPI.Query<JointForcesCaptorData, DynamicBuffer<ColumnInfo>>())
            {
                var targetEntity = jointForceCaptorData.Target;

                var transform = SystemAPI.GetComponent<LocalToWorld>(targetEntity);
                var linearImpulse = SystemAPI.GetComponent<LinearImpulseEventCapture>(targetEntity).Value;
                var angularImpulse = SystemAPI.GetComponent<AngularImpulseEventCapture>(targetEntity).Value;

                var force = linearImpulse * invDeltaTime;
                var torque = math.rotate(transform.Rotation, angularImpulse * invDeltaTime);

                commandBuffer.AppendToBuffer(columns[0].DataFrameEntity, new CaptureElement(frame, columns[0].Index, force.x));
                commandBuffer.AppendToBuffer(columns[1].DataFrameEntity, new CaptureElement(frame, columns[1].Index, force.y));
                commandBuffer.AppendToBuffer(columns[2].DataFrameEntity, new CaptureElement(frame, columns[2].Index, force.z));
                commandBuffer.AppendToBuffer(columns[3].DataFrameEntity, new CaptureElement(frame, columns[3].Index, torque.x));
                commandBuffer.AppendToBuffer(columns[4].DataFrameEntity, new CaptureElement(frame, columns[4].Index, torque.y));
                commandBuffer.AppendToBuffer(columns[5].DataFrameEntity, new CaptureElement(frame, columns[5].Index, torque.z));
            }
        }
    }
}