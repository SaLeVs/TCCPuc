using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Network
{
    public class NameSelector : MonoBehaviour
    {
        [SerializeField] private TMP_InputField nameInputField;
        [SerializeField] private Button selectNameButton;

        [SerializeField] private int minCharacterLength;
        [SerializeField] private int maxCharacterLength;
        
        [SerializeField] private GameObject errorMessage;
        [SerializeField] private TextMeshProUGUI errorMessageText;
        
        public const string PLAYER_NAME_KEY = "PlayerName";

        private void Start()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);   
            }
            
            nameInputField.text = PlayerPrefs.GetString(PLAYER_NAME_KEY, string.Empty);
        }

        public void Connect()
        {
            string playerName = nameInputField.text;

            if (playerName.Length < minCharacterLength)
            {
                errorMessage.SetActive(true);
                errorMessageText.text = $"The name must have at least {minCharacterLength} characters.";
                return;
            }

            if (playerName.Length > maxCharacterLength)
            {
                errorMessage.SetActive(true);
                errorMessageText.text = $"The name must have at most {maxCharacterLength} characters.";
                return;
            }

            errorMessage.SetActive(false);
            PlayerPrefs.SetString(PLAYER_NAME_KEY, playerName);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
        }
        
    }
}

