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
        private List<GameObject> _poolRoots = new();
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
                    _pool.Add(childText);
                    _poolRoots.Add(instance);
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
            _pool[_currentIndex].text = $"<b>{viewer}:</b> {message}";
            
            _poolRoots[_currentIndex].transform.SetAsLastSibling();
            _poolRoots[_currentIndex].SetActive(true);

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

