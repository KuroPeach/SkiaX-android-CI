namespace MinorShift.Emuera.GameProc
{
    internal class Sound
    {
        private int[] _data = new int[0];
        public int Length => _data.Length;
        public int this[int index]
        {
            get => index >= 0 && index < _data.Length ? _data[index] : 0;
            set { if (index >= 0 && index < _data.Length) _data[index] = value; }
        }
        public long GetTotalTime() => 0;
        public long GetCurrentTime() => 0;
        public bool isPlaying() => false;
        public int getVolume() => 0;
        public int getSpeed() => 0;
        public void play() { }
        public void play(string filepath, int repeat) { }
        public void play(string filepath, long repeat) { }
        public void stop() { }
        public void setVolume(int vol) { }
        public void pause() { }
        public void resume() { }
        public void SetPreservePitch(bool preserve) { }
        public void setSpeed(float speed) { }

        public static implicit operator Sound(int i) => new Sound();
        public static implicit operator int(Sound s) => 0;
        public static implicit operator Sound(Bgm bgm) => new Sound();
    }

    internal class Bgm
    {
        public bool isPlaying() => false;
        public void play() { }
        public void stop() { }
        public void setVolume(int vol) { }
        public void pause() { }
        public void resume() { }
        public void SetPreservePitch(bool preserve) { }
        public void setSpeed(float speed) { }

        public static implicit operator Bgm(Sound s) => new Bgm();
    }
}
