// EarthquakeSystem.cs
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;

public partial struct EarthquakeSystem : ISystem
{
    private void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach (var (earthquake, transform, entity) in SystemAPI.Query<RefRW<EarthquakeComponent>, RefRW<LocalTransform>>().WithEntityAccess())
        {
            // Update elapsed time
            earthquake.ValueRW.ElapsedTime += deltaTime;

            // Check if earthquake duration is over
            if (earthquake.ValueRO.ElapsedTime > earthquake.ValueRO.Duration)
            {
                // Reset shake intensity
                // shake.ValueRW.ShakeIntensity = 0;
                return;
            }

            // Calculate current intensity using amplitude, frequency, and attenuation
            float intensity = earthquake.ValueRO.Amplitude *
                              math.sin(2 * math.PI * earthquake.ValueRO.Frequency * earthquake.ValueRO.ElapsedTime);

            // Apply randomness
            float randomFactor = GenerateRandomFactor();
            float ShakeIntensity = intensity * randomFactor;

            // Update entity position based on shake intensity (simplified)
            float3 shakeOffset = new float3(
                math.sin(earthquake.ValueRO.Frequency * earthquake.ValueRO.ElapsedTime) * ShakeIntensity,
                0,
                math.cos(earthquake.ValueRO.Frequency * earthquake.ValueRO.ElapsedTime) * ShakeIntensity
            );

            transform.ValueRW.Position = earthquake.ValueRO.OriginalPosition + shakeOffset;

        }
    }

    private float GenerateRandomFactor()
    {
        // Generate a random factor between 0.8 and 1.2 to simulate randomness
        return UnityEngine.Random.Range(0.8f, 1.2f);
    }
}