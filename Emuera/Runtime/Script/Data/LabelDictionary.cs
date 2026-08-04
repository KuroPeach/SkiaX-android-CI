using MinorShift.Emuera.Runtime.Script.Statements;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MinorShift.Emuera.Runtime.Script.Data;

//1.713 LogicalLine.csから分割
/// <summary>
/// ラベルのジャンプ先の辞書。Erbファイル読み込み時に作成
/// </summary>
internal sealed class LabelDictionary
{
	#region EM_私家版_辞書獲得
	public string[] NoneventKeys => noneventLabelDic.Keys.ToArray();
	#endregion
	public LabelDictionary()
	{
		Initialized = false;
	}
	/// <summary>
	/// 本体。全てのFunctionLabelLineを記録
	/// </summary>
	Dictionary<string, List<FunctionLabelLine>> labelAtDic = new(Config.Config.StrComper);
	List<FunctionLabelLine> invalidList = [];
	Dictionary<string, Dictionary<FunctionLabelLine, GotoLabelLine>> labelDollarList = new(Config.Config.StrComper);
	int count;

	HashSet<string> loadedFileSet = [];
	int currentFileCount;
	int totalFileCount;

	public int Count { get { return count; } }

	/// <summary>
	/// 诊断用：返回标签字典的详细统计
	/// </summary>
	public (int totalLabels, int eventLabels, int nonEventLabels, int loadedFiles, long statementLines, int jumpTables, long estBytes, int privateVarCount) GetDiagnosticStats()
	{
		int eventCount = 0;
		foreach (var kvp in eventLabelDic)
			foreach (var list in kvp.Value)
				eventCount += list.Count;

		// 遍历编译后的语句链表，统计实际语句行数 / SELECTCASE 跳转表 / 私有变量
		long statementLines = 0;
		int jumpTables = 0;
		int privateVarCount = 0;
		var visited = new HashSet<LogicalLine>();

		foreach (var labelList in labelAtDic.Values)
		{
			foreach (var label in labelList)
			{
				privateVarCount += label.PrivateVarCount;
				var line = label.NextLine;
				while (line != null && !visited.Add(line))
					line = line.NextLine;
				// 从标签的下一条语句开始沿链遍历到文件末尾或下一个标签
				while (line != null)
				{
					visited.Add(line);
					if (line is InstructionLine instr)
					{
						statementLines++;
						if (instr.SelectCaseJumpTable != null)
							jumpTables++;
					}
					// 遇到下一个函数标签或文件末端（NullLine）即停止
					if (line is FunctionLabelLine || line is NullLine)
						break;
					line = line.NextLine;
				}
			}
		}

		// 估算：语句行对象（含 Argument 表达式树）平均约 250 字节
		long estBytes = statementLines * 250L
			+ (long)count * 200L          // FunctionLabelLine 对象
			+ (long)privateVarCount * 120L; // 私有变量 token
		return (count, eventCount, noneventLabelDic.Count, loadedFileSet.Count, statementLines, jumpTables, estBytes, privateVarCount);
	}

	/// <summary>
	/// これがfalseである間は式中関数は呼べない
	/// （つまり関数宣言の初期値として式中関数は使えない）
	/// </summary>
	public bool Initialized { get; set; }
	#region Initialized 前用
	public FunctionLabelLine GetSameNameLabel(FunctionLabelLine point)
	{
		string id = point.LabelName;
		if (!labelAtDic.TryGetValue(id, out List<FunctionLabelLine> value))
			return null;
		if (point.IsError)
			return null;
		List<FunctionLabelLine> labelList = value;
		if (labelList.Count <= 1)
			return null;
		return labelList[0];
	}


	Dictionary<string, List<FunctionLabelLine>[]> eventLabelDic = new(Config.Config.StrComper);
	Dictionary<string, FunctionLabelLine> noneventLabelDic = new(Config.Config.StrComper);
	
	public void SortLabel(FunctionLabelLine label)
	{
		string key = label.LabelName;

		if(!label.IsEvent)
		{
			noneventLabelDic[key] = label;
			GlobalStatic.IdentifierDictionary.resizeLocalVars("ARG", label.LabelName, label.ArgLength);
			GlobalStatic.IdentifierDictionary.resizeLocalVars("ARGS", label.LabelName, label.ArgsLength);
			GlobalStatic.IdentifierDictionary.resizeLocalVars("ARGF", label.LabelName, label.ArgFloatLength);
			return;
		}

		if (Config.Config.CompatiCallEvent)
			noneventLabelDic.Add(key, label);

		List<FunctionLabelLine>[] eventLabels;
		if (!eventLabelDic.TryGetValue(key, out eventLabels))
		{
			eventLabels = new List<FunctionLabelLine>[4];
			eventLabels[0] = new List<FunctionLabelLine>();
			eventLabels[1] = new List<FunctionLabelLine>();
			eventLabels[2] = new List<FunctionLabelLine>();
			eventLabels[3] = new List<FunctionLabelLine>();
			eventLabelDic.Add(key, eventLabels);
		}

		if (label.IsOnly) // eventLabels[0] = onlylist;
			eventLabels[0].Add(label);
		if (label.IsPri) // eventLabels[1] = prilist;
			eventLabels[1].Add(label);
		if (label.IsLater) // eventLabels[3] = laterlist;
			eventLabels[3].Add(label);
		if ((!label.IsPri) && (!label.IsLater)) // eventLabels[2] = normallist;
			eventLabels[2].Add(label);

		int localMax = 0;
		int localsMax = 0;
		int localFloatMax = 0;

		for (int i = 0; i < 4; i++)
		{
			for (int j = 0; j < eventLabels[i].Count; j++)
			{
				if (eventLabels[i][j].LocalLength > localMax)
					localMax = eventLabels[i][j].LocalLength;
				if (eventLabels[i][j].LocalsLength > localsMax)
					localsMax = eventLabels[i][j].LocalsLength;
				if (eventLabels[i][j].LocalFloatLength > localFloatMax)
					localFloatMax = eventLabels[i][j].LocalFloatLength;
			}
		}

		if (localMax < GlobalStatic.IdentifierDictionary.getLocalDefaultSize("LOCAL"))
			localMax = GlobalStatic.IdentifierDictionary.getLocalDefaultSize("LOCAL");
		if (localsMax < GlobalStatic.IdentifierDictionary.getLocalDefaultSize("LOCALS"))
			localsMax = GlobalStatic.IdentifierDictionary.getLocalDefaultSize("LOCALS");
		if (localFloatMax < GlobalStatic.IdentifierDictionary.getLocalDefaultSize("LOCALF"))
			localFloatMax = GlobalStatic.IdentifierDictionary.getLocalDefaultSize("LOCALF");

		for (int i = 0; i < 4; i++)
		{
			for (int j = 0; j < eventLabels[i].Count; j++)
			{
				eventLabels[i][j].LocalLength = localMax;
				eventLabels[i][j].LocalsLength = localsMax;
				eventLabels[i][j].LocalFloatLength = localFloatMax;
			}
		}
	}

	public void InitEventLabelDic()
	{

		foreach (KeyValuePair<string, List<FunctionLabelLine>[]> pair in eventLabelDic)
		foreach (List<FunctionLabelLine> list in pair.Value)
			list.Clear();
		eventLabelDic.Clear();
		noneventLabelDic.Clear();
	}
	public void SortLabels()
	{
		foreach (KeyValuePair<string, List<FunctionLabelLine>[]> pair in eventLabelDic)
			foreach (List<FunctionLabelLine> list in pair.Value)
				list.Clear();
		eventLabelDic.Clear();
		noneventLabelDic.Clear();
		foreach (KeyValuePair<string, List<FunctionLabelLine>> pair in labelAtDic)
		{
			string key = pair.Key;
			List<FunctionLabelLine> list = pair.Value;
			if (list.Count > 1)
				list.Sort();
			if (!list[0].IsEvent)
			{
				noneventLabelDic.Add(key, list[0]);
				GlobalStatic.IdentifierDictionary.resizeLocalVars("ARG", list[0].LabelName, list[0].ArgLength);
				GlobalStatic.IdentifierDictionary.resizeLocalVars("ARGS", list[0].LabelName, list[0].ArgsLength);
				GlobalStatic.IdentifierDictionary.resizeLocalVars("ARGF", list[0].LabelName, list[0].ArgFloatLength);
				continue;
			}
			//1810alpha010 オプションによりイベント関数をイベント関数でないかのように呼び出すことを許可
			//eramaker仕様 - #PRI #LATER #SINGLE等を無視し、最先に定義された関数1つのみを呼び出す
			if (Config.Config.CompatiCallEvent)
				noneventLabelDic.Add(key, list[0]);
			List<FunctionLabelLine>[] eventLabels = new List<FunctionLabelLine>[4];
			List<FunctionLabelLine> onlylist = [];
			List<FunctionLabelLine> prilist = [];
			List<FunctionLabelLine> normallist = [];
			List<FunctionLabelLine> laterlist = [];
			int localMax = 0;
			int localsMax = 0;
			int localFloatMax = 0;
			for (int i = 0; i < list.Count; i++)
			{
				if (list[i].LocalLength > localMax)
					localMax = list[i].LocalLength;
				if (list[i].LocalsLength > localsMax)
					localsMax = list[i].LocalsLength;
				if (list[i].LocalFloatLength > localFloatMax)
					localFloatMax = list[i].LocalFloatLength;
				if (list[i].IsOnly)
					onlylist.Add(list[i]);
				if (list[i].IsPri)
					prilist.Add(list[i]);
				if (list[i].IsLater)
					laterlist.Add(list[i]);//#PRIかつ#LATERなら二重に登録する。eramakerの仕様
				if (!list[i].IsPri && !list[i].IsLater)
					normallist.Add(list[i]);
			}
			if (localMax < GlobalStatic.IdentifierDictionary.getLocalDefaultSize("LOCAL"))
				localMax = GlobalStatic.IdentifierDictionary.getLocalDefaultSize("LOCAL");
			if (localsMax < GlobalStatic.IdentifierDictionary.getLocalDefaultSize("LOCALS"))
				localsMax = GlobalStatic.IdentifierDictionary.getLocalDefaultSize("LOCALS");
			if (localFloatMax < GlobalStatic.IdentifierDictionary.getLocalDefaultSize("LOCALF"))
				localFloatMax = GlobalStatic.IdentifierDictionary.getLocalDefaultSize("LOCALF");
			eventLabels[0] = onlylist;
			eventLabels[1] = prilist;
			eventLabels[2] = normallist;
			eventLabels[3] = laterlist;
			for (int i = 0; i < 4; i++)
			{
				for (int j = 0; j < eventLabels[i].Count; j++)
				{
					eventLabels[i][j].LocalLength = localMax;
					eventLabels[i][j].LocalsLength = localsMax;
					eventLabels[i][j].LocalFloatLength = localFloatMax;
				}
			}
			eventLabelDic.Add(key, eventLabels);
		}
	}

	public void RemoveAll()
	{
		Initialized = false;
		count = 0;
		foreach ((_, var array) in eventLabelDic)
			foreach (var list in array)
				list.Clear();
		eventLabelDic.Clear();
		noneventLabelDic.Clear();

		foreach ((_, var value) in labelAtDic)
			value.Clear();
		labelAtDic.Clear();
		foreach ((_, var value) in labelDollarList)
			value.Clear();
		labelDollarList.Clear();
		loadedFileSet.Clear();
		invalidList.Clear();
		currentFileCount = 0;
		totalFileCount = 0;
	}

	//ファイル名に基づき、そのファイルに紐づくラベルを削除する
	public void RemoveLabelWithPath(string fname)
	{
		List<string> removeFunctions = [];
		foreach (var (functionName, functions) in labelAtDic)
		{
			var removeCount = functions.RemoveAll(line => IsMatch(fname, line));

			count -= removeCount;

			if (functions.Count == 0)
				removeFunctions.Add(functionName);
		}

		foreach (var rKey in removeFunctions)
		{
			labelAtDic.Remove(rKey);
		}

		invalidList.RemoveAll(line => IsMatch(fname, line));

		static bool IsMatch(string fname, FunctionLabelLine line)
		{
			return string.Equals(line.Position.Value.Filename, fname, Config.Config.SCIgnoreCase);
		}
	}


	/// <summary>
	/// ファイルの重複をチェックし、重複していたらすでにあるそのファイルに関連するラベルを消去する
	/// </summary>
	public void IfFileLoadClearLabelWithPath(string filename)
	{
		if (loadedFileSet.Contains(filename))
		{
			currentFileCount = loadedFileSet.Count;
			RemoveLabelWithPath(filename);
			return;
		}
		totalFileCount++;
		currentFileCount = totalFileCount;
		loadedFileSet.Add(filename);
	}
	public void AddLabel(FunctionLabelLine point)
	{
		point.Index = count;
		point.FileIndex = currentFileCount;
		count++;
		string id = point.LabelName;
		if (labelAtDic.TryGetValue(id, out List<FunctionLabelLine> labelList))
		{
			labelList.Add(point);
		}
		else
		{
			labelAtDic.TryAdd(id, [point]);
		}
	}

	public bool AddLabelDollar(GotoLabelLine point)
	{
		string id = point.LabelName;
		if (labelDollarList.TryGetValue(id, out var label))
		{
			return label.TryAdd(point.ParentLabelLine, point);
		};
		labelDollarList.TryAdd(id, new() { { point.ParentLabelLine, point }, });
		return true;
	}

	#endregion


	public List<FunctionLabelLine>[] GetEventLabels(string key)
	{
		if (eventLabelDic.TryGetValue(key, out List<FunctionLabelLine>[] ret))
			return ret;
		else
			return null;
	}

	public FunctionLabelLine GetNonEventLabel(string key)
	{
		if (noneventLabelDic.TryGetValue(key, out FunctionLabelLine ret))
			return ret;
		else
			return null;
	}

	public List<FunctionLabelLine> GetAllLabels(bool getInvalidList)
	{
		List<FunctionLabelLine> ret = [];
		foreach (List<FunctionLabelLine> list in labelAtDic.Values)
			ret.AddRange(list);
		if (getInvalidList)
			ret.AddRange(invalidList);
		return ret;
	}

	public GotoLabelLine GetLabelDollar(string key, FunctionLabelLine labelAtLine)
	{
		if (labelDollarList.TryGetValue(key, out var labels))
		{
			if (labels.TryGetValue(labelAtLine, out var label))
				return label;
		}
		return null;
	}

	internal void AddInvalidLabel(FunctionLabelLine invalidLabelLine)
	{
		invalidList.Add(invalidLabelLine);
	}
}
