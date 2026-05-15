using System.Diagnostics;
using System.Text;
using MinorShift.Emuera;
using MinorShift.Emuera.GameView;
using MinorShift.Emuera.Runtime.Config;
using MinorShift.Emuera.UI.Game;
using SkiaSharp;

namespace MinorShift.Emuera.UI.Game
{
	public enum DisplayMode
	{
		Relative,
		Absolute,
		AbsoluteLeftBottom,
		AbsoluteLeftTop
	}
}

namespace MinorShift.Emuera.Runtime.Utils.EvilMask
{
	internal sealed class Utils
	{
		internal sealed class MixedNum
		{
			public static int ToPixel(MixedNum num, int def)
			{
				return num != null ? num.isPx ? num.num : num.num * Config.Config.FontSize / 100 : def;
			}
			public static int ToPixel(MixedNum num)
			{
				return num != null ? num.isPx ? num.num : num.num * Config.Config.FontSize / 100 : 0;
			}
			public static float ToPixelf(MixedNum num, float def)
			{
				return num != null ? num.isPx ? num.num : num.num * Config.Config.FontSize / 100f : def;
			}
			public static float ToPixelf(MixedNum num)
			{
				return num != null ? num.isPx ? num.num : num.num * Config.Config.FontSize / 100f : 0f;
			}
			public int num;
			public bool isPx;
			public override string ToString()
			{
				var sb = new StringBuilder();
				if (isPx) sb.Append(num).Append("px");
				else sb.Append(num * Config.Config.FontSize / 100);
				return sb.ToString();
			}
			public StringBuilder BuilderString(StringBuilder sb)
			{
				if (isPx) sb.Append(num).Append("px");
				else sb.Append(num * Config.Config.FontSize / 100);
				return sb;
			}
		}
		internal sealed class StyledBoxModel
		{
			public MixedNum[] border, margin, padding, radius;
			public int[] color;
		}
		public static T CreateIfNull<T>(ref T obj) where T : new()
		{
			if (obj == null) obj = new T();
			return obj;
		}
		public static int[] CreateIfNull(ref int[] obj, int len = 4)
		{
			if (obj == null) obj = new int[len];
			return obj;
		}
		public static void ParseMixedNum(ref MixedNum num, string tag, string word, string attrValue)
		{
			if (num == null) num = new MixedNum();
			if (attrValue.EndsWith("px", System.StringComparison.OrdinalIgnoreCase))
			{
				if (!int.TryParse(attrValue.Substring(0, attrValue.Length - 2), out num.num))
					return;
				num.isPx = true;
			}
			else if (!int.TryParse(attrValue, out num.num))
				return;
		}
		public static void ParseParam4MixedNum(ref MixedNum[] nums, string tag, string word, string attrValue)
		{
			string[] tokens = attrValue.Split(',');
			switch (tokens.Length)
			{
				case 1:
					ParseMixedNum(ref nums[0], tag, word, tokens[0].Trim());
					nums[1] = nums[0];
					nums[2] = nums[0];
					nums[3] = nums[0];
					break;
				case 2:
					ParseMixedNum(ref nums[0], tag, word, tokens[0].Trim());
					ParseMixedNum(ref nums[1], tag, word, tokens[1].Trim());
					nums[2] = nums[0];
					nums[3] = nums[1];
					break;
				case 3:
					ParseMixedNum(ref nums[0], tag, word, tokens[0].Trim());
					ParseMixedNum(ref nums[1], tag, word, tokens[1].Trim());
					ParseMixedNum(ref nums[2], tag, word, tokens[2].Trim());
					nums[3] = nums[1];
					break;
				case 4:
					for (int i = 0; i < 4; i++)
						ParseMixedNum(ref nums[i], tag, word, tokens[i].Trim());
					break;
			}
		}
		public static bool TryParseStyledBoxModel(ref StyledBoxModel box, string tag, string word, string attrValue)
		{
			switch (word.ToLower())
			{
				case "border":
					if (CreateIfNull(ref box).border == null) box.border = new MixedNum[4];
					ParseParam4MixedNum(ref box.border, tag, word, attrValue);
					return true;
				case "radius":
					if (CreateIfNull(ref box).radius == null) box.radius = new MixedNum[4];
					ParseParam4MixedNum(ref box.radius, tag, word, attrValue);
					return true;
				case "margin":
					if (CreateIfNull(ref box).margin == null) box.margin = new MixedNum[4];
					ParseParam4MixedNum(ref box.margin, tag, word, attrValue);
					return true;
				case "padding":
					if (CreateIfNull(ref box).padding == null) box.padding = new MixedNum[4];
					ParseParam4MixedNum(ref box.padding, tag, word, attrValue);
					return true;
			}
			return false;
		}
		public static void AddTagMixedNumArg(StringBuilder sb, string name, MixedNum num)
		{
			if (num != null && num.num != 0)
			{
				sb.Append(' ').Append(name).Append("='");
				num.BuilderString(sb).Append('\'');
			}
		}
		public static void AddTagMixedParam(StringBuilder sb, string name, MixedNum[] nums)
		{
			if (nums != null)
			{
				sb.Append(' ').Append(name).Append("='");
				if (ReferenceEquals(nums[0], nums[1]) && ReferenceEquals(nums[0], nums[2]) && ReferenceEquals(nums[0], nums[3]))
					nums[0].BuilderString(sb);
				else if (ReferenceEquals(nums[0], nums[2]) && ReferenceEquals(nums[1], nums[3]))
				{
					nums[0].BuilderString(sb);
					sb.Append(',');
					nums[1].BuilderString(sb);
				}
				else if (ReferenceEquals(nums[1], nums[3]))
				{
					nums[0].BuilderString(sb);
					sb.Append(',');
					nums[1].BuilderString(sb);
					sb.Append(',');
					nums[2].BuilderString(sb);
				}
				else
					for (int i = 0; i < nums.Length; i++)
					{
						nums[i].BuilderString(sb);
						if (i + 1 < nums.Length) sb.Append(',');
					}
				sb.Append('\'');
			}
		}
		public static void AddTagArg(StringBuilder sb, string name, string value)
		{
			sb.Append(' ').Append(name).Append("='").Append(value).Append('\'');
		}
		public static void MixedNum4ToInt4(MixedNum[] mnums, ref int[] nums)
		{
			if (mnums != null)
			{
				CreateIfNull(ref nums);
				for (int i = 0; i < 4; i++)
					nums[i] = MixedNum.ToPixel(mnums[i]);
			}
		}
		public static string GetValidPath(string path)
		{
			path = path.Replace('/', '\\').Replace("..\\", "");
			try
			{
				if (System.IO.Path.GetPathRoot(path) != string.Empty)
					return null;
			}
			catch
			{
				return null;
			}
			return path;
		}

		static readonly Stopwatch stopwatch = new();
		static readonly long stopwatch_base = System.DateTime.Now.Ticks;
		public static long TimePoint()
		{
			if (!stopwatch.IsRunning) stopwatch.Start();
			return stopwatch_base + stopwatch.Elapsed.Ticks;
		}

		public static void AddColorParam(StringBuilder sb, string name, System.Drawing.Color color)
		{
			if (color != System.Drawing.Color.Transparent)
			{
				sb.Append(' ').Append(name).Append("='").Append(HtmlManager.GetColorToString(color)).Append('\'');
			}
		}
		public static void AddColorParam4(StringBuilder sb, string name, System.Drawing.Color[] colors)
		{
			if (colors != null)
			{
				sb.Append(' ').Append(name).Append("='");
				if (colors[0] == colors[1] && colors[0] == colors[2] && colors[0] == colors[3])
					sb.Append(HtmlManager.GetColorToString(colors[0]));
				else if (colors[0] == colors[2] && colors[1] == colors[3])
					sb.Append(HtmlManager.GetColorToString(colors[0])).Append(',').Append(HtmlManager.GetColorToString(colors[1]));
				else if (colors[1] == colors[3])
					sb.Append(HtmlManager.GetColorToString(colors[0])).Append(',').Append(HtmlManager.GetColorToString(colors[1]))
						.Append(',').Append(HtmlManager.GetColorToString(colors[2]));
				else
					for (int i = 0; i < colors.Length; i++)
					{
						sb.Append(HtmlManager.GetColorToString(colors[i]));
						if (i + 1 < colors.Length) sb.Append(',');
					}
				sb.Append('\'');
			}
		}
	}

	internal sealed class Shape
	{
		internal struct Direction { public const int Top = 0, Right = 1, Bottom = 2, Left = 3; };
		internal struct Corner { public const int LeftTop = 0, RightTop = 1, RightBottom = 2, LeftBottom = 3; };
	}

	class ConsoleDivPart : AConsoleDisplayNode
	{
		public ConsoleDivPart(Utils.MixedNum xPos, Utils.MixedNum yPos, Utils.MixedNum width, Utils.MixedNum height, int depth, int color, Utils.StyledBoxModel box, bool isRelative, DisplayMode displayMode, ConsoleDisplayLine[] childs)
		{
			IsRelative = isRelative;
			Children = childs;
			Text = string.Empty;
		}

		public ConsoleDisplayLine[] Children { get; }
		public bool IsRelative { get; }
		public bool IsEscaped { get; set; }
		public override bool CanDivide => false;
		public override int Top { get { return 0; } }

		public override void DrawTo(SKCanvas graph, SKPoint point, bool isSelecting, bool isFocus, bool isBackLog, TextDrawingMode mode, bool isButton = false)
		{
		}

		public override void SetWidth(StringMeasure sm, float subPixel)
		{
		}
	}
}
