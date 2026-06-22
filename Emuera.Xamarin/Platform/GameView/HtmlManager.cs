using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using MinorShift.Emuera.Runtime.Utils;
using MinorShift.Emuera.Runtime.Utils.EvilMask;
using MinorShift.Emuera.GameView;
using MinorShift.Emuera.UI.Game;
using MinorShift.Emuera.Runtime.Config;
using MinorShift.Emuera.Runtime.Script;
using MinorShift.Emuera.Runtime.Script.Data;
using MinorShift.Emuera.Runtime.Script.Parser;
using System.Drawing;
using MinorShift.Emuera.Runtime.Script.Statements.Expression;
using StringStream = MinorShift.Emuera.Runtime.Utils.CharStream;
using XEmuera.Drawing;
using trerror = MinorShift.Emuera.Runtime.Utils.EvilMask.Lang.Error;
using MixedNum = MinorShift.Emuera.Runtime.Utils.EvilMask.Utils.MixedNum;
using static MinorShift.Emuera.Runtime.Utils.EvilMask.Shape;
using static MinorShift.Emuera.Runtime.Utils.EvilMask.Utils;

namespace MinorShift.Emuera.GameView
{
	internal enum FontVerticalAlign { Top, Middle, Bottom }

	internal static class HtmlManager
	{
		#region EM_私家版_HtmlManager機能拡張
		public static int HtmlLength(string s)
		{
			ConsoleDisplayLine[] lines = Html2DisplayLine(s, GlobalStatic.Console.StrMeasure, null);
			int len = 0;
			if (lines.Length <= 0) return 0;
			foreach (var btn in lines[0].Buttons) len += btn.Width;
			return len;
		}
		private static int GetSubStr(string pref, string suff, string s, ref int lmax)
		{
			int len = 0, i;
			for (i = 0; i < s.Length; i++)
			{
				int t = HtmlLength(pref + s.Substring(i, 1) + suff);
				if (len + t > lmax)
				{
					i--;
					break;
				}
				len += t;
			}
			if (i == s.Length) i--;
			lmax -= len;
			return i + 1;
		}
		private struct HTMLI
		{
			public string tag;
			public bool isStyleTag;

			public HTMLI(string v1, bool v2) : this()
			{
				this.tag = v1;
				this.isStyleTag = v2;
			}
		};
		public static string[] HtmlSubString(string str, int length)
		{
			Stack<HTMLI> beginStack = new Stack<HTMLI>(), endStack = new Stack<HTMLI>();

			length = length * Config.FontSize / 2;
			str = Unescape(str);
			int found = -1, last = 0, delbr = 0;
			bool content = false;
			int tmp;
			string tstr;
			while (true)
			{
				found = str.IndexOf('<', last);
				if (found != last)
				{
					string pref = "", suff = "";
					foreach (HTMLI s in beginStack) if (s.isStyleTag) pref += s.tag;
					Stack<HTMLI> arr = new Stack<HTMLI>(endStack);
					while (arr.Count > 0)
						if (arr.Peek().isStyleTag) suff += arr.Pop().tag;
						else arr.Pop();
					if (found < 0)
						tstr = str.Substring(last, str.Length - last);
					else
						tstr = str.Substring(last, found - last);
					tmp = GetSubStr(pref, suff, tstr, ref length);
					last += tmp + 1;
					content = true;
					if (found < 0 || tmp < tstr.Length) break;
				}
				else last++;
				found = str.IndexOf('>', last);
				if (found <= 0) break;
				if (str[last] == '/')
				{
					beginStack.Pop();
					endStack.Pop();
				}
				else
				{
					int fspace = str.IndexOf(' ', last, found - last);
					if (fspace < 0) fspace = found;
					string tag = str.Substring(last, fspace - last);
					if (tag == "br") { delbr = 1; break; }
					if (tag == "img" || tag == "shape")
					{
						var pos = last - 1;
						tstr = str.Substring(pos, found - pos + 1);
						tmp = HtmlLength(tstr);
						length -= tmp;
						if (length < 0 && content) break;
					}
					else
					{
						bool ist = (tag == "b" || tag == "i" || tag == "s");
						beginStack.Push(new HTMLI('<' + str.Substring(last, found - last) + '>', ist));
						endStack.Push(new HTMLI("</" + tag + '>', ist));
					}
				}
				last = found + 1;
			}
			string[] ret = new string[2];
			if (last == 0) return new string[] { "", str };
			ret[0] = str.Substring(0, last - 1);
			while (endStack.Count > 0) ret[0] += endStack.Pop().tag;
			while (beginStack.Count > 0) ret[1] = beginStack.Pop().tag + ret[1];
			ret[1] += str.Substring(last - 1 + delbr * 4, str.Length - last + 1 - delbr * 4);
			return ret;
		}

		public static void ParseParam4IntNum(ref int[] nums, string tag, string word, string attrValue)
		{
			if (nums == null) nums = new int[4];
			else throw new CodeEE(string.Format(trerror.DuplicateAttribute.Text, tag, word));
			string[] tokens = attrValue.Split(',');
			switch (tokens.Length)
			{
				case 1:
					nums[0] = stringToColorInt32(tokens[0].Trim());
					nums[1] = nums[0]; nums[2] = nums[0]; nums[3] = nums[0];
					break;
				case 2:
					nums[0] = stringToColorInt32(tokens[0].Trim());
					nums[1] = stringToColorInt32(tokens[1].Trim());
					nums[2] = nums[0]; nums[3] = nums[1];
					break;
				case 3:
					nums[0] = stringToColorInt32(tokens[0].Trim());
					nums[1] = stringToColorInt32(tokens[1].Trim());
					nums[2] = stringToColorInt32(tokens[2].Trim());
					nums[3] = nums[1];
					break;
				case 4:
					for (int i = 0; i < 4; i++)
						nums[i] = stringToColorInt32(tokens[i].Trim());
					break;
				default:
					throw new CodeEE(string.Format(trerror.CanNotInterpretAttribute.Text, attrValue));
			}
		}
		#endregion
		#region EM_私家版_HTML_divタグ
		private sealed class HtmlDivTag
		{
			public HtmlDivTag(MixedNum x, MixedNum y, MixedNum width, MixedNum height, int depth, int color, StyledBoxModel box, bool isRelative, DisplayMode displayMode)
			{
				X = x; Y = y; Width = width; Height = height;
				Depth = depth; Color = color; StyledBox = box;
				IsRelative = isRelative; Display = displayMode;
			}
			public ConsoleDisplayLine[] Lines;
			public MixedNum Width, Height, X, Y;
			public int Color, Depth;
			public StyledBoxModel StyledBox;
			public bool IsRelative;
			public DisplayMode Display;
		}
		#endregion
		static HtmlManager()
		{
			repDic.Add('&', "&amp;");
			repDic.Add('>', "&gt;");
			repDic.Add('<', "&lt;");
			repDic.Add('\"', "&quot;");
			repDic.Add('\'', "&apos;");
		}
		static readonly char[] rep = new char[] { '&', '>', '<', '\"', '\'' };
		static readonly Dictionary<char, string> repDic = new Dictionary<char, string>();
		private sealed class HtmlAnalzeStateFontTag
		{
			public int Color = int.MinValue;
			public int BColor = int.MinValue;
			public string FontName = null;
			public float? FontSize = null;
			public TextDrawingMode? RenderMode = null;
			public SkiaSharpFontEdging? FontEdging = null;
			public SkiaSharpFontHinting? FontHinting = null;
			public FontVerticalAlign? VerticalAlign = null;
		}

		private sealed class HtmlAnalzeStateButtonTag
		{
			public bool IsButton = true;
			public bool IsButtonTag = true;
			public Int64 ButtonValueInt = 0;
			public string ButtonValueStr = null;
			public string ButtonTitle = null;
			public bool ButtonIsInteger = false;
			public int PointX = 0;
			public bool PointXisLocked = false;
		}

		class DivState
		{
			public bool IsDiv;
			public int PosX, PosY;
			public DisplayMode Display;
			public Color? BackgroundColor;
			public int Width = -1, Height = -1;
			public bool HasBorder = false;
			public int BorderWidth;
			public Color BorderColor;
			public int[] Padding;
		}

		private sealed class HtmlAnalzeState
		{
			public bool LineHead = true;
			public FontStyle FontStyle = FontStyle.Regular;
			public List<HtmlAnalzeStateFontTag> FonttagList = new List<HtmlAnalzeStateFontTag>();
			public bool FlagNobr = false;
			public bool FlagP = false;
			public bool FlagNobrClosed = false;
			public bool FlagPClosed = false;
			public DisplayLineAlignment Alignment = DisplayLineAlignment.LEFT;

			public HtmlAnalzeStateButtonTag LastButtonTag = null;
			public HtmlAnalzeStateButtonTag CurrentButtonTag = null;

			#region EM_私家版_clearbutton
			public bool FlagClearButton = false;
			public bool FlagClearButtonTooltip = false;
			#endregion

			#region EM_私家版_HTML_divタグ
			public bool StartingSubDivision;
			public HtmlDivTag CurrentDivTag;
			public int SubDivisionWidth;
			#endregion
			public bool FlagBr = false;
			public bool FlagButton = false;

			public DivState DivState;

			public StringStyle GetSS()
			{
				Color c = Config.ForeColor;
				Color b = Config.FocusColor;
				string fontname = null;
				bool colorChanged = false;
				if (FonttagList.Count > 0)
				{
					HtmlAnalzeStateFontTag font = FonttagList[FonttagList.Count - 1];
					fontname = font.FontName;
					if (font.Color != int.MinValue)
					{
						colorChanged = true;
						c = Color.FromArgb(font.Color);
					}
					if (font.BColor != int.MinValue)
					{
						b = Color.FromArgb(font.BColor);
					}
				}
				return new StringStyle(c, colorChanged, b, FontStyle, fontname);
			}

			public TextDrawingMode? RenderMode => FonttagList.Count > 0 ? FonttagList[FonttagList.Count - 1].RenderMode : null;
			public SkiaSharpFontEdging? FontEdging => FonttagList.Count > 0 ? FonttagList[FonttagList.Count - 1].FontEdging : null;
			public SkiaSharpFontHinting? FontHinting => FonttagList.Count > 0 ? FonttagList[FonttagList.Count - 1].FontHinting : null;
			public float? FontSize => FonttagList.Count > 0 ? FonttagList[FonttagList.Count - 1].FontSize : null;
			public FontVerticalAlign? VerticalAlign => FonttagList.Count > 0 ? FonttagList[FonttagList.Count - 1].VerticalAlign : null;
		}

		public static string DisplayLine2Html(ConsoleDisplayLine[] lines, bool needPandN)
		{
			if (lines == null || lines.Length == 0)
				return "";
			StringBuilder b = new StringBuilder();
			if (needPandN)
			{
				switch (lines[0].Align)
				{
					case DisplayLineAlignment.LEFT:
						b.Append("<p align='left'>");
						break;
					case DisplayLineAlignment.CENTER:
						b.Append("<p align='center'>");
						break;
					case DisplayLineAlignment.RIGHT:
						b.Append("<p align='right'>");
						break;
				}
				b.Append("<nobr>");
			}
			for (int dispCounter = 0; dispCounter < lines.Length; dispCounter++)
			{
				if (dispCounter != 0)
					b.Append("<br>");
				ConsoleButtonString[] buttons = lines[dispCounter].Buttons;
				for (int buttonCounter = 0; buttonCounter < buttons.Length; buttonCounter++)
				{
					string titleValue = null;
					if (!string.IsNullOrEmpty(buttons[buttonCounter].Title))
						titleValue = Escape(buttons[buttonCounter].Title);
					bool hasTag = buttons[buttonCounter].IsButton || titleValue != null
						|| buttons[buttonCounter].PointXisLocked;
					if (hasTag)
					{
						if (buttons[buttonCounter].IsButton)
						{
							string attrValue = Escape(buttons[buttonCounter].Inputs);
							b.Append("<button value='");
							b.Append(attrValue);
							b.Append("'");
						}
						else
						{
							b.Append("<nonbutton");
						}
						if (titleValue != null)
						{
							b.Append(" title='");
							b.Append(titleValue);
							b.Append("'");
						}
						if (buttons[buttonCounter].PointXisLocked)
						{
							b.Append(" pos='");
							b.Append(buttons[buttonCounter].RelativePointX.ToString());
							b.Append("'");
						}
						b.Append(">");
					}
					AConsoleDisplayNode[] parts = buttons[buttonCounter].StrArray;
					for (int cssCounter = 0; cssCounter < parts.Length; cssCounter++)
					{
						if (parts[cssCounter] is ConsoleStyledString)
						{
							ConsoleStyledString css = parts[cssCounter] as ConsoleStyledString;
							b.Append(getStringStyleStartingTag(css));
							b.Append(Escape(css.Text));
							b.Append(getClosingStyleStartingTag(css));
						}
						else if (parts[cssCounter] is ConsoleImagePart)
					{
						b.Append(parts[cssCounter].AltText);
					}
					else if (parts[cssCounter] is ConsoleShapePart)
					{
						b.Append(parts[cssCounter].AltText);
					}
					else if (parts[cssCounter] is ConsoleDivPart divPart)
					{
						// 序列化 div 开标签（含 xpos/ypos/width/height/color/margin/padding/border/radius/bcolor 属性）
						b.Append(divPart.AltHeadTag);
						// 递归序列化 div 的子行
						if (divPart.Children != null && divPart.Children.Length > 0)
						{
							string childHtml = DisplayLine2Html(divPart.Children, true);
							b.Append(childHtml);
						}
						b.Append("</div>");
					}

				}
					if (hasTag)
					{
						if (buttons[buttonCounter].IsButton)
							b.Append("</button>");
						else
							b.Append("</nonbutton>");
					}

				}
			}
			if(needPandN)
			{
				b.Append("</nobr>");
				b.Append("</p>");
			}
			return b.ToString();
		}

		public static string[] HtmlTagSplit(string str)
		{
			List<string> strList = new List<string>();
			StringStream st = new StringStream(str);
			int found;
			while (!st.EOS)
			{
				found = st.Find('<');
				if (found < 0)
				{
					strList.Add(st.Substring());
					break;
				}
				else if (found > 0)
				{
					strList.Add(st.Substring(st.CurrentPosition, found));
					st.CurrentPosition += found;
				}
				found = st.Find('>');
				if(found < 0)
					return null;
				found++;
				strList.Add(st.Substring(st.CurrentPosition, found));
				st.CurrentPosition += found;
			}
			string[] ret = new string[strList.Count];
			strList.CopyTo(ret);
			return ret;
		}

		#region EM_私家版_描画拡張
		sealed class HtmlParentInfo
		{
			public HtmlAnalzeState State;
			public StringStream Stream;
			public bool HasComment;
			public bool HasReturn;
		}
		public static ConsoleDisplayLine[] Html2DisplayLine(string str, StringMeasure sm, EmueraConsole console)
		{
			return html2DisplayLine(str, sm, console, null, null);
		}
		public static ConsoleButtonString[] Html2ButtonList(string str, StringMeasure sm, EmueraConsole console)
		{
			var parts = new List<ConsoleButtonString>();
			html2DisplayLine(str, sm, console, null, parts);
			return parts.ToArray();
		}
		static ConsoleDisplayLine[] html2DisplayLine(string str, StringMeasure sm, EmueraConsole console, HtmlParentInfo parent, List<ConsoleButtonString> buttonsOutput)
		#endregion
		{
			List<AConsoleDisplayNode> cssList = new List<AConsoleDisplayNode>();
			List<ConsoleButtonString> buttonList = buttonsOutput == null ? new List<ConsoleButtonString>() : buttonsOutput;
			StringStream st = parent != null ? parent.Stream : new StringStream(str);
			int found;
			bool hasComment = parent != null ? parent.HasComment : str.IndexOf("<!--") >= 0;
			bool hasReturn = parent != null ? parent.HasReturn : str.IndexOf('\n') >= 0;
			HtmlAnalzeState state = new HtmlAnalzeState();
			#region EM_私家版_HTML_divタグ
			if (parent != null)
			{
				state.SubDivisionWidth = parent.State.SubDivisionWidth;
				state.CurrentDivTag = parent.State.CurrentDivTag;
				state.StartingSubDivision = parent.State.StartingSubDivision;
				state.FlagClearButton = parent.State.FlagClearButton;
				state.FlagClearButtonTooltip = parent.State.FlagClearButtonTooltip;
			}
			#endregion
			while (!st.EOS)
			{
				found = st.Find('<');
				if (state.DivState == null && hasReturn)
				{
					int rFound = st.Find('\n');
					if (rFound >= 0 && (found > rFound || found < 0))
						found = rFound;
				}
				if (found < 0)
				{
					string txt = Unescape(st.Substring());
					cssList.Add(new ConsoleStyledString(txt, state.GetSS(), state.RenderMode, state.FontEdging, state.FontHinting, state.FontSize, state.VerticalAlign));
					if (state.FlagPClosed)
						throw new CodeEE(trerror.TextAfterP.Text);
					if (state.FlagNobrClosed)
						throw new CodeEE(trerror.TextAfterNobr.Text);
					break;
				}
				else if (found > 0)
				{
					string txt = Unescape(st.Substring(st.CurrentPosition, found));
					cssList.Add(new ConsoleStyledString(txt, state.GetSS(), state.RenderMode, state.FontEdging, state.FontHinting, state.FontSize, state.VerticalAlign));
					state.LineHead = false;
					st.CurrentPosition += found;
				}
				if (hasComment && st.CurrentEqualTo("<!--"))
				{
					st.CurrentPosition += 4;
					found = st.Find("-->");
					if (found < 0)
						throw new CodeEE(trerror.NotFoundCloseTag.Text);
					st.CurrentPosition += found + 3;
					continue;
				}
				if (hasReturn && st.Current == '\n')
				{
					state.FlagBr = true;
					st.ShiftNext();
				}
				else
				{
					st.ShiftNext();
					AConsoleDisplayNode part = tagAnalyze(state, st);
					if (st.Current != '>')
						throw new CodeEE(trerror.NotFoundTerminateTag.Text);
					if (part != null)
						cssList.Add(part);
					st.ShiftNext();

					#region EM_私家版_HTML_divタグ
					if (state.StartingSubDivision)
					{
						if (state.CurrentDivTag != null)
						{
							if (parent == null)
							{
								if (state.CurrentButtonTag != null || state.FontStyle != FontStyle.Regular || state.FonttagList.Count > 0)
									throw new CodeEE(trerror.TagIsNotClosed.Text);
								var width = state.CurrentDivTag.Width;
								state.SubDivisionWidth = MixedNum.ToPixel(width);
								if (state.CurrentDivTag.StyledBox != null)
								{
									var box = state.CurrentDivTag.StyledBox;
									if (box.margin != null)
										state.SubDivisionWidth -= MixedNum.ToPixel(box.margin[Direction.Left]) + MixedNum.ToPixel(box.margin[Direction.Right]);
									if (box.border != null)
										state.SubDivisionWidth -= MixedNum.ToPixel(box.border[Direction.Left]) + MixedNum.ToPixel(box.border[Direction.Right]);
									if (box.padding != null)
										state.SubDivisionWidth -= MixedNum.ToPixel(box.padding[Direction.Left]) + MixedNum.ToPixel(box.padding[Direction.Right]);
								}
								state.CurrentDivTag.Lines = html2DisplayLine(null, sm, console, new HtmlParentInfo
								{
									HasComment = hasComment,
									HasReturn = hasReturn,
									State = state,
									Stream = st,
								}, null);
								var tagInfo = state.CurrentDivTag;
								state.CurrentDivTag = null;
								state.StartingSubDivision = false;
								cssList.Add(new ConsoleDivPart(tagInfo.X, tagInfo.Y, tagInfo.Width, tagInfo.Height, tagInfo.Depth, tagInfo.Color, tagInfo.StyledBox, tagInfo.IsRelative, tagInfo.Display, tagInfo.Lines));
							}
						}
						else
							break;
					}
					#endregion
				}

				if (state.DivState == null && state.FlagBr)
				{
					state.LastButtonTag = state.CurrentButtonTag;
					if (cssList.Count > 0)
						buttonList.Add(cssToButton(cssList, state, console));
					buttonList.Add(null);
				}
				if (state.FlagButton && cssList.Count > 0)
				{
					buttonList.Add(cssToButton(cssList, state, console));
				}
				state.FlagBr = false;
				state.FlagButton = false;
				state.LastButtonTag = state.CurrentButtonTag;
			}
			#region EM_私家版_HTML_divタグ
			if (state.CurrentDivTag != null)
				throw new CodeEE(trerror.TagIsNotClosed.Text);
			#endregion
			if (state.CurrentButtonTag != null || state.FontStyle != FontStyle.Regular || state.FonttagList.Count > 0)
				throw new CodeEE(trerror.TagIsNotClosed.Text);
			if (cssList.Count > 0)
				buttonList.Add(cssToButton(cssList, state, console));
			#region EM_私家版_HTML_PRINT拡張
			if (buttonsOutput != null) return null;
			#endregion

			foreach(ConsoleButtonString button in buttonList)
			{
				if (button != null && button.PointXisLocked)
				{
					if (!state.FlagNobr)
						throw new CodeEE(trerror.CanNotUsePosWithoutNobr.Text);
					if (state.Alignment != DisplayLineAlignment.LEFT)
						throw new CodeEE(trerror.CanOnlyUsePosAssignmentLR.Text);
					break;
				}
			}
			#region EM_私家版_HTML_divタグ
			ConsoleDisplayLine[] ret;
			if (state.StartingSubDivision && state.CurrentDivTag == null)
				ret = PrintStringBuffer.ButtonsToDisplayLines(buttonList, sm, state.FlagNobr, false, true, state.SubDivisionWidth);
			else
				ret = PrintStringBuffer.ButtonsToDisplayLines(buttonList, sm, state.FlagNobr, false);
			foreach (ConsoleDisplayLine dl in ret)
			{
				if (state.StartingSubDivision && state.CurrentDivTag == null)
					dl.SetAlignment(state.Alignment, state.SubDivisionWidth);
				else
					dl.SetAlignment(state.Alignment);
			}
			#endregion
			return ret;
		}

		public static string Html2PlainText(string str)
		{
			string ret = Regex.Replace(str, "\\<[^<]*\\>", "");
			return Unescape(ret);
		}

		public static string Escape(string str)
		{
			int index = 0;
			int found;
			StringBuilder b = new StringBuilder();
			while (index < str.Length)
			{
				found = str.IndexOfAny(rep, index);
				if (found < 0)
				{
					b.Append(str.Substring(index));
					break;
				}
				if (found > index)
					b.Append(str.Substring(index, found - index));
				string repnew = repDic[str[found]];
				b.Append(repnew);
				index = found + 1;
			}
			return b.ToString();
		}

		public static string Unescape(string str)
		{
			int index = 0;
			int found = str.IndexOf('&', index);
			if (found < 0)
				return str;
			StringBuilder b = new StringBuilder();
			while (index < str.Length)
			{
				found = str.IndexOf('&', index);
				if (found < 0)
				{
					b.Append(str.Substring(index));
					break;
				}
				if (found > index)
					b.Append(str.Substring(index, found - index));
				index = found;
				found = str.IndexOf(';', index);
				if (found <= index + 1)
				{
					if (found < 0)
						throw new CodeEE(trerror.MissingSemicolon.Text);
					throw new CodeEE(trerror.ContinuouslyAndSemicolon.Text);
				}
				string escWordRow = str.Substring(index + 1, found - index - 1);
				index = found + 1;
				string escWord = escWordRow.ToLower();
				int unicode;
				switch (escWord)
				{
					case "nbsp": b.Append(" "); break;
					case "amp": b.Append("&"); break;
					case "gt": b.Append(">"); break;
					case "lt": b.Append("<"); break;
					case "quot": b.Append("\""); break;
					case "apos": b.Append("\'"); break;
					default:
						{
							int iBbase = 10;
							if (escWord[0] != '#')
								throw new CodeEE(string.Format(trerror.InvalidCharacterReference.Text, escWordRow));
							if (escWord.Length > 1 && escWord[1] == 'x')
							{
								iBbase = 16;
								escWord = escWord.Substring(2);
							}
							else
								escWord = escWord.Substring(1);
							try
							{
								unicode = Convert.ToInt32(escWord, iBbase);
							}
							catch
							{

								throw new CodeEE(string.Format(trerror.InvalidCharacterReference.Text, escWordRow));
							}

							if (unicode < 0 || unicode > 0xFFFF)
								throw new CodeEE(string.Format(trerror.OoRUnicodeHtml.Text, escWordRow));
							b.Append((char)unicode);
							break;
						}
				}
			}
			return b.ToString();
		}

		private static ConsoleButtonString cssToButton(List<AConsoleDisplayNode> cssList, HtmlAnalzeState state, EmueraConsole console)
		{
			AConsoleDisplayNode[] css = new AConsoleDisplayNode[cssList.Count];
			cssList.CopyTo(css);
			cssList.Clear();
			ConsoleButtonString ret;
			if (state.LastButtonTag != null && state.LastButtonTag.IsButton)
			{
				if (state.LastButtonTag.ButtonIsInteger)
					ret = new ConsoleButtonString(console, css, state.LastButtonTag.ButtonValueInt, state.LastButtonTag.ButtonValueStr);
				else
					ret = new ConsoleButtonString(console, css, state.LastButtonTag.ButtonValueStr);
			}
			else
			{
				ret = new ConsoleButtonString(console, css);
				ret.Title = null;
			}
			if (state.LastButtonTag != null)
			{
				ret.Title = state.LastButtonTag.ButtonTitle;
				if(state.LastButtonTag.PointXisLocked)
				{
					ret.LockPointX(state.LastButtonTag.PointX);
				}
			}
			return ret;
		}

		public static string GetColorToString(Color color)
		{
			StringBuilder b = new StringBuilder();
			b.Append("#");
			int colorValue = color.R * 0x10000 + color.G * 0x100 + color.B;
			b.Append(colorValue.ToString("X6"));
			return b.ToString();
		}
		private static string getStringStyleStartingTag(ConsoleStyledString css)
		{
			bool fontChanged = !((css.StringStyle.Fontname == null || css.StringStyle.Fontname == Config.FontName) && !css.StringStyle.ColorChanged && css.StringStyle.ButtonColor == Config.FocusColor);
			bool sizeChanged = css.FontSize.HasValue;
			bool valignChanged = css.VerticalAlign.HasValue;
			bool renderChanged = css.RenderMode.HasValue && css.RenderMode.Value != Config.TextDrawingMode;
			bool edgingChanged = css.FontEdging.HasValue && css.FontEdging.Value != Config.FontEdging;
			bool hintingChanged = css.FontHinting.HasValue && css.FontHinting.Value != Config.FontHinting;
			if (!fontChanged && css.StringStyle.FontStyle == FontStyle.Regular && !sizeChanged && !valignChanged && !renderChanged && !edgingChanged && !hintingChanged)
				return "";
			StringBuilder b = new StringBuilder();
			if (fontChanged || sizeChanged || valignChanged || renderChanged || edgingChanged || hintingChanged)
			{
				b.Append("<font");
				if (css.StringStyle.Fontname != null && css.StringStyle.Fontname != Config.FontName)
				{
					b.Append(" face='");
					b.Append(HtmlManager.Escape(css.StringStyle.Fontname));
					b.Append("'");
				}
				if (css.StringStyle.ColorChanged)
				{
					b.Append(" color='#");
					int colorValue = css.StringStyle.Color.R * 0x10000 + css.StringStyle.Color.G * 0x100 + css.StringStyle.Color.B;
					b.Append(colorValue.ToString("X6"));
					b.Append("'");
				}
				if (css.StringStyle.ButtonColor != Config.FocusColor)
				{
					b.Append(" bcolor='#");
					int colorValue = css.StringStyle.ButtonColor.R * 0x10000 + css.StringStyle.ButtonColor.G * 0x100 + css.StringStyle.ButtonColor.B;
					b.Append(colorValue.ToString("X6"));
					b.Append("'");
				}
				if (sizeChanged)
				{
					b.Append(" size='");
					b.Append(css.FontSize.Value);
					b.Append("'");
				}
				if (valignChanged)
				{
					b.Append(" valign='");
					b.Append(css.VerticalAlign.Value.ToString().ToLower());
					b.Append("'");
				}
				if (renderChanged)
				{
					b.Append(" render='");
					b.Append(css.RenderMode.Value == TextDrawingMode.TEXTRENDERER ? "gdi" : "skia");
					b.Append("'");
				}
				if (edgingChanged)
				{
					b.Append(" edging='");
					string edgStr = css.FontEdging.Value == SkiaSharpFontEdging.Alias ? "alias" :
								   (css.FontEdging.Value == SkiaSharpFontEdging.AntiAlias ? "antialias" : "subpixel");
					b.Append(edgStr);
					b.Append("'");
				}
				if (hintingChanged)
				{
					b.Append(" hinting='");
					b.Append(css.FontHinting.Value.ToString().ToLower());
					b.Append("'");
				}
				b.Append(">");
			}
			if (css.StringStyle.FontStyle != FontStyle.Regular)
			{
				if ((css.StringStyle.FontStyle & FontStyle.Strikeout) != FontStyle.Regular)
					b.Append("<s>");
				if ((css.StringStyle.FontStyle & FontStyle.Underline) != FontStyle.Regular)
					b.Append("<u>");
				if ((css.StringStyle.FontStyle & FontStyle.Italic) != FontStyle.Regular)
					b.Append("<i>");
				if ((css.StringStyle.FontStyle & FontStyle.Bold) != FontStyle.Regular)
					b.Append("<b>");
			}

			return b.ToString();
		}

		private static string getClosingStyleStartingTag(ConsoleStyledString css)
		{
			var style = css.StringStyle;
			bool fontChanged = !((style.Fontname == null || style.Fontname == Config.FontName) && !style.ColorChanged && style.ButtonColor == Config.FocusColor);
			bool sizeChanged = css.FontSize.HasValue;
			bool valignChanged = css.VerticalAlign.HasValue;
			bool renderChanged = css.RenderMode.HasValue && css.RenderMode.Value != Config.TextDrawingMode;
			bool edgingChanged = css.FontEdging.HasValue && css.FontEdging.Value != Config.FontEdging;
			bool hintingChanged = css.FontHinting.HasValue && css.FontHinting.Value != Config.FontHinting;
			if (!fontChanged && style.FontStyle == FontStyle.Regular && !sizeChanged && !valignChanged && !renderChanged && !edgingChanged && !hintingChanged)
				return "";
			StringBuilder b = new StringBuilder();
			if (style.FontStyle != FontStyle.Regular)
			{
				if ((style.FontStyle & FontStyle.Bold) != FontStyle.Regular)
					b.Append("</b>");
				if ((style.FontStyle & FontStyle.Italic) != FontStyle.Regular)
					b.Append("</i>");
				if ((style.FontStyle & FontStyle.Underline) != FontStyle.Regular)
					b.Append("</u>");
				if ((style.FontStyle & FontStyle.Strikeout) != FontStyle.Regular)
					b.Append("</s>");
			}
			if (fontChanged || sizeChanged || valignChanged || renderChanged || edgingChanged || hintingChanged)
				b.Append("</font>");
			return b.ToString();
		}

		private static AConsoleDisplayNode tagAnalyze(HtmlAnalzeState state, StringStream st)
		{
			bool endTag = (st.Current == '/');
			string tag;
			if (endTag)
			{
				st.ShiftNext();
				int found = st.Find('>');
				if (found < 0)
				{
					st.CurrentPosition = st.RowString.Length;
					return null;
				}
				tag = st.Substring(st.CurrentPosition, found).Trim();
				st.CurrentPosition += found;
				FontStyle endStyle = FontStyle.Strikeout;
				switch (tag.ToLower())
				{
					case "b": endStyle = FontStyle.Bold; goto case "s";
					case "i": endStyle = FontStyle.Italic; goto case "s";
					case "u": endStyle = FontStyle.Underline; goto case "s";
					case "s":
						if ((state.FontStyle & endStyle) == FontStyle.Regular)
							throw new CodeEE(string.Format(trerror.UnexpectedCloseTag.Text, tag));
						state.FontStyle ^= endStyle;
						return null;
					case "p":
						if ((!state.FlagP) || (state.FlagPClosed))
							throw new CodeEE(string.Format(trerror.UnexpectedCloseTag.Text, "p"));
						state.FlagPClosed = true;
						return null;
					case "nobr":
						if ((!state.FlagNobr) || (state.FlagNobrClosed))
							throw new CodeEE(string.Format(trerror.UnexpectedCloseTag.Text, "nobr"));
						state.FlagNobrClosed = true;
						return null;
					case "font":
						if (state.FonttagList.Count == 0)
							throw new CodeEE(string.Format(trerror.UnexpectedCloseTag.Text, "font"));
						state.FonttagList.RemoveAt(state.FonttagList.Count - 1);
						return null;
					case "button":
						if (state.CurrentButtonTag == null || !state.CurrentButtonTag.IsButtonTag)
							throw new CodeEE(string.Format(trerror.UnexpectedCloseTag.Text, "button"));
						state.CurrentButtonTag = null;
						state.FlagButton = true;
						return null;
					case "nonbutton":
						if (state.CurrentButtonTag == null || state.CurrentButtonTag.IsButtonTag)
							throw new CodeEE(string.Format(trerror.UnexpectedCloseTag.Text, "nonbutton"));
						state.CurrentButtonTag = null;
						state.FlagButton = true;
						return null;
					#region EM_私家版_clearbutton
					case "clearbutton":
						if (!state.FlagClearButton)
							throw new CodeEE(string.Format(trerror.UnexpectedCloseTag.Text, "clearbutton"));
						state.FlagClearButton = false;
						state.FlagClearButtonTooltip = false;
						return null;
					#endregion
					#region EM_私家版_HTML_divタグ
					case "div":
						if (state.CurrentDivTag == null)
							throw new CodeEE(string.Format(trerror.UnexpectedCloseTag.Text, "div"));
						state.CurrentDivTag = null;
						return null;
					#endregion
					default:
						throw new CodeEE(string.Format(trerror.CanNotInterpretCloseTag.Text, tag));
				}
			}

			bool tempUseMacro = LexicalAnalyzer.UseMacro;
			WordCollection wc = null;
			try
			{
				LexicalAnalyzer.UseMacro = false;
				tag = LexicalAnalyzer.ReadSingleIdentifier(st);
				LexicalAnalyzer.SkipWhiteSpace(st);
				if (st.Current != '>')
					wc = LexicalAnalyzer.Analyse(st, LexEndWith.GreaterThan, LexAnalyzeFlag.AllowAssignment | LexAnalyzeFlag.AllowSingleQuotationStr);
			}
			finally
			{
				LexicalAnalyzer.UseMacro = tempUseMacro;
			}
			if (string.IsNullOrEmpty(tag))
				goto error;
			IdentifierWord word;
			FontStyle newStyle = FontStyle.Strikeout;
			switch (tag.ToLower())
			{
				case "b": newStyle = FontStyle.Bold; goto case "s";
				case "i": newStyle = FontStyle.Italic; goto case "s";
				case "u": newStyle = FontStyle.Underline; goto case "s";
				case "s":
					if (wc != null)
						throw new CodeEE(string.Format(trerror.AttributeSetToTag.Text, tag));
					if ((state.FontStyle & newStyle) != FontStyle.Regular)
						throw new CodeEE(string.Format(trerror.DuplicateTag.Text, tag));
					state.FontStyle |= newStyle;
						return null;
				case "br":
					if (wc != null)
						throw new CodeEE(string.Format(trerror.AttributeSetToTag.Text, tag));
					state.FlagBr = true;
						return null;
				case "nobr":
					if (wc != null)
						throw new CodeEE(string.Format(trerror.AttributeSetToTag.Text, tag));
					if (!state.LineHead)
						throw new CodeEE(string.Format(trerror.TagIsNotBegin.Text, "nobr"));
					if (state.FlagNobr)
						throw new CodeEE(string.Format(trerror.DuplicateTag.Text, "nobr"));
					state.FlagNobr = true;
						return null;
				case "p":
					{
						if (wc == null)
							throw new CodeEE(string.Format(trerror.TagHasNotAttribute.Text, tag));
						if (!state.LineHead)
							throw new CodeEE(string.Format(trerror.TagIsNotBegin.Text, "p"));
						if (state.FlagNobr)
							throw new CodeEE(string.Format(trerror.DuplicateTag.Text, "p"));
						word = wc.Current as IdentifierWord;
						wc.ShiftNext();
						OperatorWord op = wc.Current as OperatorWord;
						wc.ShiftNext();
						LiteralStringWord attr = wc.Current as LiteralStringWord;
						wc.ShiftNext();
						if (!wc.EOL || word == null || op == null || op.Code != OperatorCode.Assignment || attr == null)
							goto error;
						if (!word.Code.Equals("align", StringComparison.OrdinalIgnoreCase))
							throw new CodeEE(string.Format(trerror.CanNotInterpretPAttribute.Text, word.Code));
						string attrValue = Unescape(attr.Str);
						switch (attrValue.ToLower())
						{
							case "left":
								state.Alignment = DisplayLineAlignment.LEFT;
								break;
							case "center":
								state.Alignment = DisplayLineAlignment.CENTER;
								break;
							case "right":
								state.Alignment = DisplayLineAlignment.RIGHT;
								break;
							default:
								throw new CodeEE(string.Format(trerror.CanNotInterpretAttribute.Text, attr.Str));
						}
						state.FlagP = true;
						return null;
					}
				case "img":
					{
						if (wc == null)
							throw new CodeEE(string.Format(trerror.TagHasNotAttribute.Text, tag));
						string attrValue;
						string src = null;
						string srcb = null;
						string srcm = null;
						#region EM_私家版_HTMLパラメータ拡張
						MixedNum height = null;
						MixedNum width = null;
						MixedNum ypos = null;
						MixedNum xpos = null;
						string cm = null;
						DisplayMode displayMode = DisplayMode.Relative;
						while (wc != null && !wc.EOL)
						{
							word = wc.Current as IdentifierWord;
							wc.ShiftNext();
							OperatorWord op = wc.Current as OperatorWord;
							wc.ShiftNext();
							LiteralStringWord attr = wc.Current as LiteralStringWord;
							wc.ShiftNext();
							if (word == null || op == null || op.Code != OperatorCode.Assignment || attr == null)
								goto error;
							attrValue = Unescape(attr.Str);
							if (word.Code.Equals("src", StringComparison.OrdinalIgnoreCase))
							{
								if (src != null)
									throw new CodeEE(string.Format(trerror.DuplicateAttribute.Text, tag, word.Code));
								src = attrValue;
							}
							else if (word.Code.Equals("srcb", StringComparison.OrdinalIgnoreCase))
							{
								if (srcb != null)
									throw new CodeEE(string.Format(trerror.DuplicateAttribute.Text, tag, word.Code));
								srcb = attrValue;
							}
							else if (word.Code.Equals("srcm", StringComparison.OrdinalIgnoreCase))
							{
								if (srcm != null)
									throw new CodeEE(string.Format(trerror.DuplicateAttribute.Text, tag, word.Code));
								srcm = attrValue;
							}
							else if (word.Code.Equals("height", StringComparison.OrdinalIgnoreCase))
							{
								ParseMixedNum(ref height, tag, word.Code, attrValue);
							}
							else if (word.Code.Equals("width", StringComparison.OrdinalIgnoreCase))
							{
								ParseMixedNum(ref width, tag, word.Code, attrValue);
							}
							else if (word.Code.Equals("ypos", StringComparison.OrdinalIgnoreCase))
							{
								ParseMixedNum(ref ypos, tag, word.Code, attrValue);
							}
							else if (word.Code.Equals("xpos", StringComparison.OrdinalIgnoreCase))
							{
								ParseMixedNum(ref xpos, tag, word.Code, attrValue);
							}
							else if (word.Code.Equals("cm", StringComparison.OrdinalIgnoreCase))
							{
								if (cm != null)
									throw new CodeEE(string.Format(trerror.DuplicateAttribute.Text, tag, word.Code));
								cm = attrValue;
							}
							else if (word.Code.Equals("display", StringComparison.OrdinalIgnoreCase))
							{
								if (attrValue.Equals("relative", StringComparison.OrdinalIgnoreCase))
									displayMode = DisplayMode.Relative;
								else if (attrValue.Equals("absolute-lefttop", StringComparison.OrdinalIgnoreCase))
									displayMode = DisplayMode.AbsoluteLeftTop;
								else if (attrValue.Equals("absolute-leftbottom", StringComparison.OrdinalIgnoreCase))
									displayMode = DisplayMode.AbsoluteLeftBottom;
								else
									throw new CodeEE(string.Format(trerror.CanNotInterpretAttribute.Text, attrValue));
							}
							else
								throw new CodeEE(string.Format(trerror.CanNotInterpretAttributeName.Text, tag, word.Code));
						}
						#endregion
						if (src == null)
							throw new CodeEE(string.Format(trerror.NotSetAttribute.Text, tag, "src"));
						return new ConsoleImagePart(src, srcb, srcm, height, width, ypos, xpos, displayMode, cm);
					}
				#region EM_私家版_HTML_divタグ
				case "div":
					{
						if (state.CurrentDivTag != null)
							throw new CodeEE(string.Format(trerror.NestedTag.Text, "div"));
						MixedNum x = null, y = null, width = null, height = null;
						StyledBoxModel box = null;
						bool isRelative = true;
						DisplayMode divDisplayMode = DisplayMode.Relative;
						int depth = 0;
						int color = int.MinValue;
						string attrValue;
						bool isSelfClosing = false;
						while (wc != null && !wc.EOL)
						{
							// Handle self-closing tag syntax <div ... />
							if (wc.Current is OperatorWord slashOp && slashOp.Code == OperatorCode.Div)
							{
								isSelfClosing = true;
								wc.ShiftNext();
								continue;
							}
							word = wc.Current as IdentifierWord;
							wc.ShiftNext();
							OperatorWord op = wc.Current as OperatorWord;
							wc.ShiftNext();
							LiteralStringWord attr = wc.Current as LiteralStringWord;
							wc.ShiftNext();
							if (word == null || op == null || op.Code != OperatorCode.Assignment || attr == null)
								goto error;
							attrValue = Unescape(attr.Str);
							if (word.Code.Equals("height", StringComparison.OrdinalIgnoreCase))
								ParseMixedNum(ref height, tag, word.Code, attrValue);
							else if (word.Code.Equals("width", StringComparison.OrdinalIgnoreCase))
								ParseMixedNum(ref width, tag, word.Code, attrValue);
							else if (word.Code.Equals("ypos", StringComparison.OrdinalIgnoreCase))
								ParseMixedNum(ref y, tag, word.Code, attrValue);
							else if (word.Code.Equals("xpos", StringComparison.OrdinalIgnoreCase))
								ParseMixedNum(ref x, tag, word.Code, attrValue);
							else if (word.Code.Equals("depth", StringComparison.OrdinalIgnoreCase))
							{
								if (depth != 0)
									throw new CodeEE(string.Format(trerror.AttributeCanNotInterpretNum.Text, tag, attr));
								if (!int.TryParse(attrValue, out depth))
									throw new CodeEE(string.Format(trerror.AttributeCanNotInterpretNum.Text, tag, attr));
							}
							else if (word.Code.Equals("color", StringComparison.OrdinalIgnoreCase))
							{
								if (color != int.MinValue)
									throw new CodeEE(string.Format(trerror.DuplicateAttribute.Text, tag, word.Code));
								color = stringToColorInt32(attrValue);
							}
							else if (word.Code.Equals("size", StringComparison.OrdinalIgnoreCase))
							{
								string[] tokens = attrValue.Split(',');
								if (tokens.Length != 2)
									throw new CodeEE(string.Format(trerror.ClearbuttonAttributeCanNotInterpretNum.Text, tag, word.Code, attrValue));
								ParseMixedNum(ref width, tag, "width", tokens[0].Trim());
								ParseMixedNum(ref height, tag, "height", tokens[1].Trim());
							}
							else if (word.Code.Equals("rect", StringComparison.OrdinalIgnoreCase))
							{
								string[] tokens = attrValue.Split(',');
								if (tokens.Length != 4)
									throw new CodeEE(string.Format(trerror.ClearbuttonAttributeCanNotInterpretNum.Text, tag, word.Code, attrValue));
								ParseMixedNum(ref x, tag, "xpos", tokens[0].Trim());
								ParseMixedNum(ref y, tag, "ypos", tokens[1].Trim());
								ParseMixedNum(ref width, tag, "width", tokens[2].Trim());
								ParseMixedNum(ref height, tag, "height", tokens[3].Trim());
							}
							else if (word.Code.Equals("bcolor", StringComparison.OrdinalIgnoreCase))
								ParseParam4IntNum(ref CreateIfNull(ref box).color, tag, word.Code, attrValue);
							else if (word.Code.Equals("display", StringComparison.OrdinalIgnoreCase))
							{
								if (attrValue.Equals("absolute", StringComparison.OrdinalIgnoreCase)) isRelative = false;
								else if (attrValue.Equals("relative", StringComparison.OrdinalIgnoreCase)) isRelative = true;
								else if (attrValue.Equals("absolute-lefttop", StringComparison.OrdinalIgnoreCase)) { isRelative = false; divDisplayMode = DisplayMode.AbsoluteLeftTop; }
								else if (attrValue.Equals("absolute-leftbottom", StringComparison.OrdinalIgnoreCase)) { isRelative = false; divDisplayMode = DisplayMode.AbsoluteLeftBottom; }
								else throw new CodeEE(string.Format(trerror.CanNotInterpretAttribute.Text, attrValue));
							}
							else if (!TryParseStyledBoxModel(ref box, tag, word.Code, attrValue))
								throw new CodeEE(string.Format(trerror.CanNotInterpretAttributeName.Text, tag, word.Code));
						}
						if (width == null)
							throw new CodeEE(string.Format(trerror.NotSetAttribute.Text, tag, "width"));
						var divTag = new HtmlDivTag(x, y, width, height, depth, color, box, isRelative, divDisplayMode);
						if (isSelfClosing)
						{
							// Self-closing <div />: create an empty div immediately
							divTag.Lines = [];
							return new ConsoleDivPart(divTag.X, divTag.Y, divTag.Width, divTag.Height, divTag.Depth, divTag.Color, divTag.StyledBox, divTag.IsRelative, divTag.Display, divTag.Lines);
						}
						state.CurrentDivTag = divTag;
						state.StartingSubDivision = true;
						return null;
					}
				#endregion
				case "shape":
					{
						if (wc == null)
							throw new CodeEE(string.Format(trerror.TagHasNotAttribute.Text, tag));
						#region EM_私家版_HTMLパラメータ拡張
						MixedNum[] param = null;
						string type = null;
						int color = int.MinValue;
						int bcolor = int.MinValue;
						while (!wc.EOL)
						{
							word = wc.Current as IdentifierWord;
							wc.ShiftNext();
							OperatorWord op = wc.Current as OperatorWord;
							wc.ShiftNext();
							LiteralStringWord attr = wc.Current as LiteralStringWord;
							wc.ShiftNext();
							if (word == null || op == null || op.Code != OperatorCode.Assignment || attr == null)
								goto error;
							string attrValue = Unescape(attr.Str);
							switch (word.Code.ToLower())
							{
								case "color":
									if (color != int.MinValue)
										throw new CodeEE(string.Format(trerror.DuplicateAttribute.Text, tag, word.Code));
									color = stringToColorInt32(attrValue);
									break;
								case "bcolor":
									if (bcolor != int.MinValue)
										throw new CodeEE(string.Format(trerror.DuplicateAttribute.Text, tag, word.Code));
									bcolor = stringToColorInt32(attrValue);
									break;
								case "type":
									if (type != null)
										throw new CodeEE(string.Format(trerror.DuplicateAttribute.Text, tag, word.Code));
									type = attrValue;
									break;
								case "param":
									if (param != null)
										throw new CodeEE(string.Format(trerror.DuplicateAttribute.Text, tag, word.Code));
									{
										string[] tokens = attrValue.Split(',');
										param = new MixedNum[tokens.Length];
										for (int i = 0; i < tokens.Length; i++)
										{
											param[i] = new MixedNum();
											tokens[i] = tokens[i].Trim();

											if (tokens[i].EndsWith("px", StringComparison.OrdinalIgnoreCase))
											{
												if (!int.TryParse(tokens[i].Substring(0, tokens[i].Length - 2), out param[i].num))
													throw new CodeEE(string.Format(trerror.AttributeCanNotInterpretNum.Text, tag, word.Code));
												param[i].isPx = true;
											}
											else if (!int.TryParse(tokens[i], out param[i].num))
												throw new CodeEE(string.Format(trerror.AttributeCanNotInterpretNum.Text, tag, word.Code));
										}
										break;
									}
								default:
									throw new CodeEE(string.Format(trerror.CanNotInterpretAttributeName.Text, tag, word.Code));
							}
						}
						if (param == null)
							throw new CodeEE(string.Format(trerror.NotSetAttribute.Text, tag, "param"));
						if (type == null)
							throw new CodeEE(string.Format(trerror.NotSetAttribute.Text, tag, "type"));
						Color c = Config.ForeColor;
						Color b = Config.FocusColor;
						if (color != int.MinValue)
						{
							c = Color.FromArgb(color);
						}
						if (bcolor != int.MinValue)
						{
							b = Color.FromArgb(bcolor);
						}
						return ConsoleShapePart.CreateShape(type, param, c, b, color != int.MinValue);
						#endregion
					}
				case "button":
				case "nonbutton":
					{
						if (state.CurrentButtonTag != null)
							throw new CodeEE(trerror.NestedButtonTag.Text);
						HtmlAnalzeStateButtonTag buttonTag = new HtmlAnalzeStateButtonTag();
						bool isButton = tag.ToLower() == "button";
						string attrValue;
						string value = null;
						while (wc != null && !wc.EOL)
						{
							word = wc.Current as IdentifierWord;
							wc.ShiftNext();
							OperatorWord op = wc.Current as OperatorWord;
							wc.ShiftNext();
							LiteralStringWord attr = wc.Current as LiteralStringWord;
							wc.ShiftNext();
							if (word == null || op == null || op.Code != OperatorCode.Assignment || attr == null)
								goto error;
							attrValue = Unescape(attr.Str);
							if (word.Code.Equals("value", StringComparison.OrdinalIgnoreCase))
							{
								if (!isButton)
									throw new CodeEE(string.Format(trerror.NotSetAttribute.Text, tag, "value"));
								if (value != null)
									throw new CodeEE(string.Format(trerror.DuplicateAttribute.Text, tag, word.Code));
								value = attrValue;
							}
							else if (word.Code.Equals("title", StringComparison.OrdinalIgnoreCase))
							{
								if (buttonTag.ButtonTitle != null)
									throw new CodeEE(string.Format(trerror.DuplicateAttribute.Text, tag, word.Code));
								buttonTag.ButtonTitle = attrValue;
							}
							else if (word.Code.Equals("pos", StringComparison.OrdinalIgnoreCase))
							{
								if (buttonTag.PointXisLocked)
									throw new CodeEE(string.Format(trerror.DuplicateAttribute.Text, tag, word.Code));
								if (!int.TryParse(attrValue, out int pos))
									throw new CodeEE(string.Format(trerror.AttributeCanNotInterpretNum.Text, tag, "pos"));
								buttonTag.PointX = pos;
								buttonTag.PointXisLocked = true;
							}
							else
								throw new CodeEE(string.Format(trerror.CanNotInterpretAttributeName.Text, tag, word.Code));
						}
						#region EM_私家版_clearbutton
						if (!state.FlagClearButton)
						{
							if (isButton)
							{
								buttonTag.ButtonIsInteger = (Int64.TryParse(value, out long intValue));
								buttonTag.ButtonValueInt = intValue;
								buttonTag.ButtonValueStr = value;
							}
							buttonTag.IsButton = value != null;
						}
						else
						{
							buttonTag.IsButton = false;
							if (state.FlagClearButtonTooltip)
								buttonTag.ButtonTitle = null;
						}
						#endregion
						buttonTag.IsButtonTag = isButton;
						state.CurrentButtonTag = buttonTag;
						state.FlagButton = true;
						return null;
					}
				#region EM_私家版_clearbutton
				case "clearbutton":
					{
						if (state.FlagClearButton)
							throw new CodeEE(trerror.NestedClearbuttonTag.Text);
						if (wc!=null)
							while (!wc.EOL)
							{
								word = wc.Current as IdentifierWord;
								wc.ShiftNext();
								OperatorWord op = wc.Current as OperatorWord;
								wc.ShiftNext();
								LiteralStringWord attr = wc.Current as LiteralStringWord;
								wc.ShiftNext();
								if (word == null || op == null || op.Code != OperatorCode.Assignment || attr == null)
									goto error;
								if (word.Code.ToLower()=="notooltip")
								{
									var val = attr.Str.ToLower();
									if (val == "true")
										state.FlagClearButtonTooltip = true;
									else if (val != "false")
										throw new CodeEE(string.Format(trerror.ClearbuttonAttributeCanNotInterpretNum.Text, tag, word.Code, attr.Str));
								}
								else
									throw new CodeEE(string.Format(trerror.CanNotInterpretAttributeName.Text, tag, word.Code));
							}
						state.FlagClearButton = true;
						return null;
					}
				#endregion
				case "font":
					{
						if (wc == null)
							throw new CodeEE(string.Format(trerror.TagHasNotAttribute.Text, tag));
						HtmlAnalzeStateFontTag font = new HtmlAnalzeStateFontTag();
						while (!wc.EOL)
						{
							word = wc.Current as IdentifierWord;
							wc.ShiftNext();
							OperatorWord op = wc.Current as OperatorWord;
							wc.ShiftNext();
							LiteralStringWord attr = wc.Current as LiteralStringWord;
							wc.ShiftNext();
							if (word == null || op == null || op.Code != OperatorCode.Assignment || attr == null)
								goto error;
							string attrValue = Unescape(attr.Str);
							switch (word.Code.ToLower())
							{
								case "color":
									if (font.Color != int.MinValue)
										throw new CodeEE(string.Format(trerror.DuplicateAttribute.Text, tag, word.Code));
									font.Color = stringToColorInt32(attrValue);
									break;
								case "bcolor":
									if (font.BColor != int.MinValue)
										throw new CodeEE(string.Format(trerror.DuplicateAttribute.Text, tag, word.Code));
									font.BColor = stringToColorInt32(attrValue);
									break;
								case "face":
									if (font.FontName != null)
										throw new CodeEE(string.Format(trerror.DuplicateAttribute.Text, tag, word.Code));
									font.FontName = attrValue;
									break;
								case "render":
									if (font.RenderMode != null)
										throw new CodeEE(string.Format(trerror.DuplicateAttribute.Text, tag, word.Code));
									if (attrValue.Equals("gdi", StringComparison.OrdinalIgnoreCase))
										font.RenderMode = TextDrawingMode.TEXTRENDERER;
									else if (attrValue.Equals("skia", StringComparison.OrdinalIgnoreCase))
										font.RenderMode = TextDrawingMode.SKIASHARP;
									else
										throw new CodeEE(string.Format(trerror.CanNotInterpretAttribute.Text, tag, word.Code));
									break;
								case "edging":
									if (font.FontEdging != null)
										throw new CodeEE(string.Format(trerror.DuplicateAttribute.Text, tag, word.Code));
									if (attrValue.Equals("alias", StringComparison.OrdinalIgnoreCase))
										font.FontEdging = SkiaSharpFontEdging.Alias;
									else if (attrValue.Equals("antialias", StringComparison.OrdinalIgnoreCase))
										font.FontEdging = SkiaSharpFontEdging.AntiAlias;
									else if (attrValue.Equals("subpixel", StringComparison.OrdinalIgnoreCase))
										font.FontEdging = SkiaSharpFontEdging.SubpixelAntiAlias;
									else
										throw new CodeEE(string.Format(trerror.CanNotInterpretAttribute.Text, tag, word.Code));
									break;
								case "hinting":
									if (font.FontHinting != null)
										throw new CodeEE(string.Format(trerror.DuplicateAttribute.Text, tag, word.Code));
									if (attrValue.Equals("none", StringComparison.OrdinalIgnoreCase))
										font.FontHinting = SkiaSharpFontHinting.None;
									else if (attrValue.Equals("slight", StringComparison.OrdinalIgnoreCase))
										font.FontHinting = SkiaSharpFontHinting.Slight;
									else if (attrValue.Equals("normal", StringComparison.OrdinalIgnoreCase))
										font.FontHinting = SkiaSharpFontHinting.Normal;
									else if (attrValue.Equals("full", StringComparison.OrdinalIgnoreCase))
										font.FontHinting = SkiaSharpFontHinting.Full;
									else
										throw new CodeEE(string.Format(trerror.CanNotInterpretAttribute.Text, tag, word.Code));
									break;
								case "size":
									if (font.FontSize != null)
										throw new CodeEE(string.Format(trerror.DuplicateAttribute.Text, tag, word.Code));
									string sizeStr = attrValue;
									if (sizeStr.EndsWith("px", StringComparison.OrdinalIgnoreCase))
										sizeStr = sizeStr.Substring(0, sizeStr.Length - 2);
									if (!float.TryParse(sizeStr, out float sizeValue) || sizeValue <= 0)
										throw new CodeEE(string.Format(trerror.CanNotInterpretAttribute.Text, tag, word.Code));
									font.FontSize = sizeValue;
									break;
								case "valign":
									if (font.VerticalAlign != null)
										throw new CodeEE(string.Format(trerror.DuplicateAttribute.Text, tag, word.Code));
									if (attrValue.Equals("top", StringComparison.OrdinalIgnoreCase))
										font.VerticalAlign = FontVerticalAlign.Top;
									else if (attrValue.Equals("middle", StringComparison.OrdinalIgnoreCase))
										font.VerticalAlign = FontVerticalAlign.Middle;
									else if (attrValue.Equals("bottom", StringComparison.OrdinalIgnoreCase))
										font.VerticalAlign = FontVerticalAlign.Bottom;
									else
										throw new CodeEE(string.Format(trerror.CanNotInterpretAttribute.Text, tag, word.Code));
									break;
								default:
									throw new CodeEE(string.Format(trerror.CanNotInterpretAttributeName.Text, tag, word.Code));
							}
						}
						if (state.FonttagList.Count > 0)
						{
							HtmlAnalzeStateFontTag oldFont = state.FonttagList[state.FonttagList.Count - 1];
							if (font.Color == int.MinValue)
								font.Color = oldFont.Color;
							if (font.BColor == int.MinValue)
								font.BColor = oldFont.BColor;
							if (font.FontName == null)
								font.FontName = oldFont.FontName;
							if (font.FontSize == null)
								font.FontSize = oldFont.FontSize;
							if (font.RenderMode == null)
								font.RenderMode = oldFont.RenderMode;
							if (font.FontEdging == null)
								font.FontEdging = oldFont.FontEdging;
							if (font.FontHinting == null)
								font.FontHinting = oldFont.FontHinting;
							if (font.VerticalAlign == null)
								font.VerticalAlign = oldFont.VerticalAlign;
						}
						state.FonttagList.Add(font);
						return null;
					}
				default:
					goto error;
			}


		error:
			throw new CodeEE(string.Format(trerror.HtmlTagError.Text, st.RowString));
		}

		private static int stringToColorInt32(string str)
		{
			if(str.Length == 0)
				throw new CodeEE(trerror.RequireColorCode.Text);
			int i;
			if (str[0] == '#')
			{
				string colorvalue = str.Substring(1);
				// Strip optional "0x"/"0X" prefix (e.g. "#0xf19ec2" → "f19ec2")
				if (colorvalue.Length > 2 &&
					colorvalue[0] == '0' && (colorvalue[1] == 'x' || colorvalue[1] == 'X'))
				{
					colorvalue = colorvalue.Substring(2);
				}
				try
				{
					// Support ARGB: 6 hex digits or less → RGB (alpha defaults to 0xFF),
					// more than 6 hex digits → ARGB
					if (colorvalue.Length <= 6)
					{
						// RGB mode: value must be in [0, 0xFFFFFF]
						i = Convert.ToInt32(colorvalue, 16);
						if (i < 0 || i > 0xFFFFFF)
							throw new CodeEE(string.Format(trerror.OoRColorValue.Text, colorvalue));
						// Set alpha to 0xFF for RGB values
						i = unchecked((int)((uint)i | 0xFF000000));
					}
					else
					{
						// ARGB mode: value must be in [0, 0xFFFFFFFF]
						long val = Convert.ToInt64(colorvalue, 16);
						if (val < 0 || val > 0xFFFFFFFF)
							throw new CodeEE(string.Format(trerror.OoRColorValue.Text, colorvalue));
						i = unchecked((int)val);
					}
				}
				catch (CodeEE)
				{
					throw;
				}
				catch
				{
					throw new CodeEE(string.Format(trerror.CanNotInterpretNumValue.Text, colorvalue));
				}
			}
			else
			{
				Color color = Color.FromName(str);
				if (color.A == 0)
				{
					if(str.Equals("transparent", StringComparison.OrdinalIgnoreCase))
						throw new CodeEE(trerror.TransparentUnsupported.Text);
					try
					{
						i = Convert.ToInt32(str, 16);
					}
					catch
					{
						throw new CodeEE(trerror.InvalidColorName.Text);
					}
					throw new CodeEE(string.Format(trerror.InvalidColorName2.Text, str));
				}
				i = unchecked((int)((uint)(color.R * 0x10000 + color.G * 0x100 + color.B) | 0xFF000000));
			}
			return i;
		}

	}
}
