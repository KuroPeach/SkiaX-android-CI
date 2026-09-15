using MinorShift.Emuera.GameData.Variable;
using MinorShift.Emuera.Runtime.Script.Statements;
using MinorShift.Emuera.Runtime.Script.Statements.Variable;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MinorShift.Emuera.Runtime.Utils
{
	/// <summary>
	/// 变量写入监视(调试用)。只记录"开始监视时变量监视列表里的变量"被 ERB 脚本写入的情况。
	/// 记录内容:变量 + 新值 + 当前函数 + 文件:行号,逐条写入独立日志文件;停止时生成汇总报告。
	/// 未开始监视时开销仅为一次静态 bool 判断。
	/// 线程约束:仅脚本主线程(写入钩子)与 UI 线程(开始/停止按钮、关闭窗口)调用,内部无锁。
	/// </summary>
	internal static class VarWriteWatch
	{
		/// <summary>明细写入日志的最大条数(超过后只统计,防止日志无限膨胀)。</summary>
		private const int MaxDetailRecords = 100000;

		private static readonly HashSet<string> targetKeys = new(StringComparer.Ordinal);
		private static readonly HashSet<string> targetNames = new(StringComparer.Ordinal);
		private static readonly Dictionary<string, WriteStat> stats = new(StringComparer.Ordinal);
		private static StreamWriter writer;
		private static DateTime startTime;
		private static int totalCount;

		private sealed class WriteStat
		{
			public int Count;
			public string LastValue;
			public string LastSite;
		}

		/// <summary>是否正在监视。</summary>
		public static bool IsEnabled { get; private set; }

		/// <summary>明细日志文件路径(监视期间有效)。</summary>
		public static string LogPath { get; private set; }

		/// <summary>
		/// 开始监视。watchedKeys 是变量标识(形如 ITEM:6、CFLAG:MASTER:100)。
		/// filePath 是明细日志输出文件。
		/// </summary>
		public static void Start(string filePath, IEnumerable<string> watchedKeys)
		{
			StopWriter();
			targetKeys.Clear();
			targetNames.Clear();
			stats.Clear();
			totalCount = 0;
			foreach (string key in watchedKeys)
			{
				if (string.IsNullOrEmpty(key) || !targetKeys.Add(key))
					continue;
				int pos = key.IndexOf(':');
				targetNames.Add(pos < 0 ? key : key[..pos]);
			}
			LogPath = filePath;
			startTime = DateTime.Now;
			try
			{
				string dir = Path.GetDirectoryName(filePath);
				if (!string.IsNullOrEmpty(dir))
					Directory.CreateDirectory(dir);
				writer = new StreamWriter(filePath, false) { AutoFlush = true };
				writer.WriteLine("# 变量写入监视开始 " + startTime.ToString("yyyy-MM-dd HH:mm:ss"));
				writer.WriteLine("# 监视对象: " + string.Join(", ", targetKeys));
			}
			catch
			{
				writer = null;
			}
			IsEnabled = targetKeys.Count > 0;
		}

		/// <summary>停止监视,把汇总报告追加到日志文件并返回报告文本(未在监视时返回 null)。</summary>
		public static string Stop()
		{
			if (!IsEnabled)
				return null;
			IsEnabled = false;
			string report = BuildReport();
			if (writer != null)
			{
				try
				{
					writer.WriteLine();
					writer.Write(report);
				}
				catch { }
			}
			StopWriter();
			return report;
		}

		private static void StopWriter()
		{
			if (writer != null)
			{
				try { writer.Dispose(); } catch { }
				writer = null;
			}
			IsEnabled = false;
		}

		/// <summary>
		/// 变量写入钩子。由 VariableTerm 的 SetValue/ChangeValue 在写入成功后调用。
		/// </summary>
		public static void OnWrite(VariableTerm term, string valueText, ExpressionMediator exm)
		{
			if (!IsEnabled || term?.Identifier == null)
				return;
			try
			{
				if (!targetNames.Contains(term.Identifier.Name))
					return;
				RecordWrite(BuildKey(term, exm), valueText);
			}
			catch
			{
				//监视本身绝不能影响脚本执行
			}
		}

		/// <summary>
		/// 批量写入钩子(VARSET)。令牌的 SetValueAll 会写"最后一维的 [start,end)",2D/3D 则写整个数组。
		/// </summary>
		public static void OnWriteAll(FixedVariableTerm term, string valueText, int start, int end)
		{
			if (!IsEnabled || term?.Identifier == null)
				return;
			try
			{
				var token = term.Identifier;
				if (!targetNames.Contains(token.Name))
					return;
				int dims = GetDimCount(token);
				if (dims == 0)
				{
					RecordWrite(token.Name, valueText);
					return;
				}
				if (dims == 1)
				{
					if (token.IsCharacterData)
					{
						//角色0D:令牌的 SetValueAll 只写 Index1 指定的单个角色,忽略 start/end
						RecordWrite(token.Name + ":" + term.Index1, valueText);
						return;
					}
					//非角色1D:索引按 [start,end) 写入
					string prefix = token.Name + ":";
					for (int i = start; i < end; i++)
						RecordWrite(prefix + i, valueText);
					return;
				}
				if (dims == 2 && token.IsCharacterData)
				{
					//角色1D:角色=Index1,索引按 [start,end) 写入
					string prefix = token.Name + ":" + term.Index1 + ":";
					for (int i = start; i < end; i++)
						RecordWrite(prefix + i, valueText);
					return;
				}
				//2D/3D:引擎会写整个数组(角色变量只写该角色)
				if (dims == 3 && token.IsCharacterData)
				{
					//角色2D:令牌的 SetValueAll 写该角色的整个 [sizes0, sizes1](sizes 只有两维,不可取 GetLength(2))
					string head = token.Name + ":" + term.Index1 + ":";
					for (long i = 0; i < token.GetLength(0); i++)
						for (long j = 0; j < token.GetLength(1); j++)
							RecordWrite(head + i + ":" + j, valueText);
					return;
				}
				if (dims == 3)
				{
					//非角色3D:令牌的 SetValueAll 写整个数组(忽略 Index1,不能只枚举该切片)
					for (long h = 0; h < token.GetLength(0); h++)
					{
						string prefix0 = token.Name + ":" + h + ":";
						for (long i = 0; i < token.GetLength(1); i++)
						{
							string prefix1 = prefix0 + i + ":";
							for (long k = 0; k < token.GetLength(2); k++)
								RecordWrite(prefix1 + k, valueText);
						}
					}
					return;
				}
				//非角色2D:令牌的 SetValueAll 写整个数组
				for (long i = 0; i < token.GetLength(0); i++)
				{
					string prefix1 = token.Name + ":" + i + ":";
					for (long j = 0; j < token.GetLength(1); j++)
						RecordWrite(prefix1 + j, valueText);
				}
			}
			catch
			{
				//监视本身绝不能影响脚本执行
			}
		}

		/// <summary>
		/// 批量写入钩子(CVARSET)。角色 i 在 [start,end);indexNum 为指定下标(-1 表示该角色变量没有下标维)。
		/// </summary>
		public static void OnWriteEachChara(FixedVariableTerm term, long indexNum, string valueText, int start, int end)
		{
			if (!IsEnabled || term?.Identifier == null)
				return;
			try
			{
				var token = term.Identifier;
				if (!targetNames.Contains(token.Name))
					return;
				if (GetDimCount(token) <= 1)
				{
					//角色0维变量:只有角色编号
					string prefix = token.Name + ":";
					for (int i = start; i < end; i++)
						RecordWrite(prefix + i, valueText);
					return;
				}
				for (int i = start; i < end; i++)
					RecordWrite(token.Name + ":" + i + ":" + indexNum, valueText);
			}
			catch
			{
				//监视本身绝不能影响脚本执行
			}
		}

		/// <summary>
		/// 数组整体赋值钩子(ERB 数组字面量赋值,如 HOGE:0 = 1,2,3)。
		/// 令牌从"除最后一维外的下标"定位起点,向最后一维连续写 values.Length 个元素。
		/// 调用方需先判断 IsEnabled 再调用,避免非监视时构造字符串的开销。
		/// </summary>
		public static void OnWriteArray(VariableTerm term, long[] values, ExpressionMediator exm)
		{
			OnWriteArrayCore(term, values.Length, exm, static (values, i) => values[i].ToString(), values);
		}

		/// <inheritdoc cref="OnWriteArray(VariableTerm, long[], ExpressionMediator)"/>
		public static void OnWriteArray(VariableTerm term, string[] values, ExpressionMediator exm)
		{
			OnWriteArrayCore(term, values.Length, exm, static (values, i) => values[i], values);
		}

		/// <inheritdoc cref="OnWriteArray(VariableTerm, long[], ExpressionMediator)"/>
		public static void OnWriteArray(VariableTerm term, double[] values, ExpressionMediator exm)
		{
			OnWriteArrayCore(term, values.Length, exm, static (values, i) => values[i].ToString(), values);
		}

		private static void OnWriteArrayCore<T>(VariableTerm term, int length, ExpressionMediator exm, Func<T, int, string> getText, T values)
		{
			if (!IsEnabled || term?.Identifier == null)
				return;
			try
			{
				var token = term.Identifier;
				if (!targetNames.Contains(token.Name))
					return;
				int dims = GetDimCount(token);
				if (dims == 0)
				{
					RecordWrite(token.Name, length > 0 ? getText(values, 0) : "");
					return;
				}
				if (dims == 1 && token.IsCharacterData)
				{
					//角色0D:整体赋值的实际语义由令牌决定,此处只记被定位到的角色
					RecordWrite(token.Name + ":" + GetBaseIndex(term, 0, exm), length > 0 ? getText(values, 0) : "");
					return;
				}
				var sb = new StringBuilder(token.Name);
				for (int i = 0; i < dims - 1; i++)
					sb.Append(':').Append(GetBaseIndex(term, i, exm));
				sb.Append(':');
				string prefix = sb.ToString();
				long start = GetBaseIndex(term, dims - 1, exm);
				for (int k = 0; k < length; k++)
					RecordWrite(prefix + (start + k), getText(values, k));
			}
			catch
			{
				//监视本身绝不能影响脚本执行
			}
		}

		/// <summary>取变量第 dim 维的定位下标(FixedVariableTerm 用缓存的 Index,普通 VariableTerm 实时求值)。</summary>
		private static long GetBaseIndex(VariableTerm term, int dim, ExpressionMediator exm)
		{
			if (term is FixedVariableTerm fixedTerm)
				return dim == 0 ? fixedTerm.Index1 : dim == 1 ? fixedTerm.Index2 : fixedTerm.Index3;
			return term.GetElementInt(dim, exm);
		}

		/// <summary>命中监视对象时记录一条(变量+新值+函数+行号)。</summary>
		private static void RecordWrite(string key, string valueText)
		{
			if (string.IsNullOrEmpty(key) || !targetKeys.Contains(key))
				return;
			//调试窗口自身的写入(监视刷新/锁定写回/控制台赋值)不记录
			var console = GlobalStatic.Console;
			if (console != null && console.RunERBFromMemory)
				return;

			totalCount++;
			string site = DescribeSite();
			if (!stats.TryGetValue(key, out WriteStat stat))
				stats[key] = stat = new WriteStat();
			stat.Count++;
			stat.LastValue = valueText;
			stat.LastSite = site;
			if (writer != null)
			{
				if (totalCount <= MaxDetailRecords)
					writer.WriteLine(string.Format("[{0}] {1} = {2}\t@ {3}",
						DateTime.Now.ToString("HH:mm:ss.fff"), key, Limit(valueText, 200), site));
				else if (totalCount == MaxDetailRecords + 1)
					writer.WriteLine("(明细超过 " + MaxDetailRecords + " 条,后续仅统计)");
			}
		}

		/// <summary>
		/// 按变量当前下标构造变量标识(形如 NAME:i 或 NAME:i:j)。
		/// 监视列表登记与实际写入共用此格式,保证两边一致。
		/// </summary>
		public static string BuildKey(VariableTerm term, ExpressionMediator exm)
		{
			if (term?.Identifier == null)
				return "";
			var token = term.Identifier;
			var sb = new StringBuilder(token.Name);
			int dims = GetDimCount(token);
			for (int i = 0; i < dims; i++)
			{
				long index;
				try
				{
					if (term is FixedVariableTerm fixedTerm)
						index = i == 0 ? fixedTerm.Index1 : i == 1 ? fixedTerm.Index2 : fixedTerm.Index3;
					else
						index = term.GetElementInt(i, exm);
				}
				catch
				{
					//下标取不到(如无参形式)时视为无效标识
					return "";
				}
				sb.Append(':').Append(index);
			}
			return sb.ToString();
		}

		/// <summary>下标个数。角色变量的维度与 VariableParser 的展开规则一致(角色1D→2,角色2D→3)。</summary>
		private static int GetDimCount(VariableToken token)
		{
			if (token.IsCharacterData)
				return token.IsArray2D ? 3 : token.IsArray1D ? 2 : 1;
			return token.IsArray3D ? 3 : token.IsArray2D ? 2 : token.IsArray1D ? 1 : 0;
		}

		private static string DescribeSite()
		{
			LogicalLine line = GlobalStatic.Process?.getCurrentLine;
			if (line == null)
				return "<unknown>";
			string func = line.ParentLabelLine?.LabelName ?? "<unknown>";
			if (line.Position == null)
				return func;
			return string.Format("{0} ({1}:{2})", func, line.Position.Value.Filename, line.Position.Value.LineNo);
		}

		private static string BuildReport()
		{
			var sb = new StringBuilder();
			sb.AppendLine("===== 变量写入监视报告 =====");
			sb.AppendLine(string.Format("监视期间: {0} ~ {1}",
				startTime.ToString("yyyy-MM-dd HH:mm:ss"), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));
			sb.AppendLine(string.Format("总写入次数: {0}", totalCount));
			sb.AppendLine("--- 按变量统计(变量 | 次数 | 最后一次写入) ---");
			foreach (var pair in stats)
				sb.AppendLine(string.Format("{0} | {1} 次 | 值={2} @ {3}",
					pair.Key, pair.Value.Count, Limit(pair.Value.LastValue, 80), pair.Value.LastSite));
			if (totalCount == 0)
				sb.AppendLine("(期间内没有任何被监视变量被写入)");
			if (!string.IsNullOrEmpty(LogPath))
				sb.AppendLine("明细日志: " + LogPath);
			return sb.ToString();
		}

		private static string Limit(string str, int max)
		{
			if (string.IsNullOrEmpty(str))
				return "";
			//日志一行一条记录,换行统一成空格
			str = str.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ');
			return str.Length <= max ? str : str[..max] + "...";
		}
	}
}
