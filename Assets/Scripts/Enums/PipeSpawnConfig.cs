using System;
using System.Collections.Generic;
using UnityEngine;

namespace Enums
{
    [Serializable]
    public class PipeSpawnConfig
    {
        public PipeType pipeType;
        public Transform spawnPoint;
        public List<int> correctSteps;
    }
}