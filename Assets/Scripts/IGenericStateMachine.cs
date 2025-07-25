using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//String is the current type for states.
//O is the type of expected output
public interface IGenericStateMachine<T>{
  
  //<summary> get Machine state <\summary>
  public string getState();
  
  //<summary> change machine state <\summary>
  private string transitionState();
  /*
   * If State transition needs input, do any of the following:
   *      1. Catch it with a function
   *      2. Save it as an attribute
   *      3. Overload this with required inputs as parameters 
   *         BUT
   *         Always implement this going to the default state.
   *
  */
  /* 
   * If this function must be called beyond the machine, 
   * wrap it with a function with the needed modifier.
   * i.e. public void forceStateChange(){
   *  this.transitionState();
   * }
   * */

  //<summary> Do whatever state entails <\summary>
  public T doState();
  //override doState required input if you want to do a mealy type of FSM.
}
