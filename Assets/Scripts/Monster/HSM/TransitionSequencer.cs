using System.Collections.Generic;
using Interfaces;
using System;

namespace Monster.HSM
{
    public class TransitionSequencer
    {
        public readonly StateMachine StateMachine;
        private ISequence _sequencer;
        
        private Action _nextPhase;
        (State from, State to)? _pending; // Save state for dont broke state

        private State _lastStateFrom;
        private State _lastSateTo;
        
        
        public TransitionSequencer(StateMachine stateMachine)
        {
            StateMachine = stateMachine;
        }

        public void RequestTransition(State from, State to)
        {
            // StateMachine.ChangeState(from, to);
            if(to == null || from == null) return;

            if (_sequencer != null)
            {
                _pending = (from, to);
                return;
            }
            
            BeginTransition(from, to);
            
        }

        private void BeginTransition(State from, State to)
        {
            // Deactivate an old branch
            _sequencer = new NoopPhase();
            _sequencer.StartSequence();
            
            _nextPhase = () =>
            {
                StateMachine.ChangeState(from, to);
                _sequencer = new NoopPhase();
                _sequencer.StartSequence();
            };
            
        }

        private void EndTransition()
        {
            _sequencer = null;

            if (_pending.HasValue)
            {
                (State from, State to) currentPending = _pending.Value;
                _pending = null;
                BeginTransition(currentPending.from, currentPending.to);
            }
            
        }

        public void Tick(float deltaTime)
        {
            if (_sequencer != null)
            {
                if (_sequencer.UpdateSequence())
                {
                    if(_nextPhase != null)
                    {
                        Action nextPhase = _nextPhase;
                        _nextPhase = null;
                        nextPhase();
                    }
                    else
                    {
                        EndTransition();
                    }
                }
                return; // While transitioning, we don't run normal updates
            }
            
            StateMachine.InternalTick(deltaTime);
            
        }

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

            // Find the first common parent of StateA and StateB
            for(State secondState = stateB; secondState != null; secondState = secondState.ParentState)
            {
                if (allParentsInState.Contains(secondState))
                {
                    return secondState;
                }
            }
            
            // if no common ancestor found, return null
            return null;
        }
    }
}