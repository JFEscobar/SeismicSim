using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Authoring;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;


namespace Unity.Physics.Authoring
{
    [RequireComponent(typeof(PhysicsBodyAuthoring))]
    public class KinematicCaptorAuthoring : MonoBehaviour
    {
        public DataFrameAuthoring DataFrameAuthoring;

        void OnEnable()
        {
            // included so tick box appears in Editor
        }
    }

    public struct KinematicCaptorData : IComponentData
    {
        public Entity Target;
    }

    public class KinematicCaptorAuthoringBaker : Baker<KinematicCaptorAuthoring>
    {
        public override void Bake(KinematicCaptorAuthoring authoring)
        {
            var dataFrameEntity = authoring.DataFrameAuthoring == null ?
                Entity.Null : GetEntity(authoring.DataFrameAuthoring, TransformUsageFlags.None);

            var entity = GetEntity(TransformUsageFlags.Dynamic);

            var captorEntity = CreateAdditionalEntity(TransformUsageFlags.None);

            AddComponent(captorEntity, new KinematicCaptorData { Target = entity });

            AddBuffer<ColumnInfo>(captorEntity);

            var columnBakingBuffer = AddBuffer<DataFrameColumnBaking>(captorEntity);
            columnBakingBuffer.Add(new DataFrameColumnBaking(dataFrameEntity, "xPosition - " + authoring.name));
            columnBakingBuffer.Add(new DataFrameColumnBaking(dataFrameEntity, "yPosition - " + authoring.name));
            columnBakingBuffer.Add(new DataFrameColumnBaking(dataFrameEntity, "zPosition - " + authoring.name));
            columnBakingBuffer.Add(new DataFrameColumnBaking(dataFrameEntity, "xRotation - " + authoring.name));
            columnBakingBuffer.Add(new DataFrameColumnBaking(dataFrameEntity, "yRotation - " + authoring.name));
            columnBakingBuffer.Add(new DataFrameColumnBaking(dataFrameEntity, "zRotation - " + authoring.name));
            columnBakingBuffer.Add(new DataFrameColumnBaking(dataFrameEntity, "xLinearVelocity - " + authoring.name));
            columnBakingBuffer.Add(new DataFrameColumnBaking(dataFrameEntity, "yLinearVelocity - " + authoring.name));
            columnBakingBuffer.Add(new DataFrameColumnBaking(dataFrameEntity, "zLinearVelocity - " + authoring.name));
            columnBakingBuffer.Add(new DataFrameColumnBaking(dataFrameEntity, "xAngularVelocity - " + authoring.name));
            columnBakingBuffer.Add(new DataFrameColumnBaking(dataFrameEntity, "yAngularVelocity - " + authoring.name));
            columnBakingBuffer.Add(new DataFrameColumnBaking(dataFrameEntity, "zAngularVelocity - " + authoring.name));
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
    partial struct KinematicCaptureSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Unity.Physics.SimulationSingleton>();
            state.RequireForUpdate<CaptureElement>();
            state.RequireForUpdate<ColumnInfo>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            int frame = Time.frameCount;
            var commandBuffer = SystemAPI.GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (captorData, columns) in SystemAPI.Query<KinematicCaptorData,
                DynamicBuffer<ColumnInfo>>())
            {
                UnityEngine.Assertions.Assert.IsTrue(columns.Length >= 12);

                var transform = SystemAPI.GetComponent<LocalToWorld>(captorData.Target);
                var velocity = SystemAPI.GetComponent<PhysicsVelocity>(captorData.Target);

                float3 position = transform.Position;
                commandBuffer.AppendToBuffer(columns[0].DataFrameEntity, new CaptureElement(frame, columns[0].Index, position.x));
                commandBuffer.AppendToBuffer(columns[1].DataFrameEntity, new CaptureElement(frame, columns[1].Index, position.y));
                commandBuffer.AppendToBuffer(columns[2].DataFrameEntity, new CaptureElement(frame, columns[2].Index, position.z));

                float3 rotation = math.Euler(transform.Rotation);
                commandBuffer.AppendToBuffer(columns[3].DataFrameEntity, new CaptureElement(frame, columns[3].Index, rotation.x));
                commandBuffer.AppendToBuffer(columns[4].DataFrameEntity, new CaptureElement(frame, columns[4].Index, rotation.y));
                commandBuffer.AppendToBuffer(columns[5].DataFrameEntity, new CaptureElement(frame, columns[5].Index, rotation.z));

                float3 linearVelocity = velocity.Linear;
                commandBuffer.AppendToBuffer(columns[6].DataFrameEntity, new CaptureElement(frame, columns[6].Index, linearVelocity.x));
                commandBuffer.AppendToBuffer(columns[7].DataFrameEntity, new CaptureElement(frame, columns[7].Index, linearVelocity.y));
                commandBuffer.AppendToBuffer(columns[8].DataFrameEntity, new CaptureElement(frame, columns[8].Index, linearVelocity.z));

                float3 angularVelocity = velocity.Angular;
                commandBuffer.AppendToBuffer(columns[9].DataFrameEntity, new CaptureElement(frame, columns[9].Index, angularVelocity.x));
                commandBuffer.AppendToBuffer(columns[10].DataFrameEntity, new CaptureElement(frame, columns[10].Index, angularVelocity.y));
                commandBuffer.AppendToBuffer(columns[11].DataFrameEntity, new CaptureElement(frame, columns[11].Index, angularVelocity.z));
            }
        }
    }
}