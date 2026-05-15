using System;
using System.Collections.Generic;
using System.Text;
using MinorShift.Emuera.Runtime.Script;
using MinorShift.Emuera.Runtime.Script.Data;
using MinorShift.Emuera.Runtime.Script.Statements;
using MinorShift.Emuera.Runtime.Script.Statements.Expression;
using MinorShift.Emuera.Runtime.Script.Statements.Variable;
using MinorShift.Emuera.GameView;
using MinorShift.Emuera.GameProc;
using MinorShift.Emuera.GameData.Variable;
using MinorShift.Emuera.UI.Framework.Forms;
using XEmuera.Forms;

namespace MinorShift.Emuera
{
	/* 1756 作成
	 * できるだけデータはprivateにして必要なものだけが参照するようにしようという設計だったのは今は昔。
	 * 改変のたびにProcess.Instance.XXXなんかがどんどん増えていく。
	 * まあ、増えるのは仕方ないと諦める事にして、行儀の悪い参照の仕方をするものたちをせめて一箇所に集めて管理しようという計画である。
	 * これからはInstanceを public static に解放することはやめ、ここから参照する。
	 * しかし、できるならここからの参照は減らしたい。
	 */
	internal static class GlobalStatic
	{
		public static MainWindow MainWindow;
		public static EmueraConsole Console;
		public static Process Process;
		public static GameBase GameBaseData;
		public static ConstantData ConstantData;
		public static VariableData VariableData;
		public static VariableEvaluator VEvaluator;
		public static IdentifierDictionary IdentifierDictionary;
		public static ExpressionMediator EMediator;
		public static LabelDictionary LabelDictionary;
		public static Runtime.Utils.CtrlZ ctrlZ = new Runtime.Utils.CtrlZ();
		public static IPathProvider PathProvider;
		public static SkiaPfc Pfc = new();
		public static GameProc.Sound[] Sound = new GameProc.Sound[16];
		public static GameProc.Sound Bgm;
		public static List<SkiaSharp.SKTypeface> CustomTypefaces = new();


		//ERBloaderに引数解析の結果を渡すための橋渡し変数
		//1756 Processから移動。Program.AnalysisMode用
		public static Dictionary<string, Int64> tempDic = new Dictionary<string, long>();
		#region EE_FORCE_QUIT_AND_RESTART
		public static bool ForceQuitAndRestart = false;//連続実行を防ぐ
		#endregion

#if DEBUG
		public static List<FunctionLabelLine> StackList = new List<FunctionLabelLine>();
#endif
		public static void Reset()
		{
			Process = null;
			ConstantData = null;
			GameBaseData = null;
			EMediator = null;
			VEvaluator = null;
			VariableData = null;
			Console = null;
			MainWindow = null;
			LabelDictionary = null;
			IdentifierDictionary = null;
			tempDic.Clear();
		}
	}
}
