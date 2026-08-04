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
        
        private const string PlayerNameKey = "PlayerName";

        private void Start()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);   
            }
            
            nameInputField.text = PlayerPrefs.GetString(PlayerNameKey, string.Empty);
            OnNameInputChanged();
        }
        
        public void OnNameInputChanged()
        {
            selectNameButton.interactable = nameInputField.text.Length >= minCharacterLength && nameInputField.text.Length <= maxCharacterLength;
        }

        public void Connect()
        {
            PlayerPrefs.SetString(PlayerNameKey, nameInputField.text);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);  
        }
        
    }
}

