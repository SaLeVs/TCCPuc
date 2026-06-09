using UnityEngine;

public class LostItemsSpawn : MonoBehaviour
{
    public static LostItemsSpawn Instance { get; private set; }

    [SerializeField] private Transform[] spawnPoints;

    private int _nextIndex;

    private void Awake()
    {
        Instance = this;
    }

    public Transform GetNextSpawnPoint()
    {
        if (spawnPoints.Length == 0) return null;

        Transform point = spawnPoints[_nextIndex];
        _nextIndex = (_nextIndex + 1) % spawnPoints.Length;
        return point;
    }
}