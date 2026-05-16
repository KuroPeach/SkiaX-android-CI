# Handoff: Slice 8A-P0/P1 完成，推进 8A Core 项目

## 会话目标

继续推进 Phase 8 移植任务，完成 8A-P0/P1 前置条件，推进 8A Core 项目创建。

## 已完成

- [x] Slice 8A-P0：System.Drawing 值类型垫片（Color/Rectangle/Point/Size）— commit `fbdead4`
- [x] Slice 8A-P1：Runtime/ WinForms 依赖清理（7 个文件）— commit `2586c84`
- [x] CHANGELOG.md 更新（v0.14.0 + v0.15.0）
- [x] HANDOFF.md / TODO.md 文档更新

## 未完成

- [ ] 切片 8A：创建 Core 项目（net8.0）
- [ ] 切片 8B：创建 Desktop 项目（net8.0-windows）
- [ ] 切片 8C：创建 Android 项目（net8.0-android）
- [ ] 切片 8D：Android 功能补全

## 关键决策

1. **项目定位修正**：LazyLoading 原版桌面端已完备，Emuera.Xamarin 的唯一目标是 APK。桌面端仅用于功能验证。
2. **方案 B（项目拆分）**：Core(net8.0) + Desktop(net8.0-windows) + Android(net8.0-android)。不能用 netstandard2.1 因为 Runtime/ 依赖 .NET 8 API。
3. **XEmuera-1 遗产不能直接复制**：旧 Xamarin 工具链（MonoAndroid）不能引用 net8.0 库，必须用 .NET 8 Android 工作负载重写 Android 层。
4. **System.Drawing 值类型自建垫片**：Color/Rectangle/Point/Size 在 net8.0（非 -windows）下不可用。方案：XEmuera.Drawing 下创建兼容值类型 + csproj Using Alias 全局替换 + `#if WINDOWS` 条件编译互操作。
5. **PlatformInterop 委托式平台抽象**：替代 Runtime/ 中 7 个文件的 WinForms 直接调用。DesktopHost 初始化所有委托。Android 层只需重新初始化委托即可。
6. **`#if WINDOWS` 是正确条件编译符号**：`#if NET8_0_WINDOWS` 不存在，net8.0-windows TFM 的正确符号是 `WINDOWS`。

## 8A-P0/P1 实施总结

### 8A-P0：值类型垫片（16 文件，+523/-42 行）

- `XEmuera.Drawing.Color`：ARGB uint 存储，60+ 命名颜色，SKColor 隐式转换，`#if WINDOWS` System.Drawing.Color 互操作
- `XEmuera.Drawing.Rectangle`：Contains/IntersectsWith/Intersect/Union/Inflate/Offset，`#if WINDOWS` 互操作
- `XEmuera.Drawing.Point`：Offset/IsEmpty/+Size 运算符，`#if WINDOWS` 互操作
- `XEmuera.Drawing.Size`：IsEmpty/+/- 运算符，`#if WINDOWS` 互操作
- csproj Using Alias：Color/Rectangle/Point/Size 全局别名替换
- SkiaPfc/InstalledFontCollection：移除 System.Drawing.FontFamily 依赖
- ColorExtensions：添加 ToFormsColor()
- DrawTextUtils：添加 Font 隐式转换 from System.Drawing.Font

### 8A-P1：Runtime/ WinForms 依赖清理（9 文件，+68/-31 行）

- PlatformInterop 静态类：DoEvents/ShowMessage/ShowQuestion/MeasureText/MeasureTextWidth/GetMousePosition/SetClipboardText/GetProductVersion
- DesktopHost：初始化所有 PlatformInterop 委托（WinForms 实现 + SKPaint.MeasureText）
- 7 个 Runtime/ 文件替换：ErbLoader/ErhLoader/Config/Instraction.Child/VariableEvaluator/Creator.Method/Sys
- **Runtime/ 编译文件不再有任何直接 WinForms 依赖**

## 阻塞项

- 无代码阻塞。下一步 8A 可立即开始。

## 下一步建议

1. **切片 8A**：创建 Core 项目（net8.0），从现有 csproj 提取共享代码
2. **切片 8B**：创建 Desktop 项目（net8.0-windows），WinForms 验证
3. **切片 8C**：创建 Android 项目（net8.0-android），APK 产出

## 推荐技能

- `powershell-git`：终端命令
- `knowledge-builder`：8A 完成后更新知识库
- `erabasic`：如需确认 Runtime/ 中 ERB 相关代码

## 关键文件

### 本次修改的文件

| 文件 | 变更 |
|------|------|
| `Drawing/Color.cs` | 新建：ARGB uint 值类型 + 60+ 命名颜色 + SKColor 互操作 |
| `Drawing/Rectangle.cs` | 新建：值类型 + `#if WINDOWS` 互操作 |
| `Drawing/Point.cs` | 新建：值类型 + `#if WINDOWS` 互操作 |
| `Drawing/Size.cs` | 新建：值类型 + `#if WINDOWS` 互操作 |
| `Platform/Stubs/PlatformInterop.cs` | 新建：委托式平台抽象 |
| `Emuera.Xamarin.csproj` | 添加 Using Alias + PlatformInterop 编译项 |
| `DesktopHost.cs` | 初始化所有 PlatformInterop 委托 |
| `Drawing/ColorExtensions.cs` | 添加 ToFormsColor() |
| `Drawing/DrawTextUtils.cs` | 添加 Font 隐式转换 |
| `Utils.cs` | alias 类型替换 |
| `Platform/Stubs/SkiaPfc.cs` | 移除 System.Drawing.FontFamily |
| `Platform/Stubs/InstalledFontCollection.cs` | 同上 |
| `Platform/Stubs/MainWindow.cs` | alias 类型替换 |
| `Platform/GameView/EmueraConsole.ex.cs` | alias 类型替换 + ToFormsColor() |
| `Models/ConfigModel.cs` | `is Color` 替换 |
| 7 个 Runtime/ 文件 | WinForms → PlatformInterop 替换 |

### 关键参考文件（下次会话需要）

| 文件 | 用途 |
|------|------|
| `Emuera.Xamarin.csproj` | 当前 csproj，8A 需要拆分 |
| `Platform/Stubs/PlatformInterop.cs` | 平台抽象，Core 项目包含 |
| `Drawing/Color.cs` 等 4 个垫片 | Core 项目包含 |
| `DesktopHost.cs` | Desktop 项目包含，8B 需要迁移 |
