using Unity.Entities;
using UnityEngine;

public class AnimationVisualsPrefabsAuthoring : MonoBehaviour
{
    [SerializeField] private GameObject npcPrefab;

    private class AnimationVisualsPrefabsBaker : Baker<AnimationVisualsPrefabsAuthoring>
    {
        public override void Bake(AnimationVisualsPrefabsAuthoring authoring)
        {
            Entity playerPrefabEntity = GetEntity(TransformUsageFlags.None);

            AddComponentObject(playerPrefabEntity, new AnimationVisualsPrefabs
            {
                NPC = authoring.npcPrefab
            });
        }
    }
}
