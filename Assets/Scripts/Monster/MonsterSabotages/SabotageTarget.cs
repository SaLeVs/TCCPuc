using UnityEngine;

namespace Monster.MonsterSabotages
{
    public class SabotageTarget : MonoBehaviour
    {
        [SerializeField] private SabotageType sabotageType;
        
        public SabotageType SabotageType => sabotageType;
        public bool IsSabotaged { get; private set; }
        
        public void MarkAsSabotaged() => IsSabotaged = true;
        public void Restore() => IsSabotaged = false;
    }

    public enum SabotageType
    {
        Light,
        Door,
    }
}
