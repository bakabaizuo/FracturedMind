//using System.Collections;
//using System.Collections.Generic;
using System;
using GenericStateMachine;
using UnityEngine;

namespace StealthAI
{
    enum States
    {
        SEARCH,
        CHASE,
    }

    public class StealthAI : MonoBehaviour
    {
        private float visualRange;
        private float ViewAngle;
        public Array states() => Enum.GetValues(typeof(States));
        private States currentState;
        public string getCurrentState () => currentState.ToString();
       


        private States TransitionState() =>
            currentState switch
            {
                States.SEARCH => States.CHASE,
                States.CHASE => States.SEARCH,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(currentState),
                    $"Not expected direction value: {currentState}"
                ),
            };
        private void Patrol(){

        }
        // Start is called before the first frame update
        void Start() { 

          currentState = States.SEARCH;
        }

        // Update is called once per frame
        void Update() {
          currentState =TransitionState();
        }
    }
}
