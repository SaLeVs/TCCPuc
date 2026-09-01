using System;
using System.Collections;
using System.Collections.Generic;
using Enums;
using Inputs;
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
        
        [SerializeField] private RectTransform panelRectTransform;
        [SerializeField] private float slideDuration = 0.25f;
        [SerializeField] private AnimationCurve slideCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private CanvasGroup panelCanvasGroup;
        
        [SerializeField] private Image handleIcon;

        private List<TextMeshProUGUI> _pool = new();
        private List<GameObject> _poolRoots = new();
        private int _currentIndex = 0;

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
        }

        private void OnEnable()
        {
            if (SceneManager.GetActiveScene().name != nameof(Scenes.Game)) return;

            chatManager.OnMessageSent += ChatManager_OnMessageSent;
            inputReader.OnChatEvent += ToggleChatVisibility;
        }


        private void ChatManager_OnMessageSent(string viewer, string message)
        {
            _pool[_currentIndex].text = $"<b>{viewer}:</b> {message}";

            _poolRoots[_currentIndex].transform.SetAsLastSibling();
            _poolRoots[_currentIndex].SetActive(true);

            _currentIndex = (_currentIndex + 1) % activeMessagesCount;
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