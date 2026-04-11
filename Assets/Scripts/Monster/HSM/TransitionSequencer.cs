using System.Collections.Generic;
using System;
using System.Threading;

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
        
        private CancellationTokenSource _cancellationTokenSource;
        public readonly bool UseSequential = true; // Set false to use parallel
        
        
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
            State lastCommonAncestor = LastCommonAncestor(from, to);
            
            List<State> exitChain = StatesToExit(from, lastCommonAncestor);
            List<State> enterChain = StatesToEnter(to, lastCommonAncestor);
            
            List<PhaseStep> exitSteps = GatherPhaseSteps(exitChain, true);
            
            _sequencer = UseSequential ? new SequentialPhase(exitSteps, _cancellationTokenSource.Token) : new ParallelPhase(exitSteps, _cancellationTokenSource.Token);
            _sequencer.StartSequence();
            
            _nextPhase = () =>
            {
                StateMachine.ChangeState(from, to);
                
                List<PhaseStep> enterSteps = GatherPhaseSteps(enterChain, false);
                _sequencer = UseSequential ? new SequentialPhase(enterSteps, _cancellationTokenSource.Token) : new ParallelPhase(enterSteps, _cancellationTokenSource.Token);
                
                _sequencer.StartSequence();
            };
            
        }
        
        // States to exit: from -> up to (but without LCA)
        private static List<State> StatesToExit(State from, State lastCommonAncestor)
        {
            List<State> list = new List<State>();

            for (State state = from; state != null && state != lastCommonAncestor; state = state.ParentState)
            {
                list.Add(state);
            }
            
            return list;
        }
        
        // Create list for all phases to execute in transition
        private static List<PhaseStep> GatherPhaseSteps(List<State> chain, bool deactivate)
        {
            List<PhaseStep> phaseStepsToExecute = new List<PhaseStep>();

            for (int i = 0; i < chain.Count; i++)
            {
                IReadOnlyList<IActivity> actions = chain[i].Activities;

                for (int j = 0; j < actions.Count; j++)
                {
                    IActivity finishActions = actions[j];

                    if (deactivate)
                    {
                        if (finishActions.Mode == ActivityMode.Active)
                        {
                            phaseStepsToExecute.Add(cancellationToken => finishActions.DeactivateAsync(cancellationToken));
                        }
                    }
                    else
                    {
                        if (finishActions.Mode == ActivityMode.Inactive)
                        {
                            phaseStepsToExecute.Add(cancellationToken => finishActions.ActivateAsync(cancellationToken));
                        }
                    }
                }
            }
            
            return phaseStepsToExecute;
            
        }
        
        // States to enter: from 'to' up to (but without LCA) 
        private static List<State> StatesToEnter(State to, State lastCommonAncestor)
        {
            Stack<State> stack = new Stack<State>();

            for (State state = to; state != lastCommonAncestor; state = state.ParentState)
            {
                stack.Push(state);
            }
            
            return new List<State>(stack);
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