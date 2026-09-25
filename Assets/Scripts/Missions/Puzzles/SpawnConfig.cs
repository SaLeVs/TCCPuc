using System;
using UnityEngine;

namespace Missions.Puzzles
{
    [Serializable]
    public class SpawnConfig
    {
        public GameObject[] prefabs;
        public Transform[] spawnPoints;
    }
}
