using System;
using System.Collections;
using System.Collections.Generic;
using Enums;
using Inputs;
using ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Player.Chat
{
    public class ChatUi : MonoBehaviour
    {
        public event Action<bool> OnChatVisibilityChanged;
        
        [SerializeField] private ChatManager chatManager;
        [SerializeField] private InputReader inputReader;
        [SerializeField] private Transform chatMessageHolder;
        [SerializeField] private GameObject chatMessagePrefab;
        [SerializeField] private int activeMessagesCount = 5;
        
        [Header("Message format")]
        [Tooltip("Placeholders: {icon}, {color}, {viewer} and {message}. {icon} turns into an " +
                 "inline <sprite> tag, or into nothing when the message rolled no icon. {color} " +
                 "turns into the hex drawn for that viewer.")]
        [SerializeField, TextArea] private string messageFormat = "{icon}<b><color={color}>{viewer}:</color></b> {message}";

        [Header("Message icons")]
        [Tooltip("Chance a message shows an icon at all. Most of a real chat has no badge, so " +
                 "keeping this well below 1 reads a lot more like the real thing.")]
        [SerializeField, Range(0f, 1f)] private float iconChance = 0.35f;

        [Tooltip("On: a viewer always draws the same icon, like a badge they own. Off: every " +
                 "message rerolls, so the same name flickers between icons.")]
        [SerializeField] private bool iconPerViewer = true;

        [Tooltip("Sprite indices allowed in the roll. Empty rolls over every sprite in the sprite " +
                 "asset assigned to the message prefab's TMP component.")]
        [SerializeField] private List<int> iconPool = new();

        [Header("Name colors")]
        [Tooltip("Palette {color} draws from. Every viewer keeps the same color all session. " +
                 "Leave empty and names render in the message prefab's own color.")]
        [SerializeField] private ChatColorPaletteSO nameColors;
        
        [SerializeField] private RectTransform panelRectTransform;
        [SerializeField] private float slideDuration = 0.25f;
        [SerializeField] private AnimationCurve slideCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private CanvasGroup panelCanvasGroup;
        
        [SerializeField] private Image handleIcon;

        private List<TextMeshProUGUI> _pool = new();
        private List<GameObject> _poolRoots = new();
        private int _currentIndex = 0;
        private int _spriteCount;

        /// <summary>FNV offset basis, nudged so the color roll lands elsewhere than the icon roll.</summary>
        private const uint ColorHashSeed = 2654435769u;

        private Vector2 _shownAnchoredPosition;
        private Vector2 _hiddenAnchoredPosition;
        private Coroutine _slideCoroutine;
        private bool _isChatVisible = true;


        private void Awake()
        {
            if (SceneManager.GetActiveScene().name != nameof(Scenes.Game)) return;

            if (panelRectTransform == null) panelRectTransform = (RectTransform)transform;

            _shownAnchoredPosition = panelRectTransform.anchoredPosition;

            float direction = panelRectTransform.pivot.x >= 0.5f ? 1f : -1f;
            float hideDistance = panelRectTransform.rect.width;

            _hiddenAnchoredPosition = _shownAnchoredPosition + new Vector2(direction * hideDistance, 0f);

            UpdateHandleVisual(_isChatVisible);

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
        /// Reads how many sprites the message prefab's sprite asset holds, so the roll covers the
        /// whole sheet without anyone keeping a count in sync by hand.
        /// </summary>
        private void CacheIconCount()
        {
            if (_pool.Count > 0 && _pool[0].spriteAsset != null)
            {
                _spriteCount = _pool[0].spriteAsset.spriteCharacterTable.Count;
            }

            if (_spriteCount == 0 && iconPool.Count == 0 && iconChance > 0f)
            {
                Debug.LogWarning($"{nameof(ChatUi)}: the message prefab's TMP component has no sprite asset, so messages will show no icons.", this);
            }
        }

        private void OnEnable()
        {
            if (SceneManager.GetActiveScene().name != nameof(Scenes.Game)) return;

            chatManager.OnMessageSent += ChatManager_OnMessageSent;
            inputReader.OnChatEvent += ToggleChatVisibility;
        }


        private void ChatManager_OnMessageSent(string viewer, string message)
        {
            _pool[_currentIndex].text = messageFormat
                .Replace("{icon}", PickIconTag(viewer))
                .Replace("{color}", PickNameColor(viewer))
                .Replace("{viewer}", viewer)
                .Replace("{message}", message);

            _poolRoots[_currentIndex].transform.SetAsLastSibling();
            _poolRoots[_currentIndex].SetActive(true);

            _currentIndex = (_currentIndex + 1) % activeMessagesCount;
        }

        /// <summary>
        /// Rolls the inline sprite tag for one message. Returns an empty string when the message
        /// draws no icon, so "{icon}" simply disappears from the formatted line.
        /// </summary>
        private string PickIconTag(string viewer)
        {
            int optionCount = iconPool.Count > 0 ? iconPool.Count : _spriteCount;
            if (optionCount <= 0) return string.Empty;

            float roll;
            int option;

            if (iconPerViewer)
            {
                // Both the "does this viewer have an icon" roll and the icon itself come out of the
                // name, so a viewer keeps the same badge all session without storing anything.
                uint hash = StableHash(viewer);
                roll = (hash & 0xFFFF) / (float)0xFFFF;
                option = (int)((hash >> 16) % (uint)optionCount);
            }
            else
            {
                roll = UnityEngine.Random.value;
                option = UnityEngine.Random.Range(0, optionCount);
            }

            if (roll >= iconChance) return string.Empty;

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
            uint hash = StableHash(viewer, ColorHashSeed);
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

        public void ToggleChatVisibility()
        {
            SetChatVisible(!_isChatVisible);
        }

        public void SetChatVisible(bool visible)
        {
            if (_isChatVisible == visible) return;
            _isChatVisible = visible;

            if (_slideCoroutine != null) StopCoroutine(_slideCoroutine);

            if (visible)
            {
                UpdateHandleVisual(true);
            }

            _slideCoroutine = StartCoroutine(SlideChat(visible ? _shownAnchoredPosition : _hiddenAnchoredPosition, visible ? 1f : 0f, visible));

            OnChatVisibilityChanged?.Invoke(visible);
        }

        private void UpdateHandleVisual(bool chatVisible)
        {
            if (handleIcon == null) return;

            handleIcon.enabled = !chatVisible;
        }

        private IEnumerator SlideChat(Vector2 targetPosition, float targetAlpha, bool chatVisible)
        {
            Vector2 startPosition = panelRectTransform.anchoredPosition;
            float startAlpha = panelCanvasGroup != null ? panelCanvasGroup.alpha : 1f;
            float elapsed = 0f;

            while (elapsed < slideDuration)
            {
                elapsed += Time.deltaTime;
                float t = slideCurve.Evaluate(Mathf.Clamp01(elapsed / slideDuration));
                panelRectTransform.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, t);
                if (panelCanvasGroup != null) panelCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                yield return null;
            }

            panelRectTransform.anchoredPosition = targetPosition;
            if (panelCanvasGroup != null) panelCanvasGroup.alpha = targetAlpha;
            
            if (!chatVisible)
            {
                UpdateHandleVisual(false);
            }
        }


        private void OnDisable()
        {
            if (SceneManager.GetActiveScene().name != nameof(Scenes.Game)) return;

            chatManager.OnMessageSent -= ChatManager_OnMessageSent;
            inputReader.OnChatEvent -= ToggleChatVisibility;
        }

    }
}