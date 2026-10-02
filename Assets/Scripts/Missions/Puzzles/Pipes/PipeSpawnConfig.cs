using System;
using System.Collections.Generic;
using Enums;
using UnityEngine;

namespace Missions.Puzzles
{
    [Serializable]
    public class PipeSpawnConfig : SpawnConfig
    {
        public GameObject prefab;
        public List<Transform> spawnPoint;
        public List<int> correctSteps;

        // Posição encaixada vinda do grid (PipeGridResolver). Quando existe, vale no lugar do spawnPoint.
        [NonSerialized] public Pose? fittedPose;
    }
}