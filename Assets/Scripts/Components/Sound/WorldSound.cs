using Components.Perception;
using Unity.Netcode;
using UnityEngine;

namespace Components.Sound
{
    /// <summary>
    /// The single way into the world for a sound, so none can end up heard only on the machine that
    /// made it.
    ///
    /// <para>Call it once, from whoever decides the sound happened: the server for the monster, the
    /// doors and damage; the owner for its own footsteps. Never from inside an RPC or a
    /// NetworkVariable callback — those already run on every peer, and each copy would send the
    /// sound again.</para>
    /// </summary>
    public static class WorldSound
    {
        /// <param name="emitter">Who made it. Followed while it plays when the sound asks for that,
        /// and named as the source to the monster's hearing so it can ignore itself.</param>
        /// <param name="loudness">Scales the range — crouching and sprinting drive it for footsteps.</param>
        public static void Play(SoundDefinitionSO sound, Vector3 position, NetworkObject emitter = null, float loudness = 1f)
        {
            if (sound == null) return;

            WorldSoundNetwork network = WorldSoundNetwork.Instance;

            if (network != null && network.IsSpawned)
            {
                network.Broadcast(sound, position, emitter, loudness);
                return;
            }

            // No network running, like a test scene: this machine is everyone there is.
            PlayHere(sound, position, emitter != null ? emitter.transform : null, loudness);
        }

        /// <summary>
        /// What each peer does when a world sound reaches it: play it, and report it to the monster.
        /// Runs exactly once per sound on every machine, the server included — the only one whose
        /// HearingSensor is listening.
        /// </summary>
        internal static void PlayHere(SoundDefinitionSO sound, Vector3 position, Transform emitter, float loudness)
        {
            if (SoundPlayer.Instance != null)
            {
                SoundPlayer.Instance.PlayWorld(sound, position, loudness, emitter);
            }

            if (sound.AlertsMonster)
            {
                NoiseBus.Report(position, sound.Range * loudness, sound.NoiseType, emitter != null ? emitter.root : null);
            }
        }
    }

    /// <summary>
    /// HUD feedback: flat in this player's ears and heard by nobody else. For anything that happens
    /// in the world, use <see cref="WorldSound"/>.
    /// </summary>
    public static class UiSound
    {
        public static void Play(SoundDefinitionSO sound)
        {
            if (sound == null || SoundPlayer.Instance == null) return;

            SoundPlayer.Instance.PlayUi(sound);
        }
    }
}
