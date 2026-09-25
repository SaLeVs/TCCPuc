using System.Collections.Generic;
using UnityEngine;

namespace Missions.Puzzles
{
    // Puzzle "coloque o item certo em cada slot" (ex.: caixas com formas).
    public class ItemSlotPuzzleManager : PuzzleManagerBase
    {
        [SerializeField] private List<SpawnConfig> totemConfigs;
        [SerializeField] private List<SpawnConfig> pickableConfigs;


        protected override void SpawnPuzzle()
        {
            SpawnSlots();
            SpawnPickables();
        }

        private void SpawnSlots()
        {
            foreach (SpawnConfig config in totemConfigs)
            {
                foreach ((GameObject prefab, Transform spawnPoint) in SpawnUtility.GenerateSpawnAssignments(config))
                {
                    SpawnPiece<ItemSlotTotem>(prefab, spawnPoint);
                }
            }
        }

        private void SpawnPickables()
        {
            foreach (SpawnConfig config in pickableConfigs)
            {
                foreach ((GameObject prefab, Transform spawnPoint) in SpawnUtility.GenerateSpawnAssignments(config))
                {
                    GameObject spawned = SpawnTracked(prefab, spawnPoint);

                    if (spawned.TryGetComponent(out IMissionOwnerAware ownerAware))
                    {
                        ownerAware.BindToPuzzle(this);
                        MissionItemRegistry.Instance?.Register(ownerAware.ItemId, this);
                    }
                }
            }
        }

    }
}
