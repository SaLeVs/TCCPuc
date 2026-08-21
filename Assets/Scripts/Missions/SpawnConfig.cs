using System;
using UnityEngine;

namespace Missions
{
    [Serializable]
    public class SpawnConfig
    {
        public GameObject[] prefabs;
        public Transform[] spawnPoints;
    }
}