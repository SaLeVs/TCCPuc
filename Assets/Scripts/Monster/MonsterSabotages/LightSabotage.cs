using UnityEngine;

namespace Monster.MonsterSabotages
{
    /// <summary>
    /// Sits on the point light of a lamp and answers to the blackout. A regular lamp shines until
    /// the monster takes the lights out; an emergency lamp is the opposite, dark until then. Only
    /// the point light is ever switched: the lamp's mesh stays visible either way.
    /// </summary>
    public class LightSabotage : MonoBehaviour
    {
        [SerializeField] private Light lightComponent;

        [Tooltip("Emergency lamp: its point light stays off while the power is on and comes on while the monster has the lights out.")]
        [SerializeField] private bool emergency;

        public bool IsEmergency => emergency;

        /// <summary>True while the monster has the lights out - which is when an emergency lamp shines.</summary>
        public bool IsSabotaged { get; private set; }

        /// <summary>
        /// Whether the point light should be on as far as the blackout goes. LightDistanceCuller
        /// reads this before giving a light back, so it never relights one that is meant to be dark.
        /// </summary>
        public bool WantsLit => emergency ? IsSabotaged : !IsSabotaged;

        private void Awake()
        {
            lightComponent = GetComponent<Light>();
        }

        /// <summary>
        /// Announces itself. MonsterSabotage cannot list ahead of time the lamps inside a room
        /// prefab - they are spawned once the match is already running - so every lamp registers
        /// and the monster picks it up from there, already in the right state.
        /// </summary>
        private void OnEnable()
        {
            // Dark by default, whatever the prefab says, monster or no monster in the scene.
            if (emergency && !IsSabotaged && lightComponent != null) lightComponent.enabled = false;

            LightRegistry.Register(this);
        }

        private void OnDisable()
        {
            LightRegistry.Unregister(this);
        }

        public void SetBlackout(bool blackout)
        {
            if (IsSabotaged == blackout) return;

            IsSabotaged = blackout;
            if (lightComponent != null) lightComponent.enabled = WantsLit;
        }
    }
}
