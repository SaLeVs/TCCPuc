using UnityEngine;

namespace Monster.MonsterSabotages
{
    public class LightSabotage : MonoBehaviour, ISabotageable
    {
        [SerializeField] private Light light;
        
        public SabotageType SabotageType => SabotageType.Light;
        public bool IsSabotaged { get; private set; }

        public void Sabotage()
        {
            IsSabotaged = true;
            light.enabled = false;
        }

        public void Restore()
        {
            IsSabotaged = false;
            light.enabled = true;
        }
        
    }
}