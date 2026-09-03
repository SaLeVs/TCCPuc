namespace Interfaces
{
    public interface IHighlighted
    {
        public void Enable();
        public void Disable();
        public void EnableBlocked() => Enable();
    }
}
