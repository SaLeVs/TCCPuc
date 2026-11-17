using System;
using System.Collections.Generic;
using System.Reflection;
using Chat;
using Enums;
using Player.Chat;
using Unity.Netcode;
using UnityEngine;

namespace Tutorial
{
    /// <summary>
    /// A voz do tutorial dentro do chat do jogo.
    ///
    /// <para>Duas portas, nenhuma mexe no código do chat:</para>
    /// <list type="bullet">
    /// <item><see cref="Post"/> escreve uma linha direto no painel, com o nome que a gente quiser
    /// (a moderadora). Usa o evento <c>OnMessageSent</c> do <c>ChatManager</c>, que é o mesmo que o
    /// <c>ChatUi</c> escuta - por reflexão, porque um evento só pode ser disparado pela classe dona.
    /// Assim a instrução chega inteira, na ordem e na hora, sem concorrer com a fila da plateia.</item>
    /// <item><see cref="Flavor"/> entrega um pool de falas à <c>ChatDirector</c> real: ela sorteia
    /// quem fala e quando, com o ritmo do chat de verdade. É o "povo" comentando em volta da aula.</item>
    /// </list>
    /// </summary>
    public static class TutorialChat
    {
        private static ChatManager _chat;
        private static ChatDirector _director;
        private static FieldInfo _messageSentField;

        /// <summary>O chat do jogador local já existe e está pronto para receber linhas.</summary>
        public static bool IsReady => Resolve();

        public static bool Resolve()
        {
            if (_chat != null) return true;

            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || manager.LocalClient == null || manager.LocalClient.PlayerObject == null) return false;

            _chat = manager.LocalClient.PlayerObject.GetComponentInChildren<ChatManager>(true);
            if (_chat == null) return false;

            _director = _chat.GetComponent<ChatDirector>();
            _messageSentField = typeof(ChatManager).GetField("OnMessageSent", BindingFlags.Instance | BindingFlags.NonPublic);

            if (_messageSentField == null)
            {
                Debug.LogWarning("TutorialChat: ChatManager.OnMessageSent não foi encontrado por reflexão; as falas da mod vão sair pelo barramento.");
            }

            return true;
        }

        public static void Forget()
        {
            _chat = null;
            _director = null;
            _messageSentField = null;
        }

        /// <summary>Escreve uma linha no painel do chat agora, assinada por <paramref name="viewer"/>.</summary>
        public static bool Post(string viewer, string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return false;
            if (!Resolve()) return false;

            if (_messageSentField != null && _messageSentField.GetValue(_chat) is Action<string, string> handlers)
            {
                handlers.Invoke(viewer, message);
                return true;
            }

            // Sem o evento, pelo menos registra: o barramento só fala o que estiver no banco de tópicos.
            ChatStimulusBus.Raise("tutorial.linha", 0.5f, message);
            return false;
        }

        /// <summary>
        /// Falas da plateia, sorteadas e espaçadas pela diretora real do chat. Sempre passam pelo
        /// piso de viewers (a aula não pode depender de ter gente assistindo).
        /// </summary>
        public static void Flavor(IList<string> lines, int count = 1, float intensity = 0.3f, int priority = 4)
        {
            if (lines == null || lines.Count == 0 || count <= 0) return;
            if (!Resolve() || _director == null) return;

            TargetChatData pool = new TargetChatData();

            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                pool.messages.Add(new ChatMessage { message = line, allowedArchetypes = ViewerArchetype.Everyone });
            }

            if (pool.Count == 0) return;

            _director.Enqueue(pool, count, intensity, ChatMood.Idle, null, priority, true);
        }

        /// <summary>Uma fala da plateia escolhida ao acaso de um pool.</summary>
        public static void FlavorOne(IList<string> lines) => Flavor(lines, 1);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Forget();
    }
}
