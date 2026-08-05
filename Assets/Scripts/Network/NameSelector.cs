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
        
        public const string PLAYER_NAME_KEY = "PlayerName";

        private void Start()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);   
            }
            
            nameInputField.text = PlayerPrefs.GetString(PLAYER_NAME_KEY, string.Empty);
            OnNameInputChanged();
        }
        
        public void OnNameInputChanged()
        {
            selectNameButton.interactable = nameInputField.text.Length >= minCharacterLength && nameInputField.text.Length <= maxCharacterLength;
        }

        public void Connect()
        {
            PlayerPrefs.SetString(PLAYER_NAME_KEY, nameInputField.text);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);  
        }
        
    }
}

