using UnityEngine;
using Unity.Entities;
using UnityEngine.Experimental.AI;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Burst;
using UnityEditor;
using Unity.Entities.UniversalDelegates;

[BurstCompile]
public partial struct NavAgentSystem : ISystem
{
    [BurstCompile]
    private void OnUpdate(ref SystemState state)
    {

        EntityQuery goalQuery = state.GetEntityQuery(ComponentType.ReadOnly<GoalComponent>());
        NativeArray<GoalComponent> goals = goalQuery.ToComponentDataArray<GoalComponent>(Allocator.TempJob);

        foreach (var (navAgent, transform, entity) in SystemAPI.Query<RefRW<NavAgentComponent>, RefRW<LocalTransform>>().WithEntityAccess())
        {
            DynamicBuffer<WaypointBuffer> waypointBuffer = state.EntityManager.GetBuffer<WaypointBuffer>(entity);

            if (navAgent.ValueRO.isIdle) { return ; }

            // if (navAgent.ValueRO.nextPathCalculateTime < SystemAPI.Time.ElapsedTime)
            if (navAgent.ValueRO.arrived || waypointBuffer.Length == 0)
            {
                navAgent.ValueRW.nextPathCalculateTime += 1;
                navAgent.ValueRW.pathCalculated = false;
                navAgent.ValueRW.arrived = false;

                CalculatePath(navAgent, transform, waypointBuffer, goals, ref state);
            }
            else
            {
                Move(navAgent, transform, waypointBuffer, ref state);
            }
        }

        goals.Dispose();
    }

    [BurstCompile]
    private void Move(RefRW<NavAgentComponent> navAgent, RefRW<LocalTransform> transform, DynamicBuffer<WaypointBuffer> waypointBuffer,
        ref SystemState state)
    {
        if (math.distance(transform.ValueRO.Position, waypointBuffer[navAgent.ValueRO.currentWaypoint].wayPoint) < 0.4f)
        {
            if (navAgent.ValueRO.currentWaypoint + 1 < waypointBuffer.Length)
            {
                navAgent.ValueRW.currentWaypoint += 1;
            }
        }

        float3 direction = waypointBuffer[navAgent.ValueRO.currentWaypoint].wayPoint - transform.ValueRO.Position;
        float angle = math.degrees(math.atan2(direction.z, direction.x));

        transform.ValueRW.Rotation = math.slerp(
                        transform.ValueRW.Rotation,
                        quaternion.Euler(new float3(0, angle, 0)),
                        SystemAPI.Time.DeltaTime);

        transform.ValueRW.Rotation = Quaternion.LookRotation(direction);

        transform.ValueRW.Position += math.normalize(direction) * SystemAPI.Time.DeltaTime * navAgent.ValueRO.moveSpeed;

        float distance = math.distance(transform.ValueRO.Position, navAgent.ValueRO.currentGoalPosition);

        if (distance < 1.0f)
        {
            navAgent.ValueRW.arrived = true;
        }
    }


    [BurstCompile]
    private void CalculatePath(RefRW<NavAgentComponent> navAgent, RefRW<LocalTransform> transform, DynamicBuffer<WaypointBuffer> waypointBuffer,
         NativeArray<GoalComponent> goals, ref SystemState state)
    {
        NavMeshQuery query = new NavMeshQuery(NavMeshWorld.GetDefaultWorld(), Allocator.TempJob, 1000);

        float3 fromPosition = transform.ValueRO.Position;

        int i = UnityEngine.Random.Range(0, goals.Length);
        // Access goal.Position here
        float3 goalPosition = goals[i].Position;

        UnityEngine.Debug.Log($"Random Goal {i} Position: {goalPosition}");
        //float3 toPosition = state.EntityManager.GetComponentData<LocalTransform>(navAgent.ValueRO.targetEntity).Position;
        float3 toPosition = goalPosition;
        navAgent.ValueRW.currentGoalPosition = toPosition;
        float3 extents = new float3(1, 1, 1);

        NavMeshLocation fromLocation = query.MapLocation(fromPosition, extents, 0);
        NavMeshLocation toLocation = query.MapLocation(toPosition, extents, 0);

        PathQueryStatus status;
        PathQueryStatus returningStatus;
        int maxPathSize = 1000;

        if (query.IsValid(fromLocation) && query.IsValid(toLocation))
        {
            status = query.BeginFindPath(fromLocation, toLocation);
            if (status == PathQueryStatus.InProgress)
            {
                status = query.UpdateFindPath(100, out int iterationsPerformed);
                if (status == PathQueryStatus.Success)
                {
                    status = query.EndFindPath(out int pathSize);

                    NativeArray<NavMeshLocation> result = new NativeArray<NavMeshLocation>(pathSize + 1, Allocator.Temp);
                    NativeArray<StraightPathFlags> straightPathFlag = new NativeArray<StraightPathFlags>(maxPathSize, Allocator.Temp);
                    NativeArray<float> vertexSide = new NativeArray<float>(maxPathSize, Allocator.Temp);
                    NativeArray<PolygonId> polygonIds = new NativeArray<PolygonId>(pathSize + 1, Allocator.Temp);
                    int straightPathCount = 0;

                    query.GetPathResult(polygonIds);

                    returningStatus = PathUtils.FindStraightPath
                        (
                        query,
                        fromPosition,
                        toPosition,
                        polygonIds,
                        pathSize,
                        ref result,
                        ref straightPathFlag,
                        ref vertexSide,
                        ref straightPathCount,
                        maxPathSize
                        );

                    if (returningStatus == PathQueryStatus.Success)
                    {
                        waypointBuffer.Clear();

                        foreach (NavMeshLocation location in result)
                        {
                            if (location.position != Vector3.zero)
                            {
                                waypointBuffer.Add(new WaypointBuffer { wayPoint = location.position });
                            }
                        }

                        navAgent.ValueRW.currentWaypoint = 0;
                        navAgent.ValueRW.pathCalculated = true;
                        
                    }
                    straightPathFlag.Dispose();
                    polygonIds.Dispose();
                    vertexSide.Dispose();
                }
            }
        }
        query.Dispose();
    }
}