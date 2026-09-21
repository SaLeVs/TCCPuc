using System.Collections.Generic;
using Enums;
using ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Player.Chat
{
    public class ChatUi : MonoBehaviour
    {
        [SerializeField] private ChatManager chatManager;
        [SerializeField] private Transform chatMessageHolder;
        [SerializeField] private GameObject chatMessagePrefab;
        [SerializeField] private int activeMessagesCount = 5;
        
        // Same Odin quirk as ChatManager: a [Header] on a field that also carries a Unity
        // PropertyDrawer attribute (TextArea, Range, Min) gets drawn twice. Each title sits on a
        // plain field, and the decorated ones follow underneath.

        [Header("Message format")]
        [SerializeField]
        [Tooltip("Placeholders: {icon}, {color}, {viewer} and {message}")]
        private string messageFormat = "{icon}<b><color={color}>{viewer}:</color></b> {message}";

        [Header("Message icons")]
        [SerializeField]
        [Tooltip("On: a viewer always draws the same icon, like a badge they own")]
        private bool iconPerViewer = true;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Chance a message shows an icon at all")]
        private float iconAppearChance = 0.35f;

        [SerializeField]
        [Tooltip("Sprite indices allowed in the roll. Empty rolls over the whole sprite asset")]
        private List<int> iconPool = new();

        [Header("Name colors")]
        [SerializeField] private ChatColorPaletteSO nameColors;

        private List<TextMeshProUGUI> _pool = new();
        private List<GameObject> _poolRoots = new();
        private int _currentIndex = 0;
        private int _spriteCount;

        /// <summary>FNV offset basis, nudged so the color roll lands elsewhere than the icon roll.</summary>
        private const uint COLOR_HASH_SEED = 2654435769u;


        private void Awake()
        {
            if (SceneManager.GetActiveScene().name != nameof(Scenes.Game)) return;

            for (int i = 0; i < activeMessagesCount; i++)
            {
                GameObject instance = Instantiate(chatMessagePrefab, chatMessageHolder);
                instance.SetActive(false);

                if (instance.GetComponentInChildren<TextMeshProUGUI>() is TextMeshProUGUI childText)
                {
                    _pool.Add(childText);
                    _poolRoots.Add(instance);
                }
            }

            CacheIconCount();
        }

        /// <summary>
        /// Reads how many sprites the message prefab's sprite asset holds
        /// </summary>
        private void CacheIconCount()
        {
            if (_pool.Count > 0 && _pool[0].spriteAsset != null)
            {
                _spriteCount = _pool[0].spriteAsset.spriteCharacterTable.Count;
            }

            if (_spriteCount == 0 && iconPool.Count == 0 && iconAppearChance > 0f)
            {
                Debug.LogWarning($"{nameof(ChatUi)}: the message prefab's TMP component has no sprite asset, so messages will show no icons.", this);
            }
        }

        private void OnEnable()
        {
            if (SceneManager.GetActiveScene().name != nameof(Scenes.Game)) return;

            chatManager.OnMessageSent += ChatManager_OnMessageSent;
        }


        private void ChatManager_OnMessageSent(string viewer, string message)
        {
            _pool[_currentIndex].text = messageFormat.Replace("{icon}", PickIconTag(viewer)).Replace("{color}", PickNameColor(viewer)).Replace("{viewer}", viewer).Replace("{message}", message);

            _poolRoots[_currentIndex].transform.SetAsLastSibling();
            _poolRoots[_currentIndex].SetActive(true);

            _currentIndex = (_currentIndex + 1) % activeMessagesCount;
        }
        
        private string PickIconTag(string viewer)
        {
            int optionCount = iconPool.Count > 0 ? iconPool.Count : _spriteCount;
            if (optionCount <= 0) return string.Empty;

            float roll;
            int option;

            if (iconPerViewer)
            {
                // Both the "does this viewer have an icon" roll and the icon itself come out of the
                uint hash = StableHash(viewer);
                roll = (hash & 0xFFFF) / (float)0xFFFF;
                option = (int)((hash >> 16) % (uint)optionCount);
            }
            else
            {
                roll = Random.value;
                option = Random.Range(0, optionCount);
            }

            if (roll >= iconAppearChance) return string.Empty;

            return $"<sprite={(iconPool.Count > 0 ? iconPool[option] : option)}> ";
        }

        /// <summary>
        /// Hex the viewer's name is drawn in. Falls back to white so "{color}" never expands into
        /// a broken tag when no palette is assigned.
        /// </summary>
        private string PickNameColor(string viewer)
        {
            if (nameColors == null || nameColors.Count == 0) return "#FFFFFF";

            // Seeded apart from the icon roll, otherwise name color and badge would move together
            // and every viewer wearing badge N would also be wearing color N.
            uint hash = StableHash(viewer, COLOR_HASH_SEED);
            return nameColors.GetHex((int)(hash % (uint)nameColors.Count)) ?? "#FFFFFF";
        }

        /// <summary>
        /// FNV-1a. string.GetHashCode isn't guaranteed to be stable between runs, this is.
        /// </summary>
        private static uint StableHash(string value, uint seed = 2166136261u)
        {
            uint hash = seed;

            for (int i = 0; i < value.Length; i++)
            {
                hash ^= value[i];
                hash *= 16777619u;
            }

            return hash;
        }

        private void OnDisable()
        {
            if (SceneManager.GetActiveScene().name != nameof(Scenes.Game)) return;

            chatManager.OnMessageSent -= ChatManager_OnMessageSent;
        }

    }
}