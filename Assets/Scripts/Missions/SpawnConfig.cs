using System;
using UnityEngine;

namespace Missions
{
    [Serializable]
    public class SpawnConfig
    {
        public GameObject prefab;
        public Transform spawnPoint;
    }
}