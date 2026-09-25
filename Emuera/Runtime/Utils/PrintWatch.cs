using MinorShift.Emuera.Runtime.Script.Statements;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MinorShift.Emuera.Runtime.Utils
{
	/// <summary>
	/// 打印监视(调试用)。记录每一次送往控制台的文本输出,以及触发它的函数与行号。
	/// 未开启时每次打印只多一次静态 bool 判断。
	/// </summary>
	internal static class PrintWatch
	{
		/// <summary>明细日志的最大条数(超过后只统计,防止日志无限膨胀)。</summary>
		private const int MaxDetailRecords = 100000;

		private static readonly Dictionary<string, int> stats = new(StringComparer.Ordinal);
		private static StreamWriter writer;
		private static DateTime startTime;
		private static int totalCount;

		/// <summary>是否正在监视。</summary>
		public static bool IsEnabled { get; private set; }

		/// <summary>明细日志文件路径(监视期间有效)。</summary>
		public static string LogPath { get; private set; }

		/// <summary>开始监视,明细写入 filePath。</summary>
		public static void Start(string filePath)
		{
			StopWriter();
			stats.Clear();
			totalCount = 0;
			LogPath = filePath;
			startTime = DateTime.Now;
			try
			{
				string dir = Path.GetDirectoryName(filePath);
				if (!string.IsNullOrEmpty(dir))
					Directory.CreateDirectory(dir);
				writer = new StreamWriter(filePath, false) { AutoFlush = true };
				writer.WriteLine("# 打印监视开始 " + startTime.ToString("yyyy-MM-dd HH:mm:ss"));
			}
			catch
			{
				writer = null;
			}
			IsEnabled = true;
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
		/// 打印钩子。由 PrintStringBuffer/EmueraConsole 的文本输出入口调用。
		/// </summary>
		public static void OnPrint(string text)
		{
			if (!IsEnabled)
				return;
			try
			{
				//调试窗口自身的输出(调试命令回显等)不记录
				var console = GlobalStatic.Console;
				if (console != null && console.RunERBFromMemory)
					return;
				//空行也记录(如 PRINTL 无内容、DRAWLINE 前的强制换行),文本显示为 (空行)
				string shown = string.IsNullOrWhiteSpace(text) ? "(空行)" : Limit(text, 500);
				string site = DescribeSite();
				totalCount++;
				stats[site] = stats.TryGetValue(site, out int count) ? count + 1 : 1;
				if (writer != null)
				{
					if (totalCount <= MaxDetailRecords)
						writer.WriteLine(string.Format("[{0}] {1}\t@ {2}",
							DateTime.Now.ToString("HH:mm:ss.fff"), shown, site));
					else if (totalCount == MaxDetailRecords + 1)
						writer.WriteLine("(明细超过 " + MaxDetailRecords + " 条,后续仅统计)");
				}
			}
			catch
			{
				//监视本身绝不能影响脚本执行
			}
		}

		/// <summary>当前执行位置:函数名 + 文件:行号。取不到时返回 &lt;system&gt;。</summary>
		private static string DescribeSite()
		{
			LogicalLine line = GlobalStatic.Process?.getCurrentLine;
			if (line == null)
				return "<system>";
			string func = line.ParentLabelLine?.LabelName ?? "<system>";
			if (line.Position == null)
				return func;
			return string.Format("{0} ({1}:{2})", func, line.Position.Value.Filename, line.Position.Value.LineNo);
		}

		private static string BuildReport()
		{
			var sb = new StringBuilder();
			sb.AppendLine("===== 打印监视报告 =====");
			sb.AppendLine(string.Format("监视期间: {0} ~ {1}",
				startTime.ToString("yyyy-MM-dd HH:mm:ss"), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));
			sb.AppendLine(string.Format("总输出条数: {0}", totalCount));
			sb.AppendLine("--- 按打印位置统计(位置 | 次数) ---");
			foreach (var pair in stats)
				sb.AppendLine(string.Format("{0} | {1} 次", pair.Key, pair.Value));
			if (totalCount == 0)
				sb.AppendLine("(期间内没有任何打印)");
			if (!string.IsNullOrEmpty(LogPath))
				sb.AppendLine("明细日志: " + LogPath);
			return sb.ToString();
		}

		private static string Limit(string str, int max)
		{
			if (string.IsNullOrEmpty(str))
				return "";
			//日志一行一条记录:把换行显示为 \n
			str = str.Replace("\r\n", "\\n").Replace("\n", "\\n").Replace("\r", "\\n");
			return str.Length <= max ? str : str[..max] + "...";
		}
	}
}
