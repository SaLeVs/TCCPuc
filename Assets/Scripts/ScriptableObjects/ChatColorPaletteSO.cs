using System.Collections.Generic;
using UnityEngine;

namespace ScriptableObjects
{
    [CreateAssetMenu(fileName = "New chat color palette", menuName = "ScriptableObjects/Game/ChatColorPalette")]
    public class ChatColorPaletteSO : ScriptableObject
    {
        [Tooltip("Colors a viewer name can be drawn in. Add, remove or recolor freely - the chat " +
                 "draws out of whatever is in this list.")]
        public List<Color> colors = new();

        public int Count => colors?.Count ?? 0;

        /// <summary>
        /// "#RRGGBB" strings, cached because every message pastes one into a TMP color tag and
        /// converting the Color each time would allocate for nothing.
        /// </summary>
        private string[] _hexCache;

        private void OnEnable()
        {
            BuildHexCache();
        }

        private void OnValidate()
        {
            // Also rebuilt here so recoloring while the game runs shows on the next message.
            BuildHexCache();
        }

        private void BuildHexCache()
        {
            _hexCache = new string[Count];

            for (int i = 0; i < _hexCache.Length; i++)
            {
                _hexCache[i] = "#" + ColorUtility.ToHtmlStringRGB(colors[i]);
            }
        }

        /// <summary>Hex of the color at <paramref name="index"/>, wrapped into range. Null when empty.</summary>
        public string GetHex(int index)
        {
            if (_hexCache == null || _hexCache.Length == 0) return null;

            return _hexCache[index % _hexCache.Length];
        }
    }
}
