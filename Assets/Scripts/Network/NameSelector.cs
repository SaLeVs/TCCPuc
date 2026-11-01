using System.Collections;
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
        
        [SerializeField] private AudioSource signAudioSource;
        [SerializeField] private AudioClip signClip;
        [SerializeField] private float signSceneLoadDelay = 2.5f;
        
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
                errorMessageText.text = $"O nome precisa ter pelo menos {minCharacterLength} caracteres.";
                return;
            }

            if (playerName.Length > maxCharacterLength)
            {
                errorMessage.SetActive(true);
                errorMessageText.text = $"O nome pode ter no máximo {maxCharacterLength} caracteres.";
                return;
            }

            errorMessage.SetActive(false);
            PlayerPrefs.SetString(PLAYER_NAME_KEY, playerName);
            StartCoroutine(SignContractAndLoad());
        }

        private IEnumerator SignContractAndLoad()
        {
            selectNameButton.interactable = false;
            nameInputField.interactable = false;

            if (signAudioSource != null && signClip != null)
                signAudioSource.PlayOneShot(signClip);

            yield return new WaitForSeconds(signSceneLoadDelay);

            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
        }
        
    }
}

