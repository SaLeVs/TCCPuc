using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Player.Chat
{
    public class ChatUi : MonoBehaviour
    {
        [SerializeField] private ChatManager chatManager;
        [SerializeField] private Transform chatMessageHolder;
        [SerializeField] private GameObject chatMessagePrefab;
        [SerializeField] private int activeMessagesCount = 5;
        
        private List<TextMeshProUGUI> _pool = new();
        private int _currentIndex = 0;
        
        
        private void Awake()
        {
            for (int i = 0; i < activeMessagesCount; i++)
            {
                GameObject instance = Instantiate(chatMessagePrefab, chatMessageHolder);
                instance.SetActive(false);

                if (instance.GetComponentInChildren<TextMeshProUGUI>() is TextMeshProUGUI childText)
                {
                    Debug.Log("Chat: Add to pool");
                    _pool.Add(childText);
                }
            }
        }
        
        private void OnEnable()
        {
            Debug.Log("Chat: Add the listener");
            chatManager.OnMessageSent += ChatManager_OnMessageSent;
        }
        
        
        private void ChatManager_OnMessageSent(string viewer, string message)
        {
            TextMeshProUGUI slot = _pool[_currentIndex];

            slot.text = $"<b>{viewer}:</b> {message}";
            slot.gameObject.SetActive(true);
            Debug.Log("Chat: Show message");
            _currentIndex = (_currentIndex + 1) % activeMessagesCount;
        }
        
        
        private void OnDisable()
        {
            Debug.Log("Chat: Remove the listener");
            chatManager.OnMessageSent -= ChatManager_OnMessageSent;
        }
        
    }
}

