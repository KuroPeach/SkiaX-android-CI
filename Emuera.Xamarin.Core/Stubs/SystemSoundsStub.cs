namespace System.Media
{
    public class SystemSound
    {
        public void Play() { }
    }

    public static class SystemSounds
    {
        public static SystemSound Beep { get; } = new SystemSound();
        public static SystemSound Exclamation { get; } = new SystemSound();
        public static SystemSound Hand { get; } = new SystemSound();
        public static SystemSound Asterisk { get; } = new SystemSound();
        public static SystemSound Question { get; } = new SystemSound();
    }
}
