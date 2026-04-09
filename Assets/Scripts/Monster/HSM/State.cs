using Unity.Netcode;

namespace Monster.HSM
{
    public abstract class State
    {
        public readonly StateMachine StateMachine;
        public readonly State ParentState;
        public State ActiveChild;

        public State(StateMachine stateMachine, State parentState = null)
        {
            StateMachine = stateMachine;
            ParentState = parentState;
        }

        protected virtual State GetInitialState() => null; // When I enter, which child can be first to be active (null = this State is leaf, don't have child)
        
        protected virtual State GetTransitionState() => null; // When I want to switch state (null == I Want to switch for the current state)
        
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

        internal void Update(float deltaTime) { }
        
    }
}

