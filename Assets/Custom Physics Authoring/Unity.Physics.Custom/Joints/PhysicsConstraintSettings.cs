using System.Collections;
using System.Collections.Generic;
using System.Data;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Authoring;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PhysicsStepAuthoring))]
[RequireComponent(typeof(PhysicsDeltaTimeAuthoring))]
public class PhysicsConstraintSettings : MonoBehaviour
{
    public static float StaticSpringFrequency = Unity.Physics.Constraint.DefaultSpringFrequency;
    public static float StaticDampingRatio = Unity.Physics.Constraint.DefaultDampingRatio;

    public bool SetDefaults = false;
    public bool UseTauAndDamping = true;

    public float SpringFrequency = Unity.Physics.Constraint.DefaultSpringFrequency;
    public float DampingRatio = Unity.Physics.Constraint.DefaultDampingRatio;
    public float Tau;
    public float Damping;
}


public class PhysicsConstraintSettingsBaker : Baker<PhysicsConstraintSettings>
{
    public override void Bake(PhysicsConstraintSettings authoring)
    {
        int solverIterations = authoring.GetComponentInParent<PhysicsStepAuthoring>().SolverIterationCount;
        float timeStep = authoring.GetComponentInParent<PhysicsDeltaTimeAuthoring>().FixedTimeStep;
        if (authoring.SetDefaults)
        {
            authoring.SpringFrequency = Unity.Physics.Constraint.DefaultSpringFrequency;
            authoring.DampingRatio = Unity.Physics.Constraint.DefaultDampingRatio;

            JacobianUtilities.CalculateConstraintTauAndDamping(authoring.SpringFrequency, authoring.DampingRatio, 0.02f, solverIterations, out authoring.Tau, out authoring.Damping);
            authoring.UseTauAndDamping = false;
        }
        else if (authoring.UseTauAndDamping)
        {
            JacobianUtilities.CalculateSpringFrequencyAndDamping(authoring.Tau, authoring.Damping, timeStep, solverIterations, out authoring.SpringFrequency, out authoring.DampingRatio);
        }
        else
        {
            JacobianUtilities.CalculateConstraintTauAndDamping(authoring.SpringFrequency, authoring.DampingRatio, timeStep, solverIterations, out authoring.Tau, out authoring.Damping);
        }

        PhysicsConstraintSettings.StaticSpringFrequency = authoring.SpringFrequency; 
        PhysicsConstraintSettings.StaticDampingRatio = authoring.DampingRatio;
    }
}