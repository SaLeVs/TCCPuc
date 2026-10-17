using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LevelDesign.Toolkit
{
    /// <summary>
    /// The numbers the course says to settle before any level design: speeds, heights, reach,
    /// what blocks what. Read straight from the Player and Monster prefabs so the toolkit never
    /// disagrees with the game — tweak the prefab, rescan, and every note follows.
    /// </summary>
    [Serializable]
    public class GameMetrics
    {
        public const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";
        public const string MonsterPrefabPath = "Assets/Prefabs/Monster/Monster.prefab";

        public float walkSpeed;
        public float runSpeed;
        public float crouchSpeed;
        public float staminaMax;
        public float staminaDrainPerSecond;
        public float staminaGainPerSecond;
        public float staminaRecoverThreshold;
        public float standHeight;
        public float crouchHeight;
        public float playerRadius;
        public float maxStepHeight;
        public float interactDistance;
        public float playerHealth;
        public float flashlightRange;
        public float flashlightSeconds;

        public float monsterWalkSpeed;
        public float monsterInvestigateSpeed;
        public float monsterChaseSpeed;
        public float monsterRadius;
        public float monsterHeight;
        public float visionDistance;
        public float visionAngle;
        public float hearingRange;
        public float trackingGraceSeconds;
        public float attackDistance;
        public float attackDamage;
        public float attackCooldown;
        public float doorLockSeconds;

        public int monsterOcclusionMask;
        public int crouchCeilingMask;
        public int stepMask;
        public int interactMask;
        public string monsterOcclusionLayers;
        public string crouchCeilingLayers;
        public string stepLayers;
        public string interactLayers;

        public float SprintSeconds => staminaDrainPerSecond > 0f ? staminaMax / staminaDrainPerSecond : 0f;
        public float ExhaustedRecoverSeconds => staminaGainPerSecond > 0f ? staminaRecoverThreshold / staminaGainPerSecond : 0f;
        public float SprintGainOverChase => (runSpeed - monsterChaseSpeed) * SprintSeconds;
        public float WalkLossPerSecond => monsterChaseSpeed - walkSpeed;
        public float SecondsToBeCaughtAfterSprint => WalkLossPerSecond > 0f ? SprintGainOverChase / WalkLossPerSecond : float.PositiveInfinity;
        public int HitsToEmptyHealth => attackDamage > 0f ? Mathf.CeilToInt(playerHealth / attackDamage) : 0;

        public static GameMetrics Read()
        {
            var metrics = new GameMetrics();
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            var monster = AssetDatabase.LoadAssetAtPath<GameObject>(MonsterPrefabPath);

            if (player != null)
            {
                metrics.walkSpeed = Float(player, "PlayerMovement", "moveSpeed");
                metrics.runSpeed = metrics.walkSpeed * Float(player, "PlayerRun", "speedModifier", 1f);
                metrics.crouchSpeed = metrics.walkSpeed * Float(player, "PlayerCrouch", "speedModifier", 1f);
                metrics.staminaMax = Float(player, "PlayerRun", "staminaMax");
                metrics.staminaDrainPerSecond = Float(player, "PlayerRun", "staminaDrainPerSecond");
                metrics.staminaGainPerSecond = Float(player, "PlayerRun", "staminaGainPerSecond");
                metrics.staminaRecoverThreshold = Float(player, "PlayerRun", "staminaCooldownThreshold");
                metrics.crouchHeight = Float(player, "PlayerCrouch", "crouchHeight");
                metrics.maxStepHeight = Float(player, "PlayerStairClimber", "maxStepHeight");
                metrics.interactDistance = Float(player, "PlayerInteractor", "interactDistance");
                metrics.playerHealth = Float(player, "Health", "<MaxHealth>k__BackingField");
                metrics.crouchCeilingMask = Int(player, "PlayerCrouch", "ceilingMask");
                metrics.stepMask = Int(player, "PlayerStairClimber", "stepLayerMask");
                metrics.interactMask = Int(player, "PlayerInteractor", "layerMask");

                var capsule = player.GetComponent<CapsuleCollider>();
                if (capsule != null)
                {
                    metrics.standHeight = capsule.height;
                    metrics.playerRadius = capsule.radius;
                }

                var flashlight = player.GetComponentsInChildren<Light>(true).FirstOrDefault(l => l.type == LightType.Spot);
                if (flashlight != null) metrics.flashlightRange = flashlight.range;

                float battery = Float(player, "Flashlight", "batteryPercentMax");
                float drain = Float(player, "Flashlight", "batteryPercentDecreasePerSecond");
                metrics.flashlightSeconds = drain > 0f ? battery / drain : 0f;
            }

            if (monster != null)
            {
                metrics.monsterWalkSpeed = Float(monster, "MonsterWander", "walkSpeed");
                metrics.monsterInvestigateSpeed = Float(monster, "MonsterInvestigate", "investigateSpeed");
                metrics.monsterChaseSpeed = Float(monster, "MonsterChase", "chaseSpeed");
                metrics.visionDistance = Float(monster, "VisionSensor", "distance");
                metrics.visionAngle = Float(monster, "VisionSensor", "angle");
                metrics.hearingRange = Float(monster, "HearingSensor", "maxHearingRange");
                metrics.trackingGraceSeconds = Float(monster, "MonsterBrain", "trackingGraceSeconds");
                metrics.attackDistance = Float(monster, "MonsterAttack", "distanceToAttack");
                metrics.attackDamage = Float(monster, "MonsterAttack", "damageAmountPerAttack");
                metrics.attackCooldown = Float(monster, "MonsterAttack", "attackCooldown");
                metrics.monsterOcclusionMask = Int(monster, "VisionSensor", "occlusionLayers");

                var agent = monster.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null)
                {
                    metrics.monsterRadius = agent.radius;
                    metrics.monsterHeight = agent.height;
                }
            }

            var door = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Rooms/Props/Doors/Door_Black.prefab");
            if (door != null) metrics.doorLockSeconds = Float(door, "Door", "monsterLockSeconds");

            metrics.monsterOcclusionLayers = LayerNames(metrics.monsterOcclusionMask);
            metrics.crouchCeilingLayers = LayerNames(metrics.crouchCeilingMask);
            metrics.stepLayers = LayerNames(metrics.stepMask);
            metrics.interactLayers = LayerNames(metrics.interactMask);

            return metrics;
        }

        public string ToReadableText()
        {
            return
                $"Jogador: anda {walkSpeed:0.##} m/s · corre {runSpeed:0.##} m/s · agachado {crouchSpeed:0.##} m/s\n" +
                $"Stamina: {SprintSeconds:0.#} s de corrida · exausto volta a correr em {ExhaustedRecoverSeconds:0.#} s\n" +
                $"Corpo: {standHeight:0.##} m em pé · {crouchHeight:0.##} m agachado · degrau máx. {maxStepHeight:0.##} m · alcance de interação {interactDistance:0.##} m\n" +
                $"Vida: {playerHealth:0} · lanterna {flashlightSeconds:0} s, alcance {flashlightRange:0} m\n" +
                $"Monstro: patrulha {monsterWalkSpeed:0.##} m/s · investiga {monsterInvestigateSpeed:0.##} m/s · persegue {monsterChaseSpeed:0.##} m/s\n" +
                $"Sentidos: visão {visionDistance:0} m / {visionAngle:0}° · audição {hearingRange:0} m · segue sem ver por {trackingGraceSeconds:0.#} s\n" +
                $"Ataque: {attackDistance:0.##} m · {attackDamage:0} de dano · {attackCooldown:0.##} s de recarga · {HitsToEmptyHealth} golpes zeram a vida\n" +
                $"Perseguição: um sprint abre {SprintGainOverChase:0.#} m; andando o monstro tira {WalkLossPerSecond:0.##} m/s, alcança em {SecondsToBeCaughtAfterSprint:0.#} s\n" +
                $"Bloqueia visão do monstro: {monsterOcclusionLayers}\n" +
                $"Teto para agachar: {crouchCeilingLayers}\n" +
                $"Degraus sobem em: {stepLayers}\n" +
                $"Interação (raio): {interactLayers}";
        }

        public static string LayerNames(int mask)
        {
            var names = Enumerable.Range(0, 32)
                .Where(i => (mask & (1 << i)) != 0)
                .Select(LayerMask.LayerToName)
                .Where(n => !string.IsNullOrEmpty(n));

            return string.Join(", ", names);
        }

        private static SerializedObject Find(GameObject root, string typeName)
        {
            var component = root.GetComponentsInChildren<Component>(true)
                .FirstOrDefault(c => c != null && c.GetType().Name == typeName);

            return component != null ? new SerializedObject(component) : null;
        }

        private static float Float(GameObject root, string typeName, string field, float fallback = 0f)
        {
            var property = Find(root, typeName)?.FindProperty(field);
            if (property == null) return fallback;

            return property.propertyType == SerializedPropertyType.Integer ? property.intValue : property.floatValue;
        }

        private static int Int(GameObject root, string typeName, string field)
        {
            // LayerMask properties expose their bits through intValue too.
            return Find(root, typeName)?.FindProperty(field)?.intValue ?? 0;
        }
    }
}
