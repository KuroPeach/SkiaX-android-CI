using MinorShift.Emuera.Runtime.Script.Statements;
using System.Text;

namespace MinorShift.Emuera.Runtime.Utils
{
	/// <summary>
	/// 调试用的"当前位置 / 调用栈"格式化。打印监视与变量写入监视共用,保证两者格式一致。
	/// 变量写入监视原先的脚本外兜底标记是 &lt;unknown&gt;,共用后统一为 &lt;system&gt;。
	/// </summary>
	internal static class CallStackFormatter
	{
		/// <summary>调用栈最多显示的层数。</summary>
		private const int MaxStackFrames = 20;

		/// <summary>当前位置:函数名 (文件:行号)。取不到时返回 &lt;system&gt;。</summary>
		public static string DescribeSite()
		{
			LogicalLine line = GlobalStatic.Process?.getCurrentLine;
			if (line == null)
				return "<system>";
			string func = line.ParentLabelLine?.LabelName ?? "<system>";
			if (line.Position == null)
				return func;
			return string.Format("{0} ({1}:{2})", func, line.Position.Value.Filename, line.Position.Value.LineNo);
		}

		/// <summary>
		/// 单行内联调用栈(由内到外):当前函数(文件:行) &lt;- 调用者(调用点文件:行) &lt;- ...
		/// 每个外层帧取"它调用下一层的那一行"(ReturnAddress);取不到时退回该函数的定义位置。
		/// </summary>
		public static string DescribeStack()
		{
			var sb = new StringBuilder(DescribeSite());
			var frames = GlobalStatic.Process?.State?.FunctionList;
			if (frames == null)
				return sb.ToString();
			int shown = 0;
			//frames[^1] 是当前函数(已由 DescribeSite 表示),从栈顶下面一帧开始
			for (int i = frames.Count - 2; i >= 0; i--)
			{
				var caller = frames[i];
				if (caller == null)
					continue;
				if (shown >= MaxStackFrames)
				{
					sb.Append(" <- ...");
					break;
				}
				string where = FormatPosition(frames[i + 1]?.ReturnAddress) ?? FormatPosition(caller.CurrentLabel);
				if (where == null)
					continue;
				sb.Append(" <- ").Append(caller.FunctionName).Append('(').Append(where).Append(')');
				shown++;
			}
			return sb.ToString();
		}

		/// <summary>取某一行的"文件:行号",取不到返回 null。</summary>
		private static string FormatPosition(LogicalLine line)
		{
			if (line?.Position == null)
				return null;
			return string.Format("{0}:{1}", line.Position.Value.Filename, line.Position.Value.LineNo);
		}
	}
}
