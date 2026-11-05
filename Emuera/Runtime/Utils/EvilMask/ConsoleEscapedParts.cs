using MinorShift.Emuera.UI.Game;
using System.Collections.Generic;

namespace MinorShift.Emuera.Runtime.Utils.EvilMask;

internal sealed class ConsoleEscapedParts
{
	private readonly struct EscapedPartRecord
	{
		public readonly int Line;
		public readonly int Depth;
		public readonly int Top;
		public readonly int Bottom;
		public readonly long Id;
		public readonly sbyte DivType;
		public readonly AConsoleDisplayNode Part;

		public EscapedPartRecord(int line, int depth, int top, int bottom, long id, sbyte divType, AConsoleDisplayNode part)
		{
			Line = line;
			Depth = depth;
			Top = top;
			Bottom = bottom;
			Id = id;
			DivType = divType;
			Part = part;
		}
	}
	
	// 指定初始容量 256，减少 List 早期扩容的内存开销
	static readonly List<EscapedPartRecord> records = new List<EscapedPartRecord>(256);
	
	static bool getOnce;
	static int lastTop, lastBottom, lastGeneration;
	public static bool Changed { get; private set; }

	public static bool TestedInRange(int top, int bottom, int gen)
	{
		return getOnce && top == lastTop && bottom == lastBottom && gen == lastGeneration;
	}

	public static void Clear()
	{
		getOnce = false;
		records.Clear();
		Changed = false;
	}

	public static void Add(AConsoleDisplayNode part, int line, int depth, int top, int bottom)
	{
		var id = Utils.TimePoint();
		sbyte divType = 0;
		
		if (part is ConsoleDivPart div)
			divType = (sbyte)((!div.IsRelative ? 2 : 0) | 1);

		records.Add(new EscapedPartRecord(line, depth, top, bottom, id, divType, part));
		
		Changed = true;
	}

	public static void Remove(int line)
	{
		int removed = records.RemoveAll(r => r.Line >= line);
		if (removed > 0) Changed = true;
	}

	public static void RemoveAt(int line)
	{
		int removed = records.RemoveAll(r => r.Line == line);
		if (removed > 0) Changed = true;
	}

	#region CLI_Debug（--CliDebug モード用）
	/// <summary>
	/// 現在登録されている div 部件を画面位置順（上→下）で返す。
	/// CLI の screen / buttons / click が div 内の文字とボタンを扱えるようにするため
	/// （描画用の GetPartsInRange は行範囲・深度フィルタ付きで、全画面読みには使えない）。
	/// </summary>
	public static List<AConsoleDisplayNode> CollectDivParts()
	{
		// 全量形态供诊断/离线读取；可选中按钮必须用带行范围的形态，否则滚出可视区的 div 按钮也会被收集。
		return CollectDivParts(int.MinValue / 2, int.MaxValue / 2);
	}

	/// <summary>按可见行范围收集 div 部件（与 <see cref="GetPartsInRange"/> 同行范围判据
	/// 「r.Top &lt;= bottom + 1 &amp;&amp; r.Bottom &gt;= top」）。
	/// ⛔ 不要按 generation 过滤：div 记录没有代际语义，gen 只是缓存键。</summary>
	public static List<AConsoleDisplayNode> CollectDivParts(int top, int bottom)
	{
		var tmp = new List<EscapedPartRecord>();
		foreach (var r in records)
			if (r.Part is ConsoleDivPart && r.Top <= bottom + 1 && r.Bottom >= top)
				tmp.Add(r);
		tmp.Sort((a, b) => a.Top != b.Top ? a.Top.CompareTo(b.Top) : a.Line.CompareTo(b.Line));
		var list = new List<AConsoleDisplayNode>(tmp.Count);
		foreach (var r in tmp)
			list.Add(r.Part);
		return list;
	}
	#endregion

	public static void GetPartsInRange(int top, int bottom, int gen, Dictionary<int, List<AConsoleDisplayNode>> rmap)
	{
		if (GlobalStatic.Console?.GetLineNo > Config.Config.MaxLog)
		{
			var correction = GlobalStatic.Console.GetLineNo - Config.Config.MaxLog;
			top += correction;
			bottom += correction;
		}
		if (rmap == null) return;
		rmap.Clear();

		// 原生 foreach 遍历（不引入 LINQ 的额外分配）
		foreach (var r in records)
		{
			if ((r.DivType & 2) != 0 || (r.Top <= bottom + 1 && r.Bottom >= top && 
			(r.DivType != 0 || top > r.Line || r.Line > bottom + 1)))
			{
				if (!rmap.TryGetValue(r.Depth, out var list))
				{
					list = new List<AConsoleDisplayNode>();
					rmap[r.Depth] = list;
				}
				list.Add(r.Part);
			}
		}

		getOnce = true;
		lastTop = top; lastBottom = bottom; lastGeneration = gen;
		Changed = false;
	}
}
