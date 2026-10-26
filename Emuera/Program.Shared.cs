using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using MinorShift.Emuera.Runtime.Utils;

namespace MinorShift.Emuera;

/// <summary>
/// Program 的跨平台部分：路径、运行开关、大小写不敏感文件查找。
/// 桌面 Main / WinForms 选项解析留在 Program.cs；headless Main 在 Emuera.Headless 工程。
/// </summary>
static partial class Program
{
	public static string ExeDir { get; private set; }
	public static string WorkingDir { get; private set; }
	public static string CsvDir { get; private set; }
	public static string ErbDir { get; private set; }
	public static string DebugDir { get; private set; }
	public static string DatDir { get; private set; }
	public static string ContentDir { get; private set; }
	public static string ExeName { get; private set; }
	public static string MusicDir { get; private set; }
	public static string FontDir { get; private set; }

	public static bool rebootFlag;
	public static bool AnalysisMode;
	public static List<string> AnalysisFiles;

	public static bool DebugMode { get; private set; }
	public static bool CliDebugMode { get; private set; }
	public static bool EwpMode { get; internal set; }
	public static bool EwpHeadlessMode { get; internal set; }
	public static int EwpPort { get; internal set; }
	public static bool EwpMsgpack { get; internal set; }

	[MemberNotNull(nameof(ExeDir))]
	[MemberNotNull(nameof(CsvDir))]
	[MemberNotNull(nameof(ErbDir))]
	[MemberNotNull(nameof(DebugDir))]
	[MemberNotNull(nameof(DatDir))]
	[MemberNotNull(nameof(ContentDir))]
	internal static void SetDirPaths(string exeDir)
	{
		ExeDir = Path.GetFullPath(new DirectoryInfo(exeDir).FullName + Path.DirectorySeparatorChar);

		CsvDir = Path.Combine(ExeDir, "csv") + Path.DirectorySeparatorChar;
		ErbDir = Path.Combine(ExeDir, "erb") + Path.DirectorySeparatorChar;
		DebugDir = Path.Combine(ExeDir, "debug") + Path.DirectorySeparatorChar;
		DatDir = Path.Combine(ExeDir, "dat") + Path.DirectorySeparatorChar;
		ContentDir = Path.Combine(ExeDir, "resources") + Path.DirectorySeparatorChar;
		FontDir = Path.Combine(ExeDir, "font") + Path.DirectorySeparatorChar;
		MusicDir = Path.Combine(ExeDir, "sound") + Path.DirectorySeparatorChar;
	}

	/// <summary>大小写不敏感文件查找。找到则 filepath 更新为实际路径，返回 true。</summary>
	public static bool FileExists(ref string filepath)
	{
		if (string.IsNullOrEmpty(filepath)) return false;
		if (File.Exists(filepath)) return true;
		var dir = Path.GetDirectoryName(filepath);
		var name = Path.GetFileName(filepath);
		if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(name)) return false;
		try
		{
			foreach (var f in Directory.GetFiles(dir, name))
			{
				filepath = f;
				return true;
			}
			foreach (var f in Directory.GetFiles(dir))
			{
				if (string.Equals(Path.GetFileName(f), name, StringComparison.OrdinalIgnoreCase))
				{
					filepath = f;
					return true;
				}
			}
		}
		catch { }
		return false;
	}

	/// <summary>headless 入口的唯一初始化：路径 + 名称（不触碰 WinForms）。</summary>
	internal static void InitHeadless(string gameDir)
	{
		SetDirPaths(gameDir);
		WorkingDir = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar;
		ExeName = Path.GetFileNameWithoutExtension(AssemblyData.ExeName);
	}
}
