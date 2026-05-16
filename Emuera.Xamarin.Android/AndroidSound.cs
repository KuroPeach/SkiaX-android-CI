using System;
using Android.Media;
using MinorShift.Emuera.Runtime.Utils;

namespace XEmuera.Droid
{
    internal class AndroidSound : Sound
    {
        private MediaPlayer mediaPlayer;
        private float volume = 1.0f;
        private bool paused;
        private long savedPosition;
        private string currentFilepath;

        public AndroidSound()
        {
            mediaPlayer = new MediaPlayer();
        }

        private void ResetPlayer()
        {
            try
            {
                mediaPlayer.Reset();
            }
            catch { }
        }

        public override void play(string filepath, int repeat = 1)
        {
            stop();

            currentFilepath = filepath;

            try
            {
                mediaPlayer = new MediaPlayer();
                mediaPlayer.SetDataSource(filepath);
                mediaPlayer.Looping = (repeat == -1);
                mediaPlayer.SetVolume(volume, volume);
                mediaPlayer.Prepare();
                mediaPlayer.Start();
                Playing = true;
                paused = false;

                mediaPlayer.Completion += (sender, e) =>
                {
                    if (!mediaPlayer.Looping)
                    {
                        Playing = false;
                        paused = false;
                    }
                };
            }
            catch (Exception)
            {
                Playing = false;
            }
        }

        public override void play(string filepath, long repeat)
        {
            play(filepath, (int)repeat);
        }

        public override void stop()
        {
            try
            {
                if (mediaPlayer != null)
                {
                    mediaPlayer.Stop();
                }
            }
            catch { }

            Playing = false;
            paused = false;
            savedPosition = 0;
        }

        public override void pause()
        {
            if (!Playing || paused)
                return;

            try
            {
                if (mediaPlayer != null && mediaPlayer.IsPlaying)
                {
                    savedPosition = mediaPlayer.CurrentPosition;
                    mediaPlayer.Pause();
                    paused = true;
                }
            }
            catch { }
        }

        public override void resume()
        {
            if (!paused)
                return;

            try
            {
                if (mediaPlayer != null)
                {
                    if (savedPosition > 0)
                        mediaPlayer.SeekTo((int)savedPosition);
                    mediaPlayer.Start();
                    paused = false;
                    Playing = true;
                }
            }
            catch { }
        }

        public override void close()
        {
            stop();
            try
            {
                mediaPlayer?.Release();
            }
            catch { }
            mediaPlayer = null;
        }

        public override bool isPlaying()
        {
            try
            {
                return Playing && mediaPlayer != null && mediaPlayer.IsPlaying;
            }
            catch
            {
                return false;
            }
        }

        public override void setVolume(int volume)
        {
            this.volume = Math.Clamp(volume, 0, 100) / 100.0f;
            try
            {
                mediaPlayer?.SetVolume(this.volume, this.volume);
            }
            catch { }
        }

        public override int getVolume()
        {
            return (int)(volume * 100);
        }

        public override void setSpeed(float speed)
        {
        }

        public override double getSpeed()
        {
            return 1.0;
        }

        public override void SetPreservePitch(bool preserve)
        {
        }

        public override double GetTotalTime()
        {
            try
            {
                if (mediaPlayer != null && mediaPlayer.Duration > 0)
                    return mediaPlayer.Duration / 1000.0;
            }
            catch { }
            return 0;
        }

        public override double GetCurrentTime()
        {
            try
            {
                if (mediaPlayer != null && mediaPlayer.CurrentPosition >= 0)
                    return mediaPlayer.CurrentPosition / 1000.0;
            }
            catch { }
            return 0;
        }
    }
}
