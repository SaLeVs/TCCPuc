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

    }
}