using System;
using System.Collections.Generic;
using Enums;
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
        
        private List<TextMeshProUGUI> _pool = new();
        private int _currentIndex = 0;
        
        
        private void Awake()
        {
            if (SceneManager.GetActiveScene().name != nameof(Scenes.Game)) return;

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
            if (SceneManager.GetActiveScene().name != nameof(Scenes.Game)) return;
            
            chatManager.OnMessageSent += ChatManager_OnMessageSent;
            Debug.Log("Chat: Add the listener");
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
            
            if (SceneManager.GetActiveScene().name != nameof(Scenes.Game)) return;
            
            chatManager.OnMessageSent -= ChatManager_OnMessageSent;
            Debug.Log("Chat: Remove the listener");
        }
        
    }
}

