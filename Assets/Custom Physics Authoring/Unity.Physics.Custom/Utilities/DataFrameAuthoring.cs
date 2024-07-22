using System.Collections.Generic;
using System.IO;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics.Systems;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.Physics.Authoring
{
    public class DataFrameAuthoring : MonoBehaviour
    {
        public float CaptureTime;
        public string OutputFile = "/tmp.csv";

        public bool ExportTime = true;

        void OnEnable()
        {
            // included so tick box appears in Editor
        }
    }

    public class ExportableData : MonoBehaviour
    {
        public bool ExportData = false;
        public DataFrameAuthoring DataFrame;
    }

    public struct DataFrameSaveData : IComponentData
    {
        public FixedString512Bytes OutputFile;
        public float CaptureTime;
    }

    [InternalBufferCapacity(8)]
    public struct ColumnName : IBufferElementData
    {
        public FixedString64Bytes Name;
    }

    [InternalBufferCapacity(256)]
    public struct CaptureElement : IBufferElementData
    {
        public int Frame;
        public int Column;
        public float Value;

        public CaptureElement(int frame, int column, float value)
        {
            Frame = frame;
            Column = column;
            Value = value;
        }
    }

    public struct TimeCaptureTag : IComponentData { }

    public class DataFrameAuthoringBaker : Baker<DataFrameAuthoring>
    {
        public override void Bake(DataFrameAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);

            AddComponent(entity, new DataFrameSaveData
            {
                OutputFile = authoring.OutputFile,
                CaptureTime = authoring.CaptureTime,
            });

            AddBuffer<ColumnName>(entity);
            AddBuffer<CaptureElement>(entity);

            if (authoring.ExportTime)
            {
                var captorEntity = CreateAdditionalEntity(TransformUsageFlags.None);
                var dataFrameEntity = entity;

                AddComponent(captorEntity, new TimeCaptureTag{});

                AddBuffer<ColumnInfo>(captorEntity);
                var columnBakingBuffer = AddBuffer<DataFrameColumnBaking>(captorEntity);
                columnBakingBuffer.Add(new DataFrameColumnBaking(dataFrameEntity, "Time"));
            }
        }
    }

    [TemporaryBakingType]
    struct DataFrameColumnBaking : IBufferElementData
    {
        public Entity DataFrameEntity;
        public FixedString64Bytes ColumnName;

        public DataFrameColumnBaking(Entity dataFrameEntity, FixedString64Bytes columnName)
        {
            DataFrameEntity = dataFrameEntity;
            ColumnName = columnName;
        }
    }

    struct ColumnInfo : IBufferElementData
    {
        public Entity DataFrameEntity;
        public int Index;
    }

    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    partial struct NameColumnBakingSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DataFrameColumnBaking>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (columnBakingBuffer, entity) in SystemAPI.Query<DynamicBuffer<DataFrameColumnBaking>>().WithEntityAccess())
            {
                bool hasColumnInfoBuffer = SystemAPI.HasBuffer<ColumnInfo>(entity);
                DynamicBuffer<ColumnInfo> columnInfoData = SystemAPI.GetBuffer<ColumnInfo>(entity);

                foreach (var columnBaking in columnBakingBuffer)
                {
                    var dfColumns = SystemAPI.GetBuffer<ColumnName>(columnBaking.DataFrameEntity);
                    int index = dfColumns.Length;
                    dfColumns.Add(new ColumnName { Name = columnBaking.ColumnName });

                    columnInfoData.Add(new ColumnInfo
                    {
                        DataFrameEntity = columnBaking.DataFrameEntity,
                        Index = index
                    });
                }
            }
        }
    }

    //[BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(FixedStepSimulationSystemGroup))]
    partial struct DataFrameSaveSystem : ISystem
    {
        //[BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DataFrameSaveData>();
            state.RequireForUpdate<ColumnName>();
            state.RequireForUpdate<ColumnInfo>();
        }

        //[BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (saveParams, entity) in SystemAPI.Query<DataFrameSaveData>().WithEntityAccess())
            {
                if (saveParams.CaptureTime == 0f) continue;
                if (SystemAPI.Time.ElapsedTime >= saveParams.CaptureTime)
                {
                    SaveToCsv(ref state, entity);

                    var commandBuffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                        .CreateCommandBuffer(state.WorldUnmanaged);

                    commandBuffer.RemoveComponent<DataFrameSaveData>(entity);
                }
            }
        }

        //[BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            foreach (var (saveParams, entity) in SystemAPI.Query<DataFrameSaveData>().WithEntityAccess())
            {
                SaveToCsv(ref state, entity);
            }
        }

        //[BurstCompile]
        void SaveToCsv(ref SystemState state, Entity dataFrame)
        {
            var saveData = SystemAPI.GetComponent<DataFrameSaveData>(dataFrame);
            var columnNames = SystemAPI.GetBuffer<ColumnName>(dataFrame);
            var dataEntries = SystemAPI.GetBuffer<CaptureElement>(dataFrame);

            if (dataEntries.Length == 0) return;

            HashSet<int> frames = new HashSet<int>();
            foreach (var entry in dataEntries)
                frames.Add(entry.Frame);

            int nRows = frames.Count;
            int nCols = columnNames.Length;

            float[] consolidatedData = new float[nRows * nCols];

            int row = 0;
            int frame = dataEntries[0].Frame;
            foreach (var entry in dataEntries)
            {
                if (entry.Frame != frame)
                {
                    UnityEngine.Assertions.Assert.IsTrue(entry.Frame > frame);
                    frame = entry.Frame;
                    row++;
                }

                consolidatedData[row * nCols + entry.Column] = entry.Value;
            }

            string[] lines = new string[row + 2];

            foreach (var name in columnNames)
                lines[0] += name.Name + ",";

            for (int i = 0; i < row; ++i)
            {
                lines[i + 1] = "";
                for (int j = 0; j < nCols; ++j)
                    lines[i + 1] += consolidatedData[i * nCols + j] + ",";
            }

            using StreamWriter outputFile = new StreamWriter(Application.streamingAssetsPath + "/" + saveData.OutputFile.ToString());
            {
                foreach (string line in lines)
                    outputFile.WriteLine(line);
            }
        }
    }

    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    partial struct TimeCaptureSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Unity.Physics.SimulationSingleton>();
            state.RequireForUpdate<TimeCaptureTag>();
            state.RequireForUpdate<ColumnInfo>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            int frame = Time.frameCount;
            float time = Time.fixedTime;
            var commandBuffer = SystemAPI.GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (captureTag, columns) in SystemAPI.Query<TimeCaptureTag, DynamicBuffer<ColumnInfo>>())
            {
                var dataFrame = columns[0].DataFrameEntity;
                var columnIndex = columns[0].Index;
                
                commandBuffer.AppendToBuffer(dataFrame, new CaptureElement(frame, columnIndex, time));
            }
        }
    }
}