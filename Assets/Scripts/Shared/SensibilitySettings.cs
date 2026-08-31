using System;
using UnityEngine;

public static class SensibilitySettings
{
    private const string SENSIBILITY_KEY = "MouseSensibility";

    public static event Action<float> OnSensibilityChanged;

    public static float Current { get; private set; } = PlayerPrefs.GetFloat(SENSIBILITY_KEY, 0.5f);

    public static void Set(float value)
    {
        Current = value;
        PlayerPrefs.SetFloat(SENSIBILITY_KEY, value);
        OnSensibilityChanged?.Invoke(value);
    }
}

