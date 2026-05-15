using System.IO;

namespace MinorShift.Emuera
{
	internal class DefaultPathProvider : IPathProvider
	{
		private readonly string _exeDir;

		public DefaultPathProvider(string exeDir)
		{
			_exeDir = exeDir;
			var sep = Path.DirectorySeparatorChar;

			ErbDir = ResolveDir(_exeDir + "erb" + sep, _exeDir + "ERB" + sep);
			CsvDir = ResolveDir(_exeDir + "csv" + sep, _exeDir + "CSV" + sep);
			DebugDir = ResolveDir(_exeDir + "debug" + sep, _exeDir + "DEBUG" + sep);
			DatDir = ResolveDir(_exeDir + "dat" + sep, _exeDir + "DAT" + sep);
			ContentDir = ResolveDir(_exeDir + "resources" + sep, _exeDir + "RESOURCES" + sep);
			MusicDir = ResolveDir(_exeDir + "sound" + sep, _exeDir + "SOUND" + sep);
			SavDir = _exeDir + "sav" + sep;
			SqlDir = SavDir + "sql" + sep;
			TempDir = Path.Combine(Path.GetTempPath(), "emuera") + sep;
			LazyLoadingDir = _exeDir;
			ExeName = Path.GetFileNameWithoutExtension(System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "emuera.exe");
		}

		public string ExeDir => _exeDir;
		public string ErbDir { get; }
		public string CsvDir { get; }
		public string SavDir { get; }
		public string DatDir { get; }
		public string DebugDir { get; }
		public string ContentDir { get; }
		public string MusicDir { get; }
		public string SqlDir { get; }
		public string TempDir { get; }
		public string LazyLoadingDir { get; }
		public string ExeName { get; }

		private static string ResolveDir(string primary, string fallback)
		{
			return Directory.Exists(primary) ? primary : fallback;
		}
	}
}
