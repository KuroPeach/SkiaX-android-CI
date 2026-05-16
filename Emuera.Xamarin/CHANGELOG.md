# Emuera.Xamarin 变更记录

## [0.8.0] - 2026-05-18

### Phase 4A-Desktop 续2：ToolTip + QuickButton + DebugDialog 完善 ✅

#### 切片 4A-D17：DesktopToolTip WinForms ToolTip 桥接 ✅

- `DesktopToolTip`：从空壳升级为完整 WinForms ToolTip 桥接
  - `Show()`/`RemoveAll()`/`InitialDelay`/`AutoPopDelay`/`ForeColor`/`BackColor`/`OwnerDraw` 全部实现
  - `Show()` 通过 `form.BeginInvoke` 确保 UI 线程安全
- `ToolTipStub`：`Show`/`RemoveAll` 改为 `virtual`（支持 override）

#### 切片 4A-D18：DesktopQuickButton FlowLayoutPanel 快捷按钮 ✅

- `DesktopQuickButton`：新建 FlowLayoutPanel 快捷按钮面板
  - `AddButton()`：创建 Button 控件，点击时填入文本并 `PressEnterKey`
  - `Clear()`：清空所有快捷按钮
  - 按钮样式：FlatStyle.Flat + 深色背景 + MS Gothic 字体
- `QuickButtonGroup` stub：`AddButton`/`Clear` 改为 `virtual`
- `MainWindow` stub：`QuickButtonGroup` 改为 `virtual` 属性 + `quickButtonGroup` 别名

#### 切片 4A-D19：EmueraConsole ToolTip 扩展方法 ✅

- `SetToolTipFontName`/`SetToolTipImg`/`SetToolTipFormat`/`CustomToolTip`/`SetToolTipFontSize`：从空壳改为存储状态字段
  - `_tooltipFontName`/`_tooltipFontSize`/`_tooltipFormat`/`_tooltipImgEnabled`/`_tooltipImg`/`_tooltipDuration`
  - `CustomToolTip()` → `window.ToolTip.OwnerDraw = b`

#### 切片 4B-D6：DesktopDebugDialog WinForms 调试对话框 ✅

- 新建 `DesktopDebugDialog.cs`：WinForms 调试对话框
  - TabControl：Console/Variables/StackTrace 三个标签页
  - Console 标签页：显示 `DebugConsoleLog`（黑色背景 + 白色文字）
  - Refresh 按钮：手动刷新调试输出
  - `OnFormClosing`：隐藏而非关闭（支持重复打开）
- `DesktopMainWindow.ShowDebugDialog()` override：打开调试对话框

#### 切片 4C-1/2：DebugDialog 属性恢复 + OpenDebugDialog 激活 ✅

- `DebugDialog` 属性：从注释改为 `public object DebugDialog { get; } = null`
- `DebugConsoleLog` 属性恢复：`dConsoleLog` StringBuilder 激活
- `DebugPrint`/`DebugClear`/`DebugNewLine`：从空壳改为实际日志操作
- `OpenDebugDialog()`：从空壳改为调用 `window.ShowDebugDialog()`
- 恢复初始化时 `OpenDebugDialog` 调用（`Program.DebugMode && Config.DebugShowWindow`）
- 恢复 `@DEBUG` 命令中 `OpenDebugDialog` 调用
- `MainWindow` stub：添加 `ShowDebugDialog()` virtual 方法
- csproj：添加 `DesktopDebugDialog.cs` 编译项

## [0.7.0] - 2026-05-18

### Phase 4A-Desktop 续：鼠标交互 + WinForms 兼容性 ✅

#### 切片 4A-D10/D11：桌面端鼠标事件处理 + 输入完善 ✅

- 重写 `DesktopHost.cs`：添加完整鼠标事件处理
  - `PictureBox.MouseMove` → `console.MoveMouse()`（按钮高亮/选中）
  - `PictureBox.MouseDown/Up` → `console.MouseDown()`（INPUTMOUSEKEY 原始事件转发）
  - `PictureBox.MouseWheel` → 滚动条滚动 + `console.MouseWheel()`（INPUTMOUSEKEY）
  - `PictureBox.MouseLeave` → `console.LeaveMouse()`（清除选中状态）
  - `PictureBox.MouseEnter` → `pictureBox.Focus()`（支持鼠标滚轮）
- 添加 `MouseReleased()` 逻辑（参考 XEmuera-1）
  - backlog 跳转：点击时自动跳回最新位置
  - 按钮选择：左键点击选中按钮 → `PressEnterKey(false, str, true)`
  - 空白区域：左键确认/右键跳过
  - `IsWaitingPrimitive`：直接转发 `console.MouseDown()`
- 改进 `PressEnterKey()`：后台线程执行 + `pressEnterKeyInProcess` 重入保护
- 添加 Escape 键处理：`console.KillMacro = true` + `PressEnterKey(true, "", false)`
- 缓存 `DesktopScrollBar` 实例（修复每次访问创建新实例的 bug）
- 实现 `DesktopMainWindow.Reboot()`/`Close()`/`Title`/`ChangeTextBox()`/`clear_richText()`

#### 切片 4A-D12/D13：WinForms 兼容性修复 ✅

- 替换 `MainThread.BeginInvokeOnMainThread` → `window.Invoke`（4 处）
  - `EmueraConsole.verticalScrollBarUpdate()`：滚动条更新
  - `EmueraConsole.RefreshStrings()`：背景色更新
  - `EmueraConsole.ex.ClearQuickButton()`：快捷按钮清空
  - `EmueraConsole.ex.RefreshQuickButton()`：快捷按钮刷新
- 修复 `Color.WithAlpha(0xc0)` → `Color.FromArgb(0xc0, color)`（System.Drawing.Color 兼容）
- 移除 `using Xamarin.Essentials`（EmueraConsole.cs + EmueraConsole.ex.cs）
- `App.DoEvents()`：从空实现改为 `Application.DoEvents()`

### Phase 4B-Desktop：WinForms 配置对话框 ✅

#### 切片 4B-D1~D5：DesktopConfigDialog ✅

- 新建 `DesktopConfigDialog.cs`：WinForms 配置编辑对话框
  - 左侧 TreeView 分组导航 + 右侧 ListView 属性列表
  - 双击编辑：bool 切换 / ColorDialog 颜色选择 / 字符串输入弹窗
  - Reset Default 按钮：恢复单项默认值
  - OK 按钮：调用 `ConfigData.SaveConfig()` 保存到 emuera.config
- 11 个配置分组：
  - Environment（AutoSave/MaxLog/InfiniteLoopAlertTime 等）
  - Display（FPS/PrintCPerLine/PrintCLength/ButtonWrap）
  - Window（WindowX/ScrollHeight/PanSpeed）
  - Font（ForeColor/BackColor/FocusColor/FontName/FontSize/LineHeight）
  - System 1/2（IgnoreCase/UseRenameFile/Compat 系列）
  - Compatibility（CompatiErrorLine/CompatiCALLNAME/CompatiRAND 等）
  - Debug（WarnBackCompatibility/DisplayReport/ReduceArgumentOnLoad 等）
  - Quick Buttons / Font & Shape / Long Press Skip
- `DesktopMainWindow.ShowConfigDialog()` override：打开配置对话框 + 刷新画面

## [0.6.0] - 2026-05-17

### Phase 6：EM 私家版 HTML 扩展 ✅

#### 切片 6A：类型体系统一 ✅

- 合并 `AConsoleDisplayPart` → `AConsoleDisplayNode`，统一为 LazyLoading 单基类体系
  - `AConsoleDisplayNode` 升级为完整版：`Parent` 类型从 `object` 改为 `ConsoleButtonString`
  - 新增 Rikaichan 扩展属性：`rikaichaned`、`Ends[]`、`NextLine`
  - `AConsoleColoredPart` 新增 `BackgroundColor?` 属性
- 删除 `AConsoleDisplayPart.cs`（GameView 命名空间）
- 所有显示部件类更新基类：
  - `ConsoleStyledString`：新增 `FontSize`/`RenderMode`/`FontEdging`/`FontHinting` 扩展属性
  - `ConsoleImagePart`：新增 `Display`/`Xpos`/`ResourceNameM`/`ColorMatrix` 扩展字段
  - `ConsoleShapePart` 及其子类：基类更新
- `DrawTo` 签名统一：`(SKCanvas, int pointY, ...)` → `(SKCanvas, SKPoint point, bool isSelecting, bool isFocus, bool isBackLog, TextDrawingMode, bool isButton)`
- `Str` → `Text` 属性名统一（全项目范围）
- `ConsoleButtonString.StrArray` 类型：`AConsoleDisplayPart[]` → `AConsoleDisplayNode[]`

#### 切片 6B：HtmlManager 内部类 + 状态字段 ✅

- `HtmlAnalzeState` 新增 div 字段：`StartingSubDivision`/`CurrentDivTag`/`SubDivisionWidth`/`DivState`
- `HtmlAnalzeState` 新增 font 扩展属性：`RenderMode`/`FontEdging`/`FontHinting`/`FontSize`
- `HtmlAnalzeStateFontTag` 新增 font 扩展字段
- 新增 `HtmlDivTag` 内部类（Width/Height/X/Y/Color/Depth/StyledBox/IsRelative/Display/Lines）
- 新增 `DivState` 内部类（IsDiv/PosX/PosY/Display/BackgroundColor/Width/Height/HasBorder/BorderWidth/BorderColor/Padding）
- 新增 `HtmlParentInfo` 内部类（State/Stream/HasComment/HasReturn）
- 新增 `ParseParam4IntNum` 方法（CSS 风格 1~4 值参数解析）

#### 切片 6C：div 标签解析 + 递归渲染 ✅

- `tagAnalyze` 添加 `case "div"` 开标签（解析 width/height/xpos/ypos/depth/color/bcolor/display/rect/size 属性）
- `tagAnalyze` 添加 `case "div"` 闭标签
- `html2DisplayLine` 添加 div 递归分支（StartingSubDivision → 递归调用 → new ConsoleDivPart）
- `Html2DisplayLine` 改为委托到 `html2DisplayLine` 内部方法（支持 HtmlParentInfo 和 buttonsOutput）
- `tagAnalyze` 添加 `<font>` 扩展属性（render/edging/hinting/size）
- `tagAnalyze` 添加 `<img>` 扩展属性（srcm/xpos/cm/display）
- `ButtonsToDisplayLines` 添加 `subDiv`/`divWidth` 参数支持 div 内子区域渲染
- `ConsoleDisplayLine.SetAlignment` 添加 `customWidth` 参数
- `ConsoleDisplayLine` 新增 `IsLineEnd` 属性

#### 切片 6D：EscapedParts 注册链路 ✅

- `ConsoleButtonString.FilterEscaped()` 从空实现升级为完整逻辑（筛选 Top<0 或 Bottom>LineHeight 的越界部件）
- `ConsoleButtonString.DrawPartTo()` 修复：传入 `SKPoint(-1, pointY)` + isFocus 参数
- `EmueraConsole.Print.addDisplayLine()` 添加 `ConsoleEscapedParts.Add()` 注册
- `EmueraConsole.Print.ClearDisplay()` 添加 `ConsoleEscapedParts.Clear()`
- `EmueraConsole.Print.deleteLine()` 添加 `ConsoleEscapedParts.Remove()`
- `EmueraConsole.Print` MaxLog 溢出时添加 `ConsoleEscapedParts.RemoveAt()`
- `EmueraConsole` 新增 `ButtonIsPointing()` 方法

#### 切片 6E：PrintHtmlC 全量版 ✅

- `PrintStringBuffer.CurrentLineWidth`：遍历 m_buttonList 求和
- `PrintStringBuffer.AppendButton(ConsoleButtonString)`：直接追加按钮对象
- `HtmlManager.Html2ButtonList()`：HTML→按钮列表（buttonsOutput 分支）
- `PrintHtmlC()` 升级为全量版：CurrentLineWidth 溢出换行 + Html2ButtonList + AppendButton + LC/RC 对齐
- `PrintHtml(str, toPrintBuffer)` 升级为全量版：toPrintBuffer=true 时走 Html2ButtonList→AppendButton 路径
- `PrintStringBuffer.Flush` 设置 `IsLineEnd`
- `PrintStringBuffer.Append` 添加 `lineEnd` 参数

## [0.5.0] - 2026-05-16

### Phase 3.5：渲染管线补全 + 桌面端修复 ✅

#### 切片 3.5-A：桌面端渲染触发修复 ✅

- 修复致命 bug：`TriggerRefresh()` 调用 `pictureBox.Invalidate()` 但 `OnPaint` 重写在 `Form` 上
  - 注册 `pictureBox.Paint` 事件，将渲染委托给 PictureBox
  - 移除 `Form.OnPaint`/`OnPaintBackground` 重写

#### 切片 3.5-B：CreateSpriteFromFileDynamic 修复 ✅

- 从永远返回 false 改为完整实现
  - `SKBitmap.Decode(filepath)` 加载图片
  - `ConstImage.CreateFrom(bmp, false)` 创建图片
  - `SpriteF` 包装并注册到 `imageDictionary`
  - 异常保护（try-catch 返回 false）

#### 切片 3.5-C：EmueraConsole.Print 空方法补全 ✅

- `PrintHtmlC()`：HTML 制表打印（LC左对齐/RC右对齐列，简化版）
- `PrintImg()` 3参数版：追加 `ConsoleImagePart`（mask 参数暂忽略）
- `printCustomBar()`：自定义 DRAWLINE 字符串打印
- `PrintHtml()`：HTML 打印（flush 缓冲区 + Html2DisplayLine 独立行）
- `PrintErrorButton()`：错误按钮 + 颜色级别（0-2 半透明白色，3+ 红色）

#### 切片 3.5-D：EscapedParts zdepth 交错渲染 ✅

- OnPaint 从简单 cidx 循环升级为 eidx+cidx 双指针归并渲染
  - CBG 和 EscapedParts 按 depth 降序交错绘制
  - depth==0 时绘制普通文本行
  - 使用 LazyLoading 内核 `ConsoleEscapedParts` 全局静态记录管理
- `ConsoleButtonString` 添加 `FilterEscaped()`/`DrawPartTo()`/`EscapedParts`
- `AConsoleDisplayPart` 添加 `Parent`/`Depth` 属性
- `EmueraConsole` 添加 `using System.Linq` 支持

#### 切片 3.5-E：HTML_PRINT 岛屿输出 ✅

- `PrintHTMLIsland()`：Html2DisplayLine → `_htmlElementList`
- `ClearHTMLIsland()`：清空 `_htmlElementList`
- OnPaint 末尾绘制 `_htmlElementList`（y=0 开始，不受滚动影响）

## [0.4.0] - 2026-05-16

### Phase 3：Skia 渲染集成 ✅

#### 切片 3A：ImageLayerManager 图层管理 ✅

- 完整重写 `ImageLayerManager`（从 LazyLoading 117行移植）
  - `ImageLayer` 数据结构：`ASprite Image`（替换 `object`）、`Depth`/`X`/`Y`/`Width`/`Height`/`Opacity`/`ColorMatrix`/`FollowScroll`/`InitialScrollY`/`IsOffScreen`
  - `SetLayer()`：创建 ImageLayer 并存入字典，查找 ASprite
  - `DrawTo()`：按 Depth 升序绘制，视口裁剪，FollowScroll 偏移，SKColorFilter 支持
  - `ClearLayer()`/`ClearAll()`/`Exists()`：图层管理
  - `BuildFilter()`：ColorMatrix 或透明度 SKColorFilter 创建
- 添加 `_imageLayerManager` 字段到 EmueraConsole
- 连接 EmueraConsole 4 个方法：`SetImageLayer`/`ClearImageLayer`/`ClearImageLayerAll`/`ExistsImageLayer`
- 集成 `_imageLayerManager.DrawTo()` 到 OnPaint（`graph.Clear()` 之后、cbgList 遍历之前）
- 注意：SpriteAnime.PauseAnimation/ResumeAnimation 暂未实现（xamarin 版 SpriteAnime 缺少此 API）

#### 切片 3B：ColorMatrix 颜色矩阵 ✅

- 完整移植 `ColorMatrixHelper`（从 LazyLoading 84行移植）
  - 修正命名空间：`MinorShift.Emuera.Runtime.Utils.EvilMask` → `MinorShift.Emuera.UI.Game.Image`（与调用者 using 一致）
  - `ReadFromVariableTerm()`：从 ERB 变量表达式读取 5x5 矩阵
  - `ReadFromVariable()`：支持 2D/3D 数组 + 角色变量，值范围 0-256（÷256f 转 0.0-1.0）
  - `ToSkia()`：5x5 float[][] → 20 元素 float[]，offset 列 × 255
  - 移除 `CreateColorMatrix()`（无调用者，直接用 `SKColorFilter.CreateColorMatrix()`）

#### 切片 3C：OnPaint 升级 ✅

- 移植 `ValidateBackground()` + `bakedBackground` 背景预烘焙机制
- 升级 CBG 渲染：从 Point 定位升级为 Rectangle + SKColorFilter（支持 width/height/Opacity/ColorMatrix）
- 集成 `_imageLayerManager.DrawTo()` 到 OnPaint 渲染管线
- 添加 `ConsoleBackground` 类（Opacity + SKColorFilter 支持）
- 添加 `ClientBackGroundImage` 扩展字段（width/height/Opacity/ColorMatrix）
- 实现 `CBG_SetImage()`（从返回 false 改为创建 ClientBackGroundImage 并添加到 cbgList）
- Rikaichan 和 HTML_PRINT 延后（非核心功能）

#### 切片 3D：FontFactory 完善 ✅

- 完整移植 `FontFactory`（从 LazyLoading 294行，xamarin 24→242行）
  - 4 层字体回退链：自定义→安全字体→MatchCharacter→Default
  - `GetFont()` 完整参数：style/edging/hinting
  - `IsRasterFont()`：检测 MS Gothic 等光栅字体
  - 字体缓存：`fontDic` + `fallbackTypefaceCache`
  - `GetFallbackTypefaceForChar()`/`GetFallbackTypefaceForCodepoint()`：按字符获取回退字体
  - `TryGetGlyphFromFallback()`：从回退字体获取字形
  - `ClearFont()` + 正确的 Dispose 逻辑

#### 切片 3E：ConsoleDivPart 实现 ✅

- 完整移植 `ConsoleDivPart`（从 LazyLoading 261行）
  - 构造函数：存储所有盒模型字段（margin/padding/border/radius/borderColors/backgroundColor）
  - `DrawTo()`：完整盒模型渲染（定位计算→Canvas Save→margin裁剪→背景色→4方向边框→border/padding缩减→子元素绘制→Canvas Restore）
  - `TestChildHitbox()`：div 内子元素命中测试
  - `PointX` 属性：自定义 setter + ShiftChildrenX 联动
  - `ToString()`/`BuildString()`：div 标签序列化
  - `SetWidth()`：空实现（同 LazyLoading，div 宽度由 HTML 属性指定）
- 添加 `using System.Drawing;`/`using XEmuera;`/`using static Shape;`/`using static Utils;`
- 修复类型引用：`AConsoleDisplayNode` → `AConsoleDisplayPart`（匹配 StrArray 类型）

#### 切片 3F：GraphicsImage 缺失方法 ✅

- 重写 `GBitmapToInt64Array()`：从返回 false 改为 SkiaSharp 像素读取（`Bitmap.GetPixels()` + `Marshal.Copy`）
- 重写 `GByteArrayToBitmap()`：从返回 false 改为 SkiaSharp 像素写入（`Marshal.Copy` → `Bitmap.GetPixels()`）
- 修复 `GDrawLine()`：从临时 SKSurface 改为已有 `g` canvas 绘制，添加完整 DashStyle/DashCap 支持
  - DashCap：Flat→Butt, Round→Round, Triangle→Square
  - DashStyle：Solid/Dash/Dot/DashDot/DashDotDot → `SKPathEffect.CreateDash()`
- 修复 `GDrawPolygon()`：从临时 SKSurface 改为已有 `g` canvas 绘制
- 修复 `GFillPolygon()`：从临时 SKSurface 改为已有 `g` canvas 绘制，支持 brush 颜色
- 修复 `GDashStyle()`：从忽略参数改为正确存储 DashStyle/DashCap
- 添加 `DashStyle`/`DashCap` 枚举到 `XEmuera.Drawing` 命名空间
- 扩展 `Pen` 类：添加 `DashStyle`/`DashCap` 属性

#### 切片 3G：增量渲染 — ⏭️ 跳过

- LazyLoading 本身未实现脏区域/增量渲染，采用全量清除+全量重绘策略
- xamarin 已有的优化（背景缓存、离屏裁剪、帧率节流）与 LazyLoading 对齐

### 基础设施修复

- `project_rules.md`：注册 `xemuera` 辅助技能（Xamarin 移植工作路由）
- `graphics-rendering.md`：更新核心洞见（LazyLoading 内核已完全 Skia 化）
- `_index.md`：更新 graphics-rendering.md 描述

## [0.3.0] - 2026-05-16

### Phase 2：运行时适配（已完成部分）

#### 切片 2A：入口点适配 ✅

- 添加 `Encoding.RegisterProvider`（Shift-JIS 支持）
- 添加 `JSONConfig.Load()`（JSON 配置加载）
- 添加 `LoadFontFiles()`（font/ 目录字体加载 ttf/otf/ttc）
- 添加 `DebugConfig` 加载（debug 模式配置 + DebugDir 创建）
- Lang.LoadLanguageFiles/SetLanguage 未添加（Platform/Lang.cs 被排除）
- Bgm/Sound.close 未添加（stub 空实现）

#### 切片 2B：Pfc (PrivateFontCollection) 替换 ✅

- 创建 `SkiaPfc` + `SkiaFontFamily` 类：`Families` 返回 `System.Drawing.FontFamily[]`，`AddFontFile()` 加载到 `SKTypeface`
- 创建 `InstalledFontCollection` 命名空间覆盖（`System.Drawing.Text` 命名空间，实现 `IDisposable`）
- 修改 `GlobalStatic.Pfc` 类型声明：`dynamic` → `SkiaPfc`
- 添加 `FontFamily`/`InstalledFontCollection` 全局 using alias
- 实施方案：`SkiaPfc.Families` 返回 `System.Drawing.FontFamily[]`，内核代码中 `Pfc.Families` 的 `foreach` 和强转无需修改

#### 切片 2C：路径系统适配 ✅

- 创建 `IPathProvider` 接口（12 个路径属性 + ExeName）
- 实现 `DefaultPathProvider`（封装 `Sys.ExeDir`，含大小写 fallback 目录解析）
- `Program.*` getter 委托到 `GlobalStatic.PathProvider`（内核 122 处引用零改动）
- 实施方案：方案 A（Program getter 委托），内核 Runtime/ 代码无需任何修改

#### 切片 2E：AWAIT/INPUT 验证（桌面端）✅

- 创建桌面端宿主：WinForms Form + PictureBox + SKSurface 手动渲染
- 验证 AWAIT 在后台线程的行为：`Thread.Sleep` 不阻塞 UI ✅
- 程序已成功启动，资源文件正常加载，运行时无崩溃
- 当前空白窗口是预期的——Phase 3 Skia 渲染管线尚未集成

### 设计决策记录

- **执行顺序调整**：原计划 2A→2B→2C→2D→2E，依赖分析后调整为 **2C→2B→2A→2D→2E**（IPathProvider 是基础设施，应最先实现）
- **2D 键鼠抽象延后**：当前 TFM 是 `net8.0-windows` + `UseWindowsForms`，`WinInput.GetKeyState`（user32.dll）和 `Control.MousePosition`（WinForms）在 Windows 桌面端均可正常工作，延后至 TFM 切换到跨平台时实施
- **2D 方案决策**：采用 InputState 具体类方案（方案 C），不采用 IInputProvider 接口方案（方案 A）。理由：不排除 Creator.Method.cs（8000+ 行），避免维护两份副本

### Phase 4A-Desktop：主页面集成 Stub 扩展 ✅

- 审计 xamarin EmueraConsole 对 `window.*` 的引用：活跃 13 个，已注释 ~25 个
- 扩展 `MainPicBoxStub`：添加 `Width`/`Height`/`PointToClient(Point)`/`ClientRectangle`，DesktopMainWindow 连接 WinForms PictureBox 实际尺寸
- 扩展 `TextBoxStub`：添加 `ForeColor`/`Font` 属性，DesktopMainWindow 连接 WinForms TextBox
- 创建 `ToolTipStub` 空实现：Show/RemoveAll/InitialDelay/AutoPopDelay/ForeColor/BackColor/OwnerDraw/Draw/Popup
- 实现 `window.Invoke(Action)` 同步版本：`ManualResetEventSlim` + `form.BeginInvoke` 等待完成
- 实现 `window.Created` 替代：别名 `IsEnabled`，DesktopMainWindow override `!form.IsDisposed`
- 补充缺失接口：`Text`（别名 Title）、`ShowConfigDialog()`（空）、`Focus()`、`TextBoxPosChanged`/`ResetTextBoxPos()`/`TextBoxIgnoreScrollBarChanges`
- 新增 `DesktopMainPicBox`/`DesktopTextBox`/`DesktopToolTip` 适配类
- 取消注释 EmueraConsole 中 30+ 处 `window.*` 调用：
  - `Enabled`/`IsActive` 恢复为 `window.Created` 驱动
  - `SetWindowTitle`/`ShowConfigDialog`/`Focus` 恢复
  - ToolTip 全套恢复（Show/RemoveAll/ForeColor/BackColor/InitialDelay）
  - `GetMousePosition` 恢复（`Control.MousePosition` → `System.Windows.Forms.Control.MousePosition`）
  - 5 处鼠标位置检测恢复（PointToClient + ClientRectangle.Contains）
  - `ScrollBar.Enabled` 恢复
  - `TextBox.BackColor`/`Text` 恢复
- 编译验证：0 错误，60 警告（均为既有）

### Phase 4A 探勘

- 接口缺口已审计：EmueraConsole 对 MainWindow 的 91 处引用分类完成
- 4A-Desktop 垂直切片已规划：9 个操作步骤（4A-D1~D9）
- Stub 缺失接口清单已建立：12 个缺失接口按严重程度分级

## [0.2.0] - 2026-05-15

### Phase 1：编译错误收敛（818→336→0）

#### Round 1：类型冲突 + 存根（818→336）

#### Round 2：剩余错误收敛（336→0）

| 修复类别 | 操作 | 消除错误 |
|---------|------|---------|
| 全局 using alias | `FontStyle`/`Brush`/`Pen`/`Font`/`SolidBrush`/`GraphicsUnit` → `XEmuera.Drawing` | ~140 |
| XAML 页面排除 | 10 个 XAML 页面 + App.xaml.cs + MainPage.xaml.cs | ~80 |
| ConfigCode 枚举扩展 | 内核 ConfigCode 添加 14 个 XEmuera 成员 | ~116 |
| Nullable disable | csproj `<Nullable>disable</Nullable>` + NoWarn | ~22 |
| 存根补全 | Sound/Bgm/MainWindow/EmueraConsole/GraphicsImage/AppContents/FontFactory/Sys/App/ColorMatrixHelper | ~100 |
| 内核修复 | ScriptPosition?.Value、ConfigData global::、SkiaSharp 枚举类型、dynamic lambda | ~33 |
| 隐式转换 | XEmuera.Drawing ↔ System.Drawing (Pen/Brush/SolidBrush/Font/SKBitmap→GraphicsImage) | ~20 |

### Changed

- **TFM 升级到 net8.0-windows**（从 netstandard2.1）
  - LazyLoading 内核大量使用 .NET 7+/8+ API（GeneratedRegex/SearchValues/Span.Sort/string.Create），netstandard2.1 不可行
  - net8.0 不够（内核引用 WinForms OpenTK.GLControl），必须 -windows 后缀
  - 不再需要排除 IdentifierDictionary/EraStreamReader/ParserMediator/Lang/ExpressionParser/LexicalAnalyzer/Config/JSON
  - 不再需要 IsExternalInit NuGet 包

### Added

- Stubs 层（缺失类型的空壳实现）
  - AConsoleDisplayNode.cs — LazyLoading UI 基类 stub
  - EvilMaskUtils.cs — MixedNum/StyledBoxModel/ConsoleDivPart/DisplayMode stub
  - IPluginMethod.cs — PluginSystem stub（PluginManager/PluginMethodParameterBuilder）
  - ImageLayerManager.cs — SETIMAGELAYER stub
  - MainWindow.cs — WinForms MainWindow stub
  - WinForms.cs — Application.DoEvents/Dialog stub
  - SKMouseButton.cs — SkiaSharp.Views.Forms 缺失枚举
  - UIImageStubs.cs — AContentItem/IFileBacked stub
- Resources/Resources.Designer.cs — XEmuera.Resources 本地化资源 Designer
- using 别名消除 System.Drawing 歧义（17 个 Platform 层文件）
  - `using Font = XEmuera.Drawing.Font;`
  - `using FontStyle = XEmuera.Drawing.FontStyle;`
  - `using Brush = XEmuera.Drawing.Brush;`
  - `using Pen = XEmuera.Drawing.Pen;`
- SkiaSharp.Views.WPF NuGet 包

## [0.1.0] - 2026-05-15

### Phase 0：项目骨架

- 新建 `Emuera.Xamarin` 项目（netstandard2.1 + LangVersion latest）
- 从 XEmuera-1 复制 Skia 平台层文件到 `Platform/` 目录
- csproj 配置：引用 LazyLoading `Runtime/` 目录
- 排除不兼容内核文件（Sound/PluginSystem/Shape/Lang/Utils/Clipboard）
- 批量替换平台层命名空间（26 个文件）
- 首次编译：129 个错误
- 添加到解决方案
