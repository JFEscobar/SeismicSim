// ShakeComponent.cs
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public struct ShakeComponent : IComponentData
{
    public float ShakeIntensity;
    public float3 OriginalPosition;
}