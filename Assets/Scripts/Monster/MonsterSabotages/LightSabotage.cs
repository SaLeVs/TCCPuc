using UnityEngine;

namespace Monster.MonsterSabotages
{
    public class LightSabotage : Sabotage
    {
        [SerializeField] private Light light;
        
        public override SabotageType Type { get; }
        
        
        public override void Execute(SabotageTarget target, MonsterBrain brain)
        {
            light.enabled = false;
            target.MarkAsSabotaged();
        }

        public override void Restore(SabotageTarget target, MonsterBrain brain)
        {
            light.enabled = true;
            target.Restore();
        }
        
    }
}