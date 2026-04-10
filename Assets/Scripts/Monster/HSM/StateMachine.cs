using System.Collections;
using System.Collections.Generic;

namespace Monster.HSM
{
    public class StateMachine
    {
        public readonly State Root;
        public readonly TransitionSequencer Sequencer;

        public StateMachine(State root)
        {
            Root = root;
            Sequencer = new TransitionSequencer(this);
        }

        // First exit for ancestor and after enter in the target
        public void ChangeState(State from, State to)
        {
            if (from == to || from == null || to == null) return;
            
            State lastCommonAncestor = TransitionSequencer.LastCommonAncestor(from, to);
            
            // Exit current state, start in state from and finish in last common ancestor 
            for (State state = from; state != lastCommonAncestor; state = state.ParentState)
            {
                state.Exit();
            }
            
            // We use stack for invert order, we need to enter from top to bottom, but we can only go from bottom to top without stack
            Stack<State> stateStack = new Stack<State>();

            // Bottom to top until ancestor 
            for (State state = to; state != lastCommonAncestor; state = state.ParentState)
            {
                stateStack.Push(state);
            }
            
            // Top to bottom until target state "to"
            while (stateStack.Count > 0)
            {
                stateStack.Pop().Enter();
            }
        }
        
    }
}