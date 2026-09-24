using System;
using Enums;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RecTimeUi : MonoBehaviour
{
    private const string Prefix = "REC: ";

    [SerializeField] private GameObject recTimePanel;
    [SerializeField] private TextMeshProUGUI recTimeInGameTxt;

    private bool _initialized;
    private float _elapsedTime;

    // The clock ticks in hundredths, so it changes almost every frame. Formatting it with string
    // interpolation made a new string each time; writing the digits into one reused buffer and
    // handing that to TMP costs nothing to the garbage collector.
    private readonly char[] _buffer = new char[24];
    private int _lastCentiseconds = -1;

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

        int totalCentiseconds = Mathf.FloorToInt(_elapsedTime * 100f);
        if (totalCentiseconds == _lastCentiseconds) return;

        _lastCentiseconds = totalCentiseconds;

        int minutes = totalCentiseconds / 6000;
        int seconds = totalCentiseconds / 100 % 60;
        int centiseconds = totalCentiseconds % 100;

        int length = 0;
        foreach (char c in Prefix) _buffer[length++] = c;

        length = WriteNumber(minutes, length);
        _buffer[length++] = ':';
        length = WriteNumber(seconds, length);
        _buffer[length++] = ':';
        length = WriteNumber(centiseconds, length);

        recTimeInGameTxt.SetCharArray(_buffer, 0, length);
    }

    /// <summary>At least two digits, like the "00" format it replaces.</summary>
    private int WriteNumber(int value, int index)
    {
        int digits = value >= 100 ? (int)Math.Floor(Math.Log10(value)) + 1 : 2;

        for (int i = digits - 1; i >= 0; i--)
        {
            _buffer[index + i] = (char)('0' + value % 10);
            value /= 10;
        }

        return index + digits;
    }
}
