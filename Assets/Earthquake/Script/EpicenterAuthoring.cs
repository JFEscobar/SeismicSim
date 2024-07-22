// EpicenterAuthoring.cs
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class EpicenterAuthoring : MonoBehaviour
{
    [SerializeField] private float3 Position;

    private class AuthoringBaker : Baker<EpicenterAuthoring>
    {
        public override void Bake(EpicenterAuthoring authoring)
        {
            Entity authoringEntity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(authoringEntity, new EpicenterComponent
            {
                Position = authoring.Position,
            });
        }
    }
}