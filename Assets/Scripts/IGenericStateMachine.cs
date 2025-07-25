using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//String is the current type for states.
//O is the type of expected output
public interface GenericStateMachine<O>{
  
  //<summary> get Machine state <\summary>
  public string getState();
  
  /*store required input as variable or catch it with function then use it in transitionState()
    or create an override for required input.
  */
  //why is transitionState public? Machine must be updated by anything that needs it.
  //<summary> change machine state <\summary>
  public string transitionState();

  //override doState required input if you want to do a mealy type of FSM.
  //<summary> Do whatever state entails <\summary>
  public O doState();
}
