# Handoff: 音频系统研判完成，准备 5B-D + 8C

## 会话目标

抬高审视计划表，研判音频系统，重新整理 TODO 优先级。

## 已完成

- [x] Slice 8A-P0：System.Drawing 值类型垫片（Color/Rectangle/Point/Size）— commit `fbdead4`
- [x] Slice 8A-P1：Runtime/ WinForms 依赖清理（7 个文件）— commit `2586c84`
- [x] Slice 8A：创建 Core 项目（net8.0 + net8.0-windows 双目标）— commit `a8f3d2e`
- [x] Slice 8B：创建 Desktop 项目（net8.0-windows）— commit `15ecfb3`
- [x] 剩余 WinForms 依赖清理（App/Dialog/EmueraConsole/Program）— commit `c5b1a7f`
- [x] 音频系统全面研判（LazyLoading/XEmuera-1/Xamarin 三方对比）
- [x] TODO.md 5B 切片重写（IPlayer 方案 → SoundStub 虚方法方案）
- [x] 优先级调整：音频 P0、Rikaichan P3

## 未完成

- [ ] 切片 5B-D：Desktop 音频验证（NAudio 接入 + SoundStub 架构验证）
- [ ] 切片 8C：创建 Android 项目（net8.0-android）
- [ ] 切片 5B-A：Android 音频实现（AndroidSound : Sound）
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
10. **音频架构决策：SoundStub 虚方法方案**（新增）：不采用 XEmuera-1 的 IPlayer 接口。理由：SoundStub 已有 13 个虚方法覆盖全部 ERB 指令，与 Runtime/ 零改动兼容，子类化模式与现有 Stub 体系一致。
11. **NAudio Sound 重命名为 NAudioSound**（新增）：NAudio `Sound` 类与 Core DLL 中的 SoundStub 同名同类名（`MinorShift.Emuera.Runtime.Utils.Sound`），Desktop 不能重复定义。解决方案：将 NAudio `Sound` 改为 `NAudioSound : Sound`（继承 SoundStub），override 虚方法。
12. **Rikaichan 降为 P3 最低优先级**（新增）：Core 项目已排除 Rikaichan.cs（引用 System.Windows.Forms），Android 端不需要词典悬浮提示。

## 音频系统研判总结

### 现状

- **Emuera.Xamarin 音频 = 完全空壳**：SoundStub（13 个虚方法全空）、SystemSoundsStub（Play 空操作）、SoundAndBgm（GameProc 适配器空壳）
- **Desktop 也无音频**：DesktopHost 无音频初始化，Desktop csproj 不含 NAudio
- **XEmuera-1 音频仅 Android 部分可用**：MobileMediaPlayer 基础播放/停止/循环/音量，缺少 pause/resume/setSpeed/Seek

### 架构对比

| 维度 | LazyLoading (NAudio) | XEmuera-1 (IPlayer) | Xamarin (SoundStub) |
|------|---------------------|---------------------|---------------------|
| 播放器类 | `Sound : ISampleProvider` | `IPlayer` 接口 | `Sound` 虚方法基类 |
| 混音 | SoundMixer (MixingSampleProvider) | MediaPlayer 多实例 | 待实现 |
| 通道数 | 16 | 10 | 16（声明但空壳） |
| 变速 | VarispeedSampleProvider (SoundTouch) | 不支持 | 待实现 |
| 暂停/恢复 | 支持 | 不支持 | Stub 有接口 |
| ERB 指令覆盖 | 12/12 | 6/12 | 0/12 |

### NAudio 移植性

| 组件 | 跨平台？ | 处置 |
|------|---------|------|
| SoundMixer + WasapiOut | ❌ Windows-only | Desktop 保留 |
| Sound (ISampleProvider) | ❌ 依赖 NAudio | 改为 NAudioSound : Sound |
| LoopStream / RepeatStream | ❌ 依赖 NAudio | Desktop 保留 |
| VarispeedSampleProvider | ❌ x86/x64 DLL | Desktop 保留 |
| AudioDeviceTracker | ❌ WASAPI | Desktop 保留 |

### 关键技术问题

1. **同名类冲突**：NAudio `Sound` 与 SoundStub 同名同类名 → 重命名为 `NAudioSound : Sound`
2. **SoundMixer 引用更新**：`PlaySound(Sound)` → `PlaySound(NAudioSound)`，`SoundEnded` 事件中类型转换
3. **SoundStub 缺 Playing 字段**：NAudio SoundMixer 依赖 `Sound.Playing` → SoundStub 需添加
4. **Android 变速不可用**：MediaPlayer 无原生变速 API → `setSpeed()` 返回 0

## 阻塞项

- 无代码阻塞。5B-D 和 8C 可立即开始。

## 下一步建议

1. **切片 5B-D**（优先）：Desktop 音频验证 — NAudio 接入 + SoundStub 架构验证
2. **切片 8C**（并行）：创建 Android 项目骨架
3. **切片 5B-A**（8C 后）：Android 音频实现
4. **切片 8D**（5B-A 后）：Android 功能补全

## 推荐技能

- `powershell-git`：终端命令
- `knowledge-builder`：5B-D 完成后更新知识库
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
| `Emuera.Xamarin.Core/Stubs/SoundStub.cs` | Sound 虚方法基类，5B-D 需添加 Playing 字段 |
| `Emuera/Runtime/Utils/Sound.NAudio.cs` | NAudio 完整实现，5B-D 需重命名为 NAudioSound |
| `Emuera/Runtime/Utils/NAudio_LoopStream.cs` | NAudio 循环流，5B-D 需包含到 Desktop |
| `Emuera/Runtime/Utils/SoundTouch_byMarkHeath/` | SoundTouch 变速库，5B-D 需包含到 Desktop |
| `Emuera.Xamarin/DesktopHost.cs` | Desktop 委托初始化，5B-D 需添加音频初始化 |
| `Emuera.Xamarin.Core/Emuera.Xamarin.Core.csproj` | Core 项目配置，8C 需要参考排除项 |
| `Emuera.Xamarin.Desktop/Emuera.Xamarin.Desktop.csproj` | Desktop 项目配置，5B-D 需添加 NAudio |
| `Platform/Stubs/PlatformInterop.cs` | 平台抽象，Android 需要重新初始化委托 |
| XEmuera-1 `XEmuera.Android/` | Android 层参考，8C 直接复制 MainActivity 等 |
| XEmuera-1 `DroidDependencyService.cs` | Android MediaPlayer 参考，5B-A 参考 |
