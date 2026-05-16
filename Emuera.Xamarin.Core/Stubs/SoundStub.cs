using System.Collections.Generic;

namespace MinorShift.Emuera.Runtime.Utils
{
    public class Sound
    {
        public virtual void play(string filepath, int repeat = 1) { }
        public virtual void play(string filepath, long repeat) { }
        public virtual void stop() { }
        public virtual void pause() { }
        public virtual void resume() { }
        public virtual void close() { }
        public virtual bool isPlaying() => false;
        public virtual void setVolume(int volume) { }
        public virtual int getVolume() => 0;
        public virtual void setSpeed(float speed) { }
        public virtual int getSpeed() => 0;
        public virtual void SetPreservePitch(bool preserve) { }
        public virtual double GetTotalTime() => 0;
        public virtual double GetCurrentTime() => 0;
        public static Dictionary<int, string> SoundNames = new Dictionary<int, string>();
    }
}
