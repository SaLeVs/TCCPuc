using System.Collections.Generic;
using Components;
using Enums;
using UnityEngine;

namespace Tutorial
{
    /// <summary>
    /// A missão principal do jogo (gravar o monstro no palco) sem o MissionManager atrás: escuta o
    /// sensor de visão do jogador no servidor, como o MissionsRecorder faz, e fica satisfeita quando
    /// Monstro e Palco estão no quadro ao mesmo tempo e perto um do outro.
    /// </summary>
    public class TutorialRecordingObjective : MonoBehaviour
    {
        [Tooltip("Distância máxima entre o monstro e o palco, igual à da missão Laerte.")]
        [SerializeField, Min(1f)] private float maxDistanceBetweenTargets = 20f;

        private readonly Dictionary<RecordableTarget, GameObject> _inFrame = new();
        private VisionSensor _sensor;

        public bool SeesMonster => _inFrame.ContainsKey(RecordableTarget.Monster);
        public bool SeesStage => _inFrame.ContainsKey(RecordableTarget.MainStage);

        public bool IsSatisfied
        {
            get
            {
                if (!_inFrame.TryGetValue(RecordableTarget.Monster, out GameObject monster) || monster == null) return false;
                if (!_inFrame.TryGetValue(RecordableTarget.MainStage, out GameObject stage) || stage == null) return false;

                return Vector3.Distance(monster.transform.position, stage.transform.position) <= maxDistanceBetweenTargets;
            }
        }

        public void Bind(VisionSensor sensor)
        {
            Unbind();

            _sensor = sensor;
            if (_sensor == null) return;

            _sensor.OnTargetEnterServer += Sensor_OnTargetEnter;
            _sensor.OnTargetExitServer += Sensor_OnTargetExit;
        }

        public void Unbind()
        {
            if (_sensor != null)
            {
                _sensor.OnTargetEnterServer -= Sensor_OnTargetEnter;
                _sensor.OnTargetExitServer -= Sensor_OnTargetExit;
            }

            _sensor = null;
            _inFrame.Clear();
        }

        private void Sensor_OnTargetEnter(GameObject target, RecordableTarget type) => _inFrame[type] = target;

        private void Sensor_OnTargetExit(GameObject target, RecordableTarget type)
        {
            if (_inFrame.TryGetValue(type, out GameObject current) && (current == target || current == null))
            {
                _inFrame.Remove(type);
            }
        }

        private void OnDisable() => Unbind();
    }
}
