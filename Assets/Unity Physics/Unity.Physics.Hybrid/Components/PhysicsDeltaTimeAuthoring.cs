using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Physics.Authoring;
using UnityEngine;

namespace Unity.Physics.Authoring
{
    [AddComponentMenu("Entities/Physics/Physics Delta Time")]
    [DisallowMultipleComponent]
    public class PhysicsDeltaTimeAuthoring : MonoBehaviour
    {
        /// <summary>
        ///     Specifies the simulation time step.<para/>
        ///     Lower values mean more stability, but also worse performance.
        /// </summary>
        public float FixedTimeStep
        {
            get => m_FixedTimeStep;
            set => m_FixedTimeStep = value;
        }
        [SerializeField]
        float m_FixedTimeStep = 0.02f;
    }

    [TemporaryBakingType]
    struct DeltaTimeBaking : IComponentData
    {
        public float DeltaTime;
    }

    internal class PhysicsDeltaTimeBaker : Baker<PhysicsDeltaTimeAuthoring>
    {
        public override void Bake(PhysicsDeltaTimeAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new DeltaTimeBaking { DeltaTime = authoring.FixedTimeStep } );

            Time.fixedDeltaTime = authoring.FixedTimeStep;
        }
    }

    partial class SetTimeStepSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<DeltaTimeBaking>();
        }
        protected override void OnUpdate()
        {
            foreach (var deltaTimeBaking in SystemAPI.Query<DeltaTimeBaking>())
            {
                var fixedStepGroup = World.GetExistingSystemManaged<FixedStepSimulationSystemGroup>();
                fixedStepGroup.Timestep = deltaTimeBaking.DeltaTime;
            }
        }
    }
}
