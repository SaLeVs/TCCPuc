using UnityEngine;

namespace Monster.MonsterSabotages
{
    public class LightSabotage : MonoBehaviour, ISabotageable
    {
        [SerializeField] private Light lightComponent;
        
        public SabotageType SabotageType => SabotageType.Light;
        public bool IsSabotaged { get; private set; }

        public void Sabotage()
        {
            IsSabotaged = true;
            lightComponent.enabled = false;
        }

        public void Restore()
        {
            IsSabotaged = false;
            lightComponent.enabled = true;
        }
        
    }
}