using System;
using Enums;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RecTimeUi : MonoBehaviour
{
    [SerializeField] private GameObject recTimePanel;
    [SerializeField] private TextMeshProUGUI recTimeInGameTxt;

    private bool _initialized;
    private float _elapsedTime;

    private void Start()
    {
        if (SceneManager.GetActiveScene().name == nameof(Scenes.Game))
        {
            recTimePanel.SetActive(true);
            _initialized = true;
            _elapsedTime = 0f;
        }
        else
        {
            recTimePanel.SetActive(false);
        }
    }

    private void Update()
    {
        if (!_initialized) return;

        _elapsedTime += Time.deltaTime;

        int minutes = Mathf.FloorToInt(_elapsedTime / 60);
        int seconds = Mathf.FloorToInt(_elapsedTime % 60);
        int centiseconds = Mathf.FloorToInt((_elapsedTime * 100) % 100);

        recTimeInGameTxt.text = $"REC: {minutes:00}:{seconds:00}:{centiseconds:00}";
    }
    
    
}
