# Emuera.Xamarin 变更记录

## [0.2.0] - 2026-05-15

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

### Known Issues

- 818 个编译错误，主要分类：
  - **A 类**：类型重复定义（ConfigCode/MixedNum/DisplayWarningFlag）
  - **B 类**：System.Drawing 歧义（GraphicsUnit/SolidBrush 未添加别名）
  - **C 类**：EmueraConsole API 不完整（11 个缺失方法/属性）
  - **D 类**：缺失类型（DisplayLineAlignment/GlobalStatic.ctrlZ/InputType）
  - **E 类**：Nullable 不匹配（ScriptPosition? → ScriptPosition）

## [0.1.0] - 2026-05-15

### Added

- 新建 `Emuera.Xamarin` 项目（netstandard2.1 + LangVersion latest）
- 从 XEmuera-1 复制 Skia 平台层文件到 `Platform/` 目录
- csproj 配置：引用 LazyLoading `Runtime/` 目录
- 排除不兼容内核文件（Sound/PluginSystem/Shape/Lang/Utils/Clipboard）
- 批量替换平台层命名空间（26 个文件）
- 首次编译：129 个错误
