// EarthquakeComponent.cs
using Unity.Entities;

public struct EarthquakeComponent : IComponentData
{
    public float Amplitude;
    public float Frequency;
    public float Duration;
    public float ElapsedTime;
    public float OriginalPosition;
}