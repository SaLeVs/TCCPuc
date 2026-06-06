using System;
using System.Collections.Generic;
using Enums;

namespace Missions
{
    [Serializable]
    public class PipeSpawnConfig : SpawnConfig
    {
        public List<int> correctSteps;
    }
}