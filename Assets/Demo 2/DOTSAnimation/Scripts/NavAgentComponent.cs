using Unity.Entities;
using Unity.Mathematics;

public struct NavAgentComponent : IComponentData
{
	// public Entity targetEntity;
	public float3 currentGoalPosition;
	public bool pathCalculated;
	public int currentWaypoint;
	public float moveSpeed;
	public float nextPathCalculateTime;
	public bool arrived;
	public float wOffset;
	public float speedMultiplier;
}

public struct WaypointBuffer : IBufferElementData
{
	public float3 wayPoint;
}