using System;
using GenericStateMachine;
using UnityEngine;

/*
 * LIGHT BASED DETECTION FOR THE FUTURE:
 * 1. do a check for any visible lights in fov
 * 2. partition each light radius in fov.
 * 3. ray-cast towards each partition
 * */
/*
 * FOV based approach:
 * 1. designate area
 * 2. ray cast
 * 3. profit
 * */
namespace StealthTest
{
    enum Phases
    {
        SEARCH,
        CHASE,
    }

    public class StealthTest : MonoBehaviour, IGenericStateMachine<Phases>
    {
        private float visualRange;
        private float ViewAngle;
        private readonly Phases[] phases = (Phases[])Enum.GetValues(typeof(Phases));
        private Phases state;

        //private alerted = false;
        Phases[] IGenericStateMachine<Phases>.States
        {
            get => phases;
        }

        Phases IGenericStateMachine<Phases>.CurrentState
        {
            get => state;
        }

        Phases IGenericStateMachine<Phases>.TransitionState() =>
            state = state switch
            {
                Phases.SEARCH => Phases.CHASE,
                Phases.CHASE => Phases.SEARCH,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(state),
                    $"Not expected direction value: {state}"
                ),
            };
    }
}
