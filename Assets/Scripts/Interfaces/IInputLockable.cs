namespace Interfaces
{
    public interface IInputLockable
    {
        void SetInputLocked(InputLockReason reason, bool locked);
    }
}
