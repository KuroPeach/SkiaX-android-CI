using System.IO;

namespace MinorShift.Emuera
{
	internal interface IPathProvider
	{
		string ExeDir { get; }
		string ErbDir { get; }
		string CsvDir { get; }
		string SavDir { get; }
		string DatDir { get; }
		string DebugDir { get; }
		string ContentDir { get; }
		string MusicDir { get; }
		string SqlDir { get; }
		string TempDir { get; }
		string LazyLoadingDir { get; }
		string ExeName { get; }
	}
}
