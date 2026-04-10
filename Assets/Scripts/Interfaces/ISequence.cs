namespace Interfaces
{
    public interface ISequence
    { 
        public bool IsDone { get; }
        public void StartSequence();
        public bool UpdateSequence();
    }
}