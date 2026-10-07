using System.Collections.Generic;
using UnityEngine;

namespace Components.Sound
{
    [CreateAssetMenu(fileName = "SoundLibrary", menuName = "ScriptableObjects/Audio/Sound Library")]
    public class SoundLibrarySO : ScriptableObject
    {
        [SerializeField] private SoundDefinitionSO[] sounds;

        private Dictionary<SoundDefinitionSO, ushort> _ids;

        public bool TryGetId(SoundDefinitionSO sound, out ushort id)
        {
            _ids ??= BuildIds();

            return _ids.TryGetValue(sound, out id);
        }

        public SoundDefinitionSO Get(ushort id)
        {
            if (sounds == null || id >= sounds.Length) return null;

            return sounds[id];
        }

        private Dictionary<SoundDefinitionSO, ushort> BuildIds()
        {
            var ids = new Dictionary<SoundDefinitionSO, ushort>();

            if (sounds == null) return ids;

            for (int i = 0; i < sounds.Length && i <= ushort.MaxValue; i++)
            {
                if (sounds[i] != null) ids.TryAdd(sounds[i], (ushort)i);
            }

            return ids;
        }

        // Edited in the inspector, or survived a play session with domain reload off: rebuild on use.
        private void OnEnable() => _ids = null;
        private void OnValidate() => _ids = null;

#if UNITY_EDITOR
        [ContextMenu("Collect every Sound Definition in the project")]
        private void CollectAll()
        {
            var found = new List<SoundDefinitionSO>();

            foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:" + nameof(SoundDefinitionSO)))
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var sound = UnityEditor.AssetDatabase.LoadAssetAtPath<SoundDefinitionSO>(path);

                if (sound != null) found.Add(sound);
            }

            found.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            sounds = found.ToArray();
            _ids = null;

            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
