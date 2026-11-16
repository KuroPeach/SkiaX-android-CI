using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace MinorShift.Emuera.Runtime.Utils;

/// <summary>
/// 资源包（resources.pak／resources.zip）读取层。RPK P0 只服务图片读取点，CSV 入包归 P1。
/// <para>两种状态：索引态＝启动时读一次包内 <c>.rpk-index.bin</c>，之后用 RandomAccess 直读（无锁，UI 与 ERB 线程可并发）；
/// 退化态＝索引缺失或损坏（第三方重打包）时持 ZipArchive＋全局锁整条读，语义一致、速度≈散图。</para>
/// <para>查找顺序＝磁盘 → 包（任务卡 §4.3）：磁盘有同名文件时一律返回 null 让调用方走原路径，包只补磁盘缺口。
/// 无包时所有方法都返回「未命中」，行为与接入前逐字节一致。</para>
/// <para>⛔ 不引用 <c>MinorShift.Emuera.Program</c>：安卓端没有这个类，路径由两端调 <see cref="Configure"/> 注入。</para>
/// </summary>
internal static class ResourceStore
{
	/// <summary>包内条目名前缀，与打包器一致（任务卡 §4.1）。</summary>
	const string EntryPrefix = "resources/";
	/// <summary>加速索引条目（派生物，删掉即自动退化）。</summary>
	const string IndexEntryName = EntryPrefix + ".rpk-index.bin";
	/// <summary>索引头长度：magic(4) + version(2) + reserved(2) + entryCount(4) + nameTableSize(4) + delayTableSize(4)。</summary>
	const int IndexHeaderSize = 20;
	/// <summary>单条索引记录长度。</summary>
	const int IndexRecordSize = 26;

	/// <summary>
	/// 测试开关：ERATW_FORCE_PAK=1 时跳过「磁盘优先」，强制查包。
	/// 用途＝在散图仍在磁盘的开发目录上验证包路径本身（条目名归一、大小写）是否正确。
	/// 只读环境变量、不写 config，避免污染玩家配置。
	/// </summary>
	static readonly bool ForcePak = Environment.GetEnvironmentVariable("ERATW_FORCE_PAK") == "1";

	/// <summary>是否忽略磁盘同名文件：只有「强制走包」且包确实可用才算——没有包时没什么可强制的，仍回退磁盘。</summary>
	static bool IgnoreDisk => ForcePak && Enabled;

	/// <summary>索引记录：数据在包内的绝对偏移＋长度＋打包器填的宽高（宽高为 0 表示打包时未解析出尺寸）。</summary>
	readonly struct Entry
	{
		public readonly long Offset;
		public readonly int Length;
		public readonly int Width;
		public readonly int Height;

		public Entry(long offset, int length, int width, int height)
		{
			Offset = offset;
			Length = length;
			Width = width;
			Height = height;
		}
	}

	static readonly object gate = new();

	static string exeDir;
	/// <summary>归一化后的 ContentDir，必带结尾分隔符。</summary>
	static string contentDir;
	static volatile bool openAttempted;

	static string pakPath;
	static FileStream pakStream;
	static Microsoft.Win32.SafeHandles.SafeFileHandle pakHandle;
	/// <summary>索引态：条目名 → 元数据。null 表示未启用索引态。</summary>
	static Dictionary<string, Entry> index;
	/// <summary>退化态：条目名 → 条目。null 表示未启用退化态。</summary>
	static ZipArchive zip;
	static Dictionary<string, ZipArchiveEntry> zipEntries;

	/// <summary>已探测到可用包（索引态或退化态任一）。</summary>
	public static bool Enabled
	{
		get
		{
			EnsureOpen();
			return index != null || zipEntries != null;
		}
	}

	/// <summary>两端各注入一次，只记路径；包在首次查询时才打开（无图场景不白占句柄）。</summary>
	public static void Configure(string exeDirPath, string contentDirPath)
	{
		lock (gate)
		{
			CloseCore();
			exeDir = NormalizeDir(exeDirPath);
			contentDir = NormalizeDir(contentDirPath);
			openAttempted = false;
		}
	}

	/// <summary>磁盘存在 ‖ 包命中。前缀外路径只认磁盘。</summary>
	public static bool Exists(string fullPath)
	{
		if (string.IsNullOrEmpty(fullPath))
			return false;

		var entryName = ToEntryName(fullPath);
		if (entryName == null)
			return File.Exists(fullPath);
		if (!IgnoreDisk && File.Exists(fullPath))
			return true;
		return Contains(entryName);
	}

	/// <summary>索引命中的宽高，0 I/O。退化态／索引未填尺寸／未命中均返回 false（调用方自行解码读头）。</summary>
	public static bool TryGetInfo(string fullPath, out int width, out int height)
	{
		width = height = 0;
		if (string.IsNullOrEmpty(fullPath))
			return false;

		var entryName = ToEntryName(fullPath);
		if (entryName == null)
			return false;
		// 磁盘优先：磁盘有这条时由调用方按原路径读，避免用包内尺寸去量磁盘图
		if (!IgnoreDisk && File.Exists(fullPath))
			return false;

		EnsureOpen();
		if (index == null || !index.TryGetValue(entryName, out var entry))
			return false;
		if (entry.Width <= 0 || entry.Height <= 0)
			return false;

		width = entry.Width;
		height = entry.Height;
		return true;
	}

	/// <summary>
	/// 动画帧信息。v1 索引不记帧数与延时（打包器 frameCount 恒填 1、延时表恒空），
	/// 故恒返回 false ⇒ 调用方退回从字节里解（AnimatedImageHelper）。
	/// </summary>
	public static bool TryGetAnim(string fullPath, out int frameCount, out int[] delays)
	{
		frameCount = 0;
		delays = null;
		return false;
	}

	/// <summary>整条读入。null ＝ 未命中／磁盘优先（调用方回退磁盘原路径）。</summary>
	public static byte[] ReadAllBytes(string fullPath)
	{
		if (string.IsNullOrEmpty(fullPath))
			return null;

		var entryName = ToEntryName(fullPath);
		if (entryName == null)
			return null;
		if (!IgnoreDisk && File.Exists(fullPath))
			return null;

		try
		{
			return ReadEntry(entryName);
		}
		catch
		{
			// 包损坏／条目被截断：当作未命中，调用方回退磁盘（磁盘也没有时按原有语义不显示）
			return null;
		}
	}

	/// <summary>CSV 枚举（P1 才用）：包内以 suffix 结尾的相对路径。</summary>
	public static IEnumerable<string> EnumerateRelative(string suffix)
	{
		return Array.Empty<string>();
	}

	/// <summary>诊断用：当前态与条目数。</summary>
	internal static string Describe()
	{
		EnsureOpen();
		if (index != null)
			return Path.GetFileName(pakPath) + "：索引态 " + index.Count + " 条";
		if (zipEntries != null)
			return Path.GetFileName(pakPath) + "：退化态 " + zipEntries.Count + " 条";
		return "未启用（无 resources.pak／resources.zip）";
	}

	// ---------- 路径归一 ----------

	/// <summary>
	/// 磁盘绝对路径 → 包内条目名（"resources/相对路径"），不属于 ContentDir 前缀返回 null。
	/// 必须归一：CSV 里实测有写 <c>infoThumb\blank.webp</c> 的，包内是 <c>infoThumb/blank.webp</c>，
	/// 不归一就查不到 ⇒ 回退磁盘 ⇒ 磁盘也没有 ⇒ 静默不显示。
	/// </summary>
	static string ToEntryName(string fullPath)
	{
		var cd = contentDir;
		if (cd == null)
			return null;

		var path = NormalizeSeparators(fullPath);
		// Windows 语义：前缀比较必须忽略大小写
		if (!path.StartsWith(cd, StringComparison.OrdinalIgnoreCase))
			return null;

		var rel = path.Substring(cd.Length);
		if (rel.Length == 0)
			return null;

		var segments = rel.Split(separators, StringSplitOptions.RemoveEmptyEntries);
		var kept = new List<string>(segments.Length);
		foreach (var segment in segments)
		{
			if (segment == ".")
				continue;
			// 含 ".." 段 ⇒ 拒绝（不许跳出内容目录）
			if (segment == "..")
				return null;
			kept.Add(segment);
		}
		if (kept.Count == 0)
			return null;

		return EntryPrefix + string.Join("/", kept);
	}

	static readonly char[] separators = ['/', '\\'];

	static string NormalizeDir(string dir)
	{
		if (string.IsNullOrEmpty(dir))
			return null;
		var normalized = NormalizeSeparators(dir);
		if (!normalized.EndsWith(Path.DirectorySeparatorChar))
			normalized += Path.DirectorySeparatorChar;
		return normalized;
	}

	/// <summary>
	/// 分隔符归一。Windows 把 '/' 视作 '\'；其它平台把 '\' 视作 '/'（包内与 CSV 两侧都用 '/'）。
	/// </summary>
	static string NormalizeSeparators(string path)
	{
		if (string.IsNullOrEmpty(path))
			return path;
		var other = Path.DirectorySeparatorChar == '\\' ? '/' : '\\';
		return path.IndexOf(other) >= 0 ? path.Replace(other, Path.DirectorySeparatorChar) : path;
	}

	// ---------- 开包 ----------

	static void EnsureOpen()
	{
		if (openAttempted)
			return;
		lock (gate)
		{
			if (openAttempted)
				return;
			openAttempted = true;
			try
			{
				OpenCore();
			}
			catch
			{
				// 包不可用（被占用／不是 ZIP／索引损坏且退化态也失败）：当作无包，行为回到接入前
				CloseCore();
			}
		}
	}

	static void OpenCore()
	{
		if (exeDir == null)
			return;

		foreach (var candidate in new[] { Path.Combine(exeDir, "resources.pak"), Path.Combine(exeDir, "resources.zip") })
		{
			if (!File.Exists(candidate))
				continue;

			// 64 KiB 缓冲只服务开包时读中央目录；图片数据走 RandomAccess.Read，不经过 FileStream 缓冲
			var stream = new FileStream(candidate, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.RandomAccess);
			try
			{
				if (TryLoadIndex(stream, out var loaded))
				{
					pakPath = candidate;
					pakStream = stream;
					pakHandle = stream.SafeFileHandle;
					index = loaded;
					return;
				}

				// 索引缺失／损坏 ⇒ 退化态
				var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
				var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
				foreach (var entry in archive.Entries)
					entries[entry.FullName] = entry;
				if (entries.Count == 0)
				{
					archive.Dispose();
					stream.Dispose();
					continue;
				}

				pakPath = candidate;
				pakStream = stream;
				zip = archive;
				zipEntries = entries;
				return;
			}
			catch
			{
				stream.Dispose();
			}
		}
	}

	static void CloseCore()
	{
		zip?.Dispose();
		zip = null;
		zipEntries = null;
		index = null;
		pakHandle = null;
		pakStream?.Dispose();
		pakStream = null;
		pakPath = null;
	}

	/// <summary>读包内索引 → 条目名（含 resources/ 前缀）→ 元数据。索引缺失、损坏或版本不认都返回 false。</summary>
	static bool TryLoadIndex(FileStream stream, out Dictionary<string, Entry> entries)
	{
		entries = null;

		using (var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true))
		{
			var indexEntry = archive.GetEntry(IndexEntryName);
			if (indexEntry == null || indexEntry.Length < IndexHeaderSize || indexEntry.Length > 64 * 1024 * 1024)
				return false;

			var raw = new byte[indexEntry.Length];
			using (var source = indexEntry.Open())
				source.ReadExactly(raw);

			if (raw[0] != (byte)'R' || raw[1] != (byte)'P' || raw[2] != (byte)'K' || raw[3] != (byte)'I')
				return false;
			if (BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(4)) != 1)
				return false;

			int count = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(8)));
			int nameTableSize = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(12)));
			int delayTableSize = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(16)));
			if (count <= 0 || count > 1_000_000)
				return false;

			long recordsEnd = IndexHeaderSize + (long)count * IndexRecordSize;
			long namesEnd = recordsEnd + nameTableSize;
			if (namesEnd + delayTableSize > raw.Length)
				return false;

			var names = Encoding.UTF8.GetString(raw, (int)recordsEnd, nameTableSize).Split('\0');
			if (names.Length < count)
				return false;

			long pakLength = stream.Length;
			var loaded = new Dictionary<string, Entry>(count, StringComparer.OrdinalIgnoreCase);
			for (int i = 0; i < count; i++)
			{
				var record = raw.AsSpan(IndexHeaderSize + i * IndexRecordSize, IndexRecordSize);
				long offset = (long)BinaryPrimitives.ReadUInt64LittleEndian(record);
				int length = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(record.Slice(8)));
				int width = BinaryPrimitives.ReadUInt16LittleEndian(record.Slice(12));
				int height = BinaryPrimitives.ReadUInt16LittleEndian(record.Slice(14));
				// 16..25 是 frameCount/delayOffset/flags/reserved：v1 打包器恒填 1/0/0/0，P0 不用

				var name = names[i];
				if (string.IsNullOrEmpty(name))
					return false;
				if (offset < 0 || length <= 0 || offset + length > pakLength)
					return false;

				loaded[EntryPrefix + name] = new Entry(offset, length, width, height);
			}

			entries = loaded;
			return true;
		}
	}

	// ---------- 读条目 ----------

	static bool Contains(string entryName)
	{
		EnsureOpen();
		if (index != null)
			return index.ContainsKey(entryName);
		if (zipEntries != null)
			return zipEntries.ContainsKey(entryName);
		return false;
	}

	static byte[] ReadEntry(string entryName)
	{
		EnsureOpen();

		if (index != null)
		{
			if (!index.TryGetValue(entryName, out var entry))
				return null;
			// 句柄可能被 reboot 期间的 Configure 关掉：取本地引用并判空，避免空引用逃逸到调用方
			var handle = pakHandle;
			if (handle == null)
				return null;
			var pooled = ArrayPool<byte>.Shared.Rent(entry.Length);
			try
			{
				ReadAt(handle, pooled.AsSpan(0, entry.Length), entry.Offset);
				var copy = new byte[entry.Length];
				Buffer.BlockCopy(pooled, 0, copy, 0, entry.Length);
				return copy;
			}
			finally
			{
				ArrayPool<byte>.Shared.Return(pooled);
			}
		}

		if (zipEntries != null && zipEntries.TryGetValue(entryName, out var zipEntry) && zipEntry.Length > 0)
		{
			// 退化态：ZipArchive 共用一个底层流，整个读过程必须持锁
			lock (gate)
			{
				var copy = new byte[zipEntry.Length];
				using var source = zipEntry.Open();
				source.ReadExactly(copy);
				return copy;
			}
		}

		return null;
	}

	/// <summary>RandomAccess.Read 可能短读，必须循环到读满；短读即视为包损坏。</summary>
	static void ReadAt(Microsoft.Win32.SafeHandles.SafeFileHandle handle, Span<byte> buffer, long offset)
	{
		int total = 0;
		while (total < buffer.Length)
		{
			int read = RandomAccess.Read(handle, buffer.Slice(total), offset + total);
			if (read <= 0)
				throw new EndOfStreamException();
			total += read;
		}
	}
}
