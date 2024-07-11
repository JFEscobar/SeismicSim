using UnityEngine;
using Unity.Entities;

public class NavAgentAuthoring : MonoBehaviour
{
    [SerializeField] private float moveSpeed;
    [SerializeField] private bool isIdle;

    private class AuthoringBaker : Baker<NavAgentAuthoring>
    {
        public override void Bake(NavAgentAuthoring authoring)
        {
            Entity authoringEntity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(authoringEntity, new NavAgentComponent
            {
                moveSpeed = authoring.moveSpeed,
                isIdle = authoring.isIdle
            });

            AddBuffer<WaypointBuffer>(authoringEntity);
        }
    }
}