using Network;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// The JAF cards a player can be dealt in an online lobby, in the order of
    /// <see cref="PlayerCards"/>' indices. The lobby slots and the pause roster both read it, so a
    /// player keeps the same card from the lobby to the end of the match.
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerCardPalette", menuName = "TCC/UI/Player Card Palette")]
    public class PlayerCardPaletteSO : ScriptableObject
    {
        public const string RESOURCE_PATH = "PlayerCardPalette";

        [Tooltip("One per colour, as many as PlayerCards.COUNT (4).")]
        [SerializeField] private Sprite[] cards = System.Array.Empty<Sprite>();

        private static PlayerCardPaletteSO _loaded;
        private static bool _triedLoading;

        public static PlayerCardPaletteSO Instance
        {
            get
            {
                if (_loaded == null && !_triedLoading)
                {
                    _triedLoading = true;
                    _loaded = Resources.Load<PlayerCardPaletteSO>(RESOURCE_PATH);
                    if (_loaded == null) Debug.LogWarning($"PlayerCardPalette: Resources/{RESOURCE_PATH} not found.");
                }

                return _loaded;
            }
        }

        public Sprite Get(int index)
        {
            if (cards == null || cards.Length == 0 || index < 0) return null;
            return cards[index % cards.Length];
        }

        /// <summary>The card dealt to <paramref name="playerId"/> in the online lobby, or null (LAN, not dealt yet).</summary>
        public static Sprite For(string playerId)
        {
            PlayerCardPaletteSO palette = Instance;
            if (palette == null) return null;

            return PlayerCards.TryGet(playerId, out int index) ? palette.Get(index) : null;
        }
    }
}
