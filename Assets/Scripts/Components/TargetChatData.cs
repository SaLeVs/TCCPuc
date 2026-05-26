using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chat
{
    [Serializable]
    public class ChatMessage
    {
        [TextArea(2, 4)]
        public string message;

        [Min(0f)]
        public float weight = 1f;
    }

    [Serializable]
    public class TargetChatData
    {
        public List<ChatMessage> messages;

        public string GetWeightedRandom()
        {
            float total = 0f;
            
            foreach (ChatMessage entry in messages)
            {
                total += entry.weight;
            }

            float roll = UnityEngine.Random.Range(0f, total);
            float cumulative = 0f;

            foreach (ChatMessage entry in messages)
            {
                cumulative += entry.weight;
                
                if (roll <= cumulative)
                {
                    return entry.message;
                }
            }

            return messages[^1].message;
        }
    }
}