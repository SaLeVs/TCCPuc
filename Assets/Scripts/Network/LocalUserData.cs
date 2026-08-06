using Network;
using UnityEngine;

public static class LocalUserData
{
    private const string DESCRIPTION_KEY = "PlayerDescription";

    public static UserData Load()
    {
        return new UserData
        {
            playerName = LoadPlayerName(),
            avatarIndex = LoadAvatarIndex(),
            description = PlayerPrefs.GetString(DESCRIPTION_KEY, "")
        };
    }

    private static string LoadPlayerName()
    {
        string name = PlayerPrefs.GetString(NameSelector.PLAYER_NAME_KEY, "");
        
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Player";
        }

        return name;
    }

    private static int LoadAvatarIndex()
    {
        // Integrate steam works, change for steam avatar 
        return PlayerPrefs.GetInt("AvatarIndex", 0);
    }
}