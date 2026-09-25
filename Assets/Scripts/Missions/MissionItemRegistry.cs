using System.Collections.Generic;
using Missions.Puzzles;
using UnityEngine;

namespace Missions
{
    public class MissionItemRegistry : MonoBehaviour
    {
        public static MissionItemRegistry Instance { get; private set; }

        private readonly Dictionary<int, PuzzleManagerBase> _itemIdToManager = new();

        private void Awake() => Instance = this;

        public void Register(int itemId, PuzzleManagerBase manager)
        {
            _itemIdToManager[itemId] = manager;
        }

        public bool TryGetManager(int itemId, out PuzzleManagerBase manager)
        {
            if (_itemIdToManager.TryGetValue(itemId, out manager) && manager != null)
                return true;

            _itemIdToManager.Remove(itemId);
            manager = null;
            return false;
        }
    } 
}

