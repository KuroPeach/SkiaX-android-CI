# Emuera.Xamarin 变更记录


## [0.55.0] - 2026-05-23

### Phase 2-3：扩展音频 API + 编译修复

> 完成 PLAYSOUND repeat 参数升级、6 个扩展音频函数移植、变速支持，修复编译错误和运行时 bug。

#### 新增功能

| # | 功能 | 说明 |
|---|------|------|
| 1 | PLAYSOUND repeat 参数 | ArgumentBuilder 升级为 SP_HTML_PRINT，支持 `PLAYSOUND "file", 3` 重复播放 |
| 2 | GETSOUNDORBGMINFO | 获取音频信息：总长度/当前时间/播放状态/音量/速度 |
| 3 | ISPLAYINGSOUND / ISPLAYINGBGM | 检查指定通道/BGM 播放状态 |
| 4 | SOUNDCONTROL / BGMCONTROL | 控制：暂停(0)/恢复(1)/停止(2)/变速(3) |
| 5 | 变速支持 | NAudioSound: VarispeedSampleProvider; AndroidSound: PlaybackParams (Android 6+) |

#### 编译错误修复

| # | 问题 | 修复 |
|---|------|------|
| 1 | CS0117: Config.Config 未定义 | Config 是静态类，Config.Config 被解析为嵌套类型。改为 Config（6 处） |
| 2 | CS0023: !Exception 无效 | AppContents.LoadContents 返回 Exception 非 bool，改为 != null |
| 3 | CS0115: NAudioSound.play(string,long) 无基类重载 | 删除无效 override |

#### Bug 修复

| # | 问题 | 修复 |
|---|------|------|
| 4 | IsPlayingSoundMethod 死代码+无限循环 | arguments[0]==null 永假；for 条件用 channelId 非 i 导致死循环 |
| 5 | AndroidSound.ApplySpeed OnPrepared 不生效 | 移除 !_player.IsPlaying 前置条件 |

#### 潜在风险

- Android PlaybackParams 设备兼容性（部分 ROM 不支持变速）
- Droid 版 AndroidSound 无变速实现（空 stub）
- NAudioSound pause/resume 缓冲区状态一致性


## [0.54.0] - 2026-05-23

### 内核回流合并（develop-skiasharp v4.1.0）

> 合并 develop-skiasharp 的跨平台音频架构重构和回流修复。

| # | 变更 | 说明 |
|---|------|------|
| 1 | Sound 基类 + Factory 工厂模式 | 内核代码通过 Factory 创建实例，消除平台直接依赖 |
| 2 | NAudioSound : Sound, ISampleProvider | 原 Sound 类重命名为 NAudioSound，所有方法加 override |
| 3 | SoundMixer 参数类型修正 | PlaySound/StopSound 参数改为 NAudioSound |
| 4 | PlatformInterop 委托模式 | 替换内核 WinForms 直接调用（DoEvents/ShowMessage 等） |
| 5 | Program.MusicDir + FileExists | 替换硬编码音频路径 |
| 6 | Creator.Method/Instraction.Child | `new Sound()` → `Sound.Factory()` |
| 7 | B 类回流修复 | 跨平台适配（#if WINDOWS 条件编译等） |
| 8 | EraStreamReader try-catch 修复 | 缺少 catch 块导致 CS1524 |

### 合并后修复

| # | 问题 | 修复 |
|---|------|------|
| 1 | CS0101: PlatformInterop 重复定义 | Xamarin csproj 排除内核版 PlatformInterop.cs |
| 2 | CS0111: Config.WildcardToRegex/GetFilesCaseInsensitive 重复 | 删除合并残留的重复方法 |
| 3 | CS0176/CS0172: Creator.Method.cs Color 类型歧义 | `GGetColor` 直接返回 SKColor，移除 `(Color)` 强转；三元表达式显式声明 `SKColor` |
| 4 | CS0108: SolidBrush.Color 隐藏继承成员 | 添加 `new` 关键字 |
| 5 | CS8632: Argument.cs nullable 注释 | 添加 `#nullable enable` |
| 6 | XA1029/XA0036: Android csproj 弃用属性 | `AotAssemblies` → 移除；`AndroidSupportedAbis` → `RuntimeIdentifiers` |

### 冲突解决

| 文件 | 策略 |
|------|------|
| Config.cs | 移除 `using System.Windows.Forms`（跨平台不引用） |
| ExpressionMediator.cs | 移除 `#if !WINDOWS` 条件编译（Xamarin 不需要） |
| Creator.Method.cs | 统一 `Sound.Factory()` 命名，保留 `(Color)` 转换 |
| Instraction.Child.cs | 统一 Factory 命名，采用 develop-skiasharp 的 ShowQuestion 签名 |
| EraStreamReader.cs | 保留 Xamarin 的详细错误日志 |
| Sound.NAudio.cs | 保留 XML 文档注释 |
| Sys.cs | 移除 `using System.Windows.Forms` |
| WinmmTimer.cs | 保留 XML 文档注释 |
| ImgUtils.cs | 保留 `LoadImageSK` 跨平台方法 |

## [0.53.1] - 2026-05-23

### Gemini 审查修复 + 内核解耦

| # | 问题 | 根因 | 修复 |
|---|------|------|------|
| 1 | GlobalStatic.cs 引用 NAudioSound 导致 Android 编译失败 | `Bgm = new NAudioSound()` 和 `Reset()` 中 `Factory = () => new NAudioSound()` 直接引用平台子类 | 改为 `new Sound()` 基类 + WinForms `Program.Main()` 设置 Factory |
| 2 | AndroidSound repeat>1 只播放一次 | `OnCompletion` 中 `_remainingRepeats--` 但没有 SeekTo+Start | 补 `_player.SeekTo(0); _player.Start();` |
| 3 | AndroidSound play() 同步 Prepare() 阻塞 UI | `MediaPlayer.Prepare()` 是同步阻塞调用 | 改为 `PrepareAsync()` + `Prepared` 事件回调 |
| 4 | Platform 层 GraphicsImage 缺 PenColorArgb/PenWidth/BrushColorArgb | GGETPEN/GGETBRUSH null-safe 属性未在 Platform 版实现 | 添加三个 long 属性，用 `DrawingColor.ToArgb()` |
| 5 | 切换游戏时音量不重置 | `GlobalStatic.Reset()` stop 后 Sound/Bgm 置 null 未重建 | stop+close 后用 Factory 重建实例 |
| 6 | Volume 0-100 未归一化 | XEmuera-1 遗留：SetVolume 期望 0.0-1.0 | AndroidSound.setVolume 中 `/100.0f` 归一化 |
| 7 | AndroidSound.play 签名不匹配基类 | `play(string, long)` 与基类 `play(string, int)` 不一致 | 改为 `int` 参数 |
| 8 | AndroidSound.cs 重复 play 方法 | 两个 `play(string, int)` 导致 CS0111 | 删除重复方法 |

### 待回流 develop-skiasharp

| # | 问题 | 回流方案 |
|---|------|---------|
| 5 | PlatformInterop 不存在（29 处内核引用） | 在 develop-skiasharp 创建 PlatformInterop.cs（委托模式） |
| 6 | Program.MusicDir/FileExists 不存在（6 处引用） | 取消注释 MusicDir，添加 FileExists |
| 7 | SKColor→System.Drawing.Color 转换错误 | 用 ToArgb() 计算 long 值 |

### 回流策略

> 以上 #5-#7 应在 develop-skiasharp 分支完成，Xamarin 分支合并即可。
> 详见 [kernel-audit.md §回流前提](file:///d:/emuera/shared-trae/knowledge/lazyloading/kernel-audit.md)

## [0.53.0] - 2026-05-23

### 音频系统跨平台移植（Phase 1）

> 采用 Sound 虚方法继承方案 + Factory 工厂模式，Desktop 用 NAudioSound : Sound，Android 用 AndroidSound : Sound。

#### 架构变更

| 变更 | 说明 |
|------|------|
| `Sound` 基类 | 13 个虚方法（play/stop/pause/resume/close/isPlaying/setVolume/getVolume/setSpeed/getSpeed/SetPreservePitch/GetTotalTime/GetCurrentTime） |
| `Sound.Factory` | `Func<Sound>` 静态属性，由宿主程序设置平台工厂 |
| `NAudioSound : Sound` | Desktop 端，NAudio 混音器 + SoundTouch 变速 |
| `AndroidSound : Sound` | Android 端，MediaPlayer + PlaybackParams 变速（Android 6+） |
| csproj 条件编译 | `Sound.NAudio.cs` 仅 net8.0-windows；`AndroidSound.cs` 仅 net8.0-android |
| DesktopHost 初始化 | `Sound.Factory = () => new NAudioSound()` |
| AndroidHost 初始化 | `Sound.Factory = () => new AndroidSound()` |

#### 已实现指令

| 指令 | 类型 | Android 状态 |
|------|------|-------------|
| PLAYSOUND | 命令 | ✅ |
| PLAYBGM | 命令 | ✅ |
| STOPSOUND | 命令 | ✅ |
| STOPBGM | 命令 | ✅ |
| SETSOUNDVOLUME | 命令 | ✅ |
| SETBGMVOLUME | 命令 | ✅ |
| EXISTSOUND | 函数 | ✅ |

#### 待实现指令（Phase 3）

| 指令 | 类型 | 说明 |
|------|------|------|
| GETSOUNDORBGMINFO | 函数 | channelId + infoType，返回音频信息 |
| ISPLAYINGSOUND | 函数 | 检查 Sound 通道播放状态 |
| SOUNDCONTROL | 函数 | 暂停/恢复/停止/变速控制 |
| ISPLAYINGBGM | 函数 | 检查 BGM 播放状态 |
| BGMCONTROL | 函数 | BGM 暂停/恢复/停止/变速控制 |

#### 修改文件

| 文件 | 修改 |
|------|------|
| `Runtime/Utils/Sound.cs` | 添加 `Factory` 静态属性 |
| `Runtime/Utils/Sound.NAudio.cs` | `Sound` → `NAudioSound : Sound` |
| `emuera.xamarin/Platform/Audio/AndroidSound.cs` | 新建，MediaPlayer 实现 |
| `Emuera.Xamarin.csproj` | 条件编译排除/包含 |
| `Emuera.Xamarin.Core.csproj` | 条件编译排除/包含 |
| `emuera.xamarin/DesktopHost.cs` | Factory 初始化 |
| `Emuera/GlobalStatic.cs` | 去除 NAudioSound 直接依赖 |
| `Emuera/Program.cs` | WinForms Factory 初始化 |

## [0.52.1] - 2026-05-23

### A 类内核修复（双端受益，已回流 develop-skiasharp）

| # | 问题 | 根因 | 修复 |
|---|------|------|------|
| 1 | `ENUMFILES` 返回绝对路径，`LOADTEXT` 拒绝绝对路径导致 `XML_GET` 解析失败 | `EnumFilesMethod` 返回 `Directory.GetFiles` 的绝对路径，`GetValidPath` 只接受相对路径 | 将绝对路径转为相对路径（相对 `Program.ExeDir`） |
| 2 | `PRINTFORMC` 遇中文等非日文字符崩溃 | 硬编码 `Shift-JIS` 编码无 `ReplacementFallback`，无法编码的字符抛异常 | 用 `Config.Encode.CodePage` + `EncoderFallback.ReplacementFallback` 替换硬编码 |
| 3 | `HTML_PRINT` 解析 `<div />` 自闭合标签报错 | `HtmlManager` 不支持 XML 自闭合语法，`/` 被当作运算符解析失败 | 在 `div` 属性解析循环中检测 `/>` 并创建空 div |

### C 类修复（Xamarin 专属）

| # | 问题 | 修复 |
|---|------|------|
| 4 | Xamarin `Config.Encode`/`SaveEncode` 默认 Shift-JIS 无 fallback | 添加 `EncoderReplacementFallback("?")` / `DecoderReplacementFallback("?")` |

### 技术背景

- 修复 #3 的 `<div />` 问题源于 `XML_GET` 序列化空 div 元素时按 XML 规范输出自闭合标签，但 HTML 解析器不支持此语法（同一引擎的输出和输入不兼容）
- EmueraFL（erafl 作者 Kom1 的自研闭源引擎，基于 EMv18）已修复此问题
- 上游 emuera.em（EMv17）和 EmueraDotNet 均存在此 bug

## [0.52.0] - 2026-05-23

### 修复华为 HarmonyOS 实机简体汉字方框 + 回退字体基线偏移

> **一句话根因**：华为实机上 `FontModel.Load()` 中 `InitFontMapping`/`LoadFontsFolder`/`RegisterMissingChineseFontFallbacks` 抛异常，导致 `Init=False`、`DrawTextUtils.Load()` 未调用、`EnabledFontCaches` 为空，整个回退链未建立。此前 v0.50.0 修复的 Final 强行认领、后续添加的 GetGlyph 二次验证和 MatchCharacter 修复逻辑上正确，但在 `Init=False` 的实机环境下无法生效。基线偏移的根因是每个 TextUnit 用各自字体的 Ascent 计算基线，导致不同字体的基线位置不同；正确做法是所有字符共享主字体的基线。

#### 问题场景

华为 Mate20X HarmonyOS 4.0.0 实机，HTML 多层嵌套内联文本中，MS Gothic 不支持的简体汉字（如【贝】、【厅】、【劲】、【组】、【姬】）显示为方框。MuMu 模拟器（华为 P50 虚拟机）上正常。

#### 根因分析（按诊断时间线）

| # | 问题 | 根因 | 修复 | 实际效果 |
|---|------|------|------|---------|
| 1 | `FontModel.Init = False`，回退链未建立 | `FontModel.Load()` 中 `InitFontMapping`/`LoadFontsFolder`/`RegisterMissingChineseFontFallbacks` 抛异常（如 `FontNameMapping.Add` 冲突、外部字体目录访问失败），提前跳出，`Init` 未设为 `true`。`DrawTextUtils.Load()` 依赖 `FontModel.Init`，未调用导致 `EnabledFontCaches` 为空 | 关键路径加 `try-catch`；`AllModels[]` 改为 `TryGetValue`；强制启用 YaHei；确保 `Save()` 和 `Init = true` 一定执行 | **方框消失** ✓ |
| 2 | `SKPaint.GetGlyphs` 在 Android 上误报 | Android 上 `SKPaint.GetGlyphs(CharList)` 因字体替换机制对 MS Gothic 不支持的简体汉字返回非零 glyph ID，`PrepareGlyphs` 误判字体支持该字符 | 对 `Char > 0x7F` 添加 `Typeface.GetGlyph(Char)` 二次验证，直接查 cmap 表 | 逻辑正确，但 Init=False 时 PrepareGlyphs 根本不执行 |
| 3 | `MatchCharacter` 返回不含目标字符的字体 | 华为 HarmonyOS 的 `fonts.xml` 中 `fFallbackFor` 属性处理不当，`MatchCharacter('贝')` 可能返回 Noto Sans CJK JP（日文子集） | 对 `MatchCharacter` 返回的字体用 `GetGlyph` 做二次验证 | 逻辑正确，但 Init=False 时回退链未建立，MatchCharacter 也无法被触发 |
| 4 | FontFactory 安全回退跳过内嵌字体 | `GetFallbackTypefaceForChar` 使用 `SKTypeface.FromFamilyName` 查找，Android 上找不到 Xamarin 内嵌的 SimHei/YaHei | 优先从 `FontModel.HasFont/GetFont` 获取内嵌字体；添加 `FontModel.UserList` 遍历 | 逻辑正确，但 Init=False 时 FontFactory 回退链也无法正常工作 |
| 5 | 终极保底用 MS Gothic 画方框 | `CharRemain > 0` 时用 `UserCache.FontModel`（MS Gothic）画缺失字符 | 终极保底改用 `FontModel.Final`（Microsoft YaHei） | 逻辑正确，与 #1 一起生效 |
| 6 | `ContainsGlyph` 在 Android 上 False Positive | `SKTypeface.ContainsGlyph` 可能因系统字体替换返回 True | FontFactory 中所有 `ContainsGlyph` 替换为 `GetGlyph != 0` | 逻辑正确，防御性修复 |
| 7 | 回退字体基线偏移 | `DrawText` 循环中每个 TextUnit 用各自字体的 `FontMetrics.Ascent` 计算基线，不同字体 Ascent 不同导致基线位置不一致 | 所有字符共享主字体的 `baselineY`，回退字体切换 Typeface 后仍用主字体基线绘制 | 方框修复后发现，立即修复 ✓ |

#### 诊断过程

1. **v0.50.0**：修复 Final 字体强行认领 → 模拟器上方框消失，实机无变化
2. **后续迭代**：添加 `Typeface.GetGlyph` 二次验证 + FontFactory 内嵌字体优先 + `MatchCharacter` GetGlyph 二次验证 + 终极保底改用 YaHei → 实机无变化
3. **添加 FontDiag 诊断日志**：导出 FontDiag.log 发现 `FontModel.Init = False`、`AllModels.Count` 正常但 `EnabledFontCaches` 为空
4. **`FontModel.Load()` 异常防护** → **方框消失** ✓
5. 方框消失后发现回退字体基线偏移 → 立即修复 ✓

#### SKPaint.GetGlyphs vs Typeface.GetGlyph 差异

| API | 行为 | Android 可靠性 |
|-----|------|---------------|
| `SKPaint.GetGlyphs(text)` | 考虑字体替换，缺失字符可能返回替换字形 glyph ID | ❌ 不可靠 |
| `SKTypeface.GetGlyph(char)` | 直接查 cmap 表，缺失返回 0 | ✅ 可靠 |

#### 修复后 Xamarin (Android) 管线

```
DrawTextUtils.DrawText(canvas, text, font, x, y, color)
  → UserCache (当前 SETFONT 字体) CheckGlyphs + PrepareGlyphs
    → glyphs[i]!=0 但 Typeface.GetGlyph(Char)==0 → InvalidChar（二次验证）
  → EnabledFontCaches 遍历 CheckGlyphs + PrepareGlyphs
    → 同上二次验证
  → CharRemain > 0? → SKFontManager.Default.MatchCharacter 系统回退
    → MatchCharacter 返回的字体用 GetGlyph 二次验证
    → SystemFallbackCache 缓存结果
  → CharRemain > 0? → 终极保底：FontModel.Final（YaHei）画占位符
  → 按 FontModel 分组 → canvas.DrawText
    → 所有 TextUnit 共享主字体的 baselineY（y - 主字体 Ascent），回退字体仅切换 Typeface
```

#### 修改文件

| 文件 | 修改 |
|------|------|
| `Models/FontModel.cs` | `Load()` 关键路径加异常防护（`try-catch`）；`AllModels[]` 改为 `TryGetValue`；强制启用 `FinalFontName`；`AllModels`/`Init` 改为 `internal` |
| `Drawing/DrawTextUtils.cs` | `PrepareGlyphs()` 添加 `Typeface.GetGlyph` 二次验证；系统回退 `MatchCharacter` 增加 GetGlyph 二次验证；终极保底改用 `FontModel.Final`；所有 TextUnit 共享主字体 `baselineY`（回退字体仅切换 Typeface，不重新计算基线）；添加 `FontDiag` 诊断日志类 |
| `Platform/Stubs/FontFactory.cs` | `CreateTypefaceWithFallback` 优先内嵌字体；`ContainsGlyph`→`GetGlyph`；`MatchCharacter` 增加 GetGlyph 二次验证；`GetFallbackTypefaceForChar` 优先 `FontModel.HasFont/GetFont`；`GetFallbackTypefaceForCodepoint`/`GetFallbackFontForChar`/`TryGetGlyphFromFallback` 添加 `FontModel.UserList` 遍历 |
| `Platform/GameView/EmueraConsole.Print.cs` | 日志导出时追加 FontDiag 诊断信息 |
| `Emuera.Xamarin.Android/AndroidApp.cs` | 导出系统日志时同步导出 `FontDiag.log` |

## [0.51.1] - 2026-05-22

### 修复旋转锁定按钮回归 Bug + 屏幕旋转锁定不生效

> **一句话根因**：`IsToggled` 未初始化 → 点击无效；`menuButton` 未添加到布局 → 汉堡按钮消失；`PlatformService` 为 null（DependencyService 过早调用 + `DroidPlatformService.cs` 未加入 csproj）→ 锁定旋转静默跳过。

#### 问题场景

旋转锁定按钮点击后图标不变、无法锁定屏幕旋转；汉堡按钮消失；修复图标后锁定旋转仍不生效（`PlatformService=False` 确认）。

#### 根因分析

| # | 问题 | 根因 | 修复 |
|---|------|------|------|
| 1 | 旋转锁定图标不变 | `TintImageButton.IsToggled` 初始值为 `null`，`!null` = `null`，值无变化不触发 `Toggled` 事件 | 设置 `IsToggled = false`（与 XEmuera 上游一致） |
| 2 | 汉堡按钮消失 | `menuButton` 创建后未添加到 `toolbar.Children`，仅定义未使用 | 将 `menuButton` 添加到工具栏布局（spacer 之后） |
| 3 | 锁定旋转不生效 | `GameUtils.PlatformService` 为 null（ADB 日志确认 `PlatformService=False`）：`DependencyService.Get<IPlatformService>()` 在 `OnCreate` 阶段返回 null；`DroidPlatformService.cs` 未被 `Emuera.Xamarin.Android.csproj` 编译（`<EnableDefaultCompileItems>false</EnableDefaultCompileItems>` 模式，漏加 `<Compile Include>`） | `InitPlatformService()` 添加可选参数 `IPlatformService service = null`；`AndroidHost` 传入 `new DroidPlatformService()` 实例；`Emuera.Xamarin.Android.csproj` 添加 `<Compile Include="DroidPlatformService.cs" />` |

#### 修改文件

| 文件 | 修改 |
|------|------|
| AndroidMainWindow.cs | `lockRotationButton.IsToggled = false`；`toolbar.Children.Add(menuButton)`；Toggled 事件添加诊断日志 |
| DroidPlatformService.cs | `LockScreenOrientation` 改用 `activity.WindowManager.DefaultDisplay.Rotation`（Android 原生 API）；添加 `Log.Debug` 诊断日志 |
| Utils.cs | `InitPlatformService()` 添加可选参数 `IPlatformService service = null`，`PlatformService = service ?? DependencyService.Get<IPlatformService>()` |
| AndroidHost.cs | `GameUtils.InitPlatformService(new DroidPlatformService())` — 直接传入实例绕过 DependencyService |
| Emuera.Xamarin.Android.csproj | 添加 `<Compile Include="DroidPlatformService.cs" />`（此前漏加，csproj 为 `<EnableDefaultCompileItems>false</EnableDefaultCompileItems>` 模式） |

## [0.51.0] - 2026-05-21

### UI 默认状态调整：工具栏展开 + 虚拟手柄关闭 + 画廊打开 + 画廊宽度限制

#### 变更内容

| # | 变更 | 修改 | 说明 |
|---|------|------|------|
| 1 | 悬浮工具栏默认展开 | AndroidMainWindow.cs | 工具栏按钮初始 `IsVisible=true`，`menuShowButton.Opacity=1` |
| 2 | 虚拟手柄默认关闭 | AndroidMainWindow.cs | `virtualControllerVisible=false`，`virtualControllerLayout.IsVisible=false`，`gamepadButton.Opacity=0.5` |
| 3 | 画廊视图默认打开 | AndroidMainWindow.cs | `quickScroll.IsVisible=true`，`galleryViewButton.Opacity=1` |
| 4 | 画廊宽度限制为右半屏 | AndroidMainWindow.cs | `WidthRequest` 从 `canvasView.Width * 3 / 5` 改为 `canvasView.Width / 2`，画廊框架不超过屏幕右半部分 |

#### 修改文件

| 文件 | 修改 |
|------|------|
| AndroidMainWindow.cs | 工具栏按钮初始状态改为展开；虚拟手柄初始关闭；画廊初始打开；画廊宽度限制为 1/2 屏宽 |

## [0.50.0] - 2026-05-21

### 修复 Final 字体强行认领导致系统字体回退永远不执行（方块/tofu 问题）

> **一句话根因**：`DrawTextUtils.PrepareGlyphs()` 中 Final 字体（如 Microsoft YaHei）对 `glyphs[i]==0` 的字符有特殊逻辑：只要 `glyphWidths[i]!=0` 就强行认领。这导致 `CharRemain==0`，v0.45.0 添加的 `SKFontManager.Default.MatchCharacter` 系统回退永远不会执行，缺失字符被渲染为方块。

#### 问题场景

eratw-chs 的 `@DRAW_MAP` 调用 `SETFONT "ＭＳ ゴシック"` 后输出简体汉字地图。MS Gothic 不含简体汉字 → EnabledFontCaches 中 SimHei/YaHei 本应能渲染，但 Final 字体（YaHei）在 `PrepareGlyphs` 中强行认领了这些字符（`glyphWidths!=0` 但 `glyphs==0`），渲染为方块。

#### 根因分析

| # | 问题 | 根因 | 修复 |
|---|------|------|------|
| 1 | Final 字体强行认领不支持的字符 | `PrepareGlyphs()` 中 `if (glyphWidths[i] == 0 \|\| FontModel != FontModel.Final)` 条件，当 `FontModel==Final` 且 `glyphWidths[i]!=0` 时跳过 `InvalidChar`，直接认领 | 移除 Final 特殊逻辑，所有字体无字形一律 `InvalidChar` |
| 2 | 系统回退永远不执行 | Final 强行认领后 `CharRemain==0`，`if (CharRemain > 0)` 分支不进入 | Bug 1 修复后自然解决 |
| 3 | 极端情况文字消失 | 如果所有回退都失败，`CharRemain==CharList.Length` 直接 return，文字不渲染 | 添加终极保底：用 UserCache 画占位符 |

#### 修复后回退链

```
1. UserCache（当前 SETFONT 字体）→ CheckGlyphs + PrepareGlyphs
2. EnabledFontCaches 遍历 → CheckGlyphs + PrepareGlyphs（Final 不再特殊）
3. SKFontManager.Default.MatchCharacter → Android 系统字体回退
4. [新增] 终极保底：UserCache 画占位符（防排版错乱）
```

#### 修改文件

| 文件 | 修改 |
|------|------|
| DrawTextUtils.cs | `PrepareGlyphs()`：移除 Final 字体强行认领逻辑 |
| DrawTextUtils.cs | 添加终极保底：系统回退后仍有未匹配字符时用 UserCache 画占位符 |

## [0.49.2] - 2026-05-21

### 修复 v0.49.1 引入的 NRE 崩溃（UseLazyLoading 配置未注册）

> **一句话根因**：v0.49.1 在 `Config.SetConfig` 中添加了 `UseLazyLoading = instance.GetConfigValue<bool>(ConfigCode.UseLazyLoading)`，但 Xamarin 版 `ConfigData.SetDefault()` 中**未注册** `ConfigCode.UseLazyLoading` 对应的配置项，导致 `GetConfigValue` 返回 `null`，触发 `NullReferenceException`。

#### 问题场景

应用启动时引擎加载失败，闪退。日志显示在配置初始化阶段发生 NRE。

#### 根因分析

| # | 问题 | 根因 | 修复 |
|---|------|------|------|
| 1 | NRE 在 `GetConfigValue<bool>` 中 | `ConfigData.SetDefault()` 未注册 `ConfigCode.UseLazyLoading`，`GetConfigItem(code)` 返回 `null`，`(T)item.Value` 抛出 NRE | 在 `SetDefault()` 中添加 `configArray.Add(new ConfigItem(ConfigCode.UseLazyLoading, "UseLazyLoading", "UseLazyLoading", true))` |

#### 修改文件

| 文件 | 修改 |
|------|------|
| ConfigData.cs (Xamarin) | `SetDefault()` 中添加 `UseLazyLoading` 配置项注册，默认值 `true` |

## [0.49.1] - 2026-05-21

### 修复 Android 实机 LazyLoading 不生效（UseLazyLoading 配置未读取）

> **一句话根因**：Xamarin 版 `Config.SetConfig` 遗漏了 `UseLazyLoading` 的读取，导致该值始终保持默认 `false`。`SyncToRuntimeConfig` 将 `false` 覆盖到 Runtime Config，LazyLoading 从未被启用。

#### 问题场景

华为 Android 12 实机测试中，eratw-chs 游戏加载后未生成 `lazyloading.bin`/`lazyloadingfiles.bin`。`lazyloading.cfg` 存在，sav/log/.db 文件生成正常。同一游戏源码在 Windows 引擎和 MuMu 模拟器（m-emuera 版本）上可正常生成。

#### 根因分析

| # | 问题 | 根因 | 修复 |
|---|------|------|------|
| 1 | `UseLazyLoading` 始终为 `false` | `Emuera.Xamarin/Platform/Config/Config.cs` 的 `SetConfig` 方法未读取 `ConfigCode.UseLazyLoading`，该属性默认值为 `false`（line 650）。而 Runtime Config 的默认值为 `true`（ConfigData.cs line 122），但被 Xamarin 的 `SyncToRuntimeConfig` 覆盖为 `false` | 在 `SetConfig` 中添加 `UseLazyLoading = instance.GetConfigValue<bool>(ConfigCode.UseLazyLoading)` |

#### 调用链分析

```
Process.Initialize → ErbLoader.LoadErbDir(useLazyLoading: Config.UseLazyLoading)
  → if (useLazyLoading) { LoadLazyLoadingTable(); SaveLazyLoadingList(); }
  → useLazyLoading=false → 整个 LazyLoading 分支被跳过
```

#### 诊断日志

在 `Process.LazyLoading.cs` 的 `LoadLazyLoadingTable` 和 `GetLazyFiles` 方法中添加 `Debug.WriteLine` 诊断日志，输出：
- `UseLazyLoading` 值
- cfg/bin 文件是否存在
- `erbFiles` 数量
- `LoadLazyLoadingFolders` 返回的路径列表
- `GetLazyFiles` 匹配结果

#### 修改文件

| 文件 | 修改 |
|------|------|
| Config.cs (Xamarin) | `SetConfig` 中添加 `UseLazyLoading = instance.GetConfigValue<bool>(ConfigCode.UseLazyLoading)` |
| Process.LazyLoading.cs | `LoadLazyLoadingTable` 添加诊断日志；`GetLazyFiles` 添加诊断日志 |

## [0.49.0] - 2026-05-21

### 修复 Android 端 lazyloading.bin/lazyloadingfiles.bin 不生成

#### 问题

实机测试中（华为 Android 12），eratw-chs 游戏加载后未生成 `lazyloading.bin` 和 `lazyloadingfiles.bin`。已确认 `lazyloading.cfg` 存在，sav/log/.db 文件生成正常。同一游戏源码在 Windows 引擎上可正常生成这两个文件。

#### 根因分析

| # | 问题 | 根因 | 修复 |
|---|------|------|------|
| 1 | `LoadLazyLoadingFolders` 路径分隔符替换在 Android 上无效 | `oldChar` 根据 `RuntimeInformation.IsOSPlatform(OSPlatform.Windows)` 选择：Windows 上 `oldChar='\\'`，Android 上 `oldChar='/'`。但 `lazyloading.cfg` 在 Windows 上创建使用 `\`，Android 上 `Replace('/', '/')` 无效果，导致 `StartsWith` 匹配失败，`GetLazyFiles` 返回空集，`SaveLazyLoadingList` 判定 `files.Count==0` 直接返回 `NoLazy` | 同时替换 `\` 和 `/` 为 `Path.DirectorySeparatorChar`，无论当前平台 |
| 2 | `ErbPath` 使用硬编码 `"ERB/"` 前缀 | `ErbPath(name)` 返回 `"ERB/" + name`（相对路径），在 Windows 上当前工作目录恰为游戏目录可工作，但在 Android 上当前工作目录不是游戏目录，`File.Exists`/`File.GetLastWriteTime` 无法找到文件 | 改为 `Program.ErbDir + name`（完整路径），跨平台通用 |

#### 图片 lazy loading（AppContents SQL 索引）

Xamarin 版 `AppContents.LoadContents` 已正确处理路径分隔符（`arg2.Replace('\\', Path.DirectorySeparatorChar)`），SQL 索引构建和延迟加载无问题，无需修改。

#### 修改文件

| 文件 | 修改 |
|------|------|
| Process.LazyLoading.cs | `LoadLazyLoadingFolders`：移除 `RuntimeInformation.IsOSPlatform` 判断，改为同时替换 `\` 和 `/`；移除 `using System.Runtime.InteropServices` |
| Process.LazyLoading.cs | `ErbPath`：从 `string.Create(... "ERB/")` 改为 `Program.ErbDir + a` |

### 修复 MoveMouse 碰撞检测回归 + div 按钮失效

#### 问题

| # | 问题 | 根因 | 修复 |
|---|------|------|------|
| 1 | v0.48.2 修复导致所有触屏点击失效 | `escapedParts` 为空时 `edepth` 是空数组，`while` 循环不执行，depth=0 的普通按钮检测永远不运行。原版用 `int[] dummy = [0]` 兜底 | 改用 AI 审查方案：先检测 escapedParts，再检测普通文本流，两者独立，不存在空数组导致跳过的问题 |
| 2 | HTML div 内按钮仍无法点击 | **根因**：`ConsoleButtonString.EscapedParts` 属性使用了独立自动属性 `{ get; private set; }`，但 `FilterEscaped()` 将结果赋给了 `escaped` 字段而非属性，导致 `EscapedParts` 始终为 null → div 永远不会被注册到 `ConsoleEscapedParts` → MoveMouse 碰撞检测中 escapedParts 字典为空 → div 按钮永远检测不到。原版用 `{ get { return escaped; } }` 正确返回字段 | 1. 修复 `EscapedParts` 属性为 `{ get { return escaped; } }`（与原版一致）；2. 重写 MoveMouse 碰撞检测：先检测 escapedParts（高 depth 优先，降序），lineScreenY 用 `topPointY + (baseLineNo - topLineNo) * LineHeight`（与 DrawTo 一致）；3. 修正 `topLineNo` 计算与渲染一致 |

## [0.48.2] - 2026-05-21

### 修复 v0.48.1 两个残留 Bug

#### 问题

| # | 问题 | 根因 | 修复 |
|---|------|------|------|
| 1 | 旋转锁定按钮图标仍不可见 | `Emuera.Xamarin.Core.csproj` 缺少 `lock_rotation.svg` 和 `lock_rotation_open.svg` 的 EmbeddedResource 声明；Android 项目引用 Core 而非 Xamarin，运行时找不到 SVG 资源 | 在 Core.csproj 中添加两个 SVG 的 EmbeddedResource |
| 2 | HTML div 内按钮仍无法点击 | MoveMouse 碰撞检测缺少 `escapedParts` 系统遍历。原版 LazyLoading 通过 `ConsoleEscapedParts` 按 depth 交错检测 div 碰撞，Xamarin 版 MoveMouse 完全没有此逻辑；v0.48.0 的行级 div 检测方式与原版架构不一致（原版在行级遍历中**跳过** div，在 escapedParts 遍历中检测 div） | 重写 MoveMouse 碰撞检测，完整移植原版 escapedParts 遍历逻辑：按 depth 排序 → depth=0 遍历行级按钮（跳过 div） → depth!=0 遍历 escapedParts（div 用 TestChildHitbox） |

#### 修改文件

| 文件 | 修改 |
|------|------|
| Emuera.Xamarin.Core/Emuera.Xamarin.Core.csproj | 添加 `lock_rotation.svg` 和 `lock_rotation_open.svg` 的 EmbeddedResource |
| AndroidMainWindow.cs | 旋转锁定按钮改用 `MakeSvgToolButton` + Toggled 模式（与上游 XEmuera 一致），移除手动 Clicked + Image 切换 |
| EmueraConsole.cs | MoveMouse 碰撞检测重写：用 escapedParts 系统按 depth 交错检测（与原版一致），行级遍历跳过 ConsoleDivPart |

## [0.48.1] - 2026-05-21

### 修复 v0.48.0 三个回归 Bug

#### 问题

| # | 问题 | 根因 | 修复 |
|---|------|------|------|
| 1 | 旋转锁定按钮图标不可见（黑色透明背景框） | Toggled 模式缺少 `IsToggle="True"` 属性，TintImageButton 内部状态机未正确加载默认图片 | 退回 Clicked 模式，手动切换 `Image` 属性文件名 |
| 2 | MOUSEX()/MOUSEY() 在 Android 端始终返回 0 | `SetLastMousePosition` 仅在桌面端 `MainWindow.xaml.cs` 添加，**遗漏了 Android 端实际运行的 `AndroidMainWindow.cs`** | 在 `AndroidMainWindow.cs` 触屏事件中添加 `PlatformInterop.SetLastMousePosition(mouseLocation)` |
| 3 | HTML div 内按钮碰撞检测 Y 轴错位 | `lineScreenY` 用 `MainPicBox.Height - LineHeight * (bottomLineNo - i + 1)` 计算（行底部语义），与 DrawTo 的行顶部语义不一致；`TestChildHitbox` 遗漏 border/padding 偏移 | `lineScreenY = point.Y - relPointY`（行顶部语义）；`TestChildHitbox` 添加 border/padding 偏移计算 |

#### 修改文件

| 文件 | 修改 |
|------|------|
| AndroidMainWindow.cs | 旋转锁定按钮退回 Clicked 模式（移除 ToggledImage/IsToggled/Toggled 事件，改用 Clicked + 手动切换 Image） |
| AndroidMainWindow.cs | 触屏事件添加 `PlatformInterop.SetLastMousePosition(mouseLocation)` |
| EmueraConsole.cs | MoveMouse 中 `lineScreenY = point.Y - relPointY`（与 DrawTo point.Y 一致） |
| EvilMaskUtils.cs | TestChildHitbox 添加 border/padding 偏移（与 DrawTo 中 content 区域一致） |

## [0.48.0] - 2026-05-21

### HTML div 触屏定位修复 + div 按钮碰撞检测

#### 问题

| # | 问题 | 根因 | 修复 |
|---|------|------|------|
| 1 | COM_OPTION 函数 div 框出现在画面左上角 | `PlatformInterop.GetMousePosition` 在 Android 端返回 `Point.Empty`，MOUSEX()/MOUSEY() 始终为 0 | 添加 `_lastMousePosition` 存储，触屏事件更新，`GetMousePosition` 委托返回最后触屏坐标 |
| 2 | HTML div 内按钮无法激活/点击 | `MoveMouse` 只遍历 `displayLineList` 行按钮，不检测 `ConsoleDivPart` 子按钮 | MoveMouse 中检测 `ConsoleDivPart` → 调用 `TestChildHitbox`；`TestChildHitbox` rect 计算与 `DrawTo` 一致 |

#### 修改文件

| 文件 | 修改 |
|------|------|
| Platform/Stubs/PlatformInterop.cs | 添加 `_lastMousePosition` 静态字段 + `SetLastMousePosition()` 方法 + `LastMousePosition` 属性 |
| Emuera.Xamarin.Android/AndroidHost.cs | `GetMousePosition` 委托从 `() => Point.Empty` 改为 `() => PlatformInterop.LastMousePosition` |
| Platform/Forms/MainWindow.xaml.cs | 触屏事件中调用 `PlatformInterop.SetLastMousePosition(MouseLocation)` |
| Platform/GameView/EmueraConsole.cs | MoveMouse 中 `ConsoleDivPart` 碰撞检测分支（调用 `TestChildHitbox`） |
| Platform/Stubs/EvilMaskUtils.cs | `TestChildHitbox` 重写：rect 计算与 `DrawTo` 一致（IsRelative/AbsoluteLeftTop/AbsoluteLeftBottom + margin 偏移） |

#### 各移植项目 div 按钮方案对比

| 项目 | 方案 | MOUSEX/MOUSEY |
|------|------|---------------|
| LazyLoading（WinForms） | `escapedParts` 系统 + `TestChildHitbox` | `Control.MousePosition` |
| Emuera.Xamarin（Skia） | MoveMouse 检测 ConsoleDivPart → TestChildHitbox | `PlatformInterop.SetLastMousePosition` |
| gEmuera（Godot） | `BuildDivControl` Godot Control 节点树，原生触屏 | `GenericUtils.SetPointerPosition` |
| XEmuera（上游原型） | MoveMouse 检测 ConsoleDivPart → TestChildHitbox | `_lastTouchPoint` 字段 |

## [0.47.1] - 2026-05-21

### 旋转锁定按钮正确移植：修复图标不可见 + 点击闪退

#### 根因分析

| 问题 | 根因 | 修复 |
|------|------|------|
| 点击闪退 | `GameUtils.PlatformService` 未初始化（Android 端不调用 `GameUtils.Load()`），NRE | 添加 `GameUtils.InitPlatformService()` 方法，在 `AndroidHost.InitMainWindow()` 中调用 |
| 图标不可见 | 使用 `Clicked` + `Opacity` 模式而非 XEmuera 的 `Toggled` + `ToggledImage` 模式 | 改用 `TintImageButton` 的 `Toggled` 事件 + `IsToggled` + `ToggledImage` |
| 按钮顺序不对 | lock_rotation 放在 gamepad 之后 | 移到 XEmuera 一致位置（spacer 后第一个） |

#### 修改文件

| 文件 | 修改 |
|------|------|
| Utils.cs | 添加 `InitPlatformService()` 公共方法，供 Android 端初始化 `PlatformService` |
| AndroidHost.cs | `InitMainWindow()` 中调用 `GameUtils.InitPlatformService()` |
| AndroidMainWindow.cs | 旋转锁定按钮改用 `Toggled` 模式（`lock_rotation_open.svg`/`lock_rotation.svg`）；按钮顺序对齐 XEmuera；添加 `SetButtonOpacity` 辅助方法；初始状态所有工具按钮不可见 |

## [0.47.0] - 2026-05-21

### 修复 GETCONFIG/FPS 移动端支持 + 画廊 QuickButton HTML 过滤 + 旋转锁定按钮 + SVG 资源补全

#### Bug 修复

| 修改 | 文件 | 说明 |
|------|------|------|
| 修复 GETCONFIG 移动端支持 | ConfigData.cs | `GetConfigValueInERB` 的 `default` 分支直接报错；改为用 `ConfigItem.ValueToString(item.Value)` 解析值 |
| 添加 EngText 检查 | ConfigData.cs | `GetReplaceItem`/`GetDebugItem` 缺少 `EngText` 大小写不敏感匹配 |
| 修复画廊 QuickButton 显示 | EmueraConsole.ex.cs | `RefreshQuickButtonAsync` 使用 `button.ToString()` 导致 `ConsoleShapePart.ToString()` 输出 HTML 标签；改为遍历 `StrArray` 跳过 shape/image 节点 |
| 补全 SVG 资源 | Emuera.Xamarin.csproj | 添加 `lock_rotation.svg` 等缺失的 EmbeddedResource 条目 |

#### 新增功能

| 修改 | 文件 | 说明 |
|------|------|------|
| 旋转锁定服务 | DroidPlatformService.cs | 实现 `IPlatformService` 接口，`LockScreenOrientation()`/`UnlockScreenOrientation()` 使用 `RequestedOrientation` |
| 旋转锁定按钮 | AndroidMainWindow.cs | 工具栏添加 `lock_rotation.svg` 按钮，点击切换锁定/解锁屏幕方向 |

## [0.46.0] - 2026-05-21

### Release 发布准备：版本号更新 + 签名配置 + GitHub Release

> **发布 v0.46.0**，包含 v0.45.0 的所有字体修复和 Android 备发布状态。

#### 变更内容

| 修改 | 文件 | 说明 |
|------|------|------|
| 更新 AboutPage 引擎版本 | AboutPage.xaml.cs | `Detail = AssemblyData.EmueraVersionText`，动态显示 `"Emuera.NET SkiaSharp X.Y.Z"` |
| 版本号升级 | AndroidManifest.xml | versionCode 35→46, versionName 0.35.0→0.46.0 |
| 添加 Release 签名配置 | Emuera.Xamarin.Android.csproj | AndroidKeyStore + keystore 路径/别名/密码 |
| 生成签名密钥库 | xemuera.keystore | JKS 格式，RSA 2048，有效期 10000 天 |
| .gitignore 更新 | .gitignore | 添加 *.keystore 和 *.pfx 排除项 |

#### 发布文件

- APK 路径：`Emuera.Xamarin.Android/bin/Release/net8.0-android/Emuera.SkiaX-Signed.apk`（~77 MB）

## [0.45.0] - 2026-05-21

### 修复 DrawTextUtils 字形回退 + Mapping 双向字典陷阱 + 移除诊断日志

> **一句话根因**：`DrawTextUtils` 的逐字符回退仅限于 `EnabledFontCaches`（MS Gothic/Microsoft YaHei/SimHei），
> 不使用 `SKFontManager.Default.MatchCharacter()` 做系统级回退。
> 当所有启用字体都不含某字符字形时（如 ❤ U+2764），Final 字体渲染方框（tofu）。

#### 问题场景

eratw-chs 的 `@HEARTMARK` 调用 `SETFONT "Times New Roman"` + `PRINTFORM %"❤" * ARG%`。
Android 上 Times New Roman 不存在 → `GetFont()` 回退到 Default（MS Gothic）→
MS Gothic 不含 ❤ 字形 → EnabledFontCaches 遍历后仍无字形 → Final 字体(SimHei)渲染方框。

#### 修复内容

| 修改 | 文件 | 说明 |
|------|------|------|
| 添加 `SystemFallbackCache` | DrawTextUtils.cs | `Dictionary<char, FontModel>` 缓存 MatchCharacter 结果，避免每帧重复查询 |
| 添加 MatchCharacter 回退 | DrawTextUtils.cs | 在 EnabledFontCaches 遍历后，对剩余字符调用 `SKFontManager.Default.MatchCharacter(c)` 查找系统字体 |
| 添加 `ResolveFontName` | FontModel.cs | 西文字体别名（Times New Roman/Arial→YaHei, Courier New→Gothic）改用方法映射，避免 Mapping 双向字典 value 重复 |
| 修正 Final 字体顺序 | FontModel.cs | SimHei 先于 Microsoft YaHei 启用，使 Microsoft YaHei 成为 Final（字形覆盖最广） |
| 移除诊断日志 | EmueraConsole.cs | 移除三处渲染诊断日志代码块及相关字段（_debugLogFrameCount 等） |

#### Mapping 双向字典陷阱（v0.45.0 引入的回归，已修复）

`XEmuera.Mapping<T1,T2>` 是双向字典，`Add(key, value)` 同时写 `KeyDictionary` 和 `ValueDictionary`。
初始修复在 `InitFontMapping()` 中添加了 `FontNameMapping.Add("Times New Roman", "Microsoft YaHei")` 和
`FontNameMapping.Add("Arial", "Microsoft YaHei")`，导致 `ValueDictionary["Microsoft YaHei"]` 重复 →
`ArgumentException` → `FontModel.Load()` 失败 → 字体系统未初始化 → 引擎 NRE 崩溃。

修复：西文字体别名从 `FontNameMapping` 移除，改用 `ResolveFontName()` 方法做一对一映射。

#### 回退链（修复后）

```
1. UserCache（当前 SETFONT 字体）→ CheckGlyphs + PrepareGlyphs
2. EnabledFontCaches 遍历 → CheckGlyphs + PrepareGlyphs
3. [新增] SKFontManager.Default.MatchCharacter → 系统字体回退
4. 放弃（CharRemain == CharList.Length → return）
```

## [0.44.0] - 2026-05-21

### 修复 HTML img/space 内联元素与文字行偏移

> **一句话根因**：`DrawTextUtils.DrawText` 把 `y` 参数（行顶部坐标）直接当作基线坐标传给 SkiaSharp `DrawText`，
> 导致文字渲染位置偏下约一个 Ascent。为补偿此偏移，之前引入了 `ShapeHeightOffset = -(LineHeight - 6)` 的硬编码 hack，
> 使 img/shape 元素也偏移同样量来"对齐"。但这个 hack 不精确，且与内核原版不一致。
>
> **正确修复**：在 `DrawTextUtils.DrawText` 中将 `y` 从行顶部转为基线坐标（`y - Paint.FontMetrics.Ascent`），
> 与内核原版 `point.Offset(0, -Font.Metrics.Ascent)` 一致，然后移除 `ShapeHeightOffset`。

#### 修复内容

| 修改 | 文件 | 说明 |
|------|------|------|
| `baselineY = y - Paint.FontMetrics.Ascent` | DrawTextUtils.cs | 将 y 从行顶部坐标转为基线坐标，与内核原版一致 |
| 移除 `+ DisplayUtils.ShapeHeightOffset` | ConsoleImagePart.cs DrawTo | img 元素直接用行顶部坐标，无需偏移 |
| 移除 `rect.Y += DisplayUtils.ShapeHeightOffset` | ConsoleShapePart.cs SetWidth | shape 元素直接用行顶部坐标，无需偏移 |
| 移除 ShapeHeightOffset 保存/恢复 | EvilMaskUtils.cs ConsoleDivPart.DrawTo | div 子元素不再需要特殊偏移处理 |
| 移除 `ShapeHeightOffset = -(LineHeight - 6)` | Config.cs | 不再需要此 hack |
| 移除 `ShapeHeightOffset` 属性 | Utils.cs | 不再需要 |

#### 内核原版参考

内核 `ConsoleStyledString.DrawTo` 在调用 `graph.DrawText` 前做 `point.Offset(0, -Font.Metrics.Ascent)` 将行顶部转为基线。
Xamarin 版 `DrawTextUtils.DrawText` 缺少此转换，导致文字偏下，进而需要 `ShapeHeightOffset` 补偿 img/shape。

## [0.43.0] - 2026-05-21

### 修复 pointY 语义错误：首行截断 + 按钮错位根因修复

> **一句话根因**：OnPaint 从"从下往上画"改为"从上往下画"时，`pointY` 的语义从"行底部"变成了"行顶部"，
> 但 MoveMouse 仍按"行底部"计算命中区域，导致 24px（1个 LineHeight）的偏移。

#### 修复内容

| 修改 | 文件 | 说明 |
|------|------|------|
| `pointY -= Config.LineHeight` | EmueraConsole.cs OnPaint | 在 topLineNo 计算前将 pointY 从"最后一行底部"调整为"最后一行顶部"，与渲染语义一致 |

#### ADB 验证结果（MuMu, density=1.75）

| 指标 | 修复前 (0.42.0) | 修复后 (0.43.0) |
|------|-----------------|-----------------|
| 第一行 renderY | 859 | 835 |
| 最后一行 renderY | 1075 | 1051 |
| 最后一行 absBottom | 1099 (超出1079) | 1075 (在1079内) |
| 首行截断 | 20px 不可见 | 全部可见 ✅ |
| 按钮命中 vs 渲染 | [1051,1075] vs [1075,1099] 差24px | [1051,1075] vs [1051,1075] 完全对齐 ✅ |
| last line exceeds canvas | True | False ✅ |

#### 调试历程总结（9N→9U→0.43.0，共7轮）

从 Phase 9N (f41712b) 到 0.43.0，经历了 7 轮反复修改，核心教训：

| 轮次 | 修改 | 结果 | 教训 |
|------|------|------|------|
| 9N | FontSize/LineHeight *= density | 字体双倍大 | ❌ 坐标系混用：物理像素 ClientHeight / 物理像素 LineHeight → 行数减半 |
| 9O | 移除 density 乘法 | 最后一行溢出+按钮错位 | ❌ 移除了正确的部分，没有找到真正的 bug |
| 9P | 恢复 density 乘法 | 同 9N | ❌ 反复横跳 |
| 9Q | 回退到基准版本坐标系 | 画面只在左上角 | ❌ 回退太远 |
| 9R | 移除 graph.Scale(density) | 画面只在左上角 | ❌ Scale 是必要的 |
| 9S | 恢复 graph.Scale(density) | 字体双倍大 | ❌ 同 9N |
| 9T | 移除 Scale + 物理像素统一 + 诊断日志 | 字体正确但首行截断+按钮错位 | ✅ 字体终于对了，但偏移问题暴露 |
| **0.43.0** | **pointY -= LineHeight** | **全部修复** | ✅ 根因是 pointY 语义，不是坐标系 |

**核心教训**：7 轮反复修改的根本原因是**没有理解 pointY 的语义变化**。
旧版代码"从下往上画"时 pointY=1075 是行的底部，改为"从上往下画"后 pointY=1075 变成了行的顶部。
MoveMouse 始终按"行底部"计算，导致 24px 偏移。修复只需一行：`pointY -= Config.LineHeight`。

**反模式**：遇到渲染偏移问题时，AI 倾向于修改坐标系（Scale/density/物理像素vs逻辑像素），
而不是检查已有代码的语义假设。这会导致大量无效修改和回归。

## [0.42.0] - 2026-05-21 (baseline 55f0df9)

### 基线状态：字体正确，但第一行溢出 + 按钮错位

> **此提交为已知问题基线**，后续修复从此状态出发。如改错可 `git checkout 55f0df9` 回退。

#### ADB 验证结果（MuMu, density=1.75）

| 项目 | 值 | 状态 |
|------|-----|------|
| 字体大小 | FontSize=24 渲染正确 | ✅ 用户确认合适 |
| 第一行渲染位置 | firstLineY=859（Canvas高1079） | ❌ 在屏幕外 |
| 最后一行 | bottomY=1099 > ClientHeight=1079 | ❌ 溢出20px |
| 按钮对齐 | 渲染Y vs 命中检测Y 差 ~24px | ❌ 错位一个行高 |

#### 已回退的错误修改（9U）

- `graph.Scale(density)`：导致字体放大1.75倍
- `ClientWidth/ClientHeight` 改逻辑像素：同样导致字体变大
- 触摸坐标 `/ density`：与回退的坐标系不匹配

#### 根因分析

**问题1（首行溢出）**: `PicBoxHeight = MainPicBox.Height * density = 1079`(物理)，但 `LineHeight = 24`(逻辑未乘density)。
`pointY/LH = 1075/24 ≈ 44` 行可见空间估算错误 → firstLineY=859。

**问题2（按钮错位）**: OnPaint 的 topLineNo 用 `(pointY/LH + 1)` 有 `+1`，
MoveMouse 的 topLineNo 用 `(ClientH/LH)` 无 `+1`。两者差1导致所有按钮偏移24px。

#### 诊断工具

- logcat 过滤: `Select-String "EmueraRender"`
- WaitInput 对齐诊断: 输出每行 renderY、按钮 absY、可见性
- 坐标系参数: Canvas尺寸、密度、ClientW/H、FontSize/LineHeight

---

## [0.41.0] - 2026-05-21

### Phase 9T：移除 graph.Scale(density) + 物理像素统一 + 诊断日志

#### 修改内容

1. **移除 `graph.Scale(density)`**：与 XEmuera 对齐，在物理像素 Canvas 上直接绘制
2. **`ClientWidth/ClientHeight` 改为物理像素**：`DisplayUtils.PicBoxWidth/PicBoxHeight`
3. **`Config.WindowX/WindowY` 改为物理像素**
4. **触摸坐标直接用物理像素**：不除以 density
5. **滚动 delta 直接用物理像素差值**
6. **`bakedBackground` 绘制用物理像素尺寸**
7. **诊断日志系统**：
   - 新增 `PlatformInterop.DebugLog` 委托（`PlatformInterop.cs`）
   - Android 端实现：`Android.Util.Log.Debug("EmueraRender", msg)`（`AndroidHost.cs`）
   - 桌面端默认：`System.Diagnostics.Debug.WriteLine(msg)`
   - `OnPaintSurface` 输出 Canvas 属性（物理尺寸、density、TotalMatrix）
   - `OnPaint` 输出行号计算、pointY、可见行数
   - 行渲染输出每行 Y 坐标
   - HTML 按钮输出 PointX/Width/Top/Bottom/absTop/absBottom

#### 待验证

- 字体大小是否正常（物理像素 Canvas 上 Paint.TextSize=24 → 24 物理像素）
- 最后一行是否可见
- 渲染位置与按键位置是否对齐

## [0.40.0] - 2026-05-21

### Phase 9S：恢复 graph.Scale(density) + 逻辑像素坐标系

#### 问题现象

- 字体双倍大（FontSize=24 显示为 42px）
- 最后一行在屏幕外
- 渲染位置与按键逻辑位置差约一个行高

#### 根因分析（本轮洞见）

通过阅读 SkiaSharp 源码 `SKCanvasView.cs`（Android 平台），确认了 `IgnorePixelScaling` 的真实行为：

```csharp
// SkiaSharp 源码 OnDraw 方法
if (IgnorePixelScaling)
{
    var skiaCanvas = surface.Canvas;
    skiaCanvas.Scale(density);  // 只有 IgnorePixelScaling=true 时才 Scale！
    skiaCanvas.Save();
}
```

**`IgnorePixelScaling=false`（默认）时，SkiaSharp 不做任何自动缩放。Canvas 是纯物理像素坐标系。**

之前的 9R 版本假设 `IgnorePixelScaling=false` 时 Canvas 已自动缩放为逻辑像素坐标系——这是错误的。
9R 移除 `graph.Scale(density)` 后，在物理像素 Canvas（1920x1080）上用逻辑像素坐标（1097x617）绘制，
导致画面只在屏幕左上角。

#### 修改内容

| 修改 | 文件 | 说明 |
|------|------|------|
| 恢复 graph.Scale(density) | EmueraConsole.cs | 将物理像素 Canvas 转为逻辑像素坐标系 |
| bakedBackground 绘制 | EmueraConsole.cs | `DrawBitmap(bitmap, SKRect(0,0,W,H))` 指定逻辑像素目标矩形，防止 Scale 放大位图 |

#### 当前状态

**字体双倍大 + 最后一行溢出 + 按键偏移**——与 9R(v3) 结果相同。
`graph.Scale(density)` 将 Canvas 转为逻辑像素坐标系后，`Paint.TextSize=24` 在逻辑像素 Canvas 上
应该是 24 逻辑像素，但实际显示为 42px（24 * 1.75）。原因待查。

#### 修复方案循环记录

| 版本 | 方案 | 结果 |
|------|------|------|
| 9R(v1) | graph.Scale(density) + baselineY 修改 | 字体双倍大 + 位置偏移 |
| 9R(v2) | FontSize *= density（无 Scale） | 字体双倍大 + 最后一行溢出 + 按键偏移 |
| 9R(v3) | graph.Scale(density) + 统一逻辑像素 | 字体双倍大 + 最后一行溢出 + 按键偏移 |
| 9R(v4) | 移除 graph.Scale(density) | 画面只在屏幕左上角 |
| **9S** | **恢复 graph.Scale(density) + bakedBackground 修复** | **字体双倍大 + 最后一行溢出 + 按键偏移** |

**核心矛盾仍未解决**：`graph.Scale(density)` 后 Canvas 应该是逻辑像素坐标系，
`Paint.TextSize=24` 应该渲染为 24 逻辑像素（= 42 物理像素），字体大小应该正确。
但用户反馈字体"双倍大"——可能 `Paint.TextSize` 已经在别处被乘以 density？

#### 诊断日志问题

当前版本的 `render_debug.log` 文件未生成（`run-as Emuera.SkiaX ls files/` 无此文件）。
可能原因：`AppendAllText` 抛异常被空 catch 吞掉，或 `OnPaint` 未被调用。
需要改用 Android `Log.Debug()` 输出到 logcat。

## [0.39.0] - 2026-05-21

### Phase 9R：移除 graph.Scale(density)，entryLayout 改为覆盖层

#### 问题现象

- 字体双倍大（FontSize=24 显示为 42px）
- 最后一行在屏幕外
- 渲染位置与按键逻辑位置差约一个行高
- 画面只在屏幕左上角（与之前某次提交结果相同）

#### 根因分析

1. `graph.Scale(density)` 在 `IgnorePixelScaling=false` 的 Canvas 上造成**双重缩放**：
   - `IgnorePixelScaling=false` 时 SkiaSharp 已自动将 Canvas 缩放为逻辑像素坐标系
   - `graph.Scale(density)` 再次乘以 density → 坐标和字体都放大 density 倍
2. `entryLayout`（软键盘输入栏）放在 `mainLayout` Grid Row 1 (Auto)，
   当 `update_lastinput()` 使其可见时会压缩 Row 0 (Star) 的 `canvasView`，
   导致 `MainPicBox.Height` 减小 → `ClientHeight` 减小 → 行数减少

#### XEmuera 对比

| 方面 | XEmuera | 本项目（修复后） |
|------|---------|-----------------|
| 布局 | FlexLayout（entryGroup 不压缩 mainPicBox） | Grid 单行（entryLayout 覆盖层） |
| Canvas 坐标系 | 逻辑像素（IgnorePixelScaling=false 自动缩放） | 逻辑像素（同） |
| ClientWidth/Height | 物理像素（PicBoxWidth/PicBoxHeight） | 逻辑像素（MainPicBox.Width/Height） |
| graph.Scale | 无 | 无 |
| FontSize | 逻辑像素（不乘 density） | 逻辑像素（不乘 density） |

#### 修改内容

| 修改 | 文件 | 说明 |
|------|------|------|
| 移除 graph.Scale(density) | EmueraConsole.cs | 消除双重缩放，恢复逻辑像素坐标系 |
| entryLayout 改为覆盖层 | AndroidMainWindow.cs | 从 mainLayout Grid Row 1 移到 canvasGrid 内，不再压缩 canvasView |
| WindowSoftInputMode | MainActivity.cs | 添加 `SoftInput.AdjustNothing`，防止软键盘弹出时压缩 Activity |
| ClientWidth/ClientHeight | EmueraConsole.cs | 保持 `MainPicBox.Width/Height`（逻辑像素） |
| WindowX/WindowY | Config.cs | 保持 `MainPicBox.Width/Height`（逻辑像素） |
| 触摸坐标 | AndroidMainWindow.cs | 保持除以 density（逻辑像素坐标系） |
| 诊断日志 | EmueraConsole.cs | 更新注释和单位标注 |

#### 当前状态

**画面只在屏幕左上角**——与之前某次提交结果相同。保留此提交记录用于逐步回退对比。

## [0.38.0] - 2026-05-20

### Phase 9Q：回归基准版本坐标系，修复缩放/按钮错位/缺字三大回归

#### 根因分析

以基准版本 `53a2bd0c`（一切正常）为参照，对比之后引入的变更：

9N/9P 的 `FontSize/LineHeight *= ScreenDensity` 修复方向错误。基准版本的坐标系设计：
- `MainPicBox.Width/Height` = 逻辑像素（Canvas 尺寸 / density）
- `ClientWidth/ClientHeight` = `GetRealLength(MainPicBox.Width)` = 物理像素
- `FontSize/LineHeight` = 逻辑像素（**不乘** density）——与 XEmuera 一致
- 触摸坐标 = 物理像素（直接用 `e.Location`）
- Canvas 绘制时 `Paint.TextSize` = 逻辑像素值，Skia 自动处理缩放

9N 添加 `FontSize/LineHeight *= ScreenDensity` 后：
1. **字体2倍大**：`Paint.TextSize` 变成物理像素值，在物理像素 Canvas 上绘制导致字体过大
2. **行数减半**：`ClientHeight / LineHeight` 行数计算变少
3. **按钮位置错位**：`MoveMouse` 中步长 `Config.LineHeight` 变大

9N 添加的 `TryFallbackToEmbeddedFont`/`TryFallbackToSystemFont` 在基准版本和 XEmuera 中均不存在，`EnabledFontCaches` 已包含 MS Gothic 和 Microsoft YaHei，正常遍历即可覆盖回退需求。

#### Bug 修复（回退到基准版本坐标系）

| 修复 | 文件 | 说明 |
|------|------|------|
| 字体2倍大 | Config.cs | 移除 `FontSize/LineHeight *= ScreenDensity`（SetConfig + ApplyRuntimeDisplaySettings 两处） |
| 字体2倍大 | Config.cs | 移除 `QuickButtonFontSize *= ScreenDensity` |
| 按钮错位 | AndroidMainWindow.cs | 移除 `ToLogicalTouchPoint`，恢复直接用 `e.Location`（与基准版本一致） |
| 缺字 | DrawTextUtils.cs | 移除 `TryFallbackToEmbeddedFont`/`TryFallbackToSystemFont`（基准版本和 XEmuera 均无此方法，EnabledFontCaches 正常遍历即可） |

#### 保留的改进（不回退）

| 改进 | 文件 | 说明 |
|------|------|------|
| glyphCount 安全检查 | DrawTextUtils.cs | `PrepareGlyphs` 中 `Math.Min(CharList.Length, Math.Min(glyphs.Length, glyphWidths.Length))` |
| FontModel 扩展扫描 | FontModel.cs | font/+fonts/ + .ttf/.otf/.ttc + 别名注册 + RegisterChineseFontAliases |
| AddFontFromResource 回退 | FontModel.cs | 嵌入字体加载失败时回退到 SKTypeface.Default |
| 方向变化 reflow | AndroidMainWindow.cs | OnPaintSurface 检测尺寸变化触发 ApplyRuntimeDisplayScale |

#### 待验证

| 项 | 说明 |
|----|------|
| 缺字 | 回退到基准版本的 EnabledFontCaches 遍历机制后，中文字符回退是否正常需真机验证 |
| 缩放比例 | 移除 density 乘法后字体大小应恢复基准版本状态 |
| 按钮位置 | 触摸坐标恢复为物理像素，与 ClientWidth/ClientHeight 一致 |

## [0.37.0] - 2026-05-20

### Phase 9P：坐标系统一修复（接替 kimi2.6 分析）

#### 根因分析

kimi2.6 的 9O 修复为何无效：
- **缺字**：回退链重排无效，因为 `EnabledFontCaches` 默认已包含 `[MS Gothic, Microsoft YaHei]`，搜索顺序正确；真正问题在 `PrepareGlyphs` 缺少 `glyphCount` 数组越界保护
- **最后一行越界+按钮错位**：9O 移除 `FontSize/LineHeight *= ScreenDensity` 导致坐标系不一致——`ClientHeight` 是物理像素，`FontSize/LineHeight` 是逻辑像素，`OnPaint` 行数计算和 `MoveMouse` 按钮定位都出错

#### Bug 修复

| 修复 | 文件 | 说明 |
|------|------|------|
| 最后一行越界+按钮错位 | Config.cs | 恢复 `FontSize/LineHeight *= ScreenDensity`，统一为物理像素坐标系（9O 错误移除导致回归） |
| HeightOffset 偏移 | Utils.cs | 改回常量 `4`（与 XEmuera 一致），9O 的 `4 * ScreenDensity` 在物理像素坐标系下偏移过大 |
| 缺字数组越界 | DrawTextUtils.cs | `PrepareGlyphs` 添加 `glyphCount = Math.Min(...)` 安全检查（与 XEmuera 一致） |
| 运行时缩放同步 | Config.cs | `ApplyRuntimeDisplaySettings` 同样添加 density 乘法，保持与 `SetConfig` 一致 |

#### 待验证

| 项 | 说明 |
|----|------|
| 缺字 | `glyphCount` 修复了数组越界，但 Android 上 `SKFontManager.Default.MatchCharacter` 是否能正确匹配中文字符仍需真机验证 |
| 缩放比例 | 恢复 density 乘法后需确认不会再次出现"缩放过大"（9N 的"过大"可能是其他地方的双重缩放已修复） |
| HTML div 错位 | QOL_USERCOM.ERB @COM_OPTION 弹窗位置错误，需在安卓端重新实现触屏位置侦测（已记录 TODO） |

## [0.36.0] - 2026-05-20

### Phase 9O：真机测试反馈修复 Round 2

#### Bug 修复

| 修复 | 文件 | 说明 |
|------|------|------|
| 缺字方框 | FontFactory.cs + DrawTextUtils.cs | 回退链优先检查 Microsoft YaHei（含简体汉字），再检查 MS Gothic；FontFactory safeFallbackFonts 将 Microsoft YaHei 提前到 MS Gothic 之前 |
| 最后一行越界+按钮错位 | Config.cs + EmueraConsole.cs + DisplayUtils.cs | 移除 FontSize/LineHeight 的 ScreenDensity 乘法（双重缩放根因）；HeightOffset 改为动态计算（乘 ScreenDensity）；统一 OnPaint 和 MoveMouse 坐标系 |
| 缩放过大 | Config.cs | 回滚 9N 的 FontSize/LineHeight *= ScreenDensity，恢复与 XEmuera 相似的缩放比例 |

#### 待验证

| 项 | 说明 |
|----|------|
| HTML div 错位 | QOL_USERCOM.ERB COM_OPTION 弹窗位置错误，需进一步分析（暂不修复） |

## [0.35.0] - 2026-05-20

### Phase 9N：真机测试反馈修复 + UI 完善

#### Bug 修复

| 修复 | 文件 | 说明 |
|------|------|------|
| 文字渲染全黑 | DrawTextUtils.cs | Paint==null 或 FontCaches 为空时 fallback 到 SKTypeface.Default 直接绘制，不再被 OnPaint try-catch 吞掉 |
| FontModel 加载崩溃 | FontModel.cs | AddFontFromResource 嵌入字体失败时回退到 SKTypeface.Default，不再抛 ArgumentNullException |
| Language 页面崩溃 | AndroidLanguagePage.cs | SaveConfig() 添加 try-catch，引擎未初始化时用 Preferences 保存语言选择 |
| 语言偏好丢失 | AndroidHost.cs | 新增 ApplyLanguagePreference()，引擎初始化前将 Preferences 中的语言写入 emuera.config |
| 文字行向下偏移 | Config.cs | FontSize/LineHeight/QuickButtonFontSize 乘以 ScreenDensity，与物理像素 ClientHeight 统一坐标系 |
| 按钮点击位置偏移 | AndroidMainWindow.cs | ToLogicalTouchPoint 移除 density 除法，保持触摸坐标与 ClientWidth/ClientHeight 一致 |
| 字体回退缺字 | FontModel.cs | 新增 RegisterMissingChineseFontFallbacks()，外部字体缺失时中文别名映射到内嵌 Microsoft YaHei |

#### UI 完善

| 功能 | 文件 | 说明 |
|------|------|------|
| 工具栏 SVG 图标 | AndroidMainWindow.cs | Unicode 字符（✏🎮⊞⇕☰）替换为 TintImageButton + SVG 矢量图标（edit.svg/gamepad.svg/gallery_view.svg/scroll_vertical.svg/menu_show.svg） |
| 汉堡菜单按钮 | AndroidMainWindow.cs | 新增 ☰ 按钮打开 FlyoutPage 侧边菜单（原 ☰ 改为收起/展开工具栏） |
| 菜单中文化 | AndroidMainMenuPage.cs + AndroidLanguagePage.cs + AndroidConfigPage.cs + AndroidApp.cs + AndroidGameListPage.cs + AndroidLogPage.cs | 硬编码英文字符串替换为 StringsText 本地化资源 |
| 英文翻译 | StringsText.en.resx | 新增英文翻译资源文件（48 条） |
| StringsText 扩展 | StringsText.resx + Designer.cs | 新增 ViewLog/SaveLog/LogSaved/SaveFailed/ScanningGames/StoragePermissionRequired/NoGamesFound/LanguageChanged/NeedRestartToApply/SaveAndClose 共 10 条 |
| SVG 嵌入资源 | Emuera.Xamarin.Core.csproj | 添加 5 个 SVG 文件为 EmbeddedResource + Views/\*\* 编译引用 |
| gamepad.svg | Resources/Images/ | 从 XEmuera 项目复制 |

#### 应用标识

| 项目 | 变更 |
|------|------|
| 应用名 | "Emuera" → "Emuera skiaX" |
| 版本 | 0.1.0 → 0.35.0 (versionCode=35) |
| APK 图标 | Emuera\icon.ico → 5 密度桶 mipmap PNG（48/72/96/144/192px） |

## [0.34.0] - 2026-05-20

### Phase 9L/9M：运行时崩溃保护 + 动态缩放完善 + 字体加载修复

#### Bug 修复

| 修复 | 文件 | 说明 |
|------|------|------|
| PrintHtml 存根 | PrintStringBuffer.cs × 2 | 废弃存根返回空数组替代 NotImplementedException |
| EmueraConsole 异常 | EmueraConsole.cs | catch(Win32Exception) 追加 catch(Exception) 覆盖 Android |
| Process.Start 崩溃 | Instraction.Child.cs | UseShellExecute try-catch 保护 Android |
| GETMEMORY/CLEARMEMORY | Creator.Method.cs | WorkingSet64 try-catch 保护返回 0 |
| GraphicsImage useGDI | GraphicsImage.cs | 静默忽略替代 NotImplementedException |
| 方向变化 reflow | AndroidMainWindow.cs | OnPaintSurface 检测尺寸变化>10px 触发 ApplyRuntimeDisplayScale |
| 触摸坐标转换 | AndroidMainWindow.cs | ToLogicalTouchPoint 物理像素→逻辑坐标（除以 ScreenDensity） |
| FontModel 字体扫描 | FontModel.cs | LoadFontsFolder 从只扫 fonts/ 改为 font/ + fonts/，支持 .ttf/.otf/.ttc |
| FontModel 别名注册 | FontModel.cs | AddFontModel 自动注册文件名→族名映射，RegisterFontAlias 新增 |

#### 功能完善

| 功能 | 说明 |
|------|------|
| 7C-P2 动态缩放 | 完整实现：FontScale + 方向变化 reflow + 触摸坐标转换 + A+/A- 按钮 |
| 7E 本地化 UI | AndroidLanguagePage + 菜单入口（日/中/英三语切换） |
| 7F 日志查看器 | AndroidLogPage + 菜单入口（读取 emuera.log + 刷新 + 复制） |

#### 跨平台编译保护（#if WINDOWS）

| 文件 | 保护内容 |
|------|---------|
| WinmmTimer.cs | DllImport(winmm.dll) |
| GraphicsImage.cs | GDrawStringGDIFallback |
| ImgUtils.cs | LoadImage() + 新增 LoadImageSK() |
| EmueraConsole.cs | ToolTip_Draw ToBitmap() |
| ConsoleStyledString.cs | DrawTo/DrawToBitmap GDI 路径 + CreateSkBitmapFromGdiBitmap |

## [0.33.0] - 2026-05-20

### Phase 9K：跨平台编译保护 + GSAVE/ShowQuestion 修复

#### Bug 修复

| 修复 | 文件 | 说明 |
|------|------|------|
| GSAVE Android 实现 | [Creator.Method.cs](file:///d:/emuera/emuera_lazyloading_selfmodified_version/Emuera/Runtime/Script/Statements/Function/Creator.Method.cs) | `#else` 分支用 `SKBitmap.Encode(Png)` 替代 `return 0` |
| ShowQuestion 对话框 | [AndroidHost.cs](file:///d:/emuera/emuera_lazyloading_selfmodified_version/Emuera.Xamarin.Android/AndroidHost.cs) | ManualResetEvent 同步等待 + AlertDialog Yes/No |
| WinmmTimer 编译保护 | [WinmmTimer.cs](file:///d:/emuera/emuera_lazyloading_selfmodified_version/Emuera/Runtime/Utils/WinmmTimer.cs) | DllImport(winmm.dll) 用 `#if WINDOWS` 保护 |
| GraphicsImage GDI 保护 | [GraphicsImage.cs](file:///d:/emuera/emuera_lazyloading_selfmodified_version/Emuera/UI/Game/image/GraphicsImage.cs) | GDrawStringGDIFallback 用 `#if WINDOWS` 保护 |
| ImgUtils 编译保护 | [ImgUtils.cs](file:///d:/emuera/emuera_lazyloading_selfmodified_version/Emuera/UI/Game/image/ImgUtils.cs) | LoadImage() 用 `#if WINDOWS` 保护，添加 LoadImageSK() |
| EmueraConsole 编译保护 | [EmueraConsole.cs](file:///d:/emuera/emuera_lazyloading_selfmodified_version/Emuera/UI/Game/EmueraConsole.cs) | ToolTip_Draw ToBitmap() 用 `#if WINDOWS` 保护 |

#### 验证

- APK 构建成功（170.7MB，0 错误）
- ADB 不可用，真机测试待后续执行

## [0.32.0] - 2026-05-20

### Phase 9J：Android 兼容性修复 + UI 功能补全

#### Bug 修复

| 修复 | 文件 | 说明 |
|------|------|------|
| GETTEXTBOX 同步 | [AndroidMainWindow.cs](file:///d:/emuera/emuera_lazyloading_selfmodified_version/Emuera.Xamarin.Android/AndroidMainWindow.cs) | ChangeTextBox 同步更新 TextBox.Text，GETTEXTBOX() 在 SETTEXTBOX 后立即可读 |
| WebP 图片回退 | [PlatformInterop.cs](file:///d:/emuera/emuera_lazyloading_selfmodified_version/emuera.xamarin/Platform/Stubs/PlatformInterop.cs) + [AndroidHost.cs](file:///d:/emuera/emuera_lazyloading_selfmodified_version/Emuera.Xamarin.Android/AndroidHost.cs) | SkiaSharp 解码失败时回退到 Android BitmapFactory（原生支持 WebP） |
| CompatiErrorLine | [Config.cs](file:///d:/emuera/emuera_lazyloading_selfmodified_version/emuera.xamarin/Platform/Config/Config.cs) × 2 | 从硬编码 true 改为 `Program.DebugMode \|\| config值`，清理调试日志 |

#### 新增组件

| 组件 | 文件 | 说明 |
|------|------|------|
| AndroidLanguagePage | [AndroidLanguagePage.cs](file:///d:/emuera/emuera_lazyloading_selfmodified_version/Emuera.Xamarin.Android/AndroidLanguagePage.cs) | 语言选择页面（日本語/简体中文/English），修改后保存到 emuera.config |
| AndroidLogPage | [AndroidLogPage.cs](file:///d:/emuera/emuera_lazyloading_selfmodified_version/Emuera.Xamarin.Android/AndroidLogPage.cs) | 解释器日志查看器（读取 emuera.log + 刷新 + 复制到剪贴板） |
| 图片解码回退委托 | [PlatformInterop.cs](file:///d:/emuera/emuera_lazyloading_selfmodified_version/emuera.xamarin/Platform/Stubs/PlatformInterop.cs) | DecodeBitmapFallback/GetBitmapInfoFallback/DecodeAnimatedFallback/GetAnimInfoFallback |

#### 修改组件

| 组件 | 修改内容 |
|------|---------|
| AndroidMainMenuPage | 添加 Language 和 View Log 菜单项 + 事件 |
| AndroidApp | 订阅 LanguageRequested/ViewLogRequested 事件，导航到新页面 |
| SharedBitmapCache | SKBitmap.Decode 失败时调用 PlatformInterop.DecodeBitmapFallback |
| SharedBitmapCache.GetInfo | SKCodec.Create 失败时调用 PlatformInterop.GetBitmapInfoFallback |
| AnimatedImageHelper | SKCodec.Create 失败时调用 PlatformInterop 回退委托 |
| AndroidHost | 初始化 BitmapFactory 回退委托 + AndroidBitmapToSKBitmap 转换方法 |
| Emuera.Xamarin.Android.csproj | 添加 AndroidLanguagePage.cs 和 AndroidLogPage.cs 编译项 |

#### 关键决策

- **GETTEXTBOX 不依赖 user32.dll**：HANDOFF/TODO 中"GETTEXTBOX user32.dll"为误报，GETTEXTBOX 仅读取 TextBox.Text 属性。真正问题是 ChangeTextBox 异步更新 textEntry.Text 但未同步 TextBox.Text
- **WebP 回退策略**：不升级 SkiaSharp（2.88.8→3.x 可能破坏渲染管线），而是添加 Android BitmapFactory 回退。通过 PNG 中间格式转换确保像素格式正确
- **CompatiErrorLine 条件启用**：DebugMode 下始终 true（容忍错误继续运行），Release 模式下尊重用户配置

## [0.31.0] - 2026-05-20

### Phase 9I：图片 LazyLoading + LRU 系统移植

从 LazyLoading 内核移植图片延迟加载和 LRU 缓存系统到 Xamarin 项目，解决内存占用过高和动图支持缺失问题。

#### 新增组件

| 组件 | 文件 | 说明 |
|------|------|------|
| SharedBitmapCache | [ConstImage.cs](file:///d:/emuera/emuera_lazyloading_selfmodified_version/emuera.xamarin/Platform/Content/ConstImage.cs) | 静态位图 LRU 缓存（max 200），替代原来每次直接解码 |
| IFileBacked | [ConstImage.cs](file:///d:/emuera/emuera_lazyloading_selfmodified_version/emuera.xamarin/Platform/Content/ConstImage.cs) | 可驱逐接口，支持动画帧的内存回收 |
| AnimatedImageHelper | [AnimatedImageHelper.cs](file:///d:/emuera/emuera_lazyloading_selfmodified_version/emuera.xamarin/Platform/Content/AnimatedImageHelper.cs) | GIF/WebP 多帧解码 |
| SpriteAnimated | [CroppedImage.cs](file:///d:/emuera/emuera_lazyloading_selfmodified_version/emuera.xamarin/Platform/Content/CroppedImage.cs) | 原生动画 Sprite，支持 Evict/Reload 生命周期 |
| AnimSpriteCache | [CroppedImage.cs](file:///d:/emuera/emuera_lazyloading_selfmodified_version/emuera.xamarin/Platform/Content/CroppedImage.cs) | 动画 LRU 缓存（max 6），超出自动淘汰最久未用的动画 |

#### 修改组件

| 组件 | 修改内容 |
|------|---------|
| ConstImage | CreateFrom 仅读尺寸（SKCodec.Info），EnsureBitmapLoaded 按需解码；Dispose 不释放 Bitmap（由 SharedBitmapCache 统一管理） |
| ASpriteSingle.Bitmap | 添加延迟加载：BaseImage.Bitmap 为 null 时调用 EnsureBitmapLoaded |
| SpriteAnime.GraphicsDraw | 添加延迟加载：BaseImage.Bitmap 为 null 时从 SharedBitmapCache 获取 |
| AppContents | SpriteMeta 表添加 DestW/DestH 列；LoadSpriteFromMeta 支持 SpriteAnimated 创建；GetSprite 触发 AnimSpriteCache.Touch；SpriteDisposeAll 清理 SharedBitmapCache |

#### SkiaSharp 2.88.x 兼容性

- `SKCodecFrameInfo.FrameRect` 在 2.88.x 中不可用，退化为清除整个画布（影响有限，大多数 GIF 使用 Keep/RestoreToPrevious 处置方法）
- `Stopwatch.GetElapsedTime()` 在 net8.0 中可用，无需适配

#### 编译验证

- `dotnet build Emuera.Xamarin.Core.csproj` → 0 错误，0 警告

#### 知识库更新

- [graphics-rendering.md](file:///d:/emuera/shared-trae/knowledge/xemuera/graphics-rendering.md)：新增 Xamarin LazyLoading + LRU 移植详情章节
- [lazyloading-features.md](file:///d:/emuera/shared-trae/knowledge/erabasic/lazyloading-features.md)：标记 SpriteAnimated 已移植

## [0.30.0] - 2026-05-19

### Phase 9G：XEmuera 风格 UI 控件 + 三轮 Bug 修复

#### 新增：XEmuera 风格工具栏

| 控件 | 说明 |
|------|------|
| ✏ edit_button | 切换式（Opacity 0.5/1），显示/隐藏 Entry 输入栏 |
| 🎮 gamepad_button | 切换虚拟手柄显示 |
| ⊞ gallery_view_button | 切换 QuickButton 快捷按钮面板 |
| ⇕ scroll_vertical_button | 切换旋转90° Slider 滚动条 |
| A+/A- | 字体缩放 |
| ☰ menu_show_button | 收起/展开工具栏按钮组 |

按钮样式：半透明黑底 `FromRgba(0,0,0,160)` + 白字 + 34×34 + CornerRadius=3，任何主题下可见。

#### Bug 修复

| # | BUG | 根因 | 修复 | 文件 |
|---|-----|------|------|------|
| 1 | **Return(0) NRE 导致无法进入游戏** | `functionList.Count==0` 时 Return 直接 return，`runScriptProc` 循环继续 `ShiftNextLine()` 访问 null | Return 设 `currentLine=null`；ShiftNextLine 加 null 保护；runScriptProc 加 null 退出检查 | Process.State.cs, Process.ScriptProc.cs |
| 2 | **滚动后点击触发错误按钮** | `MoveMouse` 中 `ScrollBar.Value != Maximum` 时 `goto end` 跳过按钮搜索，保留旧 `selectingButton` | 注释掉该检查（与 XEmuera 一致），让 `bottomLineNo` 基于 `ScrollBar.Value` 正确搜索 | EmueraConsole.cs |
| 3 | **工具栏按钮不可见** | 按钮用 `invertMainColor`（BackColor）作背景色，黑底主题下与画布同色 | 改为半透明黑底 + 白字 | AndroidMainWindow.cs |

#### 滚动条 Slider 双向同步

- `vScrollBar`（Xamarin Slider）与引擎 `ScrollBar`（Stub）双向同步
- `SyncVScrollBar()` 在 `RefreshStrings()` 和 `ScrollBar.Value` 赋值后调用
- 拖拽期间禁止反向同步（`IsDragScrollBar` 标志）

#### BADAPPLE 无法退出

用户确认是模拟器渲染速度过慢导致 AWAIT 1 几乎无检测窗口，非引擎 Bug。将禁用 demo。

## [0.29.0] - 2026-05-19

### Phase 9F-ADB 续：ADB 测试第二轮反馈修复

| # | BUG | 根因 | 修复 | 文件 |
|---|-----|------|------|------|
| 1 | **ProcessState.Return NRE** | `functionList` 为空时 `functionList[^1]` 抛出 ArgumentOutOfRangeException | 添加 `functionList.Count == 0` 空检查 | Process.State.cs |
| 2 | **HTML div 文字被图片盖住** | 之前的修复在错误的文件中（`Emuera/Runtime/Utils/EvilMask/ConsoleDivPart.cs` 被 `<Compile Remove>` 排除）；实际编译使用 `Emuera.Xamarin/Platform/Stubs/EvilMaskUtils.cs` 中的 `ConsoleDivPart`，该版本没有 `ShapeHeightOffset` 处理 | 在 `EvilMaskUtils.cs` 的 `ConsoleDivPart.DrawTo` 中，绘制子元素前临时设置 `ShapeHeightOffset = 0`，绘制后恢复 | EvilMaskUtils.cs |
| 3 | **Emuera 版 ConsoleDivPart 编译错误** | 之前在 `Emuera/Runtime/Utils/EvilMask/ConsoleDivPart.cs` 中添加了 `using XEmuera;`，但该文件在 `Emuera.csproj` 中编译，没有 XEmuera 命名空间 | 移除 `using XEmuera;` 和 `ShapeHeightOffset` 修改，恢复原始状态 | ConsoleDivPart.cs (Emuera) |

### 缩放差异调查

| 项目 | AdaptiveFont 默认值 | FontSize=14 时实际渲染 |
|------|---------------------|----------------------|
| **XEmuera** | `true` | 32px（被 AdaptiveFontSize=32 覆盖） |
| **LazyLoading** | `false` | 14px（保持用户设置） |

**结论**：缩放差异是配置差异，不是引擎 bug。XEmuera 默认启用 AdaptiveFont，将 FontSize 覆盖为 AdaptiveFontSize（32）。用户可在 emuera.config 中设置 `ADAPTIVEFONT:YES` + `ADAPTIVEFONTSIZE:28` 来匹配 XEmuera 行为。

### 关键发现：双 ConsoleDivPart 文件架构

| 文件 | 命名空间 | 编译项目 | 实际使用 |
|------|---------|---------|---------|
| `Emuera/Runtime/Utils/EvilMask/ConsoleDivPart.cs` | `MinorShift.Emuera.Runtime.Utils.EvilMask` | Emuera.csproj（桌面版） | ❌ 被 Core 项目 `<Compile Remove>` 排除 |
| `Emuera.Xamarin/Platform/Stubs/EvilMaskUtils.cs` 内嵌类 | `MinorShift.Emuera.Runtime.Utils.EvilMask` | Emuera.Xamarin.Core.csproj | ✅ Android 端实际使用 |

**教训**：修改文件前必须确认该文件是否被目标项目编译。`<Compile Remove>` 排除的文件修改不会生效。

## [0.28.0] - 2026-05-19

### Phase 9F-ADB：ADB 测试反馈修复 — 6 个 BUG 全部修复

| # | BUG | 根因 | 修复 | 文件 |
|---|-----|------|------|------|
| 1 | **BadApple 无法触摸关闭** | Android OnTouch 未映射 WinInput 键状态，GETKEYTRIGGERED 永远返回 0 | OnTouch Pressed/Released 添加 SetKeyPressed(1,13)/SetKeyReleased | AndroidMainWindow.cs |
| 2 | **LOADTEXT 返回空** | GetValidPath 返回裸相对路径 + 硬编码反斜杠，Android 上 CWD≠ExeDir | GetValidPath 跨平台分隔符 + LOADTEXT/SAVETEXT/ENUMFILES/EXISTFILE 拼接 ExeDir | EvilMaskUtils.cs + Creator.Method.cs |
| 3 | **读档看不到存档** | UseSaveFolder 默认 false，SavDir 指向游戏根目录而非 sav/ | 默认值改为 true | ConfigData.cs |
| 4 | **软键盘看不到输入栏** | textEntry.IsVisible 与 entryLayout.IsVisible 不同步 | 统一由 entryLayout 控制可见性 | AndroidMainWindow.cs |
| 5 | **BadApple 跳过后崩溃** | endNormal() 抛 CodeEE 异常，SYSTEM_TITLE 结束后无容错 | 改为 state.Begin(BeginType.TITLE) 回到标题 | Process.SystemProc.cs |
| 6 | **移动端无多语言** | Core 项目未嵌入 emuera-zhs.xml/emuera-eng.xml + EmueraLang 默认空 | 添加嵌入式资源 + 默认"简体中文" | Core.csproj + ConfigData.cs |

### 补丁修复

| # | 修复 | 文件 | 说明 |
|---|------|------|------|
| 7 | **entryLayout 编译错误** | AndroidMainWindow.cs | 局部变量→类字段，update_lastinput/clear_richText 可访问 |
| 8 | **emuera.log 保存到工作目录** | AndroidHost.cs + AndroidApp.cs + Process.SystemProc.cs | OnSleep 时自动保存日志 + endNormal 时保存日志 |

## [0.27.0] - 2026-05-19

### Phase 9F-P0/P1：Android 兼容性补全 — P0 修复 + P1-A/B 实施

#### P0 修复（已完成）

| # | 修复项 | 文件 | 说明 |
|---|--------|------|------|
| 1 | **AdaptiveFont 默认值 false** | ConfigData.cs L184 | `true` → `false`，FontSize 不再被 AdaptiveFontSize=32 覆盖 |
| 2 | **TOFULL/TOHALF 跨平台替代** | Creator.Method.cs + ExpressionMediator.cs | `#if WINDOWS` 用 StrConv，`#else` 用 Unicode 映射（数字/字母/空格全角半角 + 片假名/平假名转换） |
| 3 | **CALLPLUGIN null guard** | Instraction.Child.cs L1321 | `arg.CallFunc == null` 时设置 RESULT=0 并返回，防止 NRE |

#### P1 修复（已完成）

| # | 修复项 | 文件 | 说明 |
|---|--------|------|------|
| 4 | **emuera.config 外部存储复制** | AndroidHost.cs | 游戏目录无 emuera.config 时，从 Context.FilesDir 复制 |
| 5 | **IsActive 验证** | — | xamarin 版 IsActive 已正确（Created→IsEnabled→true），WinInput 已跨平台（内部数组），无需修改 |

#### 编译验证

- Core 项目（net8.0 + net8.0-windows）：0 错误，103 警告（均为既有）
- Desktop 项目（net8.0-windows）：0 错误

## [0.26.0] - 2026-05-19

### Phase 9F 续：审查回馈研讨 — AdaptiveFont 根因 + UI 控件差异 + 智能按钮映射

#### 知识库更新

1. **maui-android-compat.md 新增 §16-17**
   - §16：AdaptiveFont 覆盖用户 FontSize 根因分析（默认 true→FontSize=32 覆盖用户设置 14）
   - §17：UI 控件差异完整报告（缺失虚拟控制器 + 智能按钮映射 + 运行时缩放 + 3 个 Style）

2. **_index.md 更新**
   - maui-android-compat.md 核心洞见补充 AdaptiveFont 根因和 UI 控件差异

3. **反模式新增 2 条**
   - "AdaptiveFont 不影响字体大小" → 实际覆盖 FontSize
   - "XEmuera 和当前项目显示应该一样" → 缺少 3 个关键方法 + 虚拟控制器 + 缩放按钮

#### 审查回馈分析

| # | 问题 | 根因 | 修复方案 | 优先级 |
|---|------|------|---------|--------|
| 1 | Clipboard 空实现 | LazyLoading 内核无 SETCLIPBOARD/GETCLIPBOARD 指令类，排除的是 ClipboardProcessor | 保持空实现即可 | ✅ 已确认 |
| 2 | 字体/图片偏大 | AdaptiveFont=true（默认）→ FontSize 被 AdaptiveFontSize=32 覆盖 | 修改默认值为 false | P0 |
| 3 | 缺失虚拟控制器 | XEmuera 有完整虚拟控制器面板 + 缩放按钮 + 智能按钮映射 | 移植 XEmuera 实现 | P1 |
| 4 | 缺失智能按钮映射 | XEmuera 能扫描 INPUT 按钮关键词映射为虚拟控件 | 移植关键词匹配系统 | P1 |
| 5 | 缺失运行时缩放 | XEmuera 有 ApplyRuntimeDisplayScale + ReflowDisplayLines | 移植运行时缩放方法 | P1 |

#### TODO 重新整理

- 切片编号统一为 9F-P0-A/B/C → 9F-P1-A/B/C/D/E，按执行顺序排列
- 新增 9F-P0-A（AdaptiveFont 默认值修复，一行代码，影响面最大，最先做）
- 新增 9F-P1-D（虚拟控制器 XAML + Style + 事件处理器）
- 新增 9F-P1-E（智能按钮映射，INPUT→虚拟控件关键词匹配）
- 远期任务补充 7D-P1（外部字体加载增强）

## [0.25.0] - 2026-05-19

### Phase 9F：Android 兼容性补全 — 系统性自拷问与知识库更新

#### 知识库更新

1. **maui-android-compat.md 新增 §11-15**
   - §11：Sound.WMP → NAudio 迁移后 SoundStub 空实现架构（虚方法继承体系）
   - §12：PluginSystem 空实现（IPluginMethod 接口 + PluginManager 空操作）
   - §13：Clipboard 空实现（PlatformInterop 委托模式跨平台适配）
   - §14：配置系统覆盖机制（emuera.config 三层加载验证）
   - §15：显示效果差异（FontScale + AdaptiveFontSize + ScreenDensity 三重缩放根因分析）

2. **xemuera 知识库兼容性标注**
   - io-cross-platform.md：添加 LazyLoading Xamarin 适配标注（已实施/待修复 IO 问题清单）
   - ui-integration.md：添加 LazyLoading Xamarin 适配标注（软键盘输入管线 + 待修复 UI 问题）

3. **反模式新增 6 条**（SoundStub/PluginSystem/Clipboard/配置覆盖/字体偏大相关误解）

#### 自拷问发现

| # | 发现 | 优先级 | 风险 |
|---|------|--------|------|
| 1 | SoundStub 虚方法签名与 NAudio Sound 类匹配，编译兼容性已验证 | — | 低 |
| 2 | CALLPLUGIN_Instruction 缺少 null guard，GetMethod 返回 null 时可能 NRE | P0 | 中 |
| 3 | Clipboard.cs 排除后 SETCLIPBOARD/GETCLIPBOARD 指令可能缺失 | P1 | 中 |
| 4 | 软键盘"无法输入"需 ADB 日志确认根因（3 种可能） | P1 | 中 |
| 5 | BadApple 结束触发需 InputState 平台抽象（切片 2D 移动端实现） | P1 | 低 |
| 6 | emuera.config 外部存储路径不可达（MuMu 共享文件夹不在加载路径） | P1 | 中 |
| 7 | 字体偏大可能是 FontScale/AdaptiveFontSize 配置问题，非分辨率 Bug | P2 | 低 |

#### TODO 新增切片 9F

- 9F-P0：TOFULL/TOHALF 跨平台替代 + CALLPLUGIN null guard
- 9F-P1：软键盘输入检测 + BadApple 触发 + emuera.config 外部存储复制
- 9F-P2：FontScale/AdaptiveFontSize 显示差异调整

## [0.24.0] - 2026-05-18

### Phase 9E：渲染范围修复 + DebugLog 路径修复 + Config 完整同步 + ADB 测试流程建立

#### 修复项

1. **双重 density 缩放（核心问题）**
   - `AndroidMainWindow.OnPaintSurface` 将 `e.Info.Width/Height`（物理像素）直接赋给 `MainPicBox.Width/Height`
   - `DisplayUtils.PicBoxWidth/PicBoxHeight` 又通过 `GetRealLength()` 再次乘以 density
   - 导致 `ClientWidth/ClientHeight` 是实际画布尺寸的 density 倍
   - 修复：OnPaintSurface 中将物理像素除以 density 转回逻辑像素
   - **验证结果**：渲染范围填满整个屏幕高度 ✅

2. **ScreenDensity 恢复设备实际值**
   - 从硬编码 `1.0` 改为 `context.Resources.DisplayMetrics.Density`

3. **DebugLog 路径修复**
   - `Process.DebugLog` 使用相对路径 `"debug_log.log"`，在 Android 上写入只读目录
   - 修复：改为 `Program.ExeDir + "debug_log.log"`

4. **Config 双类完整同步**
   - `SyncToRuntimeConfig()` 从 12 个属性扩展到 63 个属性
   - Runtime.Config.Config 的 `private set` 改为 `internal set`（63 个属性）

5. **Runtime.Config.Config.nameDic 同步**
   - **问题**：ERB 脚本解析时大量 NullReferenceException，堆栈指向 `GetConfigName()`
   - **根本原因**：Runtime.Config.Config.nameDic 是 private static，SyncToRuntimeConfig() 未同步该字段
   - **修复**：
     - Runtime.Config.Config.cs:21：nameDic 从 `private static` 改为 `internal static`
     - Xamarin.Platform.Config.Config.cs:290：SyncToRuntimeConfig() 中添加 `Runtime.Config.Config.nameDic = nameDic;`
   - **结果**：消除了大量脚本解析错误

6. **CompatiErrorLine 强制启用**
   - 在 Xamarin 和 Runtime Config 中强制设置 `CompatiErrorLine = true`
   - ⚠️ **重要提醒**：此设置仅用于调试阶段！
     - 启用时：将编译错误推迟到运行时抛出
     - 禁用时：编译时立即抛出错误（正式版本行为）
     - 正式版本必须禁用！

#### ADB 测试流程建立

- ✅ 成功连接 MuMu 模拟器：`127.0.0.1:16384`
- ✅ 成功拉取 emuera.log：`adb pull /storage/emulated/0/emuera/eratw-chs/emuera.log`
- ✅ 日志分析：定位到 nameDic 同步问题
- ✅ 创建知识库条目：[adb-testing.md](file:///d:/emuera/shared-trae/knowledge/xemuera/adb-testing.md)
  - 包含完整测试流程、常见问题、Logcat 过滤技巧、应用私有文件访问

#### 知识库更新

- [android-rendering.md](file:///d:/emuera/shared-trae/knowledge/xemuera/android-rendering.md)：新增密度缩放、坐标系统、渲染管线等知识条目
- [compati-errorline.md](file:///d:/emuera/shared-trae/knowledge/xemuera/compati-errorline.md)：新增 CompatiErrorLine 配置说明（调试用途、启用条件、风险提示）
- [adb-testing.md](file:///d:/emuera/shared-trae/knowledge/xemuera/adb-testing.md)：新增完整 ADB 测试流程

#### 验证结果

- ✅ 渲染范围验证：文本填满整个屏幕高度
- ✅ 脚本解析核心 Bug 修复：nameDic 同步问题已修复

#### 待处理

- 🔴 分析完整 emuera_android.log（1.26MB，引擎页面运行时拉取），分类所有加载错误
- 🔴 修复所有已知加载错误（ConsoleButtonString.CalcWidth NRE 等）
- ⚠️ UI 交互验证：触摸/点击/输入（需先完成脚本加载错误修复）
- ⚠️ CompatiErrorLine 条件化（调试模式启用，正式版本禁用）

## [0.23.0] - 2026-05-18

### Phase 9D：SQL 懒加载 + Config 同步 + 工具栏/滚动修复 + 完整启动验证

#### 修复项

1. **OOM 崩溃（lowmemorykiller 杀掉 5.7GB RSS 进程）**
   - Xamarin 版 AppContents.LoadContents 全量解码 25421 个 sprite → 内存爆炸
   - 修复：重写 AppContents.cs，使用 SQLite 内存数据库存储元数据，启动时只解析 CSV 写入 SQLite，不解码任何图片
   - 修复：ConstImage.CreateFrom 改为只读尺寸信息（SKCodec.Info），延迟到实际使用时解码
   - 修复：GetSprite 改为按需从 SQLite 查询并解码

2. **Config 双类不同步**
   - Xamarin 版 `MinorShift.Emuera.Config` 和 Runtime 版 `MinorShift.Emuera.Runtime.Config.Config` 是两个独立的类
   - Runtime 代码引用 Runtime 版 Config，但只有 Xamarin 版被初始化
   - 修复：在 Xamarin Config.SetConfig() 中同步 SystemSaveInBinary 到 Runtime.Config.Config
   - 修复：Runtime.Config.Config.SystemSaveInBinary setter 改为 internal

3. **GetConfigItem 大小写比较 Bug**
   - `key.ToUpper()` 后与 `item.EngText`（未转大写）比较，导致英文配置键无法匹配
   - 修复：所有比较项都调用 `.ToUpper()`

4. **工具栏布局修复**
   - ☰ 汉堡菜单按钮移入悬浮工具栏（与其他按钮并排），不再被侧边栏遮挡
   - 移除 AndroidApp.cs 中的浮动 ☰ 按钮（冗余，无法关闭侧边栏）

5. **滚动修复**
   - 移除本地 scrollBarValue/scrollBarMaximum 字段，直接使用 ScrollBar stub 属性
   - 修复滚动时 Value 与 Maximum 不同步导致无法滚动

6. **渲染范围修复**
   - 添加 canvasView.IgnorePixelScaling = true，确保渲染填满整个屏幕

#### 真机验证

- ✅ eratw-chs 完整启动成功（Process.Initialize: SUCCESS, starting TITLE）
- ✅ SQL 懒加载：25421 sprite 元数据写入 SQLite，0 图片解码
- ✅ 进程存活（无 OOM 崩溃）
- ✅ 工具栏布局正确：☰/⌨/A+/A- 四按钮右上角并排
- ✅ Canvas 区域占满屏幕 [0,0][1920,1059]

#### 知识库更新

- [android-engine-init.md](file:///d:/emuera/shared-trae/knowledge/xemuera/android-engine-init.md)：新增 OOM/SQL 懒加载、Config 双类、GetConfigItem 大小写、ConstImage 延迟解码等知识条目

## [0.22.0] - 2026-05-18

### Phase 9C：AOT 崩溃修复 + Lang 初始化顺序 + 诊断日志 + 真机验证

#### 修复项

1. **Release 模式 AOT GUID 不匹配崩溃（SIGABRT）**
   - `dotnet publish` Release 模式启用 AOT 编译，缓存过期 GUID 导致运行时崩溃
   - 错误：`module 'XEmuera.Android.dll.so' is unusable (GUID of dependent assembly Xamarin.Forms.Platform.Android doesn't match)`
   - 修复：Debug 和 Release 均设置 `AotAssemblies=false` + `RunAOTCompilation=false` + `EmbedAssembliesIntoApk=true` + `AndroidUseSharedRuntime=false`
   - 需清理 obj/bin 目录后重新构建

2. **Lang.SetLanguage() ArgumentNullException**
   - `Lang.SetLanguage()` 在 `ConfigData.LoadConfig()` 之前调用，`Config.Config.EmueraLang` 为 null
   - 修复：将 `Lang.SetLanguage()` 移到 `ConfigData.Instance.LoadConfig()` + `JSONConfig.Load()` 之后
   - 修复：在 `Lang.SetLanguage()` 中添加 `string.IsNullOrEmpty(Config.Config.EmueraLang)` null guard

3. **MANAGE_EXTERNAL_STORAGE 权限无法通过 ADB 授予**
   - `adb pm grant` 抛 `SecurityException: not a changeable permission type`
   - 修复：通过 ADB uiautomator dump + input tap 自动化 Android Settings UI 授予

4. **Debug 模式 APK 不更新**
   - `EmbedAssembliesIntoApk=false` 时 `dotnet publish` 不重新生成 APK
   - 修复：Debug 配置也设置 `EmbedAssembliesIntoApk=true` + `AndroidUseSharedRuntime=false`

5. **Android.Util.Log 在 Core 项目不可用**
   - Core 项目 TFM 是 net8.0，不含 Android API
   - 修复：所有日志改用 `System.Diagnostics.Debug.WriteLine("[Emuera] ...")`

#### 新增功能

1. **Process.Initialize 诊断日志** — 7+ 个关键阶段 Debug.WriteLine
   - start / AppContents / KeyMacro+Replace+Rename / GAMEBASE.CSV / other CSVs / ERH files / ERB files
   - 异常处理器 / labelDic null 检查 / 成功点

2. **EmueraConsole.Initialize 诊断日志**
   - emuera.Initialize() 调用前后 + callEmueraProgram 完成点

3. **AppContents.LoadContents 诊断日志**
   - ContentDir 路径 + CSV 文件数量 + 目录不存在跳过

4. **状态标签** — AndroidHost.UpdateStatus() 显示引擎初始化状态

#### 真机验证

- ✅ APK 成功安装到 Android 模拟器（ADB 127.0.0.1:16384）
- ✅ MANAGE_EXTERNAL_STORAGE 权限通过 Settings UI 授予
- ✅ 引擎成功启动 eratw-chs，进入脚本预加载阶段
- ✅ logcat 确认初始化流程：start → AppContents(93 CSV) → KeyMacro → GAMEBASE.CSV → other CSVs → ERH → ERB
- ⚠️ 脚本预加载阶段报 IO/逻辑错误，需进一步调试

#### 知识库更新

- [android-engine-init.md](file:///d:/emuera/shared-trae/knowledge/xemuera/android-engine-init.md)：新增 6 个知识条目 + 6 个反模式

## [0.21.0] - 2026-05-17

### Preload 缓存 miss 修复 + eratw-chs 闪退诊断 + UI 功能

#### 修复项

1. **Preload.GetFileLines 缓存 miss 抛 KeyNotFoundException**
   - `EraStreamReader.OpenOnCache` 内部 `Preload.GetFileLines(path)` 用 `files[path]` 索引器，缓存未命中时直接崩溃
   - 添加 `Preload.TryGetFileLines(path, out lines)` 方法
   - `OpenOnCache` 改为：缓存命中用缓存，缓存 miss 降级到 `File.ReadAllLines` + `EncodingHandler.DetectEncoding`

2. **Lang 未初始化** — `Program.Init()` 前未调用 `Lang.LoadLanguageFiles()` + `Lang.SetLanguage()`
   - 取消注释 `Emuera.Xamarin/Platform/Program.cs` 中的 Lang 初始化调用

3. **engineInitialized 竞态条件** — `AndroidHost.StartGame()` 中 `engineInitialized = true` 在 `Task.Run` 之前设置
   - 移到 Task 完成后设置，消除并发访问未初始化对象的风险

4. **Application.DoEvents() 平台不兼容** — `System.Windows.Forms.Application` 仅 Windows 可用
   - 替换所有 `Application.DoEvents()` 为 `PlatformInterop.DoEvents()`（5 处）
   - 涉及文件：EmueraConsole.cs (3) + EmueraConsole.Print.cs (1) + MainWindow.cs (1)

5. **console.Initialize() fire-and-forget** — 异步 Task 异常被吞
   - 改进 `AndroidHost.StartGame()` 中的异常捕获和日志记录

#### 新增功能

1. **悬浮工具栏** — 叠加在 SKCanvasView 上方（InputTransparent）
   - ☰ 菜单按钮 → 打开 FlyoutPage 侧边菜单
   - ⌨ 编辑按钮 → 显示/隐藏输入条
   - A+/A- 缩放按钮 → 运行时 FontScale 调整（0.5~5.0，步进 0.1）

2. **输入条修复**
   - `HorizontalOptions = FillAndExpand`
   - 添加发送按钮 ▶
   - `Completed` 事件处理

3. **沉浸式全屏**
   - `mainLayout` 黑色背景（消除底部灰色空白条）

#### 对比调研

- 对比 m-emuera 平台实现（Preload/初始化/DoEvents/AWAIT/SQLite）
- 更新知识库：[android-engine-init.md](file:///d:/emuera/shared-trae/knowledge/xemuera/android-engine-init.md)

## [0.20.0] - 2026-05-17

### Android 引擎启动修复（框架自纠）

勘测 XEmuera-1 和 m-emuera 的引擎初始化流程，对比发现并修复以下问题：

#### 修复项

1. **ConfigsText/StringsText 资源命名空间不匹配** — `MissingManifestResourceException`
   - `Emuera.Xamarin.Core.csproj` 中 .resx 文件缺少 `LogicalName`，导致嵌入资源名与 Designer.cs 期望的不一致
   - 为 4 个 .resx 文件添加 `<LogicalName>XEmuera.Resources.{name}.resources</LogicalName>`

2. **WinmmTimer 在 Android 上 DllNotFoundException** — `TypeInitializationException`
   - `Emuera\Runtime\Utils\WinmmTimer.cs` 直接 P/Invoke `winmm.dll`，Android 上不存在
   - 在 csproj 中排除原版文件，创建 `WinmmTimer.Runtime.cs` stub（平台检查 + fallback 到 `Environment.TickCount64`）

3. **FontModel.Load() 未被调用** — `NullReferenceException` in StringMeasure
   - XEmuera-1 在 `GameUtils.Load()` 中调用 `FontModel.Load()`（App 启动时），我们遗漏了
   - 在 `AndroidHost.StartGame()` 中添加 `FontModel.Load()` 调用 + `GameFolderModel.Instance` 初始化
   - `GameFolderModel.Instance` setter 改为 `internal set`

4. **Program.Init() 覆盖 AndroidPathProvider** — 路径回退到内部存储
   - `Program.Init()` 中 `GlobalStatic.PathProvider = new DefaultPathProvider(...)` 覆盖了 Android 设置的路径
   - 改为 `if (GlobalStatic.PathProvider == null)` 条件赋值

5. **OnPaint 中 SpinLock 缺少 Exit** — `LockRecursionException` 导致崩溃
   - `EmueraConsole.OnPaint()` 中 `displayLineListSpinLock.Enter()` 后无 `Exit`
   - 用 try-finally 包裹，确保锁释放

6. **导航栏常驻问题** — 游戏页面应隐藏 NavigationBar
   - 游戏页面设置 `NavigationPage.SetHasNavigationBar(gamePage, false)`
   - 添加浮动菜单按钮（☰），点击切换 FlyoutPage 侧边栏

7. **Android 存储权限** — `MANAGE_EXTERNAL_STORAGE`
   - `AndroidManifest.xml` 添加 `MANAGE_EXTERNAL_STORAGE` 权限声明
   - `MainActivity` 添加运行时权限请求逻辑（Android 11+ 和低版本分别处理）
   - `AndroidGameListPage` 添加权限检查

#### 已知问题

- 游戏画面渲染待验证（SpinLock 修复后应正常，需模拟器确认）
- QuickButton 悬浮 UI 组件待实现（XEmuera 有画布上方的工具按钮栏）
- 沉浸式全屏模式待完善（参考 XEmuera-1 的 IsEmueraPage 机制）

## [0.19.1] - 2026-05-17

### Slice 7C-P1-3：ConfigDialog FontScale 编辑 ✅

- DesktopConfigDialog：添加 `float` 类型专门处理分支
  - 输入框标题显示范围提示 `(0.5~5.0)`
  - FontScale 范围验证：超出 0.5~5.0 时弹窗报错并恢复默认值
- AndroidConfigPage：修复 `float` 类型处理 Bug
  - BuildConfigItems：添加 `ConfigItem<float>` 分支，保留原始 float 值和 `IsFloat` 标记
  - OnItemSelected：添加 `IsFloat` 分支，使用 `float.TryParse` 解析输入
  - FontScale 范围验证：超出 0.5~5.0 时红色提示
  - 数值键盘（`Keyboard.Numeric`）+ 范围提示标签
  - 内部 ConfigItem 类添加 `IsFloat` 属性
- 编译验证：四项目 0 错误

## [0.19.0] - 2026-05-17

### Slice 4A-Xamarin：ContentPage MainWindow + SKCanvasView + 触摸 + 输入 + 导航 ✅

#### 4A-X1：MainWindow extends ContentPage ✅

- Stub MainWindow 从 `internal class MainWindow` 改为 `internal class MainWindow : ContentPage`
- 添加 `new` 关键字到 Title/BackgroundColor/IsEnabled/Focus 以抑制 CS0108 警告
- 使 XEmuera-1 的 MainWindow（也是 ContentPage）可正确继承

#### 4A-X2：SKMouseButton 命名空间迁移 + Views 纳入 Core ✅

- SKMouseButton 从 `SkiaSharp.Views.Forms` 命名空间迁移到 `MinorShift.Emuera.UI.Framework.Forms`
- 消除 Core stub 与 NuGet 包的类型冲突
- SelectableLabel.cs + TintImageButton.cs 纳入 Core 编译
- XAML 页面（ConfigPage/SettingsPage 等）仍排除（Core 不支持 XAML 代码生成）
- Android extern alias `skviews` 恢复，仅用于 NuGet 独有类型

#### 4A-X3~X5：SKCanvasView + 触摸 + 输入连接 ✅

- OnPaintSurface 连接 MainPicBox.Width/Height 到 canvas 尺寸
- QuickButtonGroup override 返回视觉布局（QuickButtonGroup 类型）
- quickButton_Clicked override 处理按钮点击 → PressEnterKey
- quickButtonScrollView 连接到视觉 ScrollView
- MainWindow stub: quickButton_Clicked 改为 virtual

#### 4A-X6：MainMenu_Reboot/GotoTitle + Reboot ✅

- AndroidMainWindow.Reboot() 调用 console.GotoTitle()（后台线程）
- MainMenu_Reboot() 委托给 Reboot()
- MainMenu_GotoTitle() 检查 IsInProcess 后 Reboot()
- MainWindow stub: MainMenu_Reboot/GotoTitle 改为 virtual

#### 4A-X7：编译验证 ✅

- Core: 0 错误, 105 警告
- Desktop: 0 错误, 4 警告
- Android: 0 错误, 4 警告

### Slice 5B-E：音频路径替换 + ERB 指令验证 ✅

#### 5B-E1：音频路径替换 ✅

- Creator.Method.cs ExistSoundMethod: `Path.GetFullPath(".\\sound\\")` → `Program.MusicDir`
- Instraction.Child.cs PLAYSOUND/PLAYBGM: 同上
- `File.Exists` → `Program.FileExists`（大小写不敏感查找）
- Program.cs: 添加 `FileExists()` 委托到 `FileUtils.Exists()`

#### 5B-E2~E5：音频 ERB 指令验证 ✅

- 所有音频 ERB 指令已存在于 LazyLoading 源码中（无需额外移植）：
  - PLAYSOUND, PLAYBGM, STOPBGM, STOPSOUND (Instraction.Child.cs)
  - GETSOUNDORBGMINFO, ISPLAYINGSOUND, SOUNDCONTROL (Creator.Method.cs)
  - ISPLAYINGBGM, BGMCONTROL (Creator.Method.cs)

### Slice 7C-P1：FontScale 配置属性 ✅

- Config.cs: 添加 `FontScale` 属性 (float)，在 SetConfig 中应用
  - `if (FontScale != 1f) { FontSize = (int)(FontSize * FontScale); LineHeight = (int)(LineHeight * FontScale); }`
- ConfigData.cs: 添加 FontScale 默认值 (1f) 到两个构造函数
- ConfigData.cs: 添加 FontScale switch case (SingleFloatTerm)
- 与 XEmuera-1 行为一致

### 移出编译的文件（未删除，待后续处理）

- `Platform/Forms/**` — Core csproj 排除（XAML 依赖，无法在 net8.0 TFM 编译）
- `EraPictureBox.cs` — Core csproj 排除（依赖 SkiaSharp.Views.Forms NuGet，不支持 net8.0 TFM）
- XAML 页面（ConfigValuePopup/TxtFilePage/SettingsPage/MainMenuPage/LanguagePage/GameItemPage/FontSettingsPage/ConfigSettingsPage/ConfigPage/AboutPage/App/MainPage）— Core csproj 排除

## [0.18.0] - 2026-05-17

### Slice 8D：Android 功能补全 ✅

#### 8D-1：Android IPathProvider ✅

- 创建 `AndroidPathProvider : IPathProvider`
  - `ExeDir`：`ExternalStorageDirectory/emuera/`（支持 `gamePath` 参数覆盖）
  - `TempDir`：`Context.CacheDir.AbsolutePath`
  - `ExeName`：`PackageManager.GetPackageInfo` 获取包名
  - `ResolveDir()`：大小写不敏感目录回退（erb/ERB）

#### 8D-2+8D-3：触摸事件转发 + 文本输入 ✅

- 创建 `AndroidMainWindow : MainWindow`
  - SKCanvasView + Entry + QuickButton 布局
  - SKCanvasView.Touch → EmueraConsole 鼠标事件转换
  - 长按模拟右键（500ms LongPressTimer）
  - 垂直拖动 → ScrollBar 滚动
  - Entry.Completed → PressEnterKey
  - extern alias `skviews` 解决 Core stub 与 NuGet 类型冲突
  - MainWindow override：Invoke/Refresh/Close/update_lastinput/clear_richText/ShowConfigDialog

#### 8D-4：游戏列表 + 侧边菜单 ✅

- `AndroidGameItemModel`：扫描 ExeDir 下含 ERB/erb 子目录的文件夹
- `AndroidGameListPage`：ListView 游戏列表，下拉刷新，点击启动
- `AndroidMainMenuPage`：FlyoutPage 侧边菜单（Reboot/GotoTitle/Settings）
- `AndroidApp`：FlyoutPage 组合菜单+游戏列表+游戏页面
- `AndroidHost.StartGame(gamePath)`：延迟引擎初始化

#### 8D-5：配置页面 ✅

- `AndroidConfigPage`：ListView 显示 ConfigData 所有配置项
- bool 配置点击切换，string/int 配置点击弹出编辑页
- Save & Close 按钮保存配置
- MainWindow.ShowConfigDialog() → 导航到配置页

#### 8D-6：APK 打包验证 ✅

- `dotnet publish -c Debug -f net8.0-android` 成功
- 生成 `Emuera.SkiaX-Signed.apk`（~30.5 MB）
- 编译验证：0 错误，4 警告

### Slice 5B-D：Desktop 音频验证 ✅

- 确认 NAudio NuGet 已集成（NAudio 2.2.1 + NAudio.Vorbis 1.5.0 + NVorbis 0.10.5）
- 确认 NAudioSound.cs 已存在，继承 Sound 虚方法基类
- 确认 DesktopHost.InitEmuera() 已初始化 16 通道 Sound + 1 BGM 通道
- 编译验证：0 错误

## [0.17.0] - 2026-05-17

### Slice 8C：Android 项目创建 ✅

- 创建 `Emuera.Xamarin.Android` 项目（net8.0-android SDK 风格 Exe）
- 引用 Core 项目 + Xamarin.Forms 5.0.0.2478 + Xamarin.Essentials 1.7.3 + Xamarin.AndroidX.Media
- `MainActivity.cs`：FormsAppCompatActivity + 沉浸式模式（SystemUiFlags.ImmersiveSticky）
- `AndroidHost.cs`：PlatformInterop 委托初始化（AlertDialog 替代 MessageBox、ClipboardManager 替代 Clipboard、SKPaint 文本测量）
- `AndroidApp.cs`：Xamarin.Forms Application 子类（ContentPage 占位）
- `AndroidManifest.xml`：minSdk=23, targetSdk=34, 外部存储权限
- Resources/：styles.xml + colors.xml + strings.xml + mipmap icon（引用 Xamarin 项目 app_icon.png）
- Core 项目添加 `InternalsVisibleTo Include="XEmuera.Android"`
- `#if ANDROID_APP` 条件编译符号

### Slice 5B-A：Android 音频实现 ✅

- 创建 `AndroidSound : Sound`（继承 SoundStub 虚方法基类）
- override 13 个虚方法：play/stop/pause/resume/close/isPlaying/setVolume/getVolume/setSpeed/getSpeed/SetPreservePitch/GetTotalTime/GetCurrentTime
- Android MediaPlayer 封装：WAV/OGG/MP3 播放、音量映射（0-100→0.0-1.0）、循环播放（MediaPlayer.Looping）、暂停/恢复（CurrentPosition 保存/恢复）
- 变速限制：MediaPlayer 无原生变速 API，`setSpeed()` 空实现
- AndroidHost.InitEmuera()：初始化 16 通道 Sound + 1 BGM 通道
- 编译验证：0 错误，2 警告（SystemUiVisibility 已过时）

## [0.16.0] - 2026-05-17

### Slice 8A：Core 项目创建 ✅

- 创建 `Emuera.Xamarin.Core` 项目（net8.0 + net8.0-windows 双目标类库）
- Runtime/ 编译引用：`<Compile Include="../Emuera/Runtime/**/*.cs" />` + 排除 Windows-only 文件
- Platform/ + Drawing/ + Models/ + Forms/ 编译引用
- Using Alias：12 个类型别名（Color/Rectangle/Point/Size/FontStyle/Brush/Pen/Font 等）+ 12 个命名空间转发
- Stubs：RikaichanStub（空实现）、SoundStub（no-op）、SystemSoundsStub（no-op）
- NuGet：SkiaSharp + SkiaSharp.Svg + Microsoft.Data.Sqlite + Xamarin.Forms（ExcludeAssets=build）
- InternalsVisibleTo：`XEmuera.Desktop`
- `#if WINDOWS` 守卫：DrawBitmapUtils/DrawTextUtils 的 System.Drawing 互操作
- Creator.Method：`#if WINDOWS` 守护 SKBitmap.ToBitmap()，修复 SKColor→Color 隐式转换

### Slice 8B：Desktop 项目创建 ✅

- 创建 `Emuera.Xamarin.Desktop` 项目（net8.0-windows WinExe）
- 引用 Core 项目 + WinForms + SkiaSharp.Views.WindowsForms
- Desktop-only 文件：DesktopHost + DesktopConfigDialog + DesktopDebugDialog + VersionUpdate + SoundAndBgm
- `#if DESKTOP_APP` 分离入口点：Program.MainDesktop 只在 Desktop 项目中编译
- 移除 Desktop 文件中的 `using System.Drawing;`：避免 Using Alias 冲突

### 额外 WinForms 依赖清理

- App.cs：`Application.DoEvents()` → `PlatformInterop.DoEvents()`
- Dialog.cs：`MessageBox.Show()` → `PlatformInterop.ShowMessage()`/`ShowMessageWithTitle()`/`ShowQuestion()`
- EmueraConsole.cs：`Control.MousePosition` → `PlatformInterop.GetMousePosition()`
- Program.cs：`MessageBox.Show()` → `PlatformInterop.ShowMessageWithTitle()`，Main→MainDesktop + `#if DESKTOP_APP`
- PlatformInterop：添加 `ShowMessageWithTitle` 委托

### 三项目编译验证 ✅

- Emuera.Xamarin（原项目）：0 错误
- Emuera.Xamarin.Core（双目标）：0 错误
- Emuera.Xamarin.Desktop：0 错误

## [0.15.0] - 2026-05-19

### Slice 8A-P1：Runtime/ WinForms 依赖清理 ✅

- 创建 `PlatformInterop` 静态类（委托式平台抽象）：
  - `DoEvents`：替代 `Application.DoEvents()`
  - `ShowMessage`：替代 `MessageBox.Show()`
  - `ShowQuestion`：替代 `MessageBox.Show(..., YesNo)` + `DialogResult`
  - `MeasureText`/`MeasureTextWidth`：替代 `Bitmap+Graphics+MeasureString`（GDI+ → Skia SKPaint.MeasureText）
  - `GetMousePosition`：替代 `Control.MousePosition`
  - `SetClipboardText`：替代 `Clipboard.SetDataObject()`
  - `GetProductVersion`：替代 `Application.ProductVersion`
- DesktopHost：初始化所有 PlatformInterop 委托（WinForms 实现）
- 替换 7 个 Runtime/ 文件中的 WinForms 调用：
  - ErbLoader.cs：`Application.DoEvents()` → `PlatformInterop.DoEvents()`
  - ErhLoader.cs：3× `Application.DoEvents()` → `PlatformInterop.DoEvents()`
  - Config.cs：`MessageBox.Show()` → `PlatformInterop.ShowMessage()`
  - Instraction.Child.cs：`MessageBox.Show(YesNo)` → `PlatformInterop.ShowQuestion()`
  - VariableEvaluator.cs：`MessageBox.Show()` → `PlatformInterop.ShowMessage()`
  - Creator.Method.cs：`Bitmap+Graphics+MeasureString` → `PlatformInterop.MeasureText()` + `Control.MousePosition` → `PlatformInterop.GetMousePosition()`
  - Sys.cs：`Application.ProductVersion` → `PlatformInterop.GetProductVersion()`
- 移除 7 个 Runtime/ 文件中的 `using System.Windows.Forms;`
- **Runtime/ 编译文件不再有任何直接 WinForms 依赖**

## [0.14.0] - 2026-05-19

### Slice 8A-P0：System.Drawing 值类型垫片 ✅

- 创建 `XEmuera.Drawing.Color` 结构体：
  - ARGB uint 内部存储，`FromArgb`/`FromName`/`ToArgb`/`ToSKColor`/`FromUint`
  - 60+ 命名颜色（Transparent/Black/White/Red/Green/Blue 等）
  - 隐式转换 `SKColor` ↔ `Color`
  - `#if WINDOWS` 条件编译：隐式转换 `System.Drawing.Color` ↔ `Color`
- 创建 `XEmuera.Drawing.Rectangle` 结构体：
  - `Contains(int,int)`/`Contains(Point)`/`IntersectsWith`/`Intersect`/`Union`/`Inflate`/`Offset`
  - `#if WINDOWS` 条件编译：隐式转换 `System.Drawing.Rectangle` ↔ `Rectangle`
- 创建 `XEmuera.Drawing.Point` 结构体：
  - `Offset`/`IsEmpty`/`+/- Size` 运算符
  - `#if WINDOWS` 条件编译：隐式转换 `System.Drawing.Point` ↔ `Point`
- 创建 `XEmuera.Drawing.Size` 结构体：
  - `IsEmpty`/`+/-` 运算符
  - `#if WINDOWS` 条件编译：隐式转换 `System.Drawing.Size` ↔ `Size`
- csproj：添加 Using Alias `Color`/`Rectangle`/`Point`/`Size` 全局替换
- SkiaPfc：移除 `System.Drawing.FontFamily` 依赖，`Families` 返回 `SkiaFontFamily[]`
- InstalledFontCollection：同上 FontFamily 清理
- ColorExtensions：添加 `ToFormsColor()` 扩展方法（Xamarin.Forms 互操作）
- DrawTextUtils：添加 `Font` 隐式转换 from `System.Drawing.Font`（`#if WINDOWS`）
- 修复 Runtime/ Config.cs：移除 `System.Drawing.FontFamily[]` 强转
- 修复 Runtime/ Creator.Method.cs：显式 Point 转换
- 修复 Platform/ stubs：使用 alias 类型替代 `System.Drawing.*`
- **关键发现**：`#if NET8_0_WINDOWS` 不存在，正确符号是 `#if WINDOWS`

## [0.13.0] - 2026-05-17

### G6：GDI 注释残留清理 ✅

**第一次提交**（删除 9402 行）：
- 删除 5 组完全注释掉的 GDI 对话框文件（15 个文件）：
  ConfigDialog.cs/.Designer.cs/.resx、DebugDialog、DebugConfigDialog、ClipBoardDialog、ColorBox
- 清理 4 处 `//using System.Drawing` / `//using System.Drawing.Imaging` 注释残留
- 清理 8 处 `//xxx.Dispose()` 注释残留（Program.cs/Config.cs/AppContents.cs/EmueraConsole.cs/GraphicsImage.cs/StringMeasure.cs）
- 清理 Program.cs 图标加载注释块、AppContents.cs 超大图片注释块、Config.cs Font Dispose 注释块

**第二次提交**：
- 清理 EmueraConsole.cs 旧 ConsoleState 枚举值（WaitKey/WaitInteger 等 13 个）
- 清理 EmueraConsole.cs `getGraphics()` 方法注释块（GDI CreateGraphics）
- 清理 EmueraConsole.cs `selectingButton`/`lastSelectingButton` 注释字段声明
- 清理 GraphicsImage.cs LockGraphics/UnlockGraphics/created/locked/fontStyle 注释块

## [0.12.0] - 2026-05-17

### Slice 5A-D1：IO 跨平台适配 ✅

- `FileUtils.GetDirectories()`：新增方法，为跨平台目录枚举预留扩展点
- `AppContents.CreateSpriteFromFileDynamic()`：`System.IO.File.Exists` → `FileUtils.Exists(ref filepath)`（大小写不敏感）
- `Config.getFiles()`：`Directory.GetDirectories` → `FileUtils.GetDirectories`（统一 IO 入口）
- `GameItemModel.Load()`：`Directory.GetDirectories` → `FileUtils.GetDirectories`（统一 IO 入口）
- `Program.cs`：添加 `using XEmuera;` 以访问 FileUtils
- `Program.WorkingDir`：从 `= ""` 改为 `=> GlobalStatic.PathProvider?.ExeDir ?? ""`

### Slice 5C-D1：SqlManager + 资源清理 ✅

- `GlobalStatic.Reset()`：添加 Sound/Bgm 停止清理 + `SqlManager.CloseAll()` + `AppContents.SpriteDisposeAll(true)`
- `DesktopHostForm`：添加 `FormClosing` 事件，退出时调用 `GlobalStatic.Reset()`
- SqlManager 已通过 `**\*.cs` 通配符自动编译，`Microsoft.Data.Sqlite` NuGet 已存在
- `IPathProvider.SqlDir` 已定义，`Config.SavDir` 路径链完整

## [0.11.0] - 2026-05-18

### Phase 7D-P1：外部字体加载增强 ✅

- `Program.LoadFontFiles()`：扫描 `font/` + `FONT/` + `fonts/` 三个目录
- `Program.LoadFontFiles()`：FamilyName 去重（避免同名字体重复加载）
- `Program.LoadFontFiles()`：自动注册 fileName 和 FamilyName 为别名
- `FontFactory.fontAliases`：新增别名字典（`Dictionary<string, SKTypeface>`）
- `FontFactory.RegisterFontAlias()`：公开方法，供外部注册别名
- `FontFactory.RegisterKnownAliases()`：内置 JP/CN 字体名映射（`ＭＳ ゴシック`↔`MS Gothic` 等 5 条）
- `FontFactory.CreateTypefaceWithFallback()`：在系统字体查找后、CustomTypefaces 遍历前插入别名解析
- `FontFactory.ClearFont()`：清理别名 + 重新注册已知别名

### Phase 5B-D1：NAudio 音频系统集成 ✅

- 添加 NAudio 2.2.1 + NAudio.Vorbis 1.5.0 NuGet 包
- 移除 csproj 中对 Sound.NAudio.cs / NAudio_LoopStream.cs / SoundTouch 的排除
- 移除 xamarin SoundAndBgm.cs 空壳存根
- `GlobalStatic.Sound/Bgm`：类型从 `GameProc.Sound` 改为 `Runtime.Utils.Sound`
- 完整音频支持：WAV/OGG/MP3 播放、音量控制、暂停/恢复、变速、混音
- 编译验证：0 错误，72 警告

## [0.10.0] - 2026-05-18

### Phase 4A-D22：P1-P2 快速修复 ✅

- `AnimeTimer`：从硬编码 0 改为 `redrawTimer.Enabled ? (long)redrawTimer.Interval : 0`
- `OutputLog(string, bool)`：从空壳改为委托 `OutputLog(string)`
- `pointingStrings`：添加 `HashSet<ConsoleButtonString>` 支持多按钮重叠高亮
- `SetTextBoxPos`：从空壳改为 `DesktopMainWindow` override + 坐标裁剪
- `WinmmTimer`：恢复 `winmm.dll` P/Invoke（`timeGetTime`/`timeBeginPeriod`），跨平台 fallback 到 `Environment.TickCount64`

### Phase 4A-D23：GotoTitleAndLoadAndRepeatInput + Ctrl+Z ✅

- `GotoTitleAndLoadAndRepeatInput()`：完整 Ctrl+Z 回退重放实现
- `DesktopHostForm`：Ctrl+Z 键盘快捷键处理
- `Config.Ctrl_Z_Enabled`：委托到 Runtime Config
- 编译验证：0 错误，64 警告

## [0.9.0] - 2026-05-18

### Phase 4A-D20：桌面端 P0-P1 修复 + PressPrimitiveKey 恢复 ✅

- `BackgroundColor` setter：从空壳改为实际存储 SKColor
- `ClearText()`：从空壳改为调用 `window.clear_richText()`
- `update_lastinput()`：从空壳改为解除/重绑 TextBox KeyDown 事件（通过 `form.DoUpdateLastInput()`）
- `state` 属性化：字段改为属性，Quit/Error 时清理 `ConsoleEscapedParts`
- `PressPrimitiveKey`：从注释恢复为实际方法（参数改为 int 兼容桌面端）
- 桌面端键盘事件：`Form.KeyPreview + KeyDown` 转发到 `PressPrimitiveKey`

### Phase 4A-D21：Rikaichan 词典悬浮提示系统移植 ✅

- `ConfigCode`：添加 `RikaiEnabled`/`RikaiFilename`/`RikaiColorBack`/`RikaiColorText`/`RikaiUseSeparateBoxes` 枚举
- `Config`：添加 RikaiEnabled 等 5 个属性 + `DefaultFont` 委托属性
- `ConfigData`：添加 Rikai 默认值（RikaiEnabled=false, RikaiColorBack=DarkBlue, RikaiColorText=White）
- `Rikaichan.cs` + `RikaichanAutogenerated.cs`：从 LazyLoading 源码直接引用
- `Rikaichan.cs` + `RikaichanAutogenerated.cs`：从 LazyLoading 源码直接引用
- `EmueraConsole`：
  - 添加 `rikaichan` 字段
  - OnPaint 末尾添加 `rikaichan.OnPaint()` 调用
  - MoveMouse 添加 rikaichan 鼠标追踪逻辑
- 编译验证：0 错误，64 警告

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
- 11 个配置分组
- `DesktopMainWindow.ShowConfigDialog()` override：打开配置对话框 + 刷新画面

## [0.6.0] - 2026-05-17

### Phase 6：EM 私家版 HTML 扩展 ✅

#### 切片 6A：类型体系统一 ✅

- 合并 `AConsoleDisplayPart` → `AConsoleDisplayNode`，统一为 LazyLoading 单基类体系
- 删除 `AConsoleDisplayPart.cs`（GameView 命名空间）
- 所有显示部件类更新基类
- `DrawTo` 签名统一
- `Str` → `Text` 属性名统一（全项目范围）

#### 切片 6B-6E：HtmlManager + div + EscapedParts + PrintHtmlC ✅

- `HtmlAnalzeState` 新增 div/font 扩展字段
- 新增 `HtmlDivTag`/`DivState`/`HtmlParentInfo` 内部类
- div 标签解析 + 递归渲染
- EscapedParts zdepth 交错渲染
- PrintHtmlC 全量版

## [0.5.0] - 2026-05-16

### Phase 3.5：渲染管线补全 + 桌面端修复 ✅

- 桌面端渲染触发修复（PictureBox.Paint 事件注册）
- CreateSpriteFromFileDynamic 完整实现
- EmueraConsole.Print 空方法补全（PrintHtmlC/PrintImg/PrintHtml/PrintErrorButton）
- EscapedParts zdepth 交错渲染
- HTML_PRINT 岛屿输出

## [0.4.0] - 2026-05-16

### Phase 3：Skia 渲染集成 ✅

- ImageLayerManager 图层管理
- ColorMatrix 颜色矩阵
- OnPaint 升级（背景预烘焙 + CBG 渲染 + 图层管理）
- FontFactory 完善（4 层字体回退链）
- ConsoleDivPart 实现（盒模型渲染）
- GraphicsImage 缺失方法（GBitmapToInt64Array/GDrawLine/GDrawPolygon 等）

## [0.3.0] - 2026-05-16

### Phase 2：运行时适配 ✅

- 入口点适配（Encoding.RegisterProvider/JSONConfig/LoadFontFiles/DebugConfig）
- Pfc 替换（SkiaPfc + SkiaFontFamily + InstalledFontCollection）
- 路径系统适配（IPathProvider + DefaultPathProvider + Program getter 委托）
- AWAIT/INPUT 验证（桌面端宿主）

## [0.2.0] - 2026-05-15

### Phase 1：编译错误收敛（818→336→0）✅

- 全局 using alias 消除 System.Drawing 歧义
- XAML 页面排除
- ConfigCode 枚举扩展
- 存根补全

## [0.1.0] - 2026-05-15

### Phase 0：项目骨架 ✅

- 新建 `Emuera.Xamarin` 项目
- 从 XEmuera-1 复制 Skia 平台层文件
- csproj 配置：引用 LazyLoading `Runtime/` 目录
- 首次编译：129 个错误
