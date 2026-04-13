using System.Collections.Generic;
using System.Reflection;

namespace Monster.HSM
{
    public class StateMachineBuilder
    {
        private readonly State _root;
        
        public StateMachineBuilder(State root)
        {
            _root = root;
        }

        public StateMachine Build()
        {
            StateMachine stateMachine = new StateMachine(_root);
            Wire(_root, stateMachine, new HashSet<State>()); // We use root for insert in all states
            
            return stateMachine;
        }
        
        // Reflection method for insert state machine for all states
        private void Wire(State state, StateMachine stateMachine, HashSet<State> visitedStates)
        {
            if (state == null) return;
            if (!visitedStates.Add(state)) return; // State already visited, HashSet.Add return false if state are already visited
            
            // Define flags for search in hierarchy
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;
            FieldInfo machineField = typeof(State).GetField("StateMachine", flags);

            if (machineField != null)
            {
                machineField.SetValue(state, stateMachine);
            }

            foreach (FieldInfo fields in state.GetType().GetFields(flags))
            {
                if(!typeof(State).IsAssignableFrom(fields.FieldType)) continue; // Only consider fields that are state
                if(fields.Name == "ParentState") continue; // Skip back-edge to parent
                
                State childState = (State)fields.GetValue(state);
                
                if(childState == null) continue;
                
                if(!ReferenceEquals(childState.ParentState, state)) continue; // Ensure it's a child state
                
                Wire(childState, stateMachine, visitedStates); // Recurse into child
            }
        }
    }
}