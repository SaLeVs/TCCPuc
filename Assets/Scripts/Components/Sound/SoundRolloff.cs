using UnityEngine;

namespace Components.Sound
{
    /// <summary>
    /// The one distance curve every world sound uses, keyed in fractions of the sound's range.
    ///
    /// <para>Unity evaluates a custom rolloff at distance / maxDistance, so a single curve serves any
    /// range: a footstep and a door slam share the shape and only stretch it differently.</para>
    ///
    /// <para>The shape is the inverse-distance law real sound follows — every doubling of distance
    /// costs about 6 dB — flat inside a near field, and faded to exactly zero at the range. Pure
    /// 1/r never reaches zero, and Unity's logarithmic mode even stops attenuating at maxDistance,
    /// which is how every sound used to stay faintly audible across the whole map.</para>
    ///
    /// <para>The near field is a fraction of the range, so a louder sound is louder at every
    /// distance and not only audible further: heard from the same spot, two sounds differ by
    /// 20·log10(rangeA / rangeB) dB, the way real sources of different power do.</para>
    /// </summary>
    public static class SoundRolloff
    {
        private const int Samples = 32;

        /// <param name="nearField">Fraction of the range that plays at full volume.</param>
        public static AnimationCurve Build(float nearField)
        {
            nearField = Mathf.Clamp(nearField, 0.01f, 0.9f);

            var times = new float[Samples + 1];
            var values = new float[Samples + 1];

            for (int i = 0; i <= Samples; i++)
            {
                // Squared spacing puts most keys near the source, where the curve bends hardest.
                float t = (float)i / Samples;
                times[i] = t * t;
                values[i] = Evaluate(times[i], nearField);
            }

            var keys = new Keyframe[Samples + 1];

            for (int i = 0; i <= Samples; i++)
            {
                // Tangents from the neighbours: AnimationCurve's automatic smoothing overshoots
                // between keys this uneven, and a rolloff must never rise with distance.
                int previous = Mathf.Max(0, i - 1);
                int next = Mathf.Min(Samples, i + 1);
                float slope = (values[next] - values[previous]) / Mathf.Max(1e-5f, times[next] - times[previous]);

                keys[i] = new Keyframe(times[i], values[i], slope, slope);
            }

            return new AnimationCurve(keys);
        }

        /// <summary>Gain at <paramref name="fraction"/> of the range.</summary>
        public static float Evaluate(float fraction, float nearField)
        {
            if (fraction >= 1f) return 0f;

            float inverseDistance = fraction <= nearField ? 1f : nearField / fraction;

            // (1 - x²)² leaves the near and middle distances almost untouched and closes smoothly on
            // zero, instead of cutting the tail off at the range.
            float window = 1f - fraction * fraction;

            return inverseDistance * window * window;
        }
    }
}
