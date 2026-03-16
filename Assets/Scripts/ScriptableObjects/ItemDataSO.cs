using UnityEngine;

namespace ScriptableObjects
{
    [CreateAssetMenu(fileName = "New Item", menuName = "ScriptableObjects/Game/ItemData")]
    public class ItemDataSO : ScriptableObject
    {
        public int itemId;
        public string itemName;
        public Sprite icon;
        public GameObject prefab;
    }
}

