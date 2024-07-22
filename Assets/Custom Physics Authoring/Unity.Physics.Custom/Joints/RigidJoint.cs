using Unity.Collections;
using Unity.Entities;
using Unity.Entities.UniversalDelegates;
using Unity.Mathematics;

namespace Unity.Physics.Authoring
{
    public class RigidJoint : BallAndSocketJoint
    {
        public quaternion OrientationLocal = quaternion.identity;
        public quaternion OrientationInConnectedEntity = quaternion.identity;

        public bool ExportData = false;
        public DataFrameAuthoring DataFrame;

        public override void UpdateAuto()
        {
            base.UpdateAuto();
            if (AutoSetConnected)
            {
                RigidTransform bFromA = math.mul(math.inverse(worldFromB), worldFromA);
                OrientationInConnectedEntity = math.mul(bFromA.rot, OrientationLocal);
            }
            {
                OrientationLocal = math.normalize(OrientationLocal);
                OrientationInConnectedEntity = math.normalize(OrientationInConnectedEntity);
            }
        }
    }

    class RigidJointBaker : JointBaker<RigidJoint>
    {
        public override void Bake(RigidJoint authoring)
        {
            authoring.UpdateAuto(); 

            float springFrequency = PhysicsConstraintSettings.StaticSpringFrequency;
            float dampingRatio = PhysicsConstraintSettings.StaticDampingRatio;

            var physicsJoint = PhysicsJoint.CreateFixed(
                new RigidTransform(authoring.OrientationLocal, authoring.PositionLocal),
                new RigidTransform(authoring.OrientationInConnectedEntity, authoring.PositionInConnectedEntity),
                springFrequency,
                dampingRatio
            );

            physicsJoint.SetImpulseEventThresholdAllConstraints(authoring.MaxImpulse);

            var constraintBodyPair = GetConstrainedBodyPair(authoring);

            uint worldIndex = GetWorldIndexFromBaseJoint(authoring);
            var jointEntity = CreateJointEntity(worldIndex, constraintBodyPair, physicsJoint);

            if (authoring.ExportData)
            {
                AddComponent(jointEntity, new AngularImpulseEventCapture { Value = 0.0f });
                AddComponent(jointEntity, new LinearImpulseEventCapture { Value = 0.0f });

                var captorEntity = CreateAdditionalEntity(TransformUsageFlags.None);
                var dataFrameEntity = GetEntity(authoring.DataFrame, TransformUsageFlags.None);

                AddComponent(captorEntity, new JointForcesCaptorData { Target = jointEntity });

                AddBuffer<ColumnInfo>(captorEntity);

                var columnBakingBuffer = AddBuffer<DataFrameColumnBaking>(captorEntity);
                columnBakingBuffer.Add(new DataFrameColumnBaking(dataFrameEntity, "xForce - Constraint" + authoring.name + authoring.ConnectedBody.name));
                columnBakingBuffer.Add(new DataFrameColumnBaking(dataFrameEntity, "yForce - Constraint" + authoring.name + authoring.ConnectedBody.name));
                columnBakingBuffer.Add(new DataFrameColumnBaking(dataFrameEntity, "zForce - Constraint" + authoring.name + authoring.ConnectedBody.name));
                columnBakingBuffer.Add(new DataFrameColumnBaking(dataFrameEntity, "xTorque - Constraint" + authoring.name + authoring.ConnectedBody.name));
                columnBakingBuffer.Add(new DataFrameColumnBaking(dataFrameEntity, "yTorque - Constraint" + authoring.name + authoring.ConnectedBody.name));
                columnBakingBuffer.Add(new DataFrameColumnBaking(dataFrameEntity, "zTorque - Constraint" + authoring.name + authoring.ConnectedBody.name));
            }
        }
    }
}
