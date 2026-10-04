using UnityEngine;

namespace Audio
{
    /// <summary>
    /// Linear gain on a remote player's voice tap, applied on the audio thread.
    ///
    /// <para>AudioSource.volume stops at 1, so it can turn a voice down but never up — the right half
    /// of the roster's volume slider did nothing. This is what lets a player boost someone who talks
    /// quietly. Vivox's own local volume never reaches the tap, which carries the stream exactly as it
    /// arrived, so the two never stack.</para>
    ///
    /// <para>Has to sit after the Vivox tap on the GameObject, which AddComponent guarantees.</para>
    /// </summary>
    public class VoiceGain : MonoBehaviour
    {
        // Above this a boosted sample is bent down smoothly instead of hard-clipping at the output.
        private const float SOFT_CLIP_KNEE = 0.9f;

        // Written on the main thread, read on the audio thread.
        private volatile float _targetGain = 1f;
        private float _currentGain = 1f;

        public float Gain
        {
            get => _targetGain;
            set => _targetGain = Mathf.Max(0f, value);
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            float target = _targetGain;
            float start = _currentGain;
            int frames = data.Length / channels;

            if (frames == 0) return;

            bool boosting = start > 1f || target > 1f;

            for (int frame = 0; frame < frames; frame++)
            {
                // Ramped across the buffer, so dragging the slider doesn't zipper.
                float gain = start + (target - start) * ((frame + 1f) / frames);
                int offset = frame * channels;

                for (int channel = 0; channel < channels; channel++)
                {
                    float sample = data[offset + channel] * gain;
                    data[offset + channel] = boosting ? SoftClip(sample) : sample;
                }
            }

            _currentGain = target;
        }

        private static float SoftClip(float sample)
        {
            float magnitude = sample < 0f ? -sample : sample;
            if (magnitude <= SOFT_CLIP_KNEE) return sample;

            float headroom = 1f - SOFT_CLIP_KNEE;
            float bent = SOFT_CLIP_KNEE + headroom * (float)System.Math.Tanh((magnitude - SOFT_CLIP_KNEE) / headroom);

            return sample < 0f ? -bent : bent;
        }
    }
}
