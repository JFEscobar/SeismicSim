// PlaneAuthoring.cs
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public class PlaneAuthoring : MonoBehaviour
{
    [SerializeField] private float Amplitude = 1f;
    [SerializeField] private float Frequency = 1f;
    [SerializeField] private float Duration = 10f;

    private class AuthoringBaker : Baker<PlaneAuthoring>
    {
        public override void Bake(PlaneAuthoring authoring)
        {
            Entity authoringEntity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(authoringEntity, new EarthquakeComponent
            {
                Amplitude = authoring.Amplitude,
                Frequency = authoring.Frequency,
                Duration = authoring.Duration,
                ElapsedTime = 0f,
            });
        }
    }
}