using System.Collections.Generic;

namespace Monster.HSM
{
    public abstract class State
    {
        public readonly StateMachine StateMachine;
        public readonly State ParentState;
        public State ActiveChild;
        
        private readonly List<IActivity> _activities = new List<IActivity>();
        public IReadOnlyList<IActivity> Activities => _activities;
        
        private bool _initialized;
        

        public State(StateMachine stateMachine, State parentState = null)
        {
            StateMachine = stateMachine;
            ParentState = parentState;
        }

        // We can add animations, delay or effects using activities
        public void AddActivity(IActivity activity)
        {
            if (activity != null)
            {
                _activities.Add(activity);
            }
        }

        protected virtual State GetInitialState() => null; // When I enter, which child can be first to be active (null = this State is leaf, don't have child)
        
        protected virtual State GetTransitionState() => null; // When I want to switch state (null == I Want to switch for the current state)

        protected virtual void OnInitialize() { }
        protected virtual void OnEnter() { }
        protected virtual void OnExit() { }
        protected virtual void OnUpdate(float deltaTime) { }
        
        
        // This internal void guarantee that the parent always execute first
        internal void Enter()
        {
            if (ParentState != null)
            {
                ParentState.ActiveChild = this;
            }
            
            if (!_initialized)
            {
                _initialized = true;
                OnInitialize();
            }
            
            OnEnter();
            
            State initialState = GetInitialState();

            if (initialState != null)
            {
                initialState.Enter();
            }
        }
        internal void Exit()
        {
            if (ActiveChild != null)
            {
                ActiveChild.Exit();
            }
            
            ActiveChild = null;
            OnExit();
        }
        internal void Update(float deltaTime)
        {
            State state = GetTransitionState();

            if (state != null)
            {
                StateMachine.Sequencer.RequestTransition(this, state);
                return;
            }

            if (ActiveChild != null)
            {
                ActiveChild.Update(deltaTime);
            }
            
            OnUpdate(deltaTime);
        }
        
        
        // Pass in all state and find the most deap state executing (when return null, we find the most deep)
        public State Leaf()
        {
            State state = this;

            while (state.ActiveChild != null)
            {
                state = state.ActiveChild;
            }
            
            return state;
        }
        
        // Path for this state until find the root state
        public IEnumerable<State> PathToRoot()
        {
            for(State state = this; state != null; state = state.ParentState)
            {
                yield return state;
            }
        }
    }
}

