# Emuera.Xamarin 变更记录

## [0.4.0] - 2026-05-16

### Phase 3A：ImageLayerManager 图层管理 ✅

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

### Phase 3B：ColorMatrix 颜色矩阵 ✅

- 完整移植 `ColorMatrixHelper`（从 LazyLoading 84行移植）
  - 修正命名空间：`MinorShift.Emuera.Runtime.Utils.EvilMask` → `MinorShift.Emuera.UI.Game.Image`（与调用者 using 一致）
  - `ReadFromVariableTerm()`：从 ERB 变量表达式读取 5x5 矩阵
  - `ReadFromVariable()`：支持 2D/3D 数组 + 角色变量，值范围 0-256（÷256f 转 0.0-1.0）
  - `ToSkia()`：5x5 float[][] → 20 元素 float[]，offset 列 × 255
  - 移除 `CreateColorMatrix()`（无调用者，直接用 `SKColorFilter.CreateColorMatrix()`）

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
