using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Text;
using MinorShift.Emuera;
using MinorShift.Emuera.GameView;
using MinorShift.Emuera.Runtime.Config;
using MinorShift.Emuera.UI.Game;
using SkiaSharp;
using XEmuera;
using static MinorShift.Emuera.Runtime.Utils.EvilMask.Shape;
using static MinorShift.Emuera.Runtime.Utils.EvilMask.Utils;

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
			var sep = System.IO.Path.DirectorySeparatorChar;
			path = path.Replace('\\', sep).Replace('/', sep);
			string traversal = ".." + sep;
			path = path.Replace(traversal, "");
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

		public static void AddColorParam(StringBuilder sb, string name, Color color)
		{
			if (color != Color.Transparent)
			{
				sb.Append(' ').Append(name).Append("='").Append(HtmlManager.GetColorToString(color)).Append('\'');
			}
		}
		public static void AddColorParam4(StringBuilder sb, string name, Color[] colors)
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

		public sealed class DataTable
		{
			static Type[] builtInDTTypes = [typeof(sbyte), typeof(short), typeof(int), typeof(long), typeof(string)];
			static Dictionary<Type, string> builtInDictDTTypeNames = new()
			{
				{ typeof(sbyte), "int8" },
				{ typeof(short), "int16" },
				{ typeof(int), "int32" },
				{ typeof(long), "int64" },
				{ typeof(string), "string" },
			};
			static Dictionary<string, Type> builtInDictDTTypeNames_R = new()
			{
				{ "int8", typeof(sbyte) },
				{ "int16", typeof(short) },
				{ "int32", typeof(int) },
				{ "int64", typeof(long) },
				{ "string", typeof(string) },
			};
			public static long TypeToInt(Type t)
			{
				if (t == typeof(sbyte)) return 1;
				if (t == typeof(short)) return 2;
				if (t == typeof(int)) return 3;
				if (t == typeof(long)) return 4;
				if (t == typeof(string)) return 5;
				return long.MaxValue;
			}
			public static Type IntToType(long i)
			{
				if (i > 0 && i <= builtInDTTypes.Length) return builtInDTTypes[i - 1];
				return null;
			}
			public static Type NameToType(string n)
			{
				if (builtInDictDTTypeNames_R.ContainsKey(n)) return builtInDictDTTypeNames_R[n];
				return null;
			}
			public static string TypeToName(Type t)
			{
				if (builtInDictDTTypeNames.ContainsKey(t)) return builtInDictDTTypeNames[t];
				return null;
			}
			public static object ConvertInt(long v, Type t)
			{
				if (t == typeof(sbyte)) return (sbyte)Math.Min(Math.Max(v, sbyte.MinValue), sbyte.MaxValue);
				if (t == typeof(short)) return (short)Math.Min(Math.Max(v, short.MinValue), short.MaxValue);
				if (t == typeof(int)) return (int)Math.Min(Math.Max(v, int.MinValue), int.MaxValue);
				return v;
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
		public ConsoleDivPart(MixedNum xPos, MixedNum yPos, MixedNum width, MixedNum height, int depth, int color, StyledBoxModel box, bool isRelative, DisplayMode displayMode, ConsoleDisplayLine[] childs)
		{
			backgroundColor = color != -1 ? Color.FromArgb(color) : Color.Transparent;
			StringBuilder sb = new();
			width.num = Math.Abs(width.num);
			if (height != null)
				height.num = Math.Abs(height.num);
			sb.Append("<div");
			AddTagMixedNumArg(sb, "xpos", xPos);
			AddTagMixedNumArg(sb, "ypos", yPos);
			AddTagMixedNumArg(sb, "width", width);
			AddColorParam(sb, "color", backgroundColor);
			if (height != null)
				AddTagMixedNumArg(sb, "height", height);
			if (box != null)
			{
				AddTagMixedParam(sb, "margin", box.margin);
				MixedNum4ToInt4(box.margin, ref margin);
				AddTagMixedParam(sb, "padding", box.padding);
				MixedNum4ToInt4(box.padding, ref padding);
				AddTagMixedParam(sb, "border", box.border);
				MixedNum4ToInt4(box.border, ref border);
				AddTagMixedParam(sb, "radius", box.radius);
				MixedNum4ToInt4(box.radius, ref radius);
				if (box.color != null)
				{
					borderColors = new Color[4];
					for (int i = 0; i < 4; i++)
						borderColors[i] = box.color[i] != -1 ? Color.FromArgb(box.color[i]) : Color.Transparent;
					AddColorParam4(sb, "bcolor", borderColors);
				}
			}
			sb.Append(">");
			altHeadTag = sb.ToString();
			Text = string.Empty;
			xOffset = MixedNum.ToPixel(xPos, 0);
			if (margin != null) divXOffset += margin[Direction.Left];
			if (padding != null) divXOffset += padding[Direction.Left];
			if (border != null) divXOffset += border[Direction.Left];
			PointY = MixedNum.ToPixel(yPos, 0);
			if (margin != null) yOffset += margin[Direction.Top];
			if (padding != null) yOffset += padding[Direction.Top];
			if (border != null) yOffset += border[Direction.Top];
			this.width = MixedNum.ToPixel(width, 0);
			if (height != null)
			{
				Height = MixedNum.ToPixel(height, 0);
			}
			else
			{
				int padTop = 0, padBottom = 0;
				if (padding != null) { padTop = padding[Direction.Top]; padBottom = padding[Direction.Bottom]; }
				if (border != null) { padTop += border[Direction.Top]; padBottom += border[Direction.Bottom]; }
				Height = childs.Length * Config.Config.LineHeight + padTop + padBottom;
			}
			children = childs;
			Depth = depth;
			IsRelative = isRelative;
			Display = displayMode;
			ShiftChildrenX(PointX + xOffset + divXOffset);
		}
		int pointX;
		int xOffset;
		int divXOffset;
		int yOffset;
		int width;
		public override int PointX
		{
			get { return pointX; }
			set
			{
				var diff = value - pointX;
				pointX = value;
				ShiftChildrenX(diff);
			}
		}
		int PointY;
		int Height;
		int[] margin, padding, radius, border;
		Color[] borderColors;
		Color backgroundColor;
		string altHeadTag;
		readonly ConsoleDisplayLine[] children;
		public bool IsEscaped { get; set; }
		public override int Top { get { return PointY; } }
		public override int Bottom { get { return PointY + Height; } }
		public bool IsRelative { get; private set; }
		public DisplayMode Display { get; private set; }
		public ConsoleDisplayLine[] Children { get { return children; } }
		public override bool CanDivide => false;

		public ConsoleButtonString TestChildHitbox(int px, int py, int lineScreenY)
		{
			ConsoleButtonString pointing = null;
			// 与 DrawTo 使用相同的 rect 计算逻辑
			var rect = IsRelative
				? new Rectangle(PointX + xOffset, lineScreenY + PointY, width + 2, Height)
				: Display switch
				{
					DisplayMode.AbsoluteLeftTop => new Rectangle(xOffset, PointY, width + 2, Height),
					DisplayMode.AbsoluteLeftBottom => new Rectangle(xOffset, GlobalStatic.EMediator.Console.Window.MainPicBox.Height - PointY - Height, width + 2, Height),
					_ => new Rectangle(xOffset, GlobalStatic.EMediator.Console.Window.MainPicBox.Height - PointY - Height, width + 2, Height)
				};
			if (margin != null)
				rect = new Rectangle(rect.X + margin[Direction.Left], rect.Y + margin[Direction.Top],
					 rect.Width - margin[Direction.Left] - margin[Direction.Right], rect.Height - margin[Direction.Top] - margin[Direction.Bottom]);
			// 应用 border 和 padding 偏移，与 DrawTo 中传递给子元素的 content 区域一致
			if (border != null)
				rect = new Rectangle(rect.X + border[Direction.Left], rect.Y + border[Direction.Top],
					 rect.Width - border[Direction.Left] - border[Direction.Right], rect.Height - border[Direction.Top] - border[Direction.Bottom]);
			if (padding != null)
				rect = new Rectangle(rect.X + padding[Direction.Left], rect.Y + padding[Direction.Top],
					 rect.Width - padding[Direction.Left] - padding[Direction.Right], rect.Height - padding[Direction.Top] - padding[Direction.Bottom]);
			if (!rect.Contains(px, py)) return null;
			for (int i = 0; i < children.Length; i++)
			{
				var line = children[i];
				int actualRelPointY = rect.Y + (i * Config.Config.LineHeight);
				for (int b = line.Buttons.Length - 1; b >= 0; b--)
				{
					ConsoleButtonString button = line.Buttons[b];
					if (button == null || button.StrArray == null) continue;
					if (px >= button.PointX && px <= button.PointX + button.Width)
					{
						foreach (AConsoleDisplayNode part in button.StrArray)
						{
							if (part == null) continue;
							if (px >= part.PointX && px <= part.PointX + part.Width &&
								py >= actualRelPointY + part.Top && py <= actualRelPointY + part.Bottom)
							{
								pointing = button;
								if (pointing.IsButton)
									return pointing;
							}
						}
					}
				}
			}
			return pointing;
		}

		public override void DrawTo(SKCanvas graph, SKPoint point, bool isSelecting, bool isFocus, bool isBackLog, TextDrawingMode mode, bool isButton = false)
		{
			if (GlobalStatic.EMediator.Console.Window == null) return;
			var rect = IsRelative
				? new Rectangle(PointX + xOffset, (int)point.Y + PointY, width + 2, Height)
				: Display switch
				{
					DisplayMode.AbsoluteLeftTop => new Rectangle(xOffset, PointY, width + 2, Height),
					DisplayMode.AbsoluteLeftBottom => new Rectangle(xOffset, GlobalStatic.EMediator.Console.Window.MainPicBox.Height - PointY - Height, width + 2, Height),
					_ => new Rectangle(xOffset, GlobalStatic.EMediator.Console.Window.MainPicBox.Height - PointY - Height, width + 2, Height)
				};
			graph.Save();
			if (margin != null)
				rect = new Rectangle(rect.X + margin[Direction.Left], rect.Y + margin[Direction.Top],
					 rect.Width - margin[Direction.Left] - margin[Direction.Right], rect.Height - margin[Direction.Top] - margin[Direction.Bottom]);
			graph.ClipRect(new SKRect(rect.Left, rect.Top, rect.Right, rect.Bottom));
			if (backgroundColor != Color.Transparent)
			{
				using var backPaint = new SKPaint { Color = DisplayUtils.ToSKColor(backgroundColor) };
				graph.DrawRect(new SKRect(rect.Left, rect.Top, rect.Right, rect.Bottom), backPaint);
			}
			if (border != null && borderColors != null)
			{
				if (border[Direction.Top] > 0 && borderColors[Direction.Top] != Color.Transparent)
				{
					using var borderPaint = new SKPaint { Color = DisplayUtils.ToSKColor(borderColors[Direction.Top]) };
					graph.DrawRect(new SKRect(rect.Left, rect.Top, rect.Right, rect.Top + border[Direction.Top]), borderPaint);
				}
				if (border[Direction.Right] > 0 && borderColors[Direction.Right] != Color.Transparent)
				{
					using var borderPaint = new SKPaint { Color = DisplayUtils.ToSKColor(borderColors[Direction.Right]) };
					graph.DrawRect(new SKRect(rect.Right - border[Direction.Right], rect.Top, rect.Right, rect.Bottom), borderPaint);
				}
				if (border[Direction.Bottom] > 0 && borderColors[Direction.Bottom] != Color.Transparent)
				{
					using var borderPaint = new SKPaint { Color = DisplayUtils.ToSKColor(borderColors[Direction.Bottom]) };
					graph.DrawRect(new SKRect(rect.Left, rect.Bottom - border[Direction.Bottom], rect.Right, rect.Bottom), borderPaint);
				}
				if (border[Direction.Left] > 0 && borderColors[Direction.Left] != Color.Transparent)
				{
					using var borderPaint = new SKPaint { Color = DisplayUtils.ToSKColor(borderColors[Direction.Left]) };
					graph.DrawRect(new SKRect(rect.Left, rect.Top, rect.Left + border[Direction.Left], rect.Bottom), borderPaint);
				}
			}
			if (border != null)
				rect = new Rectangle(rect.X + border[Direction.Left], rect.Y + border[Direction.Top],
					 rect.Width - border[Direction.Left] - border[Direction.Right], rect.Height - border[Direction.Top] - border[Direction.Bottom]);
			if (padding != null)
				rect = new Rectangle(rect.X + padding[Direction.Left], rect.Y + padding[Direction.Top],
					 rect.Width - padding[Direction.Left] - padding[Direction.Right], rect.Height - padding[Direction.Top] - padding[Direction.Bottom]);
			graph.ClipRect(new SKRect(rect.Left, rect.Top, rect.Right, rect.Bottom));
			point.Y = rect.Y;
			foreach (var child in children)
			{
				child.DrawTo(graph, (int)point.Y, isBackLog, true, mode);
				point.Y += Config.Config.LineHeight;
			}
			graph.Restore();
		}

		void ShiftChildrenX(int diff)
		{
			foreach (var child in children)
				child.ShiftPositionX(diff);
		}

		public override void SetWidth(StringMeasure sm, float subPixel)
		{
		}

		public override string ToString()
		{
			var sb = new StringBuilder();
			sb.Append(altHeadTag);
			foreach (var line in children)
			{
				sb.Append(line.ToString());
				sb.Append("\r\n");
			}
			sb.Append("</div>");
			return sb.ToString();
		}
		public override StringBuilder BuildString(StringBuilder sb)
		{
			sb.Append(altHeadTag);
			foreach (var line in children)
			{
				sb.Append(line.ToString());
				sb.Append("\r\n");
			}
			sb.Append("</div>");
			return sb;
		}
	}
}
