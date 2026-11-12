using System;
using UnityEngine;

namespace Ui
{
    /// <summary>
    /// Plays the microphone straight back to the player, a few tens of milliseconds behind them, at
    /// the mic level they are setting. The mic setup used to send them to Vivox's echo channel for
    /// this, and that is a trip to the server and back: the voice came home about a second late,
    /// which makes it hard to speak at all, let alone judge the level.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class MicMonitor : MonoBehaviour
    {
        // How far playback stays behind the recording: enough that it never catches up and reads
        // samples not yet written, little enough not to be heard as an echo.
        private const float LatencySeconds = 0.06f;

        private AudioSource _source;
        private Func<AudioClip> _clip;
        private Func<int> _position;

        // Read on the audio thread.
        private volatile float _gain = 1f;

        public bool Listening { get; set; }

        public static MicMonitor Create(Func<AudioClip> clip, Func<int> position)
        {
            var go = new GameObject(nameof(MicMonitor));
            go.AddComponent<AudioSource>();

            MicMonitor monitor = go.AddComponent<MicMonitor>();
            monitor._clip = clip;
            monitor._position = position;
            return monitor;
        }

        /// <summary>The mic level, in dB: the same gain Vivox puts on the voice for everyone else.</summary>
        public void SetLevel(int decibels) => _gain = Mathf.Pow(10f, decibels / 20f);

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = true;
            _source.spatialBlend = 0f;
            _source.bypassEffects = true;
            _source.bypassListenerEffects = true;
            _source.bypassReverbZones = true;
        }

        private void Update()
        {
            AudioClip clip = Listening ? _clip?.Invoke() : null;

            if (clip == null)
            {
                if (_source.isPlaying) _source.Stop();
                _source.clip = null;
                return;
            }

            int length = clip.samples;
            int position = _position();
            int latency = Mathf.RoundToInt(clip.frequency * LatencySeconds);
            if (position < 0 || latency * 4 >= length) return;

            if (_source.clip != clip)
            {
                if (_source.isPlaying) _source.Stop();
                _source.clip = clip;
            }

            if (!_source.isPlaying)
            {
                _source.timeSamples = Wrap(position - latency, length);
                _source.Play();
                return;
            }

            // Recording and playback run on separate clocks and drift apart. Put playback back where
            // it belongs before it falls far behind or overtakes the recording.
            int behind = Wrap(position - _source.timeSamples, length);
            if (behind < latency / 4 || behind > latency * 4) _source.timeSamples = Wrap(position - latency, length);
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            float gain = _gain;

            for (int i = 0; i < data.Length; i++)
            {
                data[i] = Mathf.Clamp(data[i] * gain, -1f, 1f);
            }
        }

        private static int Wrap(int sample, int length) => ((sample % length) + length) % length;
    }
}
