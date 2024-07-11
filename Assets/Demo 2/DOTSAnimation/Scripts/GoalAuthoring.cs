using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

public class GoalAuthoring : MonoBehaviour
{
    [SerializeField] private float3 Position;

    private class AuthoringBaker : Baker<GoalAuthoring>
    {
        public override void Bake(GoalAuthoring authoring)
        {
            Entity authoringEntity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(authoringEntity, new GoalComponent
            {
                Position = authoring.transform.position
            });
        }
    }
}