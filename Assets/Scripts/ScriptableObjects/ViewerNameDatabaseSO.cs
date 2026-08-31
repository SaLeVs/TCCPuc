using System.Collections.Generic;
using UnityEngine;

namespace ScriptableObjects
{
    [CreateAssetMenu(fileName = "New chat message List", menuName = "ScriptableObjects/Game/ViewerNameDatabase")]
    public class ViewerNameDatabaseSO : ScriptableObject
    {
        public List<string> viewerNames;
        private Queue<string> _queue;

        private void OnEnable()
        {
            Shuffle();
        }

        private void Shuffle()
        {
            var list = new List<string>(viewerNames);
            
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
            
            _queue = new Queue<string>(list);
        }

        public string GetNext()
        {
            if (_queue == null || _queue.Count == 0)
            {
                Shuffle();
            }

            return _queue!.Dequeue();
        }
        
    }
}