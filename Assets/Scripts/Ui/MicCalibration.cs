using System;
using System.Collections.Generic;
using Components.Sound;
using Network;
using UnityEngine;

namespace Ui
{
    public enum MicTier
    {
        Whisper,
        Talk,
        Shout
    }

    /// <summary>
    /// The arithmetic of the mic setup, apart from its sheet: judges each five-second attempt at a
    /// whisper, normal talking or a shout, says how far to move the mic level before the next one, and
    /// at the end picks the level that puts the three where the game tells them apart.
    ///
    /// <para>Everything is on Vivox's AudioEnergy, 0..1 — the number PlayerMicReporter and
    /// RemoteVoiceFilter compare with the VoiceProfile thresholds. The VoiceProfile's calibration of the
    /// heard voice puts AudioEnergy on a straight line in dB (118 dB from 0 to 1), so a mic level of
    /// G dB moves every energy by G / 118. Each attempt is kept as the voice's level at 0 dB, which lets
    /// attempts made at different levels be compared and combined.</para>
    /// </summary>
    public class MicCalibration
    {
        public enum Outcome
        {
            Confirmed,
            NotHeard,
            TooQuiet,
            TooLoud
        }

        public readonly struct Attempt
        {
            public readonly MicTier Tier;
            public readonly int Volume;
            public readonly int VoiceSamples;
            public readonly float Level;
            public readonly float Floor;
            public readonly Outcome Outcome;

            public Attempt(MicTier tier, int volume, int voiceSamples, float level, float floor, Outcome outcome)
            {
                Tier = tier;
                Volume = volume;
                VoiceSamples = voiceSamples;
                Level = level;
                Floor = floor;
                Outcome = outcome;
            }

            public bool Heard => Outcome != Outcome.NotHeard;
        }

        /// <summary>What the sheet does after an attempt.</summary>
        public readonly struct Verdict
        {
            public readonly Outcome Outcome;
            public readonly float Level;
            public readonly int NextVolume;

            /// <summary>The level wanted to move but a limit stopped it: the player has to change instead.</summary>
            public readonly bool Blocked;

            public Verdict(Outcome outcome, float level, int nextVolume, bool blocked)
            {
                Outcome = outcome;
                Level = level;
                NextVolume = nextVolume;
                Blocked = blocked;
            }
        }

        // Voice is anything this far above the room's noise floor: about 7 dB, a usual voice activity margin.
        public const float VOICE_MARGIN = 0.06f;

        // An attempt nobody was heard in raises the level by this: twice the amplitude, 0,05 of energy.
        public const int NOT_HEARD_STEP = 6;

        // A level that was heard moves straight to where it should be, but never by less than a step
        // worth repeating, nor by more than one estimate from five seconds of voice can be trusted with.
        public const int MIN_CORRECTION = 3;
        public const int MAX_CORRECTION = 15;

        // Five-second attempts at Vivox's five readings a second: how many readings of voice are enough
        // to judge (Needed) and how few mean the player was not heard at all (MinHeard). A whispered
        // sentence is mostly pauses and a shout is short.
        private static readonly int[] Needed = { 6, 8, 3 };
        private static readonly int[] MinHeard = { 3, 4, 2 };

        // Which reading of the attempt stands for it. The game judges every reading on its own, so a
        // whisper is as loud as its louder syllables, talking is its median and a shout its strong part.
        private static readonly float[] LevelPercentile = { 0.8f, 0.5f, 0.75f };

        private readonly float _speech;
        private readonly float _shout;
        private readonly float _decibelsPerEnergy;
        private readonly List<Attempt> _attempts = new();

        public MicCalibration(VoiceProfileSO profile)
        {
            _speech = profile.SpeechEnergy;
            _shout = profile.ShoutEnergy;

            // How many dB of mic level move AudioEnergy by 1, from the VoiceProfile's calibration.
            float perDecibel = profile.EnergyFromHeardLevel(0f) - profile.EnergyFromHeardLevel(-1f);
            _decibelsPerEnergy = perDecibel > 0f ? 1f / perDecibel : 118f;
        }

        public float SpeechEnergy => _speech;
        public float ShoutEnergy => _shout;
        public float DecibelsPerEnergy => _decibelsPerEnergy;
        public float TalkCenter => (_speech + _shout) * 0.5f;
        public IReadOnlyList<Attempt> Attempts => _attempts;

        public static int NeededSamples(MicTier tier) => Needed[(int)tier];

        public void Clear() => _attempts.Clear();

        /// <summary>
        /// Whether the readings so far already settle the attempt, so it can end before its five
        /// seconds: enough voice, at the right level.
        /// </summary>
        public bool IsConfirmed(MicTier tier, List<float> voice)
        {
            if (voice.Count < Needed[(int)tier]) return false;

            return Classify(tier, Percentile(voice, LevelPercentile[(int)tier])) == Outcome.Confirmed;
        }

        /// <summary>
        /// Judges a finished attempt, records it and says which mic level the next one should be at.
        /// </summary>
        public Verdict Judge(MicTier tier, int volume, List<float> voice, float floor)
        {
            if (voice.Count < MinHeard[(int)tier])
            {
                Record(new Attempt(tier, volume, voice.Count, 0f, floor, Outcome.NotHeard));
                return Move(tier, volume, NOT_HEARD_STEP, Outcome.NotHeard, 0f);
            }

            float level = Percentile(voice, LevelPercentile[(int)tier]);
            Outcome outcome = Classify(tier, level);

            Record(new Attempt(tier, volume, voice.Count, level, floor, outcome));

            if (outcome == Outcome.Confirmed) return new Verdict(outcome, level, volume, false);

            // Moves to just inside the band, not to its middle: the other two still have to fit, and
            // the final fit is what centres them.
            float target = tier switch
            {
                MicTier.Whisper => _speech - 0.06f,
                MicTier.Talk => outcome == Outcome.TooQuiet ? _speech + 0.06f : _shout - 0.06f,
                _ => _shout + 0.04f
            };

            int change = Mathf.RoundToInt((target - level) * _decibelsPerEnergy);
            change = Math.Sign(change) * Mathf.Clamp(Mathf.Abs(change), MIN_CORRECTION, MAX_CORRECTION);

            return Move(tier, volume, change, outcome, level);
        }

        private Verdict Move(MicTier tier, int volume, int change, Outcome outcome, float level)
        {
            int highest = MicSettings.MAX_AUTOMATIC_BOOST;

            // A shout is the one asked for last and the least important: the level may only rise for it
            // as far as the whisper and the talking already confirmed stay where they are.
            if (tier == MicTier.Shout && change > 0)
            {
                if (TryRaw(MicTier.Whisper, out float whisper))
                    highest = Mathf.Min(highest, Mathf.FloorToInt((_speech - 0.02f - whisper) * _decibelsPerEnergy));
                if (TryRaw(MicTier.Talk, out float talk))
                    highest = Mathf.Min(highest, Mathf.FloorToInt((_shout - 0.04f - talk) * _decibelsPerEnergy));
            }

            int next = change > 0
                ? Mathf.Min(volume + change, Mathf.Max(highest, volume))
                : Mathf.Max(volume + change, MicSettings.MIN_INPUT_VOLUME);

            return new Verdict(outcome, level, next, next == volume);
        }

        private Outcome Classify(MicTier tier, float level)
        {
            return tier switch
            {
                MicTier.Whisper => level < _speech - 0.02f ? Outcome.Confirmed : Outcome.TooLoud,
                MicTier.Talk => level < _speech + 0.04f ? Outcome.TooQuiet
                    : level > _shout - 0.04f ? Outcome.TooLoud
                    : Outcome.Confirmed,
                _ => level >= _shout ? Outcome.Confirmed : Outcome.TooQuiet
            };
        }

        private void Record(Attempt attempt)
        {
            _attempts.Add(attempt);

            Debug.Log($"[MicSetup] {attempt.Tier} at {FormatDb(attempt.Volume)}: {attempt.VoiceSamples} readings of voice, " +
                      $"level {attempt.Level:0.000}, floor {attempt.Floor:0.000} -> {attempt.Outcome}.");
        }

        /// <summary>
        /// The tier's level at a mic level of 0 dB: from the attempt that confirmed it — the one the game
        /// recognised as that tier — or else the median of the attempts it was heard in.
        /// </summary>
        public bool TryRaw(MicTier tier, out float raw)
        {
            List<float> heard = new();

            for (int i = _attempts.Count - 1; i >= 0; i--)
            {
                Attempt attempt = _attempts[i];
                if (attempt.Tier != tier || !attempt.Heard) continue;

                float atZero = attempt.Level - attempt.Volume / _decibelsPerEnergy;

                if (attempt.Outcome == Outcome.Confirmed)
                {
                    raw = atZero;
                    return true;
                }

                heard.Add(atZero);
            }

            raw = Percentile(heard, 0.5f);
            return heard.Count > 0;
        }

        /// <summary>Where the tier lands at <paramref name="volume"/>, or false if it was never heard.</summary>
        public bool TryPredict(MicTier tier, int volume, out float energy)
        {
            bool known = TryRaw(tier, out float raw);
            energy = raw + volume / _decibelsPerEnergy;
            return known;
        }

        /// <summary>The loudest the room was in any attempt, for the advice about a whisper nobody hears.</summary>
        public float Floor()
        {
            float floor = 0f;
            foreach (Attempt attempt in _attempts) floor = Mathf.Max(floor, attempt.Floor);
            return floor;
        }

        /// <summary>
        /// The suggested mic level: the whole dB, up to Vivox's advised boost, that best puts normal
        /// talking well inside the speech band, the whisper under it and the shout over it. One level
        /// moves all three together, so when a voice's whisper and shout are too close for both, a
        /// weighted compromise decides: talking first (it is what everyone hears all match), the whisper
        /// next (staying unheard by the monster is the mechanic), the shout last (a shout in the game is
        /// usually louder than one read off a sheet). Within the room left, talking is centred.
        /// </summary>
        public int Suggest(int fallback)
        {
            if (!TryRaw(MicTier.Talk, out float talk)) return fallback;

            bool hasWhisper = TryRaw(MicTier.Whisper, out float whisper);
            bool hasShout = TryRaw(MicTier.Shout, out float shout);
            float audible = Floor() + VOICE_MARGIN;

            int best = fallback;
            float bestCost = float.MaxValue;

            for (int volume = MicSettings.MIN_INPUT_VOLUME; volume <= MicSettings.MAX_AUTOMATIC_BOOST; volume++)
            {
                float shift = volume / _decibelsPerEnergy;
                float t = talk + shift;

                float cost = 8f * Square(Mathf.Max(0f, _speech + 0.05f - t))
                             + 8f * Square(Mathf.Max(0f, t - (_shout - 0.05f)))
                             + 0.5f * Square(t - TalkCenter);

                if (hasWhisper)
                {
                    float w = whisper + shift;
                    cost += 6f * Square(Mathf.Max(0f, w - (_speech - 0.03f)));
                    // Teammates should still hear it up close.
                    cost += 1f * Square(Mathf.Max(0f, audible - w));
                }

                if (hasShout) cost += 1f * Square(Mathf.Max(0f, _shout + 0.02f - (shout + shift)));

                if (cost < bestCost - 1e-7f)
                {
                    bestCost = cost;
                    best = volume;
                }
            }

            Debug.Log($"[MicSetup] At 0 dB: whisper {(hasWhisper ? whisper.ToString("0.000") : "not heard")}, talk {talk:0.000}, " +
                      $"shout {(hasShout ? shout.ToString("0.000") : "not heard")} ({_decibelsPerEnergy:0} dB per energy) " +
                      $"-> suggested {FormatDb(best)}.{ObservedSlope()}");

            return best;
        }

        /// <summary>
        /// The real-voice check of the 118 dB per energy: a tier heard at two different mic levels tells
        /// how far its energy actually moved per dB.
        /// </summary>
        private string ObservedSlope()
        {
            string text = string.Empty;

            foreach (MicTier tier in new[] { MicTier.Whisper, MicTier.Talk, MicTier.Shout })
            {
                Attempt? low = null;
                Attempt? high = null;

                foreach (Attempt attempt in _attempts)
                {
                    if (attempt.Tier != tier || !attempt.Heard) continue;
                    if (low == null || attempt.Volume < low.Value.Volume) low = attempt;
                    if (high == null || attempt.Volume > high.Value.Volume) high = attempt;
                }

                // Closer than this, how differently the sentence was said outweighs the mic level.
                if (low == null || high.Value.Volume - low.Value.Volume < 6) continue;

                float energy = high.Value.Level - low.Value.Level;
                text += Mathf.Abs(energy) < 0.005f
                    ? $" {tier}: no change over {high.Value.Volume - low.Value.Volume} dB."
                    : $" {tier}: {(high.Value.Volume - low.Value.Volume) / energy:0} dB per energy observed.";
            }

            return text;
        }

        public static float Percentile(List<float> values, float fraction)
        {
            if (values.Count == 0) return 0f;

            List<float> sorted = new(values);
            sorted.Sort();

            return sorted[Mathf.Clamp(Mathf.RoundToInt(fraction * (sorted.Count - 1)), 0, sorted.Count - 1)];
        }

        public static string FormatDb(int decibels) => decibels > 0 ? $"+{decibels} dB" : $"{decibels} dB";

        private static float Square(float value) => value * value;
    }
}
