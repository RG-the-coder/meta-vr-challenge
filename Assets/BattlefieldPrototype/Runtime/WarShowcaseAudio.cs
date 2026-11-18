#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using Unity.Collections;
using UnityEngine;

namespace BattlefieldPrototype
{
    /// <summary>
    /// Optional, frame-synchronous capture of Unity's actual audio mix for the
    /// showcase. Set Time.captureFramerate before Start, call CaptureFrame once
    /// for every recorded video frame, and Finish when video capture ends.
    /// </summary>
    public static class WarShowcaseAudio
    {
        static BinaryWriter writer;
        static NativeArray<float> samples;
        static byte[] pcm;
        static bool ownsRenderer;
        static long dataBytes;

        public static bool IsRecording => writer != null && ownsRenderer;
        public static int SampleRate { get; private set; }
        public static int Channels { get; private set; }
        public static long SampleFramesWritten => Channels > 0 ? dataBytes / (Channels * 2) : 0;
        public static string OutputPath { get; private set; }
        public static string LastError { get; private set; }

        /// <summary>Starts a PCM16 WAV. Failure leaves video recording usable.</summary>
        public static bool Start(string path)
        {
            Finish();
            LastError = null;
            dataBytes = 0;
            try
            {
                if (Time.captureFramerate <= 0)
                    throw new InvalidOperationException("Set Time.captureFramerate before starting showcase audio.");
                OutputPath = Path.GetFullPath(path);
                var directory = Path.GetDirectoryName(OutputPath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                SampleRate = AudioSettings.outputSampleRate;
                Channels = ChannelCount(AudioSettings.GetConfiguration().speakerMode);
                if (SampleRate <= 0)
                    throw new InvalidOperationException("Unity did not provide an output sample rate.");

                // If another recorder owns AudioRenderer, leave its state intact.
                ownsRenderer = AudioRenderer.Start();
                if (!ownsRenderer)
                    throw new InvalidOperationException("Unity AudioRenderer is unavailable or already recording.");
                writer = new BinaryWriter(new FileStream(OutputPath, FileMode.Create, FileAccess.Write, FileShare.Read));
                WriteHeader();
                Debug.Log($"SHOWCASE_AUDIO started {SampleRate} Hz, {Channels} channels: {OutputPath}");
                return true;
            }
            catch (Exception error)
            {
                Fail("start", error);
                return false;
            }
        }

        /// <summary>Renders the samples belonging to one captured Unity frame.</summary>
        public static bool CaptureFrame()
        {
            if (!IsRecording) return false;
            try
            {
                int frameCount = AudioRenderer.GetSampleCountForCaptureFrame();
                if (frameCount <= 0) return true;
                int sampleCount = checked(frameCount * Channels);
                if (!samples.IsCreated || samples.Length != sampleCount)
                {
                    if (samples.IsCreated) samples.Dispose();
                    samples = new NativeArray<float>(sampleCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                    pcm = new byte[checked(sampleCount * 2)];
                }
                if (!AudioRenderer.Render(samples))
                    throw new InvalidOperationException("Unity could not render the audio frame.");
                if (dataBytes + pcm.Length > uint.MaxValue - 36L)
                    throw new IOException("The PCM WAV reached its RIFF size limit.");
                for (int i = 0; i < samples.Length; i++)
                {
                    float value = samples[i];
                    if (float.IsNaN(value) || float.IsInfinity(value)) value = 0;
                    value = Mathf.Clamp(value, -1f, 1f);
                    short quantized = (short)Mathf.RoundToInt(value * (value < 0 ? 32768f : 32767f));
                    pcm[2 * i] = (byte)(quantized & 255);
                    pcm[2 * i + 1] = (byte)((quantized >> 8) & 255);
                }
                writer.Write(pcm);
                dataBytes += pcm.Length;
                return true;
            }
            catch (Exception error)
            {
                Fail("capture", error);
                return false;
            }
        }

        /// <summary>Finalizes the WAV header and restores normal Unity playback.</summary>
        public static void Finish()
        {
            if (ownsRenderer)
            {
                ownsRenderer = false;
                try { AudioRenderer.Stop(); }
                catch (Exception error) { Debug.LogWarning("SHOWCASE_AUDIO stop: " + error.Message); }
            }
            if (samples.IsCreated) samples.Dispose();
            samples = default;
            pcm = null;
            if (writer == null) return;
            var completedWriter = writer;
            writer = null;
            try
            {
                completedWriter.Flush();
                completedWriter.BaseStream.Position = 4;
                completedWriter.Write((uint)(36 + dataBytes));
                completedWriter.BaseStream.Position = 40;
                completedWriter.Write((uint)dataBytes);
                completedWriter.Flush();
                Debug.Log($"SHOWCASE_AUDIO finished {SampleFramesWritten} sample frames ({SampleFramesWritten / (double)SampleRate:F3}s): {OutputPath}");
            }
            catch (Exception error)
            {
                LastError = "finalize: " + error.Message;
                Debug.LogWarning("SHOWCASE_AUDIO " + LastError);
            }
            finally { completedWriter.Dispose(); }
        }

        static void Fail(string stage, Exception error)
        {
            LastError = stage + ": " + error.Message;
            Debug.LogWarning("SHOWCASE_AUDIO disabled; video capture can continue. " + LastError);
            Finish();
        }

        static int ChannelCount(AudioSpeakerMode mode)
        {
            switch (mode)
            {
                case AudioSpeakerMode.Mono: return 1;
                case AudioSpeakerMode.Quad: return 4;
                case AudioSpeakerMode.Surround: return 5;
                case AudioSpeakerMode.Mode5point1: return 6;
                case AudioSpeakerMode.Mode7point1: return 8;
                default: return 2; // Stereo and matrix-encoded Prologic.
            }
        }

        static void WriteHeader()
        {
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write((uint)36);
            writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write((uint)16);
            writer.Write((ushort)1);
            writer.Write((ushort)Channels);
            writer.Write((uint)SampleRate);
            writer.Write((uint)(SampleRate * Channels * 2));
            writer.Write((ushort)(Channels * 2));
            writer.Write((ushort)16);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write((uint)0);
        }
    }
}
#endif
