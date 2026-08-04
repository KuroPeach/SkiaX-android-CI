using MinorShift.Emuera.UI.Game.Image;
using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

namespace MinorShift.Emuera.Runtime.Utils;

/// <summary>
/// 临时内存诊断日志工具
/// 在引擎关闭时将内存快照写入游戏主目录，用于诊断内存占用异常
/// 本工具为临时诊断用途，后续可移除或整合到正式功能
/// </summary>
internal static class MemoryDiagnostic
{
	private static readonly string LogFileName = "memory_diagnostic.log";

	/// <param name="consoleInstance">EmueraConsole 实例（用于反射读取私有字段）</param>
	public static void WriteDiagnosticLog(object consoleInstance = null)
	{
		// 门控：仅当 Config.MemoryDiagnosticEnabled 开启时输出，默认关闭
		try
		{
			if (!Runtime.Config.ConfigData.Instance.GetConfigValue<bool>(Runtime.Config.ConfigCode.MemoryDiagnosticEnabled))
				return;
		}
		catch
		{
			return; // 配置读取失败时安全关闭
		}

		try
		{
			var logPath = Path.Combine(Program.ExeDir, LogFileName);

			using var writer = new StreamWriter(logPath, false, Encoding.UTF8);
			writer.WriteLine("============================================");
			writer.WriteLine("  Emuera (LazyLoading) 内存诊断报告");
			writer.WriteLine("============================================");
			writer.WriteLine($"记录时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
			writer.WriteLine($"进程名: {Program.ExeName}");
			writer.WriteLine();

			WriteProcessMemory(writer);
			WriteGCInfo(writer);
			WriteGCHeapInfo(writer);
			WriteThreadStats(writer);
			WriteSpriteCache(writer);
			WritePreloadStats(writer);
			WriteCompiledScriptStats(writer);
			WriteGameDataStats(writer);
			WriteRuntimeVariableStats(writer);
			WriteConsoleImageCache(writer, consoleInstance);
			WriteFontDiagnostics(writer);
			WriteFontFactoryStats(writer);
			WriteSqlConnections(writer);

			writer.WriteLine("============================================");
			writer.WriteLine("  报告结束");
			writer.WriteLine("============================================");
		}
		catch (Exception ex)
		{
			// 诊断日志自身不应抛出异常影响主程序
			try
			{
				File.AppendAllText(
					Path.Combine(Program.ExeDir, "memory_diagnostic_error.log"),
					$"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 写入诊断日志失败: {ex.Message}{Environment.NewLine}");
			}
			catch { }
		}
	}

	private static void WriteProcessMemory(StreamWriter writer)
	{
		writer.WriteLine("--- 进程内存 ---");
		try
		{
			using var process = Process.GetCurrentProcess();
			writer.WriteLine($"  WorkingSet64 (物理内存):       {FormatBytes(process.WorkingSet64)}");
			writer.WriteLine($"  PrivateMemorySize64 (私有内存): {FormatBytes(process.PrivateMemorySize64)}");
			writer.WriteLine($"  VirtualMemorySize64 (虚拟内存): {FormatBytes(process.VirtualMemorySize64)}");
			writer.WriteLine($"  PagedMemorySize64 (分页内存):   {FormatBytes(process.PagedMemorySize64)}");
			writer.WriteLine($"  PeakWorkingSet64 (峰值物理):    {FormatBytes(process.PeakWorkingSet64)}");
			writer.WriteLine($"  PeakVirtualMemorySize64 (峰值虚拟): {FormatBytes(process.PeakVirtualMemorySize64)}");
			writer.WriteLine($"  HandleCount (句柄数):           {process.HandleCount}");
			writer.WriteLine($"  ThreadCount (线程数):           {process.Threads.Count}");

			// 非托管内存 ≈ WorkingSet64 - GC.GetTotalMemory
			long gcHeap = GC.GetTotalMemory(false);
			long unmanaged = Math.Max(0, process.WorkingSet64 - gcHeap);
			writer.WriteLine($"  非托管/原生内存 (估算):         {FormatBytes(unmanaged)}");
		}
		catch (Exception ex)
		{
			writer.WriteLine($"  (读取失败: {ex.Message})");
		}
		writer.WriteLine();
	}

	private static void WriteGCInfo(StreamWriter writer)
	{
		writer.WriteLine("--- GC 信息 ---");
		try
		{
			writer.WriteLine($"  GC.GetTotalMemory (当前托管堆):   {FormatBytes(GC.GetTotalMemory(false))}");
			writer.WriteLine($"  GC.GetTotalAllocatedBytes (已分配总量): {FormatBytes(GC.GetTotalAllocatedBytes(false))}");
			writer.WriteLine($"  Gen0 回收次数: {GC.CollectionCount(0)}");
			writer.WriteLine($"  Gen1 回收次数: {GC.CollectionCount(1)}");
			writer.WriteLine($"  Gen2 回收次数: {GC.CollectionCount(2)}");
			writer.WriteLine($"  MaxGeneration: {GC.MaxGeneration}");
			writer.WriteLine($"  TotalPauseDuration (总暂停时间): {GC.GetTotalPauseDuration().TotalMilliseconds:F1} ms");
		}
		catch (Exception ex)
		{
			writer.WriteLine($"  (读取失败: {ex.Message})");
		}
		writer.WriteLine();
	}

	private static void WriteGCHeapInfo(StreamWriter writer)
	{
		writer.WriteLine("--- GC 堆细分 ---");
		try
		{
			var info = GC.GetGCMemoryInfo();
			writer.WriteLine($"  HeapSizeBytes (总堆大小):        {FormatBytes(info.HeapSizeBytes)}");
			writer.WriteLine($"  FragmentedBytes (碎片):          {FormatBytes(info.FragmentedBytes)}");
			writer.WriteLine($"  TotalAvailableMemoryBytes:       {FormatBytes(info.TotalAvailableMemoryBytes)}");
			writer.WriteLine($"  MemoryLoadBytes (进程内存负载):  {FormatBytes(info.MemoryLoadBytes)}");
			writer.WriteLine($"  Gen0 堆大小: {FormatBytes(info.GenerationInfo[0].SizeAfterBytes)}");
			writer.WriteLine($"  Gen1 堆大小: {FormatBytes(info.GenerationInfo[1].SizeAfterBytes)}");
			writer.WriteLine($"  Gen2 堆大小: {FormatBytes(info.GenerationInfo[2].SizeAfterBytes)}");
			writer.WriteLine($"  LOH (大对象堆):  {FormatBytes(info.GenerationInfo[3].SizeAfterBytes)}");
		}
		catch (Exception ex)
		{
			writer.WriteLine($"  (读取失败: {ex.Message})");
		}
		writer.WriteLine();
	}

	private static void WriteThreadStats(StreamWriter writer)
	{
		writer.WriteLine("--- 线程统计 ---");
		try
		{
			using var process = Process.GetCurrentProcess();
			int running = 0, wait = 0, other = 0;
			foreach (System.Diagnostics.ProcessThread pt in process.Threads)
			{
				switch (pt.ThreadState)
				{
					case System.Diagnostics.ThreadState.Running: running++; break;
					case System.Diagnostics.ThreadState.Wait: wait++; break;
					default: other++; break;
				}
			}
			writer.WriteLine($"  线程总数: {process.Threads.Count}");
			writer.WriteLine($"    ├─ Running: {running}");
			writer.WriteLine($"    ├─ Wait:   {wait}");
			writer.WriteLine($"    └─ 其他:   {other}");
		}
		catch (Exception ex)
		{
			writer.WriteLine($"  (读取失败: {ex.Message})");
		}
		writer.WriteLine();
	}

	private static void WriteSpriteCache(StreamWriter writer)
	{
		writer.WriteLine("--- AppContents / 精灵缓存 ---");
		try
		{
			var (spriteCount, graphicsCount, tempConstCount, tempGraphicsCount, metaRowCount) = AppContents.GetDiagnosticStats();
			writer.WriteLine($"  activeSprites (活跃精灵数):          {spriteCount}");
			writer.WriteLine($"  gList (图形缓存数):                 {graphicsCount}");
			writer.WriteLine($"  tempLoadedConstImages (临时常量图): {tempConstCount}");
			writer.WriteLine($"  tempLoadedGraphicsImages (临时图形图): {tempGraphicsCount}");
			writer.WriteLine($"  SpriteMeta 定义数 (resources CSV):  {metaRowCount}");
			writer.WriteLine($"  SharedBitmapCache (全局位图缓存):   {SharedBitmapCache.Count}");
			writer.WriteLine($"  AnimSpriteCache (动画精灵缓存):     {AnimSpriteCache.Count}");

			// resources/ 目录的 CSV 文件总大小
			try
			{
				var contentDir = Program.ContentDir;
				if (Directory.Exists(contentDir))
				{
					long totalSize = 0;
					int fileCount = 0;
					foreach (var csvFile in Directory.EnumerateFiles(contentDir, "*.csv", SearchOption.AllDirectories))
					{
						totalSize += new FileInfo(csvFile).Length;
						fileCount++;
					}
					writer.WriteLine($"  resources/ CSV 文件数:            {fileCount}");
					writer.WriteLine($"  resources/ CSV 总大小 (磁盘):     {FormatBytes(totalSize)}");
				}
			}
			catch { }
		}
		catch (Exception ex)
		{
			writer.WriteLine($"  (读取失败: {ex.Message})");
		}
		writer.WriteLine();
	}

	private static void WritePreloadStats(StreamWriter writer)
	{
		writer.WriteLine("--- Preload 文件缓存 (脚本/数据) ---");
		try
		{
			var (fileCount, totalChars, csvCount, erbCount, erhCount) = Preload.GetDiagnosticStats();
			writer.WriteLine($"  总文件数: {fileCount}");
			writer.WriteLine($"    CSV: {csvCount}, ERB: {erbCount}, ERH: {erhCount}, 其他: {fileCount - csvCount - erbCount - erhCount}");
			writer.WriteLine($"  总字符数 (UTF-16): {FormatBytes(totalChars * 2)}");
			// 估算：每行 string 对象 ~28 字节 + 数组引用，粗略估算总内存
			writer.WriteLine($"  估算内存占用: {FormatBytes(totalChars * 2 + fileCount * 256L)}");
		}
		catch (Exception ex)
		{
			writer.WriteLine($"  (读取失败: {ex.Message})");
		}
		writer.WriteLine();
	}

	private static void WriteCompiledScriptStats(StreamWriter writer)
	{
		writer.WriteLine("--- 编译后脚本 (LabelDictionary) ---");
		try
		{
			var ld = GlobalStatic.LabelDictionary;
			if (ld != null)
			{
				var (total, eventCount, nonEventCount, loadedFiles, statementLines, jumpTables, estBytes, privateVarCount) = ld.GetDiagnosticStats();
				writer.WriteLine($"  总标签数 (FunctionLabelLine):  {total}");
				writer.WriteLine($"    ├─ 事件标签: {eventCount}");
				writer.WriteLine($"    └─ 非事件标签: {nonEventCount}");
				writer.WriteLine($"  加载文件数: {loadedFiles}");
				writer.WriteLine($"  语句行数 (InstructionLine):   {statementLines}");
				writer.WriteLine($"  SELECTCASE 跳转表数:          {jumpTables}");
				writer.WriteLine($"  私有变量数 (#DIM 声明):      {privateVarCount}");
				writer.WriteLine($"  估算编译后字节码内存: {FormatBytes(estBytes)}");
			}
			else
			{
				writer.WriteLine("  LabelDictionary: null (尚未加载)");
			}
		}
		catch (Exception ex)
		{
			writer.WriteLine($"  (读取失败: {ex.Message})");
		}
		writer.WriteLine();
	}

	private static void WriteGameDataStats(StreamWriter writer)
	{
		writer.WriteLine("--- 游戏数据 (CSV 解析后) ---");
		try
		{
			var cd = GlobalStatic.ConstantData;
			if (cd != null)
			{
				var (maxDataList, charCount, totalElements) = cd.GetDiagnosticStats();
				writer.WriteLine($"  角色模板数: {charCount}");
				writer.WriteLine($"  CSV 数组总元素数: {totalElements}");
				writer.WriteLine($"  CSV 数组明细 (前 20):");
				var csvNames = new[] { "ABL", "EXP", "TALENT", "PALAM", "TRAIN", "MARK", "ITEM", "BASE", "SOURCE", "EX", "STR", "EQUIP", "TEQUIP", "FLAG", "TFLAG", "CFLAG", "TCVAR", "CSTR", "STAIN", "CDFLAG1", "CDFLAG2", "STRNAME", "TSTR", "SAVESTR", "GLOBAL", "GLOBALS", "DAY", "TIME", "MONEY" };
				for (int i = 0; i < maxDataList.Length && i < csvNames.Length; i++)
				{
					if (maxDataList[i] > 0)
						writer.WriteLine($"    {csvNames[i],-10} {maxDataList[i],8} 个元素");
				}
				// 估算：每个 string 元素 ~50 字节，每个字典条目 ~100 字节
				long estStrings = totalElements * 50L;
				long estCharData = charCount * 8L * 20L * 100L; // 8 个字典 × 20 条目 × 100 字节
				writer.WriteLine($"  估算 CSV 数组内存: {FormatBytes(estStrings)}");
				writer.WriteLine($"  估算角色数据内存:  {FormatBytes(estCharData)}");
			}
			else
			{
				writer.WriteLine("  ConstantData: null (尚未加载)");
			}
		}
		catch (Exception ex)
		{
			writer.WriteLine($"  (读取失败: {ex.Message})");
		}
		writer.WriteLine();
	}

	private static void WriteRuntimeVariableStats(StreamWriter writer)
	{
		writer.WriteLine("--- 运行时变量数据 (VariableData) ---");
		try
		{
			var vd = GlobalStatic.VariableData;
			if (vd != null)
			{
				var (totalBytes, totalElements, charCount, userDefCount) = vd.GetDiagnosticStats();
				writer.WriteLine($"  总元素数: {totalElements}");
				writer.WriteLine($"  数组内存 (int64, 8 字节/元素): {FormatBytes(totalBytes)}");
				writer.WriteLine($"  角色数:   {charCount}");
				writer.WriteLine($"  用户定义变量数: {userDefCount}");
			}
			else
			{
				writer.WriteLine("  VariableData: null (尚未加载)");
			}
		}
		catch (Exception ex)
		{
			writer.WriteLine($"  (读取失败: {ex.Message})");
		}
		writer.WriteLine();
	}

	private static void WriteConsoleImageCache(StreamWriter writer, object console)
	{
		writer.WriteLine("--- Console 图像缓存 (CBG/背景/图层) ---");
		if (console == null)
		{
			writer.WriteLine("  (console 实例不可用，跳过)");
			writer.WriteLine();
			return;
		}

		try
		{
			// 1. cbgList (ClientBackGroundImage 列表)
			var cbgField = console.GetType().GetField("cbgList",
				BindingFlags.NonPublic | BindingFlags.Instance);
			if (cbgField?.GetValue(console) is IList cbgList)
			{
				writer.WriteLine($"  cbgList (CBG 背景图数):            {cbgList.Count}");

				// 统计 CBG 中 ASprite 的创建状态
				int namedSprites = 0, unamedSprites = 0;
				foreach (var cbg in cbgList)
				{
					var imgProp = cbg.GetType().GetField("Img",
						BindingFlags.Public | BindingFlags.Instance);
					if (imgProp?.GetValue(cbg) is ASprite spr)
					{
						if (string.IsNullOrEmpty(spr.Name))
							unamedSprites++;
						else
							namedSprites++;
					}
				}
				writer.WriteLine($"    ├─ 有名称 Sprite: {namedSprites}");
				writer.WriteLine($"    └─ 无名称 Sprite: {unamedSprites}");
			}
			else
			{
				writer.WriteLine("  cbgList: (无法读取)");
			}
		}
		catch (Exception ex)
		{
			writer.WriteLine($"  cbgList: (读取失败: {ex.Message})");
		}

		try
		{
			// 2. backgroundList (ConsoleBackground 列表)
			var bgField = console.GetType().GetField("backgroundList",
				BindingFlags.NonPublic | BindingFlags.Instance);
			if (bgField?.GetValue(console) is IList bgList)
			{
				writer.WriteLine($"  backgroundList (背景层数):         {bgList.Count}");
			}
			else
			{
				writer.WriteLine("  backgroundList: (无法读取)");
			}
		}
		catch (Exception ex)
		{
			writer.WriteLine($"  backgroundList: (读取失败: {ex.Message})");
		}

		try
		{
			// 3. bakedBackground (SKBitmap)
			var bakedField = console.GetType().GetField("bakedBackground",
				BindingFlags.NonPublic | BindingFlags.Instance);
			if (bakedField?.GetValue(console) is IDisposable bakedBmp)
			{
				// 通过 SKBitmap 的属性反射获取宽高
				var wProp = bakedBmp.GetType().GetProperty("Width");
				var hProp = bakedBmp.GetType().GetProperty("Height");
				int w = wProp?.GetValue(bakedBmp) is int iw ? iw : 0;
				int h = hProp?.GetValue(bakedBmp) is int ih ? ih : 0;
				writer.WriteLine($"  bakedBackground (合成背景图):      {(w > 0 ? $"{w}x{h}" : "已分配")}");
			}
			else
			{
				writer.WriteLine("  bakedBackground (合成背景图):      null");
			}
		}
		catch (Exception ex)
		{
			writer.WriteLine($"  bakedBackground: (读取失败: {ex.Message})");
		}

		try
		{
			// 4. _imageLayerManager (ImageLayerManager)
			var ilmField = console.GetType().GetField("_imageLayerManager",
				BindingFlags.NonPublic | BindingFlags.Instance);
			if (ilmField?.GetValue(console) is object ilm)
			{
				var layersProp = ilm.GetType().GetProperty("Layers",
					BindingFlags.Public | BindingFlags.Instance);
				if (layersProp?.GetValue(ilm) is IList layers)
				{
					writer.WriteLine($"  ImageLayerManager 图层数:         {layers.Count}");
				}
				else
				{
					writer.WriteLine("  ImageLayerManager 图层数: (无法读取)");
				}
			}
			else
			{
				writer.WriteLine("  ImageLayerManager 图层数: (无法读取)");
			}
		}
		catch (Exception ex)
		{
			writer.WriteLine($"  ImageLayerManager: (读取失败: {ex.Message})");
		}

		try
		{
			// 5. displayLineList (显示行缓存，受 Config.MaxLog 约束)
			var dllField = console.GetType().GetField("displayLineList",
				BindingFlags.NonPublic | BindingFlags.Instance);
			if (dllField?.GetValue(console) is IList dll)
			{
				writer.WriteLine($"  displayLineList (显示行缓存):      {dll.Count} / {Runtime.Config.Config.MaxLog}");
			}
			else
			{
				writer.WriteLine("  displayLineList: (无法读取)");
			}
		}
		catch (Exception ex)
		{
			writer.WriteLine($"  displayLineList: (读取失败: {ex.Message})");
		}

		writer.WriteLine();
	}

	private static void WriteFontDiagnostics(StreamWriter writer)
	{
		writer.WriteLine("--- 字体资源 ---");
		try
		{
			// GlobalStatic.CustomTypefaces — 加载的自定义字体文件
			var typefacesField = typeof(GlobalStatic).GetField("CustomTypefaces",
				BindingFlags.Public | BindingFlags.Static);
			if (typefacesField?.GetValue(null) is IList typefaces)
			{
				writer.WriteLine($"  CustomTypefaces (自定义字体数): {typefaces.Count}");
			}
			else
			{
				writer.WriteLine("  CustomTypefaces: (无法读取)");
			}

			// PrivateFontCollection 中的字体数
			var pfcField = typeof(GlobalStatic).GetField("Pfc",
				BindingFlags.Public | BindingFlags.Static);
			if (pfcField?.GetValue(null) is System.Drawing.Text.PrivateFontCollection pfc)
			{
				writer.WriteLine($"  PrivateFontCollection (已加载字体): {pfc.Families.Length}");
			}
			else
			{
				writer.WriteLine("  PrivateFontCollection: (无法读取)");
			}
		}
		catch (Exception ex)
		{
			writer.WriteLine($"  (读取失败: {ex.Message})");
		}
		writer.WriteLine();
	}

	private static void WriteFontFactoryStats(StreamWriter writer)
	{
		writer.WriteLine("--- FontFactory 缓存 ---");
		try
		{
			var (fontCount, fallbackCount, fallbackCpCount, gdiCount) = UI.FontFactory.GetDiagnosticStats();
			writer.WriteLine($"  SKFont 缓存 (fontDic):              {fontCount}");
			writer.WriteLine($"  fallbackTypefaceCache (字符级):     {fallbackCount}");
			writer.WriteLine($"  fallbackTypefaceCodepointCache:     {fallbackCpCount}");
			writer.WriteLine($"  GDI Font 缓存 (gdiFontDic):         {gdiCount}");
		}
		catch (Exception ex)
		{
			writer.WriteLine($"  (读取失败: {ex.Message})");
		}
		writer.WriteLine();
	}

	private static void WriteSqlConnections(StreamWriter writer)
	{
		writer.WriteLine("--- SQLite 连接 ---");
		try
		{
			var connField = typeof(GameData.Function.SqlManager)
				.GetField("_connections", BindingFlags.NonPublic | BindingFlags.Static);
			if (connField != null && connField.GetValue(null) is IDictionary connDict)
			{
				writer.WriteLine($"  SqlManager 连接数: {connDict.Count}");
				foreach (var key in connDict.Keys)
				{
					writer.WriteLine($"    连接: {key}");
				}
			}
			else
			{
				writer.WriteLine("  SqlManager 连接数: (无法读取)");
			}
		}
		catch (Exception ex)
		{
			writer.WriteLine($"  (读取失败: {ex.Message})");
		}
		writer.WriteLine();
	}

	private static string FormatBytes(long bytes)
	{
		if (bytes < 1024) return $"{bytes} B";
		if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
		if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
		return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
	}
}