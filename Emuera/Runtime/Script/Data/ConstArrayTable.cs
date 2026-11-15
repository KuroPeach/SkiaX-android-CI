using MinorShift.Emuera.Runtime.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace MinorShift.Emuera.Runtime.Script.Data;

/// <summary>
/// 常数表 CSV 的读取规则：
/// - 二维表用 @1 / @2 两个单元格定位。规范位置是 (1,2) 与 (2,1)（行列均从 1 起算），
///   位于 (2,1) 的标记决定纵向维度，位于 (1,2) 的标记决定横向维度；因此两种摆法对应两种维度方向。
///   纵向标记下方：同列橙色为注册数、右一列蓝色为注册名；
///   横向标记右方：同行橙色为注册数、下一行蓝色为注册名；
///   两标记夹出的灰色区域为数值区，数值所在行/列各自取对应方向的注册数。
/// - 一维表无 @ 标记，每行形如 [注册数, 注册名, 值]。
/// - 空行与以 ';' 开头的注释行忽略；注册数或值为空时按顺延/0 处理；
///   超出声明长度的索引不读，未写入的元素保持 0（字符串为 ""）。
/// - 同一维度内注册名重复时以先出现者为准；字段可用双引号包裹以包含逗号（"" 表示一个引号）。
/// - 值按数组类型转换：#DIM 取整数、#DIMS 取原样字符串、#DIMF 取浮点。
/// </summary>
internal static class ConstArrayTable
{
	private const string Marker1 = "@1";
	private const string Marker2 = "@2";
	internal const int KindInt = 0;
	internal const int KindStr = 1;
	internal const int KindFloat = 2;

	/// <summary>
	/// 查找与数组同名的常数表：CSV 目录下的 .csv / .erd，以及 ERB 目录（含子目录）下的 .erd。
	/// 只有在内容符合本表格式时才返回；找到文件但格式不合规会通过 error 说明原因，
	/// 以便区分“没有同名文件”与“文件写错了”。
	/// rows 返回已解析的表格，调用方可直接交给 TryLoad，避免重复读盘。
	/// </summary>
	public static string FindFile(string varName, out List<string[]> rows, out string error)
	{
		rows = null;
		error = null;
		if (string.IsNullOrEmpty(varName))
		{
			error = "常数表名称为空";
			return null;
		}
		var candidates = new List<string>();
		candidates.AddRange(FindFiles(varName + ".csv", false));
		candidates.AddRange(FindFiles(varName + ".erd", false));
		candidates.AddRange(FindFiles(varName + ".erd", true));
		string invalid = null;
		foreach (string path in candidates)
		{
			if (TryReadTable(path, out List<string[]> tableRows, out string reason))
			{
				rows = tableRows;
				return path;
			}
			invalid ??= path + " — " + reason;
		}
		if (invalid != null)
			error = "常数表的格式不合规：" + invalid;
		else
			error = "常数表声明找不到同名文件（CSV/" + varName + ".csv、CSV/" + varName + ".erd，或 ERB 下的 " + varName + ".ERD）";
		return null;
	}

	// CSV / ERB 目录在运行期不会变化，按文件名建一次索引，避免每个常数表声明都递归枚举一次 ERB。
	private static Dictionary<string, List<string>> csvFileIndex;
	private static Dictionary<string, List<string>> erbFileIndex;

	private static List<string> FindFiles(string fileName, bool fromErb)
	{
		Dictionary<string, List<string>> index;
		if (fromErb)
		{
			erbFileIndex ??= BuildIndex(Program.ErbDir, SearchOption.AllDirectories);
			index = erbFileIndex;
		}
		else
		{
			csvFileIndex ??= BuildIndex(Program.CsvDir, SearchOption.TopDirectoryOnly);
			index = csvFileIndex;
		}
		return index.TryGetValue(fileName, out List<string> list) ? list : [];
	}

	private static Dictionary<string, List<string>> BuildIndex(string dir, SearchOption option)
	{
		var index = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
		if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
			return index;
		try
		{
			foreach (string path in Directory.GetFiles(dir, "*", option))
			{
				string name = Path.GetFileName(path);
				if (!index.TryGetValue(name, out List<string> list))
				{
					list = [];
					index.Add(name, list);
				}
				list.Add(path);
			}
		}
		catch (IOException)
		{
			// 目录读取失败等价于没有常数表；声明处会给出“找不到同名文件”。
		}
		catch (UnauthorizedAccessException)
		{
		}
		foreach (List<string> list in index.Values)
			list.Sort(StringComparer.OrdinalIgnoreCase);
		return index;
	}

	/// <summary>
	/// 读取 EE_ERD 风格的维度名称表。二维数组用 &lt;名称&gt;@1 / @2，一维数组用 &lt;名称&gt; 或 &lt;名称&gt;@1。
	/// 每行为 [编号, 名称]；只补充空缺，不覆盖值表已注册的同名键。
	/// 返回读取过程中遇到的问题，由调用方以警告形式报告（不静默吞掉）。
	/// </summary>
	public static List<string> LoadErdNames(string varName, int[] lengths, Dictionary<string, int>[] namesByDim)
	{
		var errors = new List<string>();
		int dimension = lengths.Length;
		for (int d = 0; d < dimension; d++)
		{
			var suffixes = dimension == 1 ? new[] { "", "@1" } : new[] { "@" + (d + 1) };
			foreach (string suffix in suffixes)
				foreach (string path in FindFiles(varName + suffix + ".erd", true))
					LoadErdNameFile(path, namesByDim[d], lengths[d], errors);
		}
		return errors;
	}

	private static void LoadErdNameFile(string path, Dictionary<string, int> dic, int length, List<string> errors)
	{
		try
		{
			foreach (string raw in File.ReadAllLines(path, EncodingHandler.DetectEncoding(path)))
			{
				string line = raw.Trim();
				if (line.Length == 0 || line[0] == ';')
					continue;
				string[] cells = SplitCells(line);
				if (cells.Length < 2)
					continue;
				if (!int.TryParse(cells[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int index) || index < 0 || index >= length)
					continue;
				string name = cells[1].Trim().Trim('\uFEFF').Trim();
				if (name.Length > 0)
					TryAdd(dic, name, index);
			}
		}
		catch (Exception e)
		{
			// 名称表是可选补充，读取失败不影响值表；但要告知用户，避免名称下标莫名找不到。
			errors.Add(path + " 读取失败（" + e.Message + "），该名称表未生效");
		}
	}

	private static List<string[]> ReadGrid(string path)
	{
		var rows = new List<string[]>();
		foreach (string raw in File.ReadAllLines(path, EncodingHandler.DetectEncoding(path)))
		{
			string line = raw.Trim();
			if (line.Length == 0 || line[0] == ';')
				continue;
			string[] cells = SplitCells(line);
			bool any = false;
			for (int i = 0; i < cells.Length; i++)
			{
				cells[i] = cells[i].Trim().Trim('\uFEFF').Trim();
				if (cells[i].Length > 0)
					any = true;
			}
			if (any)
				rows.Add(cells);
		}
		return rows;
	}

	/// <summary>
	/// 按逗号切分单元格，支持双引号包裹的字段（字段内可含逗号，"" 表示一个引号）。
	/// 字符串表的值可能带逗号，不能用裸 Split(',')。
	/// </summary>
	private static string[] SplitCells(string line)
	{
		var cells = new List<string>();
		var sb = new System.Text.StringBuilder();
		bool quoted = false;
		bool fieldStart = true;
		for (int i = 0; i < line.Length; i++)
		{
			char ch = line[i];
			if (quoted)
			{
				if (ch == '"')
				{
					if (i + 1 < line.Length && line[i + 1] == '"')
					{
						sb.Append('"');
						i++;
					}
					else
						quoted = false;
				}
				else
					sb.Append(ch);
				continue;
			}
			if (ch == '"' && fieldStart)
			{
				quoted = true;
				fieldStart = false;
				continue;
			}
			if (ch == ',')
			{
				cells.Add(sb.ToString());
				sb.Clear();
				fieldStart = true;
				continue;
			}
			if (!char.IsWhiteSpace(ch))
				fieldStart = false;
			sb.Append(ch);
		}
		cells.Add(sb.ToString());
		return [.. cells];
	}

	private static bool TryReadTable(string path, out List<string[]> rows, out string reason)
	{
		reason = null;
		try
		{
			rows = ReadGrid(path);
		}
		catch (Exception e)
		{
			rows = null;
			reason = "读取失败（" + e.Message + "）";
			return false;
		}
		return CheckTable(rows, out reason);
	}

	/// <summary>
	/// 判定内容是否为本表格式，失败时给出具体原因。
	/// 含 @1 / @2 的按二维表；否则要求每行 [注册数, 注册名, 值] 三列。
	/// </summary>
	private static bool CheckTable(List<string[]> rows, out string reason)
	{
		reason = null;
		if (rows.Count == 0)
		{
			reason = "空文件，或只有空行与注释行";
			return false;
		}
		if (HasMarkers(rows))
			return true;
		for (int i = 0; i < rows.Count; i++)
		{
			if (rows[i].Length < 3)
			{
				reason = "第 " + (i + 1) + " 个有效行不足 3 列（一维表需要 [注册数,注册名,值]）";
				return false;
			}
			if (rows[i][0].Length > 0 && !int.TryParse(rows[i][0], NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
			{
				reason = "第 " + (i + 1) + " 个有效行的首列不是编号";
				return false;
			}
		}
		foreach (string[] row in rows)
			if (row[1].Length > 0 && row[0].Length > 0)
				return true;
		reason = "没有 [注册数,注册名,值] 形式的数据行";
		return false;
	}

	private static bool HasMarkers(List<string[]> rows)
	{
		foreach (string[] row in rows)
			foreach (string cell in row)
				if (cell == Marker1 || cell == Marker2)
					return true;
		return false;
	}

	/// <summary>
	/// 解析常数表。values 为按 [第1维, 第2维] 展平的一维数组，具体类型由 valueKind 决定。
	/// rows 为 FindFile 已解析好的内容（可为 null，此时按 path 读取）。
	/// </summary>
	public static bool TryLoad(string path, List<string[]> rows, int[] lengths, int valueKind, out Dictionary<string, int>[] namesByDim, out object values, out string error)
	{
		namesByDim = null;
		values = null;
		error = null;
		if (rows == null)
		{
			try
			{
				rows = ReadGrid(path);
			}
			catch (Exception e)
			{
				error = "常数表读取失败：" + e.Message;
				return false;
			}
		}

		int dimension = lengths.Length;
		long size = 1;
		foreach (int len in lengths)
			size *= len;
		if (size <= 0 || size > 1000000)
		{
			error = "常数表对应的数组尺寸非法";
			return false;
		}

		namesByDim = new Dictionary<string, int>[dimension];
		for (int d = 0; d < dimension; d++)
			namesByDim[d] = new Dictionary<string, int>();
		if (valueKind == KindStr)
		{
			var strValues = new string[size];
			for (int i = 0; i < strValues.Length; i++)
				strValues[i] = "";
			values = strValues;
		}
		else if (valueKind == KindFloat)
			values = new double[size];
		else
			values = new long[size];

		if (HasMarkers(rows))
		{
			if (dimension != 2)
			{
				error = "含 @1 / @2 的常数表只能用于二维数组";
				return false;
			}
			if (!TryLocate(rows, out int vDim, out int hDim, out int vRow, out int vCol, out int hRow, out int hCol, out error))
				return false;
			Load2D(rows, lengths, valueKind, vDim, hDim, vRow, vCol, hRow, hCol, namesByDim, values);
			return true;
		}
		if (dimension != 1)
		{
			error = "不含 @1 / @2 的常数表只能用于一维数组";
			return false;
		}
		Load1D(rows, lengths, valueKind, namesByDim, values);
		return true;
	}

	/// <summary>
	/// @1 / @2 必须分别占据 (2,1) 与 (1,2)。(2,1) 上的标记给出纵向维度编号，
	/// (1,2) 上的标记给出横向维度编号；两者互换即为转置写法。
	/// </summary>
	private static bool TryLocate(List<string[]> rows, out int vDim, out int hDim,
		out int vRow, out int vCol, out int hRow, out int hCol, out string error)
	{
		vDim = hDim = -1;
		vRow = vCol = hRow = hCol = -1;
		error = null;
		for (int r = 0; r < rows.Count; r++)
		{
			for (int c = 0; c < rows[r].Length; c++)
			{
				string cell = rows[r][c];
				if (cell != Marker1 && cell != Marker2)
					continue;
				int dim = cell == Marker1 ? 0 : 1;
				if (r == 1 && c == 0)
				{
					vDim = dim;
					vRow = r;
					vCol = c;
				}
				else if (r == 0 && c == 1)
				{
					hDim = dim;
					hRow = r;
					hCol = c;
				}
				else
				{
					error = "常数表的 @1 / @2 必须分别位于 (1,2) 与 (2,1)";
					return false;
				}
			}
		}
		if (vDim < 0 || hDim < 0 || vDim == hDim)
		{
			error = "常数表缺少 @1 / @2 标记，或两标记重复";
			return false;
		}
		return true;
	}

	private static void Load2D(List<string[]> rows, int[] lengths, int valueKind, int vDim, int hDim,
		int vRow, int vCol, int hRow, int hCol, Dictionary<string, int>[] namesByDim, object values)
	{
		int valueStartRow = vRow + 1;
		int valueStartCol = hCol + 1;
		int maxCols = 0;
		foreach (string[] row in rows)
			maxCols = Math.Max(maxCols, row.Length);

		// 纵向：橙色注册数在标记所在列，蓝色注册名在右一列；蓝色名所在行即横向标记下一行。
		var rowIndex = new int[rows.Count];
		int lastRowIndex = -1;
		for (int r = valueStartRow; r < rows.Count; r++)
		{
			string[] row = rows[r];
			int index;
			if (vCol < row.Length && int.TryParse(row[vCol], NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) && n >= 0)
			{
				index = n;
				lastRowIndex = n;
			}
			else
			{
				index = ++lastRowIndex;
			}
			rowIndex[r] = index;
			if (index >= 0 && index < lengths[vDim] && vCol + 1 < row.Length && row[vCol + 1].Length > 0)
				TryAdd(namesByDim[vDim], row[vCol + 1], index);
		}

		// 横向：橙色注册数在标记所在行，蓝色注册名在下一行；空缺的注册数按前一列顺延。
		string[] numRow = hRow < rows.Count ? rows[hRow] : [];
		string[] nameRow = hRow + 1 < rows.Count ? rows[hRow + 1] : [];
		var colIndex = new int[maxCols];
		int lastColIndex = -1;
		for (int c = valueStartCol; c < maxCols; c++)
		{
			int index;
			if (c < numRow.Length && int.TryParse(numRow[c], NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) && n >= 0)
			{
				index = n;
				lastColIndex = n;
			}
			else
			{
				index = ++lastColIndex;
			}
			colIndex[c] = index;
			if (index >= 0 && index < lengths[hDim] && c < nameRow.Length && nameRow[c].Length > 0)
				TryAdd(namesByDim[hDim], nameRow[c], index);
		}

		for (int r = valueStartRow; r < rows.Count; r++)
		{
			string[] row = rows[r];
			int ri = rowIndex[r];
			if (ri < 0 || ri >= lengths[vDim])
				continue;
			for (int c = valueStartCol; c < row.Length && c < maxCols; c++)
			{
				if (!TryConvert(row[c], valueKind, out object value))
					continue;
				int ci = colIndex[c];
				if (ci < 0 || ci >= lengths[hDim])
					continue;
				int first = vDim == 0 ? ri : ci;
				int second = vDim == 0 ? ci : ri;
				Put(values, first * lengths[1] + second, valueKind, value);
			}
		}
	}

	private static void Load1D(List<string[]> rows, int[] lengths, int valueKind, Dictionary<string, int>[] namesByDim, object values)
	{
		int lastIndex = -1;
		foreach (string[] row in rows)
		{
			if (row.Length < 3)
				continue;
			int index;
			if (int.TryParse(row[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) && n >= 0)
			{
				index = n;
				lastIndex = n;
			}
			else
			{
				index = ++lastIndex;
			}
			if (index < 0 || index >= lengths[0])
				continue;
			if (row[1].Length > 0)
				TryAdd(namesByDim[0], row[1], index);
			if (TryConvert(row[2], valueKind, out object value))
				Put(values, index, valueKind, value);
		}
	}

	private static bool TryConvert(string cell, int valueKind, out object value)
	{
		value = null;
		if (cell.Length == 0)
			return false;
		if (valueKind == KindStr)
		{
			value = cell;
			return true;
		}
		if (valueKind == KindFloat)
		{
			if (!double.TryParse(cell, NumberStyles.Float, CultureInfo.InvariantCulture, out double f))
				return false;
			value = f;
			return true;
		}
		if (!long.TryParse(cell, NumberStyles.Integer, CultureInfo.InvariantCulture, out long i))
			return false;
		value = i;
		return true;
	}

	private static void Put(object values, int index, int valueKind, object value)
	{
		if (valueKind == KindStr)
			((string[])values)[index] = (string)value;
		else if (valueKind == KindFloat)
			((double[])values)[index] = (double)value;
		else
			((long[])values)[index] = (long)value;
	}

	private static void TryAdd(Dictionary<string, int> dic, string name, int index)
	{
		if (!dic.ContainsKey(name))
			dic.Add(name, index);
	}
}
