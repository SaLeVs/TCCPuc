using System.Collections.Generic;

namespace Monster.HSM
{
    public class TransitionSequencer
    {
        public readonly StateMachine StateMachine;

        public TransitionSequencer(StateMachine stateMachine)
        {
            StateMachine = stateMachine;
        }

        public void RequestTransition(State from, State to) { }

        /// <summary>
        /// Compute the last common ancestor of two states
        /// </summary>
        /// <param name="stateA">First state</param>
        /// <param name="stateB">Second state</param>
        /// <returns></returns>
        public static State LastCommonAncestor(State stateA, State stateB)
        {
            HashSet<State> allParentsInState = new HashSet<State>();
            
            for(State firstState = stateA; firstState != null; firstState = firstState.ParentState)
            {
                allParentsInState.Add(firstState);
            }

            
            for(State secondState = stateB; secondState != null; secondState = secondState.ParentState)
            {
                if (allParentsInState.Contains(secondState))
                {
                    return secondState;
                }
            }
        }
    }
}