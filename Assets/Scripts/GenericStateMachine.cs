using System;

//String is the current type for states.
//O is the type of expected output
//S is the type of States or its Accessor
namespace GenericStateMachine
{
    public interface IGenericStateMachine< /*O,*/
        S>
    {
        public S[] States
        {
            get => States;
        }
        public S CurrentState
        {
            get => CurrentState;
        }

        //<summary> change machine state and returns the new state <\summary>
        protected S TransitionState();
    }
}
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
