using System;

// N1-c：System.Media.SystemSounds / SystemSound（Windows-only System.Windows.Extensions）。
// headless 只保留 API 形状，Play() 为 no-op。
namespace System.Media
{
	public sealed class SystemSound
	{
		public void Play() { }
	}

	public static class SystemSounds
	{
		public static SystemSound Asterisk { get; } = new();
		public static SystemSound Hand { get; } = new();
	}
}
