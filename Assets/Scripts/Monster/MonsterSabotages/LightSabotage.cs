using System;
using UnityEngine;

namespace Monster.MonsterSabotages
{
    public class LightSabotage : MonoBehaviour, ISabotageable
    {
        [SerializeField] private Light lightComponent;
        
        public SabotageType SabotageType => SabotageType.Light;
        public bool IsSabotaged { get; private set; }

        private void Awake()
        {
            lightComponent = GetComponent<Light>();
        }

        /// <summary>
        /// Announces itself the way DoorSabotage does. MonsterSabotage's serialized list can only
        /// hold lamps placed by hand in the scene, so every lamp inside a room prefab - spawned
        /// once the match is already running - used to stay lit through a blackout. Registering
        /// here covers those, and the ones nobody remembered to add to the list.
        /// </summary>
        private void OnEnable()
        {
            SabotageRegistry.Register(this);
        }

        private void OnDisable()
        {
            SabotageRegistry.Unregister(this);
        }

        public void Sabotage()
        {
            IsSabotaged = true;
            if (lightComponent != null) lightComponent.enabled = false;
        }

        public void Restore()
        {
            IsSabotaged = false;
            if (lightComponent != null) lightComponent.enabled = true;
        }
        
    }
}