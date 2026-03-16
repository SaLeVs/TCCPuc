using UnityEngine;

namespace ScriptableObjects
{
    [CreateAssetMenu(fileName = "New List", menuName = "ScriptableObjects/Game/ItemList")]
    public class ItemListSO : ScriptableObject
    {
        public ItemDataSO[] items;

        public ItemDataSO GetItem(int id)
        {
            return items[id];
        }
    }
}