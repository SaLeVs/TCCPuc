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
    }
}