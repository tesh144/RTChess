#pragma warning disable CS0414, CS0219, CS0618
using UnityEngine;

/// <summary>
/// Generates a procedural ambient music clip for the title screen.
/// Creates a soft, ethereal pad sound using layered sine waves with slow
/// volume modulation — completely distinct from the gameplay lobby/battle tracks.
///
/// Usage: Call TitleMusicGenerator.Generate() to get an AudioClip.
/// MusicSystem uses this automatically when no titleTrack is assigned.
/// </summary>
public static class TitleMusicGenerator
{
    /// <summary>
    /// Generates a loopable ambient pad AudioClip.
    /// </summary>
    /// <param name="durationSeconds">Length of the generated clip (default 16s for a smooth loop).</param>
    /// <param name="sampleRate">Audio sample rate (default 44100).</param>
    public static AudioClip Generate(float durationSeconds = 16f, int sampleRate = 44100)
    {
        int totalSamples = Mathf.RoundToInt(durationSeconds * sampleRate);
        float[] data = new float[totalSamples];

        // Chord: C major 7 spread across octaves (C2, E3, G3, B3, C4, E4)
        // These frequencies create a warm, dreamy ambient pad
        float[] frequencies = new float[]
        {
            65.41f,   // C2  — deep bass foundation
            164.81f,  // E3  — warm third
            196.00f,  // G3  — fifth
            246.94f,  // B3  — major seventh (dreamy quality)
            261.63f,  // C4  — octave
            329.63f,  // E4  — high third (shimmer)
        };

        // Each voice has slightly different volume and detune for richness
        float[] volumes = new float[] { 0.15f, 0.12f, 0.12f, 0.10f, 0.08f, 0.06f };
        float[] detune = new float[] { 0.0f, 0.3f, -0.2f, 0.5f, -0.4f, 0.6f }; // Hz offset for chorus effect

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / sampleRate;
            float sample = 0f;

            for (int v = 0; v < frequencies.Length; v++)
            {
                float freq = frequencies[v] + detune[v];

                // Soft sine wave (the core tone)
                float sine = Mathf.Sin(2f * Mathf.PI * freq * t);

                // Slow tremolo (volume wobble) — different rate per voice for movement
                float tremoloRate = 0.08f + v * 0.03f; // 0.08 to 0.23 Hz
                float tremolo = 0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * tremoloRate * t);

                sample += sine * volumes[v] * tremolo;
            }

            // Gentle overall swell — breathes in and out over the clip duration
            float breathRate = 1f / durationSeconds; // One full breath per loop
            float breath = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * breathRate * t);
            sample *= breath;

            // Soft limiting to prevent clipping
            sample = Mathf.Clamp(sample, -0.9f, 0.9f);

            // Fade in/out at the edges for seamless looping (0.5s crossfade)
            float fadeLength = 0.5f * sampleRate;
            if (i < fadeLength)
                sample *= (float)i / fadeLength;
            else if (i > totalSamples - fadeLength)
                sample *= (float)(totalSamples - i) / fadeLength;

            data[i] = sample;
        }

        // Simple low-pass smoothing to remove harshness (moving average, 3 samples)
        float[] smoothed = new float[totalSamples];
        smoothed[0] = data[0];
        smoothed[totalSamples - 1] = data[totalSamples - 1];
        for (int i = 1; i < totalSamples - 1; i++)
        {
            smoothed[i] = (data[i - 1] + data[i] * 2f + data[i + 1]) * 0.25f;
        }

        AudioClip clip = AudioClip.Create("TitleAmbient", totalSamples, 1, sampleRate, false);
        clip.SetData(smoothed, 0);
        return clip;
    }
}
