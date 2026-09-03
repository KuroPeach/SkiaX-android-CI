using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using SkiaSharp.Views.Desktop;
using Xamarin.Forms;
using MinorShift.Emuera.Runtime.Utils.EvilMask;

namespace MinorShift.Emuera.GameView
{
	internal sealed partial class EmueraConsole
	{
		StackLayout quickButtonGroup;

		public void ClearQuickButton()
		{
			window.Invoke(() =>
			{
				ClearQuickButtonAsync();
			});
		}

		public void ClearQuickButtonAsync()
		{
			quickButtonGroup = window.quickButtonGroup;

			if (quickButtonGroup.Children.Count > 0)
			{
				quickButtonGroup.Children.Clear();
				quickButtonGroup.WidthRequest = -1;
				quickButtonGroup.HeightRequest = -1;

				window.quickButtonScrollView.WidthRequest = -1;
				window.quickButtonScrollView.HeightRequest = -1;
			}
		}

		public void RefreshQuickButton()
		{
			window.Invoke(() =>
			{
				RefreshQuickButtonAsync();
			});
		}

		public void RefreshQuickButtonAsync()
		{
			if (!window.quickButtonGroup.IsVisible)
				return;

			if (state != ConsoleState.WaitInput)
				return;

			//EE_BINPUT: BINPUT/BINPUTS(IntButton/StrButton) 等待时也要收集快捷按钮，
			//否则 BINPUT 菜单按钮无法映射到画廊 QuickButton 面板
			if (inputReq.InputType != Runtime.InputType.IntValue && inputReq.InputType != Runtime.InputType.StrValue
				&& inputReq.InputType != Runtime.InputType.IntButton && inputReq.InputType != Runtime.InputType.StrButton)
				return;

			bool lockTaken = false;
			displayLineListSpinLock.Enter(ref lockTaken);

			int bottomLineNo = displayLineList.Count - 1;
			int topLineNo = Math.Max(bottomLineNo - (ClientHeight / Config.LineHeight) - 10, 0);

			int row;
			int column = 0;

			ConsoleDisplayLine curLine;
			ConsoleButtonString button;

			StackLayout layout;
			Button quickButton;

			Color color;
			Color bgcolor;
			AConsoleColoredPart coloredPart;

			quickButtonGroup = window.quickButtonGroup;
			quickButtonGroup.Spacing = Config.QuickButtonSpacing;

			for (int i = topLineNo; i <= bottomLineNo; i++)
			{
				curLine = displayLineList[i];
				row = 0;
				layout = new StackLayout
				{
					Orientation = StackOrientation.Horizontal,
					Spacing = Config.QuickButtonSpacing,
					HorizontalOptions = LayoutOptions.Start,
				};

				for (int b = 0; b < curLine.Buttons.Length; b++)
				{
					button = curLine.Buttons[b];
					if (button == null || !button.IsButton)
						continue;
					if (button.StrArray == null || button.StrArray.Length == 0)
						continue;
					if (button.Generation != lastButtonGeneration)
						continue;

					coloredPart = button.StrArray[button.StrArray.Length - 1] as AConsoleColoredPart;
					color = coloredPart?.Color ?? Config.ForeColor;

					bgcolor = Math.Abs(color.R - 0x7f) <= 0x10 && Math.Abs(color.G - 0x7f) <= 0x10 && Math.Abs(color.B - 0x7f) <= 0x10
						? Config.BackColor : color;

					// Build display text, skipping shape/image nodes that produce HTML tags
					var sb = new StringBuilder();
					foreach (var node in button.StrArray)
					{
						if (node is ConsoleShapePart || node is ConsoleImagePart)
							continue;
						if (node.Text != null)
							sb.Append(node.Text);
					}
					var displayText = sb.ToString().Trim();

					quickButton = new Button
					{
						BindingContext = button.Inputs,
						Text = displayText,
						TextColor = color.ToFormsColor(),
						BackgroundColor = Color.FromArgb(0xc0, Color.FromArgb(bgcolor.ToArgb() ^ 0xffffff)).ToFormsColor(),
						FontSize = Config.QuickButtonFontSize,
						WidthRequest = Config.QuickButtonWidth,
						HeightRequest = Config.QuickButtonHeight,
						Padding = Config.QuickButtonPadding,
					};
					quickButton.Clicked += window.quickButton_Clicked;

					if (quickButtonGroup.Children.Count - 1 < column)
						quickButtonGroup.Children.Add(layout);

					layout.Children.Add(quickButton);

					row++;
				}
				if (row > 0)
					column++;
			}

			// --- 补充：<div>/escapedParts 布局内的子按钮（与 CollectVisibleSelectionCandidates 对齐）---
			// 主循环只遍历 displayLineList 行级按钮，div 布局内的按钮（BINPUT 菜单、
			// COM_OPTION 弹窗等）不挂在行级 Buttons 上，需从 ConsoleEscapedParts 单独收集
			var escapedParts = EscapedParts;
			if (ConsoleEscapedParts.Changed || !ConsoleEscapedParts.TestedInRange(topLineNo, bottomLineNo, lastButtonGeneration))
				ConsoleEscapedParts.GetPartsInRange(topLineNo, bottomLineNo, lastButtonGeneration, escapedParts);
			if (escapedParts != null && escapedParts.Count > 0)
			{
				foreach (var kvp in escapedParts)
				{
					foreach (var part in kvp.Value)
					{
						if (!(part is ConsoleDivPart div))
							continue;
						foreach (ConsoleDisplayLine line in div.Children)
						{
							if (line == null) continue;

							// 镜像主循环：每个 div 子行一个横向 layout，row/column 计数一致
							row = 0;
							layout = new StackLayout
							{
								Orientation = StackOrientation.Horizontal,
								Spacing = Config.QuickButtonSpacing,
								HorizontalOptions = LayoutOptions.Start,
							};

							foreach (ConsoleButtonString divButton in line.Buttons)
							{
								if (divButton == null || !divButton.IsButton)
									continue;
								if (divButton.StrArray == null || divButton.StrArray.Length == 0)
									continue;
								if (divButton.Generation != lastButtonGeneration)
									continue;

								coloredPart = divButton.StrArray[divButton.StrArray.Length - 1] as AConsoleColoredPart;
								color = coloredPart?.Color ?? Config.ForeColor;

								bgcolor = Math.Abs(color.R - 0x7f) <= 0x10 && Math.Abs(color.G - 0x7f) <= 0x10 && Math.Abs(color.B - 0x7f) <= 0x10
									? Config.BackColor : color;

								// Build display text, skipping shape/image/div nodes that produce HTML tags
								var divSb = new StringBuilder();
								foreach (var node in divButton.StrArray)
								{
									if (node is ConsoleShapePart || node is ConsoleImagePart || node is ConsoleDivPart)
										continue;
									if (node.Text != null)
										divSb.Append(node.Text);
								}
								var divDisplayText = divSb.ToString().Trim();

								quickButton = new Button
								{
									BindingContext = divButton.Inputs,
									Text = divDisplayText,
									TextColor = color.ToFormsColor(),
									BackgroundColor = Color.FromArgb(0xc0, Color.FromArgb(bgcolor.ToArgb() ^ 0xffffff)).ToFormsColor(),
									FontSize = Config.QuickButtonFontSize,
									WidthRequest = Config.QuickButtonWidth,
									HeightRequest = Config.QuickButtonHeight,
									Padding = Config.QuickButtonPadding,
								};
								quickButton.Clicked += window.quickButton_Clicked;

								if (quickButtonGroup.Children.Count - 1 < column)
									quickButtonGroup.Children.Add(layout);

								layout.Children.Add(quickButton);

								row++;
							}
							if (row > 0)
								column++;
						}
					}
				}
			}

			quickButtonGroup.ResolveLayoutChanges();
			window.RefreshQuickButtonGroup();

			if (lockTaken)
				displayLineListSpinLock.Exit();
		}
	}
}
