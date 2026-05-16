using System;
using System.Threading;
using System.Diagnostics;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NAudio.CoreAudioApi;
using NAudio.Extras;
using NAudio.Vorbis;
using NAudio.CoreAudioApi.Interfaces;
using VarispeedDemo.SoundTouch;

namespace MinorShift.Emuera.Runtime.Utils
{
    internal class AudioDeviceTracker : IMMNotificationClient
    {
        private readonly SynchronizationContext syncContext;
        private MMDeviceEnumerator enumerator;
        public MMDevice Device { get; private set; }
        public event EventHandler<MMDevice> DefaultDeviceChanged;

        public AudioDeviceTracker()
        {
            if (SynchronizationContext.Current == null)
                throw new Exception("SynchronizationContext.Current is null");

            syncContext = SynchronizationContext.Current;
            enumerator = new MMDeviceEnumerator();

            if (enumerator.HasDefaultAudioEndpoint(DataFlow.Render, Role.Console))
                Device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
            else
                Device = null;

            enumerator.RegisterEndpointNotificationCallback(this);
        }

        ~AudioDeviceTracker()
        {
            enumerator.UnregisterEndpointNotificationCallback(this);
        }

        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
        {
            if (flow != DataFlow.Render || role != Role.Console)
                return;

            syncContext.Post(DispatchDefaultDeviceChanged, defaultDeviceId);
        }

        private void DispatchDefaultDeviceChanged(object defaultDeviceId)
        {
            if (Device?.ID != (string)defaultDeviceId)
            {
                if (defaultDeviceId == null)
                    Device = null;
                else
                    Device = enumerator.GetDevice((string)defaultDeviceId);

                var handler = DefaultDeviceChanged;
                if (handler != null)
                {
                    handler(this, Device);
                }
            }
        }

        public void OnDeviceStateChanged(string deviceId, DeviceState newState) { }
        public void OnDeviceAdded(string pwstrDeviceId) { }
        public void OnDeviceRemoved(string deviceId) { }
        public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key) { }
    }

    internal class DummyOut : IWavePlayer
    {
        private readonly SynchronizationContext syncContext;
        private volatile PlaybackState playbackState = PlaybackState.Stopped;
        private Thread playThread;
        private ISampleProvider sampleProvider;
        private int latency;
        public float Volume { get; set; }
        public PlaybackState PlaybackState { get => playbackState; }
        public WaveFormat OutputWaveFormat { get; }
        public event EventHandler<StoppedEventArgs> PlaybackStopped;

        public DummyOut(int latency)
        {
            syncContext = SynchronizationContext.Current;
            this.latency = latency;
        }

        public void Play()
        {
            if (playbackState != PlaybackState.Playing)
            {
                if (playbackState == PlaybackState.Stopped)
                {
                    playThread = new Thread(PlayThread);
                    playThread.IsBackground = true;
                    playbackState = PlaybackState.Playing;
                    playThread.Start();
                }
                else
                {
                    playbackState = PlaybackState.Playing;
                }
            }
        }

        public void Stop()
        {
            if (playbackState != PlaybackState.Stopped)
            {
                playbackState = PlaybackState.Stopped;
                playThread.Join();
                playThread = null;
            }
        }
        public void Pause()
        {
            if (playbackState == PlaybackState.Playing)
                playbackState = PlaybackState.Paused;
        }
        public void Init(IWaveProvider waveProvider)
        {
            sampleProvider = waveProvider.ToSampleProvider();
        }
        public void Dispose()
        {
            Stop();
        }

        private void PlayThread()
        {
            Exception exception = null;
            try
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                TimeSpan prevTime = TimeSpan.Zero;
                var format = sampleProvider.WaveFormat;
                float[] buffer = new float[format.SampleRate * format.Channels];
                int samplesRemaining = 0;

                while (playbackState != PlaybackState.Stopped)
                {
                    Thread.Sleep(latency);
                    if (playbackState == PlaybackState.Playing)
                    {
                        if (!stopwatch.IsRunning)
                        {
                            stopwatch.Start();
                            continue;
                        }

                        TimeSpan now = stopwatch.Elapsed;
                        TimeSpan delta = now - prevTime;
                        prevTime = now;

                        samplesRemaining += (int)(delta.TotalSeconds * format.SampleRate * format.Channels);
                        while (samplesRemaining > format.Channels)
                        {
                            int count = Math.Min(buffer.Length, samplesRemaining);
                            count -= count % format.Channels;
                            int samplesRead = sampleProvider.Read(buffer, 0, count);
                            samplesRemaining -= samplesRead;
                            if (samplesRead == 0)
                            {
                                playbackState = PlaybackState.Stopped;
                                break;
                            }
                        }
                    }
                    else if (playbackState == PlaybackState.Paused)
                    {
                        if (stopwatch.IsRunning)
                        {
                            stopwatch.Reset();
                            prevTime = TimeSpan.Zero;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                exception = e;
            }
            finally
            {
                var handler = PlaybackStopped;
                if (handler != null)
                {
                    if (syncContext == null)
                        handler(this, new StoppedEventArgs(exception));
                    else
                        syncContext.Post(state => handler(this, new StoppedEventArgs(exception)), null);
                }
            }
        }
    }

    internal class RepeatStream : WaveStream
    {
        readonly WaveStream sourceStream;
        readonly int total_count;
        int remaining_count;

        public RepeatStream(WaveStream source, int count)
        {
            sourceStream = source;
            total_count = count;
            remaining_count = count;
        }

        public override WaveFormat WaveFormat
        {
            get { return sourceStream.WaveFormat; }
        }

        public override long Length
        {
            get { return sourceStream.Length * total_count; }
        }

        public override long Position
        {
            get
            {
                return (total_count - remaining_count) * sourceStream.Length + sourceStream.Position;
            }
            set
            {
                remaining_count = (int)(value / sourceStream.Length);
                sourceStream.Position = value % sourceStream.Length;
            }
        }

        public override bool HasData(int count)
        {
            return sourceStream.Position < sourceStream.Length;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int total_read = 0;
            while (total_read < count)
            {
                int remaining = count - total_read;
                int read = sourceStream.Read(buffer, offset + total_read, remaining);
                if (read < remaining || sourceStream.Position >= sourceStream.Length)
                {
                    if (remaining_count > 1)
                    {
                        remaining_count--;
                        sourceStream.Position = 0;
                    }
                    else
                    {
                        return total_read + read;
                    }
                }
                total_read += read;
            }
            return total_read;
        }

        protected override void Dispose(bool disposing)
        {
            sourceStream.Dispose();
            base.Dispose(disposing);
        }
    }

    internal static class SoundMixer
    {
        private static bool initialized = false;
        public static bool Initialized { get => initialized; }
        public const int SampleRate = 44100;
        private static AudioDeviceTracker deviceTracker;
        private static IWavePlayer output;
        private static MixingSampleProvider mixer;

        public static void Initialize()
        {
            if (initialized)
                return;

            mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, 2));
            mixer.ReadFully = true;
            mixer.MixerInputEnded += SoundEnded;

            deviceTracker = new AudioDeviceTracker();
            InitializeOutput(deviceTracker.Device);
            deviceTracker.DefaultDeviceChanged += ChangeOutput;

            initialized = true;
        }

        private static void SoundEnded(object sender, SampleProviderEventArgs args)
        {
            ((NAudioSound)(args.SampleProvider)).Playing = false;
        }

        private static void InitializeOutput(MMDevice device)
        {
            if (SynchronizationContext.Current == null)
                throw new Exception("SynchronizationContext.Current is null");

            if (device != null)
                output = new WasapiOut(device, AudioClientShareMode.Shared, true, 50);
            else
                output = new DummyOut(200);

            output.Init(mixer);
            output.Play();
        }

        private static void ChangeOutput(object sender, MMDevice device)
        {
            if (output != null)
                output.Dispose();

            InitializeOutput(device);
        }

        public static void PlaySound(NAudioSound sound)
        {
            sound.Playing = true;
            mixer.AddMixerInput(sound);
        }

        public static void StopSound(NAudioSound sound)
        {
            sound.Playing = false;
            mixer.RemoveMixerInput(sound);
        }
    }

    internal class NAudioSound : Sound, ISampleProvider
    {
        private float volume = 1.0f;
        private bool paused = false;
        private long savedPosition = 0;
        private VarispeedSampleProvider varispeedProvider;
        private bool preservePitch = true;
        private WaveStream stream;
        private VolumeSampleProvider volumeProvider;
        public WaveFormat WaveFormat { get => volumeProvider.WaveFormat; }

        public override double GetCurrentTime()
        {
            if (stream == null || volumeProvider == null) return 0;

            long bytesPerSecond = stream.WaveFormat.SampleRate * stream.WaveFormat.Channels * 4;
            if (bytesPerSecond == 0) return 0;

            return (double)stream.Position / bytesPerSecond;
        }

        public override double GetTotalTime()
        {
            if (stream == null || volumeProvider == null) return 0;

            long bytesPerSecond = stream.WaveFormat.SampleRate * stream.WaveFormat.Channels * 4;
            if (bytesPerSecond == 0) return 0;

            return (double)stream.Length / bytesPerSecond;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            return volumeProvider.Read(buffer, offset, count);
        }

        public override void play(string filename, int repeat = 1)
        {
            if (!SoundMixer.Initialized)
                SoundMixer.Initialize();

            stop();

            if (filename.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
            {
                stream = new WaveFileReader(filename);
            }
            else if (filename.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
            {
                stream = new VorbisWaveReader(filename);
            }
            else
            {
                var settings = new MediaFoundationReader.MediaFoundationReaderSettings();
                settings.RequestFloatOutput = true;
                stream = new MediaFoundationReader(filename, settings);
            }

            WaveStream _stream = stream;
            if (repeat == -1)
                _stream = new LoopStream(_stream);
            else if (repeat > 1)
                _stream = new RepeatStream(_stream, repeat);

            ISampleProvider sampleProvider = _stream.ToSampleProvider();
            if (sampleProvider.WaveFormat.SampleRate != SoundMixer.SampleRate)
                sampleProvider = new WdlResamplingSampleProvider(sampleProvider, SoundMixer.SampleRate);
            if (sampleProvider.WaveFormat.Channels == 1)
                sampleProvider = sampleProvider.ToStereo();
            varispeedProvider = new VarispeedSampleProvider(sampleProvider, 100,
                new SoundTouchProfile(preservePitch, true));
            volumeProvider = new VolumeSampleProvider(varispeedProvider);
            volumeProvider.Volume = volume;

            SoundMixer.PlaySound(this);
        }

        public override void play(string filepath, long repeat)
        {
            play(filepath, (int)repeat);
        }

        public override void pause()
        {
            if (!SoundMixer.Initialized || !Playing)
                return;
            if (stream != null)
                savedPosition = stream.Position;
            if (varispeedProvider != null)
                varispeedProvider.Reposition();
            paused = true;
            SoundMixer.StopSound(this);
        }

        public override void resume()
        {
            if (!SoundMixer.Initialized || !paused)
                return;
            paused = false;
            if (stream != null && savedPosition > 0)
            {
                stream.Position = savedPosition;
            }
            SoundMixer.PlaySound(this);
        }

        public override void stop()
        {
            if (SoundMixer.Initialized)
            {
                SoundMixer.StopSound(this);
            }
            paused = false;
            if (stream != null)
            {
                stream.Dispose();
                stream = null;
            }
        }

        public override void close()
        {
            stop();
            volumeProvider = null;
        }

        public override bool isPlaying()
        {
            return Playing && !paused;
        }

        public override void setVolume(int volume)
        {
            this.volume = Math.Clamp(volume, 0, 100) / 100.0f;
            if (volumeProvider != null)
                volumeProvider.Volume = this.volume;
        }

        public override int getVolume()
        {
            return (int)(volume * 100);
        }

        public override void setSpeed(float speed)
        {
            if (varispeedProvider != null)
            {
                float clampedSpeed = Math.Clamp(speed, 0.1f, 10.0f);
                varispeedProvider.PlaybackRate = clampedSpeed;
            }
        }

        public override double getSpeed()
        {
            return varispeedProvider?.PlaybackRate ?? 1.0;
        }

        public void Seek(double timeInSeconds)
        {
            if (stream == null || volumeProvider == null)
                return;

            long bytesPerSecond = stream.WaveFormat.SampleRate * stream.WaveFormat.Channels * 4;
            long targetPosition = (long)(timeInSeconds * bytesPerSecond);
            targetPosition = Math.Clamp(targetPosition, 0, stream.Length);
            stream.Position = targetPosition;

            if (varispeedProvider != null)
                varispeedProvider.Reposition();
        }

        public void SeekRelative(double offsetInSeconds)
        {
            if (stream == null || volumeProvider == null)
                return;

            double currentTime = GetCurrentTime();
            double targetTime = currentTime + offsetInSeconds;
            Seek(targetTime);
        }

        public void SeekPercentage(double percentage)
        {
            if (stream == null || volumeProvider == null)
                return;

            percentage = Math.Clamp(percentage, 0.0, 1.0);
            double totalTime = GetTotalTime();
            double targetTime = totalTime * percentage;
            Seek(targetTime);
        }

        public override void SetPreservePitch(bool preserve)
        {
            preservePitch = preserve;
            if (varispeedProvider != null)
            {
                varispeedProvider.SetSoundTouchProfile(new SoundTouchProfile(preserve, true));
            }
        }
    }
}
