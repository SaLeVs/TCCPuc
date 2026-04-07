using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class PlayerLobbySlot : NetworkBehaviour
    {
        [SerializeField] private TextMeshProUGUI playerNameText;
        [SerializeField] private TextMeshProUGUI[] playerInfos;
        [SerializeField] private Image playerImage;
        [SerializeField] private Image playerReadyImage;
        
        
    }
}

