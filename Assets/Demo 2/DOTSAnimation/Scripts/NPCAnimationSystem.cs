using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Collections;


public partial struct NPCAnimationSystem : ISystem
{
    private EntityManager entityManager;

    private void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.ManagedAPI.TryGetSingleton(out AnimationVisualsPrefabs animationVisualPrefabs))
        {
            return;
        }

        entityManager = state.EntityManager;

        EntityCommandBuffer ECB = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (navAgentComponent, transform, entity) in SystemAPI.Query<RefRW<NavAgentComponent>, RefRW<LocalTransform>>().WithEntityAccess())
        {
            if (!entityManager.HasComponent<VisualsReferenceComponent>(entity))
            {
                GameObject playerVisuals = Object.Instantiate(animationVisualPrefabs.NPC);

                ECB.AddComponent(entity, new VisualsReferenceComponent { gameObject = playerVisuals });

                navAgentComponent.ValueRW.wOffset = UnityEngine.Random.Range(0.0f, 1.0f);
                navAgentComponent.ValueRW.speedMultiplier = UnityEngine.Random.Range(2.0f, 3.0f);
            }
            else
            {
                VisualsReferenceComponent NPCVisualsReference = entityManager.GetComponentData<VisualsReferenceComponent>(entity);

                NPCVisualsReference.gameObject.transform.position = transform.ValueRO.Position;
                NPCVisualsReference.gameObject.transform.rotation = transform.ValueRO.Rotation;

                NPCVisualsReference.gameObject.GetComponent<Animator>().SetBool("isWalking", true);

                NPCVisualsReference.gameObject.GetComponent<Animator>().SetFloat("wOffset", navAgentComponent.ValueRO.wOffset);
                
                NPCVisualsReference.gameObject.GetComponent<Animator>().SetFloat("speedMultiplier", navAgentComponent.ValueRO.speedMultiplier);
                navAgentComponent.ValueRW.moveSpeed = navAgentComponent.ValueRO.speedMultiplier;
            }

        }

        ECB.Playback(entityManager);
        ECB.Dispose();
    }
}
