using System;
using UnityEngine;

namespace Enums
{
    [Serializable]
    public class PipeSpawnConfig
    {
        public PipeType pipeType;
        public Transform spawnPoint;
    }
}