using System.Collections.Generic;
using System.Text;
using Unity.Services.Lobbies.Models;

namespace Network
{
    /// <summary>
    /// The JAF card colour each player of an online lobby was dealt. The host deals them at random,
    /// one colour per player, and keeps the deal in the lobby's data, so every machine in the lobby
    /// reads the same colours; the match keeps them, since the lobby it started from stays known
    /// until the end screen. Players are keyed by their Unity Authentication id, which is also their
    /// lobby player id and their Vivox id.
    /// </summary>
    public static class PlayerCards
    {
        /// <summary>Lobby data key, Member visibility: "playerId=index;playerId=index".</summary>
        public const string LOBBY_KEY = "PlayerCards";

        /// <summary>How many colours there are to deal: one per seat of the lobby.</summary>
        public const int COUNT = 4;

        private static readonly Dictionary<string, int> Last = new Dictionary<string, int>();
        private static string _lastLobbyId;

        /// <summary>
        /// The colour dealt to <paramref name="playerId"/>, 0..COUNT-1. Read from the lobby this
        /// machine is in, or from the last one it was in (a LAN match has none: false).
        /// </summary>
        public static bool TryGet(string playerId, out int index)
        {
            index = -1;
            if (string.IsNullOrEmpty(playerId)) return false;

            Refresh(Lobby.FindQuietly()?.JoinedLobby);

            return Last.TryGetValue(playerId, out index);
        }

        /// <summary>Takes the deal of <paramref name="lobby"/>, whenever it is known.</summary>
        public static void Refresh(Unity.Services.Lobbies.Models.Lobby lobby)
        {
            if (lobby == null) return;

            if (lobby.Id != _lastLobbyId)
            {
                Last.Clear();
                _lastLobbyId = lobby.Id;
            }

            if (lobby.Data == null || !lobby.Data.TryGetValue(LOBBY_KEY, out DataObject data)) return;

            Last.Clear();
            foreach (KeyValuePair<string, int> pair in Parse(data.Value)) Last[pair.Key] = pair.Value;
        }

        public static Dictionary<string, int> Parse(string value)
        {
            var cards = new Dictionary<string, int>();
            if (string.IsNullOrEmpty(value)) return cards;

            foreach (string entry in value.Split(';'))
            {
                int split = entry.IndexOf('=');
                if (split <= 0) continue;

                if (int.TryParse(entry.Substring(split + 1), out int index) && index >= 0 && index < COUNT)
                {
                    cards[entry.Substring(0, split)] = index;
                }
            }

            return cards;
        }

        public static string Format(Dictionary<string, int> cards)
        {
            var text = new StringBuilder();

            foreach (KeyValuePair<string, int> pair in cards)
            {
                if (text.Length > 0) text.Append(';');
                text.Append(pair.Key).Append('=').Append(pair.Value);
            }

            return text.ToString();
        }

        /// <summary>
        /// The deal for the players now in <paramref name="lobby"/>: whoever already has a colour
        /// keeps it, whoever left gives theirs back, and whoever is new draws one of the colours
        /// nobody has. Null when nothing changes.
        /// </summary>
        public static string Deal(Unity.Services.Lobbies.Models.Lobby lobby, System.Random random)
        {
            Dictionary<string, int> current = lobby.Data != null && lobby.Data.TryGetValue(LOBBY_KEY, out DataObject data)
                ? Parse(data.Value)
                : new Dictionary<string, int>();

            var dealt = new Dictionary<string, int>();
            var taken = new HashSet<int>();

            // Kept in join order, so two players holding the same colour (never dealt, but the data is
            // anyone's to write) resolve the same way on every deal: the first one keeps it.
            foreach (Player player in lobby.Players)
            {
                if (current.TryGetValue(player.Id, out int index) && taken.Add(index)) dealt[player.Id] = index;
            }

            foreach (Player player in lobby.Players)
            {
                if (dealt.ContainsKey(player.Id)) continue;

                var free = new List<int>();
                for (int i = 0; i < COUNT; i++)
                {
                    if (!taken.Contains(i)) free.Add(i);
                }

                // More players than colours cannot happen with four seats; repeat one if it ever does.
                int drawn = free.Count > 0 ? free[random.Next(free.Count)] : random.Next(COUNT);
                taken.Add(drawn);
                dealt[player.Id] = drawn;
            }

            if (current.Count == dealt.Count && Matches(current, dealt)) return null;

            return Format(dealt);
        }

        private static bool Matches(Dictionary<string, int> a, Dictionary<string, int> b)
        {
            foreach (KeyValuePair<string, int> pair in a)
            {
                if (!b.TryGetValue(pair.Key, out int other) || other != pair.Value) return false;
            }

            return true;
        }
    }
}
