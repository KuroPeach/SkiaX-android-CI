# Handoff: Slice 8A/8B 完成，推进 8C Android 项目

## 会话目标

继续推进 Phase 8 移植任务，完成 Core/Desktop 项目拆分，推进 Android 项目。

## 已完成

- [x] Slice 8A-P0：System.Drawing 值类型垫片（Color/Rectangle/Point/Size）— commit `fbdead4`
- [x] Slice 8A-P1：Runtime/ WinForms 依赖清理（7 个文件）— commit `2586c84`
- [x] Slice 8A：创建 Core 项目（net8.0 + net8.0-windows 双目标）— commit `a8f3d2e`
- [x] Slice 8B：创建 Desktop 项目（net8.0-windows）— commit `15ecfb3`
- [x] 剩余 WinForms 依赖清理（App/Dialog/EmueraConsole/Program）— commit `c5b1a7f`
- [x] CHANGELOG.md 更新（v0.14.0 + v0.15.0 + v0.16.0）
- [x] HANDOFF.md / TODO.md 文档更新

## 未完成

- [ ] 切片 8C：创建 Android 项目（net8.0-android）
- [ ] 切片 8D：Android 功能补全

## 关键决策

1. **项目定位修正**：LazyLoading 原版桌面端已完备，Emuera.Xamarin 的唯一目标是 APK。桌面端仅用于功能验证。
2. **方案 B（项目拆分）**：Core(net8.0) + Desktop(net8.0-windows) + Android(net8.0-android)。不能用 netstandard2.1 因为 Runtime/ 依赖 .NET 8 API。
3. **XEmuera-1 遗产不能直接复制**：旧 Xamarin 工具链（MonoAndroid）不能引用 net8.0 库，必须用 .NET 8 Android 工作负载重写 Android 层。
4. **System.Drawing 值类型自建垫片**：Color/Rectangle/Point/Size 在 net8.0（非 -windows）下不可用。方案：XEmuera.Drawing 下创建兼容值类型 + csproj Using Alias 全局替换 + `#if WINDOWS` 条件编译互操作。
5. **PlatformInterop 委托式平台抽象**：替代 Runtime/ 中 7 个文件的 WinForms 直接调用。DesktopHost 初始化所有委托。Android 层只需重新初始化委托即可。
6. **`#if WINDOWS` 是正确条件编译符号**：`#if NET8_0_WINDOWS` 不存在，net8.0-windows TFM 的正确符号是 `WINDOWS`。
7. **Core 双目标 net8.0;net8.0-windows**：使 `#if WINDOWS` 互操作代码在 Core 的 windows 目标上也能编译。Desktop 引用 Core 时自动获得 windows 目标。
8. **Using Alias 传递问题**：Core 的 Using Alias 会通过 DLL 元数据传递到引用项目，导致 Desktop 上 System.Drawing.Color 与 XEmuera.Drawing.Color 冲突。解决方案：移除 Desktop 文件中的 `using System.Drawing;`，让 Alias 无歧义解析到 XEmuera.Drawing 类型。
9. **`#if DESKTOP_APP` 分离入口点**：Program.MainDesktop 只在 Desktop 项目和原 Xamarin 项目中编译（两者都定义了 `DESKTOP_APP`），不在 Core 中编译。

## 8A/8B 实施总结

### 8A：Core 项目（net8.0 + net8.0-windows 双目标）

- `Emuera.Xamarin.Core.csproj`：双目标框架，Using Alias 12 个类型别名 + 12 个命名空间转发
- Runtime/ 编译：`<Compile Include="../Emuera/Runtime/**/*.cs" />` + 排除 Windows-only 文件
- Platform/ + Drawing/ + Models/ + Forms/ 编译引用
- Stubs：RikaichanStub（空实现）、SoundStub（no-op）、SystemSoundsStub（no-op）
- NuGet：SkiaSharp + SkiaSharp.Svg + Microsoft.Data.Sqlite + Xamarin.Forms（ExcludeAssets=build）
- InternalsVisibleTo：`XEmuera.Desktop`
- `#if WINDOWS` 守卫：DrawBitmapUtils/DrawTextUtils 的 System.Drawing 互操作
- Creator.Method：`#if WINDOWS` 守护 SKBitmap.ToBitmap()，修复 SKColor→Color 隐式转换

### 8B：Desktop 项目（net8.0-windows）

- `Emuera.Xamarin.Desktop.csproj`：引用 Core 项目 + WinForms + SkiaSharp.Views.WindowsForms
- `Program.cs`：入口点调用 `Program.MainDesktop(args)`
- Desktop-only 文件：DesktopHost + DesktopConfigDialog + DesktopDebugDialog + VersionUpdate + SoundAndBgm
- DESKTOP_APP 定义：启用 Main 入口点编译
- 移除 `using System.Drawing;`：避免 Using Alias 冲突

### 额外 WinForms 清理

- App.cs：`Application.DoEvents()` → `PlatformInterop.DoEvents()`
- Dialog.cs：`MessageBox.Show()` → `PlatformInterop.ShowMessage()`/`ShowMessageWithTitle()`/`ShowQuestion()`
- EmueraConsole.cs：`Control.MousePosition` → `PlatformInterop.GetMousePosition()`
- Program.cs：`MessageBox.Show()` → `PlatformInterop.ShowMessageWithTitle()`，Main→MainDesktop + `#if DESKTOP_APP`
- PlatformInterop：添加 `ShowMessageWithTitle` 委托

## 阻塞项

- 无代码阻塞。下一步 8C 可立即开始。

## 下一步建议

1. **切片 8C**：创建 Android 项目（net8.0-android），参考 XEmuera-1 的 MainActivity + DroidDependencyService
2. **切片 8D**：Android 功能补全（触摸事件、文本输入、游戏列表、APK 打包）

## 推荐技能

- `powershell-git`：终端命令
- `knowledge-builder`：8C 完成后更新知识库
- `erabasic`：如需确认 Runtime/ 中 ERB 相关代码

## 关键文件

### 项目结构

```
Emuera.Xamarin.sln（原有，仍可独立编译）
├── Emuera.Xamarin/          (net8.0-windows, WinExe)
├── Emuera.Xamarin.Core/     (net8.0;net8.0-windows, Library)
└── Emuera.Xamarin.Desktop/  (net8.0-windows, WinExe)
```

### 关键参考文件（下次会话需要）

| 文件 | 用途 |
|------|------|
| `Emuera.Xamarin.Core/Emuera.Xamarin.Core.csproj` | Core 项目配置，8C 需要参考排除项 |
| `Emuera.Xamarin.Desktop/Emuera.Xamarin.Desktop.csproj` | Desktop 项目配置，8C 参考结构 |
| `Platform/Stubs/PlatformInterop.cs` | 平台抽象，Android 需要重新初始化委托 |
| `DesktopHost.cs` | Desktop 委托初始化，Android 需要对应实现 |
| XEmuera-1 `XEmuera.Android/` | Android 层参考，8C 直接复制 MainActivity 等 |
