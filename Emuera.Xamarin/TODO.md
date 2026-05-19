# Emuera.Xamarin 任务计划

> **最后更新**：2026-05-20
> **历史记录**：[CHANGELOG.md](file:///d:/emuera/emuera_lazyloading_selfmodified_version/emuera.xamarin/CHANGELOG.md)

## 当前状态

### ✅ 已完成

| 阶段 | 内容 | 版本 |
|------|------|------|
| Phase 0-2 | 项目骨架 + 编译通过 + 桌面端可启动 + 路径/字体/入口适配 | 0.1-0.3 |
| Phase 3 | Skia 渲染集成（图层/ColorMatrix/OnPaint/FontFactory/DivPart/像素操作） | 0.4 |
| Phase 3.5 | 渲染管线补全 + 桌面端修复（触发/精灵/Print补全/EscapedParts/HTML岛屿） | 0.5 |
| Phase 4A-Desktop | Stub 扩展 + 鼠标交互 + ConfigDialog + ToolTip + QuickButton + DebugDialog | 0.6-0.8 |
| Phase 4A-D20~D23 | P0-P1修复 + Rikaichan + AnimeTimer + GotoTitleAndLoadAndRepeatInput | 0.9-0.10 |
| Phase 5B-D1 | NAudio 音频系统集成 | 0.11 |
| Phase 7D-P1 | 外部字体加载增强（fonts目录 + 别名映射） | 0.11 |
| Phase 5A-D1 | IO 跨平台适配（FileUtils + WorkingDir） | 0.12 |
| Phase 5C-D1 | SqlManager 资源清理 + FormClosing 退出流程 | 0.12 |
| Phase 6 | EM 私家版 HTML 扩展（div + PrintHtmlC 全量版） | 0.6 |
| G6 | GDI 注释残留清理 + 删除 5 组死对话框文件 | 0.13 |
| Phase 8A-P0 | System.Drawing 值类型垫片（Color/Rectangle/Point/Size） | 0.14 |
| Phase 8A-P1 | Runtime/ WinForms 依赖清理（PlatformInterop 抽象） | 0.15 |
| Phase 8A | 创建 Core 项目（net8.0 + net8.0-windows 双目标） | 0.16 |
| Phase 8B | 创建 Desktop 项目（net8.0-windows） | 0.16 |
| Phase 8C | 创建 Android 项目（net8.0-android） | 0.17 |
| Phase 5B-A | Android 音频实现（AndroidSound : Sound + MediaPlayer） | 0.17 |
| Phase 8D | Android 功能补全（IPathProvider + 触摸 + 输入 + 游戏列表 + 配置 + APK） | 0.18 |
| Phase 5B-D | Desktop 音频验证（NAudio 已集成，编译通过） | 0.18 |
| Phase 4A-X1~X7 | ContentPage MainWindow + SKCanvasView + 触摸 + 输入 + 导航框架 | 0.19 |
| Phase 5B-E | 音频路径替换 + ERB 指令验证（全部已存在于源码） | 0.19 |
| Phase 7C-P1 | FontScale 配置属性 + ConfigDialog 编辑 | 0.19 |
| Phase 8E | Android 引擎启动修复（7 项：资源命名空间/WinmmTimer/FontModel/PathProvider/SpinLock/存储权限/沉浸式） | 0.20 |
| Phase 9A | Preload 缓存 miss 修复 + eratw-chs 闪退诊断（5 项：Lang/engineInitialized/DoEvents/fire-and-forget/异常捕获） | 0.21 |
| Phase 9B | 悬浮工具栏（☰ 菜单 + ⌨ 编辑 + A+/A- 缩放）+ 输入条修复（发送按钮 + FillAndExpand）+ 沉浸式全屏 | 0.21 |
| Phase 9C | AOT 崩溃修复 + Lang 初始化顺序 + 诊断日志 + 真机验证（eratw-chs 进入脚本预加载阶段） | 0.22 |
| Phase 9D | SQL 懒加载 + Config 双类同步 + GetConfigItem 大小写修复 + 工具栏/滚动/渲染修复 + eratw-chs 完整启动 | 0.23 |
| Phase 9G | Return NRE + AWAIT 帧同步 + 字体缩放渲染 + HTML 滚动 + 虚拟控制器 + 收起工具栏 | 0.29 |
| Phase 9H | XEmuera 风格工具栏 + QuickButton 面板 + Slider 滚动条 + Return(0) NRE 修复 + MoveMouse 滚动修复 + 按钮可见性修复 | 0.30 |
| Phase 9I | 图片 LazyLoading + LRU 系统移植（SharedBitmapCache/AnimSpriteCache/SpriteAnimated/AnimatedImageHelper/ConstImage延迟加载/AppContents SQLite元数据） | 0.31 |

### ⏭️ 待推进：APK 目标路径

> **项目定位**：LazyLoading 原版桌面端已完备，本项目的**唯一目标产物是 Android APK**。
> 桌面端（net8.0-windows）仅作为功能验证手段，不是目的。
> 知识库：[android-migration.md](file:///d:/emuera/shared-trae/knowledge/xemuera/android-migration.md)

#### 当前项目结构

```
Emuera.Xamarin.sln
├── Emuera.Xamarin.Core      (net8.0;net8.0-windows) — 共享代码 + System.Drawing 垫片
├── Emuera.Xamarin.Desktop   (net8.0-windows)         — WinForms 桌面验证
├── Emuera.Xamarin.Android   (net8.0-android)         — APK 产出（.NET 8 SDK 风格）
└── Emuera.Xamarin           (net8.0-windows)         — 原项目（仍可独立编译）
```

#### 任务流总览（按执行顺序）

```
✅ 8A-P0 值类型垫片 → ✅ 8A-P1 WinForms 清理 → ✅ 8A Core → ✅ 8B Desktop → ✅ 8C Android 骨架
→ ✅ 5B-A Android 音频
→ ✅ 8D Android 功能补全（APK 可运行分界线）
→ ✅ 5B-D Desktop 音频验证
─── APK 可运行 ───
→ ✅ 4A-Xamarin SKCanvasView + 触摸 + 输入 + 导航框架
→ ✅ 5B-E 音频路径替换 + ERB 指令验证
→ ✅ 7C-P1 字体缩放配置
→ ✅ 8E Android 引擎启动修复
→ ✅ 9A Preload 缓存 miss + 闪退诊断修复
→ ✅ 9B 悬浮工具栏 + 输入条修复 + 沉浸式全屏
→ ✅ 9C AOT 崩溃修复 + Lang 初始化顺序 + 诊断日志 + 真机验证
→ ✅ 9D SQL 懒加载 + Config 同步 + 工具栏/滚动/渲染修复 + eratw-chs 完整启动
→ ✅ 9E 渲染范围修复：消除双重 density 缩放，文本填满整个屏幕高度
→ ✅ 9E-ADB：ADB 测试流程建立（设备连接、APK 安装、日志拉取与分析）
─── 引擎完整启动 ───
→ 🔴 9E-SCRIPT：分析完整 emuera_android.log（1.26MB，引擎页面运行时拉取），分类所有加载错误
→ ✅ 9E-FIX-1：GetConfigName null guard（Runtime + Xamarin Platform 两处）
→ ✅ 9E-FIX-2：SyncToRuntimeConfig 补充第二次同步调用 + 缺失属性（SavDir/ForceSavDir/Encode/SaveEncode/ImageQuality/FontHinting/FontEdging/ReplaceContinuationBR/NeedReduceArgumentOnLoad）
→ ✅ 9E-FIX-3：CalcWidth null 元素防御检查
→ ✅ 9E-FIX-4：重建 APK + ADB 测试验证（引擎完整启动，进入 @SYSTEM_TITLE）
→   - 已知：ConsoleButtonString.CalcWidth → AConsoleDisplayNode.SetWidth → StringMeasure 空引用（已添加 null guard）✅
→   - 已知：317× GetConfigName NRE（根因：CompatiFuncArg* 未同步 + nameDic null，已修复）✅
→   - 已知：SAVEGLOBAL 错误（根因：SavDir 未同步，已修复）✅
→   - 已验证：SQL_CONNECT 相对路径解析修复 ✅
→   - 已验证：CompatiFuncArgAutoConvert=YES 配置修复 ✅
→   - 已验证：引擎进入 @SYSTEM_TITLE 函数，标题界面显示 ✅
→ 🔴 9E-NEXT：修复 Android 特有错误（GETTEXTBOX user32.dll / .webp 图片缺失）
→ ⚠️ 9E-COMPATI：CompatiErrorLine 仅在调试模式启用（当前强制 true，需改为条件启用）
─── 9F：Android 兼容性补全（按执行顺序） ───
→ ✅ 9F-P0-A：AdaptiveFont 默认值修复（字体偏大根因，一行代码修复，影响面最大）
→ ✅ 9F-P0-B：TOFULL/TOHALF 跨平台替代（Microsoft.VisualBasic 不可用）
→ ✅ 9F-P0-C：CALLPLUGIN null guard（PluginSystem 空实现安全检查）
→ ✅ 9F-P1-A：emuera.config 外部存储复制机制（MuMu 共享文件夹→内部存储，解决配置不生效）
→ ✅ 9F-P1-B：IsActive 已验证正常（Created→IsEnabled→true，WinInput 已跨平台，无需修改）
→ ✅ 9F-P1-C：移植运行时缩放（ApplyRuntimeDisplayScale + ReflowDisplayLinesForCurrentScale）
→ ✅ 9F-P1-D：移植虚拟控制器 XAML + Style + 事件处理器
→ ✅ 9F-P1-E：移植智能按钮映射（INPUT→虚拟控件，关键词匹配确认/返回/AVG/数字键盘）
→ 🟡 9F-P2：FontScale/AdaptiveFontSize 显示差异系统性调整（P1-C 完成后验证）
─── 9G：v0.30 ADB 验证反馈修复（按优先级） ───
→ ✅ 9G-P0：ProcessState.Return NRE + SQL 嵌套事务修复（ReturnF空检查 + Import方法嵌套事务保护）
→ ✅ 9G-P1：BADAPPLE AWAIT 16 帧同步失效（AWAIT不带参数时添加Thread.Sleep(1)让出UI线程）
→ ✅ 9G-P2：运行时字体缩放渲染错误修复（ApplyRuntimeDisplaySettings中DrawTextUtils.Reset()清除旧Advance缓存）
→ ✅ 9G-P3：HTML 交互控件在向上滚动时失效（MouseReleased bug，已修复）
→ ✅ 9G-P4：虚拟控制器默认可见（默认true + IsVisible=true + SetConsole时Refresh）
→ ✅ 9G-P5：移植收起工具栏按钮 + 滚动按钮（▲收起/▼展开 + ↑↓半页滚动）
─── 9H：XEmuera 风格 UI 控件 + Bug 修复 ───
→ ✅ 9H-P1：XEmuera 风格工具栏（edit/gamepad/gallery/scroll_vertical/zoom/menu_show 按钮，半透明黑底白字 34×34）
→ ✅ 9H-P2：画廊视图 QuickButton 面板（⊞ 切换 + RefreshQuickButtonGroup）
→ ✅ 9H-P3：旋转90° Slider 滚动条（双向同步 ScrollBar + SyncVScrollBar）
→ ✅ 9H-P4：Return(0) NRE 修复（functionList空时设currentLine=null + ShiftNextLine null保护 + runScriptProc null退出）
→ ✅ 9H-P5：滚动后点击错误按钮修复（注释掉 MoveMouse 中 ScrollBar.Value!=Maximum 检查）
→ ✅ 9H-P6：工具栏按钮不可见修复（invertMainColor→半透明黑底白字）
→ 🟡 9H-P7：BADAPPLE 无法退出（用户确认模拟器渲染慢导致，将禁用 demo）
─── 9I：图片 LazyLoading + LRU 系统移植 ───
→ ✅ 9I-P1：SharedBitmapCache 静态位图 LRU 缓存（max 200）
→ ✅ 9I-P2：IFileBacked 可驱逐接口 + ConstImage 延迟加载
→ ✅ 9I-P3：AnimatedImageHelper GIF/WebP 多帧解码
→ ✅ 9I-P4：SpriteAnimated 原生动画 Sprite（Evict/Reload 生命周期）
→ ✅ 9I-P5：AnimSpriteCache 动画 LRU 缓存（max 6）
→ ✅ 9I-P6：AppContents SQLite 元数据（DestW/DestH 列 + SpriteAnimated 创建）
→ ✅ 9I-P7：SkiaSharp 2.88.x 兼容性修复（FrameRect 缺失退化处理）
→ ✅ 9I-P8：编译验证（0 错误 0 警告）
─── 下一步 ───
→ 🔴 9J-P1：APK 构建验证 + 真机测试 LazyLoading/LRU 系统
→ 🔴 9J-P2：动图渲染验证（GIF/WebP 在 Android 上是否正确播放）
→ 🔴 9J-P3：内存占用对比测试（移植前后 SharedBitmapCache 效果）
→ 🔴 9E-NEXT：修复 Android 特有错误（GETTEXTBOX user32.dll / .webp 图片缺失）
→ 🟡 9E-COMPATI：CompatiErrorLine 仅在调试模式启用
─── 远期 ───
→ ⏭️ 7C-P2 动态缩放（高难度，依赖 9F-P1-C；唯一真正需要大量移植的远期任务）
→ ~~7D-P1 外部字体加载增强~~ ✅ v0.11 已完成（font/fonts 扫描 + FontFactory.fontAliases + RegisterKnownAliases）
→ ~~7G 游戏列表~~ ✅ 8D-4 已完成（AndroidGameListPage + AndroidMainMenuPage + FlyoutPage）
→ ⏭️ 7E 本地化（90% 完成：LanguageModel/LanguagePage/Lang.cs 已移植；缺 Android 菜单入口 + LoadLanguageFile() stub）
→ ⏭️ 7F 日志查看器（80% 完成：TxtFilePage/SelectableLabel 已移植；缺 AndroidLogPage + 菜单入口）
→ ⏭️ 真机测试
```

#### 🔴 P0：APK 可运行（当前焦点）

##### 切片 8D：Android 功能补全

> **目标**：让 APK 可安装启动、可交互
> **前置条件**：8C ✅ + 5B-A ✅

| # | 操作 | 说明 | XEmuera 参考 | 状态 |
|---|------|------|-------------|------|
| 8D-1 | Android IPathProvider | `Context.FilesDir` / ExternalStorage 路径 | IPlatformService.GetStoragePath() | ✅ |
| 8D-2 | 触摸事件转发 | EraPictureBox_Touch → EmueraConsole 鼠标事件 | **直接复制** XEmuera 触摸转鼠标 | ✅ |
| 8D-3 | 文本输入 | Entry → PressEnterKey | XEmuera MainWindow.xaml.cs | ✅ |
| 8D-4 | 游戏列表 + 侧边菜单 | GameItemPage + MainMenuPage + FlyoutPage | **直接复制** XEmuera Views/ | ✅ |
| 8D-5 | 配置页面 | ConfigPage + ConfigValuePopup | **直接复制** XEmuera Views/ | ✅ |
| 8D-6 | APK 打包验证 | 签名 + 安装 + 启动测试 | — | ✅ |

##### 切片 5B-D：Desktop 音频验证（可与 8D 并行）

| # | 操作 | 说明 | 状态 |
|---|------|------|------|
| 5B-D1 | SoundStub.cs 添加 `Playing` 字段 | `public volatile bool Playing = false;` | ✅ |
| 5B-D2 | Desktop csproj 添加 NAudio NuGet | `NAudio` 2.2.1 + `NAudio.Vorbis` 1.5.0 | ✅ |
| 5B-D3 | Desktop csproj 包含 NAudio 源文件 | `Sound.NAudio.cs` + `NAudio_LoopStream.cs` + `SoundTouch_byMarkHeath/**` | ✅ |
| 5B-D4 | 修改 Sound.NAudio.cs：`Sound` → `NAudioSound : Sound` | 继承 SoundStub，override 虚方法，更新 SoundMixer 引用 | ✅ |
| 5B-D5 | DesktopHost.InitEmuera() 初始化音频 | `GlobalStatic.Sound = new NAudioSound[16]` + `GlobalStatic.Bgm = new NAudioSound()` | ✅ |
| 5B-D6 | 编译+运行验证 | 桌面端播放 WAV/OGG/BGM | ✅ |

#### 🟡 P1：框架开发（APK 可运行后回归）

##### 切片 4A-Xamarin：启用 ContentPage MainWindow

> **前置条件**：8D 完成（APK 可运行）
> **与 8D 的关系**：8D-2（触摸）= 4A-X4，8D-3（输入）= 4A-X5，8D-4（游戏列表）= 7G

| # | 操作 | 说明 | 状态 |
|---|------|------|------|
| 4A-X1 | MainWindow extends ContentPage | Stub 改为继承 ContentPage，添加 new 关键字 | ✅ |
| 4A-X2 | SKMouseButton 命名空间迁移 + Views 纳入 Core | 迁移到 MinorShift.Emuera.UI.Framework.Forms + SelectableLabel/TintImageButton | ✅ |
| 4A-X3 | 连接 SKCanvasView 到 EmueraConsole.OnPaint | OnPaintSurface → console.OnPaint + MainPicBox.Width/Height | ✅ |
| 4A-X4 | 实现触摸事件转发 | `EraPictureBox_Touch` → `console.MouseDown`/`PressEnterKey` | ✅ |
| 4A-X5 | 实现文本输入 | `Entry` → `console.PressEnterKey`（ONEPHRASE/ANYKEY 自动提交） | ✅ |
| 4A-X6 | 实现 MainMenu_Reboot/GotoTitle + Reboot | FlyoutPage 导航框架 + 游戏重启/回到标题 | ✅ |
| 4A-X7 | 编译+运行验证 | 三项目 0 错误 | ✅ |

##### 切片 5B-E：音频路径 + ERB 指令补全

| # | 操作 | 说明 | 状态 |
|---|------|------|------|
| 5B-E1 | 替换音频路径构建 | `Path.GetFullPath(".\\sound\\")` → `Program.MusicDir` + `Program.FileExists()` | ✅ |
| 5B-E2 | 升级 PLAYSOUND ArgumentBuilder | 支持 repeat 参数（已存在于源码） | ✅ |
| 5B-E3 | 移植 GETSOUNDORBGMINFO | 已存在于 Creator.Method.cs | ✅ |
| 5B-E4 | 移植 ISPLAYINGSOUND/ISPLAYINGBGM | 已存在于 Creator.Method.cs | ✅ |
| 5B-E5 | 移植 SOUNDCONTROL/BGMCONTROL | 已存在于 Creator.Method.cs | ✅ |
| 5B-E6 | 编译+运行验证 | Core 0 错误 | ✅ |

##### 切片 4B：配置页面

| # | 操作 | 说明 | 决策 |
|---|------|------|------|
| 4B-1 | 移植 XEmuera-1 ConfigPage 体系 | `SettingsPage` → `ConfigSettingsPage` → `ConfigPage` → `ConfigValuePopup` | 方案 A：移植 XEmuera-1 |
| 4B-2 | 移植 ConfigModel | 配置分组管理 + MVVM 绑定 | — |
| 4B-3 | 扩展 ConfigCodeGroups 添加 LazyLoading 独有分组 | CB 配置 / Rikaichan / Rendering / Debug | — |
| 4B-4 | 保持 LazyLoading 的 Copy+SaveConfig 保存策略 | 不改用 Preferences，保持 emuera.config 文件格式 | 方案 A |
| 4B-5 | 实现 DebugConfigDialog（低优先级） | `GetDebugItem()` 通道 + `SaveDebugConfig()` | — |
| 4B-6 | 编译+运行验证 | — | — |

##### 切片 7G：游戏列表 + 侧边菜单

| # | 操作 | 说明 | 状态 |
|---|------|------|------|
| 7G-1 | 移植 `GameItemModel` + `GameItemPage` | 游戏列表 + 长按配置编辑 | ⏭️ |
| 7G-2 | 移植 `MainMenuPage` | 侧边菜单（重启/回到标题/设置/解释器日志） | ⏭️ |
| 7G-3 | 移植 `GameUtils` 游戏管理 | `StartEmuera()`/`CurrentGamePath`/`PrefKeyLastGamePath` | ⏭️ |
| 7G-4 | 编译+运行验证 | — | ⏭️ |

#### 🟢 P2：框架优化

| 工作项 | 说明 | 切片 |
|--------|------|------|
| 静态字体缩放 | 配置层 FontScale 属性 + 启动时应用 | 7C-P1 |
| 本地化 UI | LanguagePage + CultureInfo 联动 | 7E |
| 日志查看器 | TxtFilePage + 侧边菜单入口 | 7F |
| ToolTip OwnerDraw | SKBitmap→Bitmap 转换 | — |
| Rikaichan 词典悬浮提示 | Core 已排除 Rikaichan.cs，Android 端不需要 | **P3 最低** |

***

## Phase 2（剩余延后项）

### 切片 2D：键鼠输入抽象 — ⏭️ 延后至移动端

> 延后理由：当前 TFM `net8.0-windows` 下 `WinInput.GetKeyState` 和 `Control.MousePosition` 均可正常工作

| #    | 操作                           | 说明                                                                                   |
| ---- | ---------------------------- | ------------------------------------------------------------------------------------ |
| 2D-1 | 创建 `InputState` 具体类          | `lock` 保护的按键状态数组 + 鼠标位置 + 鼠标按钮值                                                      |
| 2D-2 | 修改 Creator.Method.cs 中 3 个方法 | `GetKeyStateMethod`/`MouseButtonMethod`/`MousePosMethod` 从 InputState 读取             |
| 2D-3 | 触摸事件写入 InputState            | `EraPictureBox_Touch` → `SetKeyDown/Up` + `SetMousePosition` + `SetMouseButtonValue` |
| 2D-4 | Windows 事件写入 InputState      | `KeyDown/Up` + `MouseDown/Up` → `SetKeyDown/Up`（保持 WinInput 兼容）                      |
| 2D-5 | 编译验证                         | —                                                                                    |

**方案决策**：采用 InputState 具体类方案（方案 C）。详见 [await-async.md](file:///d:/emuera/shared-trae/knowledge/xemuera/await-async.md)。

### 切片 2E（移动端待验证项）

| #    | 操作                             | 说明                      |
| ---- | ------------------------------ | ----------------------- |
| 2E-2 | 验证 TINPUT/TINPUTS Timer        | Android 上 Timer 行为      |
| 2E-3 | 验证 GETKEYTRIGGERED/MOUSEB 触摸映射 | 配合 2D 的 InputState 触屏映射 |

***

## Phase 3：Skia 渲染集成

> **知识库**：[graphics-rendering.md](file:///d:/emuera/shared-trae/knowledge/xemuera/graphics-rendering.md)、[font-rendering.md](file:///d:/emuera/shared-trae/knowledge/xemuera/font-rendering.md)
> **前置条件**：Phase 2 + 4A-Desktop 完成 ✅
> **勘探日期**：2026-05-16
> **关键发现**：
>
> 1. LazyLoading 内核渲染管线**已完全 Skia 化**，GDI+ 仅在光栅字体回退路径残留
> 2. xamarin OnPaint 是**旧版简化**，缺少背景烘焙/图层混合/EscapedParts/Rikaichan
> 3. Drawing/ 目录的 DrawBitmapUtils + DrawTextUtils 是**完整实现**，可直接使用
> 4. ImageLayerManager 和 ColorMatrixHelper 是**P0 级 stub**，必须优先实现
> 5. FontFactory 需要从简化版升级为完整版（4层回退链+缓存）

### 切片 3A：ImageLayerManager 图层管理（P0）

> **目标**：实现 SETIMAGELAYER 系列指令的图层管理
> **参考**：LazyLoading `Emuera/UI/Game/ImageLayerManager.cs`（117行，完全 Skia）

| #    | 操作                             | 说明                                                                  | 状态 |
| ---- | ------------------------------ | ------------------------------------------------------------------- | -- |
| 3A-1 | 升级 `ImageLayer` 数据结构           | 添加 `ASprite Image`（替换 `object`）、`SKColorFilter?`、`SpriteAnime` 动画管理 | ✅  |
| 3A-2 | 实现 `SetLayer()`                | 创建 ImageLayer 并存入 `_layers` 字典，查找 ASprite                           | ✅  |
| 3A-3 | 实现 `DrawAll()` → 改名 `DrawTo()` | 按 Depth 升序绘制，视口裁剪，离屏暂停动画，FollowScroll 偏移                            | ✅  |
| 3A-4 | 集成到 EmueraConsole.OnPaint      | 添加 `_imageLayerManager.DrawTo(graph, ...)` 调用                       | ✅  |
| 3A-5 | 编译+运行验证                        | 0 编译错误                                                              | ✅  |

**隐含依赖**：`AppContents.CreateSprite<XXX>()` 系列方法需能正确返回 ASprite（当前 `CreateSpriteFromFileDynamic` 返回 false）

### 切片 3B：ColorMatrix 颜色矩阵（P0）

> **目标**：实现 GSETCOLORMATRIX 指令和 HTML img cm 属性
> **参考**：LazyLoading `Emuera/UI/Game/image/ColorMatrixHelper.cs`（84行，完全 Skia）

| #    | 操作                                     | 说明                                           | 状态 |
| ---- | -------------------------------------- | -------------------------------------------- | -- |
| 3B-1 | 重写 `CreateColorMatrix()`               | 移除（无调用者，直接用 SKColorFilter.CreateColorMatrix） | ✅  |
| 3B-2 | 实现 `ReadFromVariableTerm()`            | 从 ERB 变量表达式读取 5x5 矩阵，调用 `ToSkia()` 转换        | ✅  |
| 3B-3 | 实现 `ReadFromVariable()`                | 从 VariableToken 读取 2D/3D 数组，支持角色变量           | ✅  |
| 3B-4 | 验证 `SKColorFilter.CreateColorMatrix()` | 确认 offset × 255 转换正确                         | ✅  |
| 3B-5 | 编译+运行验证                                | 0 编译错误                                       | ✅  |

### 切片 3C：OnPaint 升级（P0）

> **目标**：将 xamarin OnPaint 从旧版简化升级为 LazyLoading 完整版
> **这是 Phase 3 的核心工作量**

| #    | 操作                                            | 说明                           | 状态    |
| ---- | --------------------------------------------- | ---------------------------- | ----- |
| 3C-1 | 移植 `ValidateBackground()` + `bakedBackground` | 背景预烘焙机制                      | ✅     |
| 3C-2 | 移植 EscapedParts + CBG 混合渲染                    | 按 zdepth 降序排列，depth=0 时绘制文本行 | ✅     |
| 3C-3 | 集成 `_imageLayerManager.DrawTo()`              | 在背景和文本之间绘制图层                 | ✅     |
| 3C-4 | 移植 `rikaichan.OnPaint()`                      | Rikaichan 悬浮提示渲染             | ✅     |
| 3C-5 | 移植 `_htmlElementList` HTML 元素绘制               | HTML\_PRINT 岛屿输出             | ⏭️ 延后 |
| 3C-6 | 编译+运行验证                                       | —                            | ✅     |

**风险**：OnPaint 升级涉及大量行级修改，需逐段 diff LazyLoading 版和 xamarin 版

### 切片 3D：FontFactory 完善（P1）

> **目标**：从简化版升级为完整版字体工厂
> **参考**：LazyLoading `Emuera/UI/FontFactory.cs`（294行）

| #    | 操作                            | 说明                                    | 状态 |
| ---- | ----------------------------- | ------------------------------------- | -- |
| 3D-1 | 移植字体回退链                       | 4 层回退：自定义→安全字体→MatchCharacter→Default | ✅  |
| 3D-2 | 实现 `GetFont()` 完整参数           | 应用 style/edging/hinting 参数            | ✅  |
| 3D-3 | 实现 `IsRasterFont()`           | 检测 MS Gothic 等光栅字体                    | ✅  |
| 3D-4 | 实现字体缓存                        | `fontDic` + `fallbackTypefaceCache`   | ✅  |
| 3D-5 | 实现 `GetFallbackFontForChar()` | 按字符获取回退字体                             | ✅  |
| 3D-6 | 编译+运行验证                       | —                                     | ✅  |

**注意**：光栅字体的 GDI 桥接路径（`TextRenderer.DrawText` → `SKBitmap`）在 Android/iOS 不可用，需条件编译或替代方案

### 切片 3E：ConsoleDivPart 实现（P1）

> **目标**：实现 div 容器的渲染和宽度计算

| #    | 操作                             | 说明                          | 状态 |
| ---- | ------------------------------ | --------------------------- | -- |
| 3E-1 | 移植 `ConsoleDivPart.DrawTo()`   | div 容器绘制逻辑                  | ✅  |
| 3E-2 | 移植 `ConsoleDivPart.SetWidth()` | div 宽度计算（空实现，同 LazyLoading） | ✅  |
| 3E-3 | 编译+运行验证                        | —                           | ✅  |

### 切片 3F：GraphicsImage 缺失方法（P2）

> **目标**：补全 GBitmapToInt64Array/GByteArrayToBitmap 等像素操作

| #    | 操作                                                 | 说明                                         | 状态 |
| ---- | -------------------------------------------------- | ------------------------------------------ | -- |
| 3F-1 | 实现 `GBitmapToInt64Array()`                         | SkiaSharp 像素读取（GetPixels + Marshal.Copy）   | ✅  |
| 3F-2 | 实现 `GByteArrayToBitmap()`                          | SkiaSharp 像素写入（Marshal.Copy → GetPixels）   | ✅  |
| 3F-3 | 修复 `GDrawLine()`/`GDrawPolygon()`/`GFillPolygon()` | 改用已有 `g` canvas 绘制，添加 DashStyle/DashCap 支持 | ✅  |
| 3F-4 | 编译+运行验证                                            | —                                          | ✅  |

### 切片 3G：增量渲染（可选优化，P3）— ⏭️ 跳过

> LazyLoading 本身未实现脏区域/增量渲染，采用全量清除+全量重绘策略。
> xamarin 已有的优化（背景缓存、离屏裁剪、帧率节流）与 LazyLoading 对齐。

| #    | 操作                       | 说明           | 状态    |
| ---- | ------------------------ | ------------ | ----- |
| 3G-1 | 移植 `DirtyLineTracker`    | 脏行追踪         | ⏭️ 跳过 |
| 3G-2 | 移植 `IncrementalRenderer` | 增量渲染         | ⏭️ 跳过 |
| 3G-3 | 移植 `RenderLoop`          | 渲染循环         | ⏭️ 跳过 |
| 3G-4 | 性能对比测试                   | 全屏重绘 vs 增量渲染 | ⏭️ 跳过 |

***

## Phase 3.5：渲染管线补全 + 桌面端修复 ✅

> 勘探发现：桌面端无法显示文本（pictureBox.Invalidate 不触发 Form.OnPaint）、多个 Print 方法空实现、EscapedParts 交错渲染缺失、HTML\_PRINT 岛屿输出缺失

### 切片 3.5-A：桌面端渲染触发修复 ✅

| #      | 操作                    | 说明                                                   | 状态 |
| ------ | --------------------- | ---------------------------------------------------- | -- |
| 3.5A-1 | 修复 `TriggerRefresh()` | `pictureBox.Invalidate()` → 注册 `pictureBox.Paint` 事件 | ✅  |

### 切片 3.5-B：CreateSpriteFromFileDynamic 修复 ✅

| #      | 操作                                 | 说明                                        | 状态 |
| ------ | ---------------------------------- | ----------------------------------------- | -- |
| 3.5B-1 | 实现 `CreateSpriteFromFileDynamic()` | SKBitmap.Decode + ConstImage + SpriteF 注册 | ✅  |

### 切片 3.5-C：EmueraConsole.Print 空方法补全 ✅

> ⚠️ PrintHtmlC 当前为简化版：缺少 `CurrentLineWidth`/`AppendButton(ConsoleButtonString)`/`Html2ButtonList`
> 三个底层 API，无法实现列内同行渲染，改为 flush 后独立行渲染。全量版见 Phase 6。

| #      | 操作                      | 说明                                            | 状态 |
| ------ | ----------------------- | --------------------------------------------- | -- |
| 3.5C-1 | 实现 `PrintHtmlC()`       | HTML 制表打印（LC左对齐/RC右对齐列，简化版无 CurrentLineWidth） | ✅  |
| 3.5C-2 | 实现 `PrintImg()` 3参数版    | mask 参数暂忽略                                    | ✅  |
| 3.5C-3 | 实现 `printCustomBar()`   | 自定义 DRAWLINE 字符串                              | ✅  |
| 3.5C-4 | 实现 `PrintHtml()`        | HTML 打印（简化版，无 toPrintBuffer 行内模式）             | ✅  |
| 3.5C-5 | 实现 `PrintErrorButton()` | 错误按钮 + 颜色级别                                   | ✅  |

### 切片 3.5-D：EscapedParts zdepth 交错渲染 ✅

> ⚠️ 渲染循环已升级为 eidx+cidx 双指针，但 div 渲染链三层断裂：
>
> 1. **解析层**：HtmlManager 无 `case "div"`，遇到 `<div>` 抛异常
> 2. **创建层**：无 `new ConsoleDivPart(...)` 代码路径，ConsoleDivPart 是"死类"
> 3. **注册层**：`FilterEscaped()` 空实现，`ConsoleEscapedParts.Add()` 从未被调用
>    当前 EscapedParts 系统仅基础设施就绪，实际无数据流过。完整 div 支持见 Phase 6。

| #      | 操作                                                      | 说明                              | 状态 |
| ------ | ------------------------------------------------------- | ------------------------------- | -- |
| 3.5D-1 | 添加 `ConsoleButtonString.FilterEscaped()`/`DrawPartTo()` | 越界部件识别与绘制                       | ✅  |
| 3.5D-2 | 添加 `AConsoleDisplayPart.Parent`/`Depth` 属性              | 越界部件关联                          | ✅  |
| 3.5D-3 | OnPaint 替换为 eidx+cidx 双指针循环                             | CBG+EscapedParts 按 depth 降序归并绘制 | ✅  |
| 3.5D-4 | 使用 LazyLoading 内核 `ConsoleEscapedParts`                 | 全局静态记录管理                        | ✅  |

### 切片 3.5-E：HTML\_PRINT 岛屿输出 ✅

| #      | 操作                             | 说明                                   | 状态 |
| ------ | ------------------------------ | ------------------------------------ | -- |
| 3.5E-1 | 实现 `PrintHTMLIsland()`         | Html2DisplayLine → \_htmlElementList | ✅  |
| 3.5E-2 | 实现 `ClearHTMLIsland()`         | 清空 \_htmlElementList                 | ✅  |
| 3.5E-3 | OnPaint 末尾绘制 \_htmlElementList | y=0 开始，不受滚动影响                        | ✅  |

***

## Phase 4：Xamarin.Forms UI 集成

> **知识库**：[ui-integration.md](file:///d:/emuera/shared-trae/knowledge/xemuera/ui-integration.md)
> **前置条件**：Phase 3 完成（4A-Desktop 已提前完成）

### 切片 4A：主页面集成

#### 子阶段 4A-Desktop：扩展 Stub + DesktopMainWindow（当前 TFM）

> **目标**：取消注释 xamarin EmueraConsole 中更多 `window.*` 引用，逐步恢复 LazyLoading EmueraConsole 完整功能

| #     | 操作                                            | 说明                                                                                                                               | 状态 |
| ----- | --------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------- | -- |
| 4A-D1 | 扩展 `MainPicBoxStub`                           | 添加 `Width`/`Height`（连接实际尺寸）、`PointToClient(Point)`、`ClientRectangle`                                                             | ✅  |
| 4A-D2 | 扩展 `TextBoxStub`                              | 添加 `ForeColor`、`Font` 属性；DesktopMainWindow 连接 WinForms TextBox                                                                   | ✅  |
| 4A-D3 | 实现 `window.Invoke(Action)` 同步版本               | Stub 添加 virtual 方法；DesktopMainWindow 用 `ManualResetEventSlim` + `form.BeginInvoke` 实现                                            | ✅  |
| 4A-D4 | 实现 `window.Created` 替代                        | Stub 添加 `Created` 属性（别名 `IsEnabled`）；DesktopMainWindow override `!form.IsDisposed`                                               | ✅  |
| 4A-D5 | 创建 `ToolTipStub` 空实现                          | Show/RemoveAll/InitialDelay/AutoPopDelay/ForeColor/BackColor/OwnerDraw/Draw/Popup 全部空实现                                          | ✅  |
| 4A-D6 | 补充其他缺失接口                                      | `Text`（别名 Title）、`ShowConfigDialog()`（空）、`Focus()`（空）、`TextBoxPosChanged`/`ResetTextBoxPos()`/`TextBoxIgnoreScrollBarChanges`（空） | ✅  |
| 4A-D7 | 取消注释 EmueraConsole 中 `window.MainPicBox.*` 调用 | PointToClient/ClientRectangle/Width/Height，替换 `Control.MousePosition` 为 `System.Windows.Forms.Control.MousePosition`             | ✅  |
| 4A-D8 | 取消注释 EmueraConsole 中 `window.ToolTip.*` 调用    | ToolTip stub 化后可安全取消注释                                                                                                           | ✅  |
| 4A-D9 | 编译+运行验证                                       | 0 编译错误，60 警告（均为既有警告）                                                                                                             | ✅  |

**Stub 缺失接口清单（4A-D1\~D6 已全部补充）**：

| 缺失接口                                           | 严重程度  | 目标切片  | 状态 |
| ---------------------------------------------- | ----- | ----- | -- |
| `window.Created`                               | **高** | 4A-D4 | ✅  |
| `window.Invoke(Action)`                        | **高** | 4A-D3 | ✅  |
| `window.ToolTip.*`                             | **高** | 4A-D5 | ✅  |
| `window.MainPicBox.PointToClient()`            | **高** | 4A-D1 | ✅  |
| `window.MainPicBox.ClientRectangle`            | **中** | 4A-D1 | ✅  |
| `window.MainPicBox.Width/Height`               | **中** | 4A-D1 | ✅  |
| `window.TextBox.ForeColor/Font`                | **中** | 4A-D2 | ✅  |
| `window.TextBoxPosChanged`/`ResetTextBoxPos()` | **中** | 4A-D6 | ✅  |
| `window.TextBoxIgnoreScrollBarChanges`         | **中** | 4A-D6 | ✅  |
| `window.Text`                                  | **低** | 4A-D6 | ✅  |
| `window.ShowConfigDialog()`                    | **低** | 4A-D6 | ✅  |
| `window.Focus()`                               | **低** | 4A-D6 | ✅  |

**风险 Top 3**：

1. `window.Invoke(Action)` 同步语义 — WinForms `Invoke` 是同步的，`MainThread.BeginInvokeOnMainThread` 是异步的
2. 取消注释后引入新编译错误 — 注释代码引用 `Control.MousePosition`/`Cursor.Position` 等 WinForms API
3. MainPicBoxStub 与 EraPictureBox 双轨并存 — 未来切换 TFM 时需确保不冲突

#### 子阶段 4A-Desktop 续：鼠标交互 + WinForms 兼容性 ✅

> **目标**：让桌面端真正可交互（鼠标点击/移动/滚轮/按钮选择/滚动条/配置对话框）

| #      | 操作                                          | 说明                                                                     | 状态 |
| ------ | ------------------------------------------- | ---------------------------------------------------------------------- | -- |
| 4A-D10 | 实现桌面端鼠标事件处理                                 | MouseMove→MoveMouse, MouseDown/Up→按钮选择, MouseWheel→滚动+InputMouseKey    | ✅  |
| 4A-D11 | 实现桌面端输入完善                                   | PressEnterKey后台线程+重入保护, Escape跳过, MouseLeave→LeaveMouse                | ✅  |
| 4A-D12 | 修复 `MainThread.BeginInvokeOnMainThread` 兼容性 | 替换为 `window.Invoke`（WinForms 不支持 Xamarin.Essentials MainThread）        | ✅  |
| 4A-D13 | 修复 `Color.WithAlpha` 兼容性                    | `System.Drawing.Color` 无 WithAlpha → 改用 `Color.FromArgb(alpha, color)` | ✅  |
| 4A-D14 | 缓存 `DesktopScrollBar` 实例                    | 修复每次访问 `ScrollBar` 属性创建新实例的问题                                          | ✅  |
| 4A-D15 | 实现 `App.DoEvents()`                         | 从空实现改为 `Application.DoEvents()`                                        | ✅  |
| 4A-D16 | 编译+运行验证                                     | 0 编译错误，62 警告（均为既有警告）                                                   | ✅  |

#### 子阶段 4B-Desktop：WinForms 配置对话框 ✅

> **目标**：实现桌面端配置编辑对话框

| #     | 操作                       | 说明                                                                                       | 状态 |
| ----- | ------------------------ | ---------------------------------------------------------------------------------------- | -- |
| 4B-D1 | 创建 `DesktopConfigDialog` | WinForms Form + TreeView分组 + ListView属性列表 + 编辑弹窗                                         | ✅  |
| 4B-D2 | 实现 `ShowConfigDialog()`  | DesktopMainWindow override 打开配置对话框                                                       | ✅  |
| 4B-D3 | 支持 bool/Color/string 编辑  | bool切换/ColorDialog/输入弹窗                                                                  | ✅  |
| 4B-D4 | 11 个配置分组                 | Environment/View/Window/Font/System/Compati/Debug/QuickButton/FontAndShape/LongPressSkip | ✅  |
| 4B-D5 | 编译验证                     | 0 错误                                                                                     | ✅  |

#### 子阶段 4A-Desktop 续2：ToolTip + QuickButton + DebugDialog 完善 ✅

> **目标**：让桌面端 ToolTip/QuickButton/DebugDialog 从空壳升级为可用功能

| #      | 操作                                    | 说明                                                                                          | 状态 |
| ------ | ------------------------------------- | ------------------------------------------------------------------------------------------- | -- |
| 4A-D17 | 实现 DesktopToolTip                     | WinForms ToolTip 桥接（Show/RemoveAll/InitialDelay/AutoPopDelay/ForeColor/BackColor/OwnerDraw） | ✅  |
| 4A-D18 | 实现 DesktopQuickButton                 | FlowLayoutPanel 快捷按钮面板（AddButton/Clear + 点击提交）                                              | ✅  |
| 4A-D19 | 实现 ToolTip 扩展方法                       | SetToolTipFontName/SetToolTipImg/SetToolTipFormat/CustomToolTip/SetToolTipFontSize 存储状态字段   | ✅  |
| 4B-D6  | 实现 DesktopDebugDialog                 | WinForms 调试对话框（Console/Variables/StackTrace 标签页）                                            | ✅  |
| 4C-D1  | 恢复 DebugDialog 属性                     | 从注释改为 `public object DebugDialog { get; } = null`                                           | ✅  |
| 4C-D2  | 恢复 DebugPrint/DebugClear/DebugNewLine | 从空壳改为实际日志操作                                                                                 | ✅  |
| 4C-D3  | 恢复 OpenDebugDialog                    | 从空壳改为 `window.ShowDebugDialog()`                                                            | ✅  |
| 4C-D4  | 恢复初始化和 @DEBUG 命令中 OpenDebugDialog 调用  | `Program.DebugMode && Config.DebugShowWindow`                                               | ✅  |
| 4C-D5  | 编译验证                                  | 0 错误，63 警告（均为既有）                                                                            | ✅  |

#### 子阶段 4A-Xamarin：启用 ContentPage MainWindow（未来 TFM 切换）

> **前置条件**：4A-Desktop 完成 + TFM 切换到 net8.0-android 或多目标框架

| #     | 操作                                      | 说明                                                          | 状态 |
| ----- | --------------------------------------- | ----------------------------------------------------------- | -- |
| 4A-X1 | 移除 csproj 中 `Platform\Forms\**` 排除项     | 启用 XAML 代码生成                                                | ⏭️ |
| 4A-X2 | 移除 csproj 中 XAML 页面排除项                  | Views/\*.xaml.cs + MainPage.xaml.cs + App.xaml.cs           | ⏭️ |
| 4A-X3 | 连接 SKCanvasView 到 EmueraConsole.OnPaint | `mainPicBox_Paint` → `console.OnPaint(canvas)`              | ⏭️ |
| 4A-X4 | 实现触摸事件转发                                | `EraPictureBox_Touch` → `console.MouseDown`/`PressEnterKey` | ⏭️ |
| 4A-X5 | 实现文本输入                                  | `Entry` → `console.PressEnterKey`（ONEPHRASE/ANYKEY 自动提交）    | ⏭️ |
| 4A-X6 | 移植 App.xaml + MainPage.xaml             | FlyoutPage 导航框架 + GameItemPage 游戏列表                         | ⏭️ |
| 4A-X7 | 编译+运行验证                                 | —                                                           | ⏭️ |

### 切片 4B：配置页面

| #    | 操作                                      | 说明                                                                        | 决策                |
| ---- | --------------------------------------- | ------------------------------------------------------------------------- | ----------------- |
| 4B-1 | 移植 XEmuera-1 ConfigPage 体系              | `SettingsPage` → `ConfigSettingsPage` → `ConfigPage` → `ConfigValuePopup` | 方案 A：移植 XEmuera-1 |
| 4B-2 | 移植 ConfigModel                          | 配置分组管理 + MVVM 绑定                                                          | —                 |
| 4B-3 | 扩展 ConfigCodeGroups 添加 LazyLoading 独有分组 | CB 配置 / Rikaichan / Rendering / Debug                                     | —                 |
| 4B-4 | 保持 LazyLoading 的 Copy+SaveConfig 保存策略   | 不改用 Preferences，保持 emuera.config 文件格式                                     | 方案 A              |
| 4B-5 | 实现 DebugConfigDialog（低优先级）              | `GetDebugItem()` 通道 + `SaveDebugConfig()`                                 | —                 |
| 4B-6 | 编译+运行验证                                 | —                                                                         | —                 |

**ConfigData 双通道设计**：

- 主配置：`GetConfigItem(ConfigCode)` → `Copy() → SaveConfig()`
- 调试配置：`GetDebugItem(ConfigCode)` → 直接修改 → `SaveDebugConfig()`
- JSON 配置：`JSONConfig.Data` → `JSONConfig.Save()`

### 切片 4C：调试窗口

| #    | 操作                                   | 说明                      | 决策        |
| ---- | ------------------------------------ | ----------------------- | --------- |
| 4C-1 | 保持 DebugDialog 注释状态                  | 移动端调试窗口优先级低             | 方案 D：暂不实现 |
| 4C-2 | 确保 EmueraConsole 中 DebugDialog 引用不报错 | `DebugDialog` 属性返回 null | —         |
| 4C-3 | （可选）Tab 页方案实现                        | 在 MainWindow 中添加调试 Tab  | 低优先级      |

***

## Phase 5：平台特定功能

> **知识库**：[io-cross-platform.md](file:///d:/emuera/shared-trae/knowledge/xemuera/io-cross-platform.md)、[audio-system.md](file:///d:/emuera/shared-trae/knowledge/xemuera/audio-system.md)
> **前置条件**：Phase 4 完成

### 切片 5A：IO 跨平台适配

| #    | 操作                                                                               | 说明                                                                                   | 决策                    | 状态 |
| ---- | -------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------ | --------------------- | -- |
| 5A-1 | 移植 XEmuera `FileUtils` 到 emuera.xamarin                                          | 大小写不敏感文件查找（`Exists(ref)`/`GetFiles()`/`GetDirectories()`）                             | 方案 B：封装到 Program 静态方法 | ✅  |
| 5A-2 | 添加 `Program.FileExists(ref)`/`Program.DirectoryExists(ref)`/`Program.GetFiles()` | 内部委托 FileUtils                                                                       | —                     | ✅  |
| 5A-3 | 替换内核中所有 `File.Exists`/`Directory.GetFiles` 调用                                    | AppContents/Config/Program/GameItemModel                                              | —                     | ✅  |
| 5A-4 | 替换音频路径构建                                                                         | `Path.GetFullPath(".\\sound\\")` → `Program.MusicDir`                          | —                     | ✅ |
| 5A-5 | 实现 `IPathProvider` 的 Android/iOS 版本                                              | Android: `Context.FilesDir`; iOS: `NSBundle` + `Documents/`                          | —                     | ⏭️ |
| 5A-6 | Android 资源解压                                                                     | APK assets → Context.FilesDir（首次启动全量解压）                                              | 方案 A                  | ⏭️ |
| 5A-7 | 编译+运行验证                                                                          | —                                                                                    | —                     | ✅  |

### 切片 5B：音频系统（P0 — 重新评估后提升优先级）

> **研判日期**：2026-05-17
> **知识库**：[audio-system.md](file:///d:/emuera/shared-trae/knowledge/xemuera/audio-system.md)
>
> **架构决策**：坚持 SoundStub 虚方法方案，不采用 XEmuera-1 的 IPlayer 接口
>
> **理由**：
> 1. SoundStub 已有 13 个虚方法，API 覆盖 LazyLoading 全部 12 个 ERB 指令/函数
> 2. 与 Runtime/ 代码零改动兼容（GlobalStatic.Sound[] 就是 Runtime.Utils.Sound 类型）
> 3. 子类化模式与 DesktopMainWindow/MainPicBoxStub 一致
> 4. IPlayer 方案需改 Instraction.Child + Creator.Method + GlobalStatic，违反最小改动原则
>
> **NAudio 移植性**：NAudio 体系整体不可移植到 Android（WasapiOut/MediaFoundationReader/SoundTouch 原生 DLL 均为 Windows-only），但 Desktop 端可直接使用
>
> **关键问题**：NAudio `Sound` 类与 SoundStub 同名同类名（`MinorShift.Emuera.Runtime.Utils.Sound`），Desktop 引用 Core DLL 后不能重复定义。解决方案：将 NAudio `Sound` 重命名为 `NAudioSound : Sound`（继承 SoundStub），override 所有虚方法
>
> **SoundMixer 适配**：SoundMixer.PlaySound/StopSound 参数类型需从 `Sound` 改为 `NAudioSound`；SoundEnded 事件中 `((Sound)args.SampleProvider)` 需改为 `((NAudioSound)args.SampleProvider)`
>
> **SoundStub 补充**：需添加 `public volatile bool Playing = false;` 字段（NAudio SoundMixer 依赖此字段）
>
> **Android 变速限制**：Android MediaPlayer 无原生变速 API，`setSpeed()` 返回 0（不支持）。未来可考虑 ExoPlayer 或 Oboe 替代

#### 切片 5B-D：Desktop 音频验证（先做，验证架构）

| # | 操作 | 说明 | 状态 |
|---|------|------|------|
| 5B-D1 | SoundStub.cs 添加 `Playing` 字段 | `public volatile bool Playing = false;` | ⏭️ |
| 5B-D2 | Desktop csproj 添加 NAudio NuGet | `NAudio` 2.2.1 + `NAudio.Vorbis` 1.5.0 | ⏭️ |
| 5B-D3 | Desktop csproj 包含 NAudio 源文件 | `Sound.NAudio.cs` + `NAudio_LoopStream.cs` + `SoundTouch_byMarkHeath/**` | ⏭️ |
| 5B-D4 | 修改 Sound.NAudio.cs：`Sound` → `NAudioSound : Sound` | 继承 SoundStub，override 虚方法，更新 SoundMixer 引用 | ⏭️ |
| 5B-D5 | DesktopHost.InitEmuera() 初始化音频 | `GlobalStatic.Sound = new NAudioSound[16]` + `GlobalStatic.Bgm = new NAudioSound()` | ⏭️ |
| 5B-D6 | 编译+运行验证 | 桌面端播放 WAV/OGG/BGM | ⏭️ |

#### 切片 5B-A：Android 音频实现

| # | 操作 | 说明 | 状态 |
|---|------|------|------|
| 5B-A1 | 创建 `AndroidSound : Runtime.Utils.Sound` | override 13 个虚方法，内部用 Android MediaPlayer | ✅ |
| 5B-A2 | Android 多通道 | 16 个 Sound 通道 = 16 个 AndroidSound 实例 | ✅ |
| 5B-A3 | Android BGM 循环 | `MediaPlayer.Looping = true` | ✅ |
| 5B-A4 | Android 音量映射 | `setVolume(0-100)` → `MediaPlayer.SetVolume(0.0-1.0)` | ✅ |
| 5B-A5 | Android 暂停/恢复 | `MediaPlayer.Pause()/Start()` + `CurrentPosition` 保存 | ✅ |
| 5B-A6 | Android 变速 | MediaPlayer 无原生变速，`setSpeed` 空实现 | ✅ |
| 5B-A7 | AndroidHost 初始化 GlobalStatic.Sound/Bgm | 创建 AndroidSound 实例 | ✅ |
| 5B-A8 | 编译验证 | Android 目标 0 错误 | ✅ |

#### 切片 5B-E：音频路径 + ERB 指令补全（P1）

| # | 操作 | 说明 | 状态 |
|---|------|------|------|
| 5B-E1 | 替换音频路径构建 | `Path.GetFullPath(".\\sound\\")` → `IPathProvider.MusicDir` + `Program.FileExists()` | ⏭️ |
| 5B-E2 | 升级 PLAYSOUND ArgumentBuilder | 支持 repeat 参数（移植 `SpHtmlPrint`） | ⏭️ |
| 5B-E3 | 移植 GETSOUNDORBGMINFO | `Creator.Method.cs` 新增方法 | ⏭️ |
| 5B-E4 | 移植 ISPLAYINGSOUND/ISPLAYINGBGM | `Creator.Method.cs` 新增方法 | ⏭️ |
| 5B-E5 | 移植 SOUNDCONTROL/BGMCONTROL | `Creator.Method.cs` 新增方法 | ⏭️ |
| 5B-E6 | 编译+运行验证 | — | ⏭️ |

### 切片 5C：SQL 系统

| #    | 操作          | 说明                     | 状态 |
| ---- | ----------- | ---------------------- | -- |
| 5C-1 | SQLite 路径适配 | `IPathProvider.SqlDir` | ✅  |
| 5C-2 | 编译+运行验证     | —                      | ✅  |

***

## Phase 6：EM 私家版 HTML 扩展（div + PrintHtmlC 全量版）✅

> **前置条件**：Phase 3.5 完成 ✅
> **知识库**：[html-rendering.md](file:///d:/emuera/shared-trae/knowledge/xemuera/html-rendering.md)、[graphics-rendering.md](file:///d:/emuera/shared-trae/knowledge/xemuera/graphics-rendering.md)
> **依赖关系**：6A（类型体系统一）→ 6B（HtmlManager 内部类+状态字段）→ 6C（div 标签解析+递归渲染）→ 6D（EscapedParts 注册）→ 6E（PrintHtmlC 全量版）
> **核心挑战**：xamarin 将 LazyLoading 的 `AConsoleDisplayNode` 分裂为 `AConsoleDisplayNode` + `AConsoleDisplayPart` 两个基类，ConsoleDivPart 无法放入 StrArray

### 切片 6A：类型体系统一 ✅

> **决策**：合并为统一基类 `AConsoleDisplayNode`，StrArray 改为 `AConsoleDisplayNode[]`

| #    | 操作                                               | 说明                                                                    | 状态 |
| ---- | ------------------------------------------------ | --------------------------------------------------------------------- | -- |
| 6A-1 | 评估合并方案影响                                         | 搜索所有 `AConsoleDisplayPart` 引用，评估改动范围                                  | ✅  |
| 6A-2 | 合并 `AConsoleDisplayPart` → `AConsoleDisplayNode` | 升级 Node 为完整版（Parent=ConsoleButtonString, rikaichaned, Ends, NextLine） | ✅  |
| 6A-3 | 更新 `ConsoleButtonString.StrArray` 类型             | `AConsoleDisplayPart[]` → `AConsoleDisplayNode[]`                     | ✅  |
| 6A-4 | 更新所有 StrArray 消费者                                | DrawTo/SetWidth/FilterEscaped/PrintStringBuffer/HtmlManager 等         | ✅  |
| 6A-5 | 编译验证                                             | 0 错误                                                                  | ✅  |

### 切片 6B：HtmlManager 内部类 + 状态字段 ✅

| #    | 操作                             | 说明                                                                                             | 状态 |
| ---- | ------------------------------ | ---------------------------------------------------------------------------------------------- | -- |
| 6B-1 | 添加 `HtmlAnalzeState` div 字段    | `StartingSubDivision`/`CurrentDivTag`/`SubDivisionWidth`/`DivState`                            | ✅  |
| 6B-2 | 添加 `HtmlAnalzeState` font 扩展属性 | `RenderMode`/`FontEdging`/`FontHinting`/`FontSize`                                             | ✅  |
| 6B-3 | 添加 `HtmlDivTag` 内部类            | Width/Height/X/Y/Color/Depth/StyledBox/IsRelative/Display/Lines                                | ✅  |
| 6B-4 | 添加 `DivState` 内部类              | IsDiv/PosX/PosY/Display/BackgroundColor/Width/Height/HasBorder/BorderWidth/BorderColor/Padding | ✅  |
| 6B-5 | 添加 `HtmlParentInfo` 内部类        | State/Stream/HasComment/HasReturn                                                              | ✅  |
| 6B-6 | 添加 `ParseParam4IntNum` 方法      | CSS 风格 1\~4 值参数解析（margin/border/padding/radius）                                                | ✅  |
| 6B-7 | 编译验证                           | 0 错误                                                                                           | ✅  |

### 切片 6C：div 标签解析 + 递归渲染 ✅

| #    | 操作                                 | 说明                                              | 状态 |
| ---- | ---------------------------------- | ----------------------------------------------- | -- |
| 6C-1 | 添加 `tagAnalyze` 的 `case "div"` 开标签 | 解析属性 → 创建 HtmlDivTag → StartingSubDivision=true | ✅  |
| 6C-2 | 添加 `tagAnalyze` 的 `case "div"` 闭标签 | CurrentDivTag=null                              | ✅  |
| 6C-3 | 添加 `html2DisplayLine` 主循环 div 递归分支 | StartingSubDivision → 递归调用 → new ConsoleDivPart | ✅  |
| 6C-4 | 添加 `<font>` 扩展属性解析                 | render/edging/hinting/size                      | ✅  |
| 6C-5 | 添加 `<img>` 扩展属性解析                  | srcm/cm/display/xpos                            | ✅  |
| 6C-6 | 编译验证                               | 0 错误                                            | ✅  |

### 切片 6D：EscapedParts 注册链路 ✅

| #    | 操作                                                             | 说明                                             | 状态 |
| ---- | -------------------------------------------------------------- | ---------------------------------------------- | -- |
| 6D-1 | 实现 `FilterEscaped()` 完整逻辑                                      | 遍历 StrArray 找 Top<0 或 Bottom>LineHeight 的 node | ✅  |
| 6D-2 | 在 `EmueraConsole.Print` 中添加 `ConsoleEscapedParts.Add()` 注册     | 每行打印后注册越界部件                                    | ✅  |
| 6D-3 | 在 CLEARLINE/MaxLog 清理中添加 `ConsoleEscapedParts.Remove/RemoveAt` | 行删除时同步清理记录                                     | ✅  |
| 6D-4 | 编译验证                                                           | 0 错误                                           | ✅  |

### 切片 6E：PrintHtmlC 全量版 ✅

| #    | 操作                                                       | 说明                          | 状态 |
| ---- | -------------------------------------------------------- | --------------------------- | -- |
| 6E-1 | 移植 `PrintStringBuffer.CurrentLineWidth`                  | 遍历 m\_buttonList 求和         | ✅  |
| 6E-2 | 移植 `PrintStringBuffer.AppendButton(ConsoleButtonString)` | 直接追加按钮对象                    | ✅  |
| 6E-3 | 移植 `HtmlManager.Html2ButtonList()`                       | HTML→按钮列表（buttonsOutput 分支） | ✅  |
| 6E-4 | 升级 `PrintHtmlC()` 为全量版                                   | 列内同行渲染 + 空白填充 + LC/RC 对齐    | ✅  |
| 6E-5 | 升级 `PrintHtml()` 为全量版                                    | toPrintBuffer 行内模式          | ✅  |
| 6E-6 | 编译验证                                                     | 0 错误                        | ✅  |

***

## Phase 7：XEmuera 上游跟进

> **来源**：XEmuera-R 0.60\~0.63 版本功能分析
> **知识库**：[xemuera-upstream-tracking.md](file:///d:/emuera/shared-trae/knowledge/xemuera/xemuera-upstream-tracking.md)
> **分析日期**：2026-05-16
> **分类原则**：内核更新（影响解释器行为）vs 框架更新（影响移动端 UI/UX）

### 切片 7A：编码自动识别（内核）— ❌ 已覆盖

> **研判修正（2026-05-16）**：LazyLoading 内核**已有** `EncodingHandler` 类（`Emuera/Runtime/Utils/EncodingHandler.cs`，76行），
> 实现了与 XEmuera `EncodingHelper` 等价的编码自动识别功能。
>
> **事实**：
>
> - `Config.Encode = EncodingHandler.UTF8BOMEncoding`（默认 UTF-8 BOM，非 Shift-JIS）
> - `Config.SaveEncode = EncodingHandler.UTF8BOMEncoding`
> - `EraStreamReader.Open()` 使用 `File.ReadAllLines(filepath, EncodingHandler.DetectEncoding(path))`
> - `EraDataStream.EraDataReader` 使用 `new StreamReader(file, EncodingHandler.DetectEncoding(file))`
> - 全项目 9 处已调用 `EncodingHandler.DetectEncoding()`
> - `LangManager.setEncode()` 仅影响字符串宽度计算（`GetStrlenLang`），不影响文件编码
> - `UseLanguage` 配置仅设置 `LangManager` 编码（932/949/936/950），不覆盖 `Config.Encode`
>
> **XEmuera vs LazyLoading 差异**：
>
> | 特性              | XEmuera        | LazyLoading                |
> | --------------- | -------------- | -------------------------- |
> | 默认编码            | SHIFT-JIS      | **UTF-8 BOM**              |
> | 自动识别            | EncodingHelper | EncodingHandler            |
> | EncoderFallback | 默认             | ExceptionFallback          |
> | BOM 检测          | byte\[] 手动比较   | Span<byte> + SequenceEqual |
>
> **结论**：无需移植，LazyLoading 编码系统已优于 XEmuera。

| #    | 操作                                     | 说明                             | 状态    |
| ---- | -------------------------------------- | ------------------------------ | ----- |
| 7A-1 | ~~移植 EncodingHelper~~                  | LazyLoading 已有 EncodingHandler | ❌ 不需要 |
| 7A-2 | ~~替换 EraStreamReader 编码选择~~            | 已使用 DetectEncoding             | ❌ 不需要 |
| 7A-3 | ~~替换 ErbLoader/HeaderFileLoader 编码选择~~ | 已使用 DetectEncoding             | ❌ 不需要 |
| 7A-4 | ~~添加 Config 选项 AutoDetectEncoding~~    | 已默认自动检测                        | ❌ 不需要 |
| 7A-5 | ~~编译+运行验证~~                            | —                              | ❌ 不需要 |

### 切片 7B：虚拟手柄映射（框架）— ❌ 不移植

> **研判修正（2026-05-16）**：虚拟手柄不移植。
> 理由：移动端触屏直接点击按钮比虚拟手柄更自然；虚拟手柄是键盘机/功能机时代的遗留设计；
> LazyLoading 内核已有 `VirtualSelectionDirection`/`ClickSelectedButton()`/`NavigateVirtualSelection()` 触屏选择导航，
> 无需额外虚拟手柄层。

| #    | 操作                                   | 说明        | 状态    |
| ---- | ------------------------------------ | --------- | ----- |
| 7B-1 | ~~移植虚拟手柄 XAML 布局~~                   | 触屏直接点击更自然 | ❌ 不移植 |
| 7B-2 | ~~移植 ActivateKeypadButton~~          | —         | ❌ 不移植 |
| 7B-3 | ~~移植 TryActivateKeywordButton~~      | —         | ❌ 不移植 |
| 7B-4 | ~~移植虚拟选择导航~~                         | 内核已有      | ❌ 不移植 |
| 7B-5 | ~~移植 RefreshVirtualControllerState~~ | —         | ❌ 不移植 |
| 7B-6 | ~~编译+运行验证~~                          | —         | ❌ 不移植 |

### 切片 7C：运行时字体缩放（框架，P1）

> **研判（2026-05-16 深化）**：分阶段实施。
>
> **LazyLoading 现状**：`ConfigCode.FontScale`/`AdaptiveFont`/`AdaptiveFontSize` 枚举已定义，
> 但核心 `Config.cs` 无 `FontScale` 属性，启动时不读取也不应用。
> Xamarin 子项目 `Config.cs` 已有 `FontScale` 属性 + AdaptiveFont 应用逻辑（可直接参考）。
>
> **三层差距**：
>
> 1. **配置层（低难度）**：添加 `FontScale` 属性 + 启动时应用 → 静态缩放重启生效
> 2. **运行时入口（中难度）**：`ApplyRuntimeDisplayScale()` + `AdjustRuntimeDisplayScale()` + `ToLogicalTouchPoint()`
> 3. **显示行重排（高难度）**：`ReflowDisplayLinesForCurrentScale()` — XEmuera 用 `HtmlManager.DisplayLine2Html` → `Html2DisplayLine` 序列化/反序列化重排，LazyLoading 无 HTML 中间层
>
> **分阶段策略**：先实现 7C-Phase1（静态缩放），后实现 7C-Phase2（动态缩放）

#### 7C-Phase1：静态缩放（配置层，低难度）

| #       | 操作                                             | 说明                                               | 状态 |
| ------- | ---------------------------------------------- | ------------------------------------------------ | -- |
| 7C-P1-1 | 在 LazyLoading `Config.cs` 添加 `FontScale` 属性    | 参考 Xamarin 子项目 Config.cs                         | ✅ |
| 7C-P1-2 | 在 `SetConfig()` 中读取 `ConfigCode.FontScale` 并应用 | `FontSize *= FontScale; LineHeight *= FontScale` | ✅ |
| 7C-P1-3 | 在 ConfigDialog 添加 FontScale 编辑                 | 数值 0.5\~5.0，步进 0.1                               | ✅ |
| 7C-P1-4 | 编译+运行验证                                        | 重启后缩放生效                                          | ✅ |

#### 7C-Phase2：动态缩放（运行时，高难度）

| #       | 操作                                       | 说明                                            | 状态 |
| ------- | ---------------------------------------- | --------------------------------------------- | -- |
| 7C-P2-1 | 移植 `ApplyRuntimeDisplayScale()`          | 缩放应用 + InitGameView 重初始化                      | ⏭️ |
| 7C-P2-2 | 移植 `ToLogicalTouchPoint()`               | 触摸坐标逻辑缩放转换                                    | ⏭️ |
| 7C-P2-3 | 移植 `AdjustRuntimeDisplayScale()`         | 缩放按钮 +/- 0.1 调整                               | ⏭️ |
| 7C-P2-4 | 实现 `ReflowDisplayLinesForCurrentScale()` | DisplayLine → HTML → 重新排版 → DisplayLine（核心难点） | ⏭️ |
| 7C-P2-5 | 添加缩放 UI 按钮                               | zoom\_in/zoom\_out 按钮                         | ⏭️ |
| 7C-P2-6 | 编译+运行验证                                  | —                                             | ⏭️ |

**前置条件**：Phase 4A-Xamarin 完成
**风险**：ReflowDisplayLines 依赖 HTML 中间层，LazyLoading 无此机制，需引入或另辟蹊径

### 切片 7D：外部字体加载（框架，P1）

> **研判（2026-05-16 深化）**：推荐最小方案。
>
> **LazyLoading 现状**：`Program.cs` 启动时扫描 `ExeDir + "font\\"` 目录（仅 `font`，不支持 `fonts`），
> 扩展名 `*.ttf`/`*.otf`/`*.ttc`，直接 `SKTypeface.FromFile` 加入 `GlobalStatic.CustomTypefaces`。
> `FontFactory` 有 4 层回退链但无别名映射、无 FamilyName 去重、无版本后缀剥离。
>
> **关键差距**：当 ERB 脚本写 `SETFONT 等距时代黑体 SC` 时，XEmuera 通过别名映射找到 `EraMonoSC` 字体文件，
> LazyLoading 会回退到默认字体。
>
> **两种方案**：
>
> - **最小方案（推荐）**：扩展目录扫描 + 在 `FontFactory.CreateTypefaceWithFallback` 插入精确别名解析层
> - **完整方案**：移植 `FontModel` 的完整别名系统

#### 7D-Phase1：最小方案（推荐）

| #       | 操作                                     | 说明                                   | 状态 |
| ------- | -------------------------------------- | ------------------------------------ | -- |
| 7D-P1-1 | 扩展 `Program.cs` 扫描 `fonts` 目录          | 同时支持 `font` 和 `fonts`                | ⏭️ |
| 7D-P1-2 | 在 `FontFactory` 添加 `FontAliases` 字典    | 文件名/FamilyName → SKTypeface 映射       | ⏭️ |
| 7D-P1-3 | 在 `CreateTypefaceWithFallback` 插入别名解析层 | 系统字体查找失败后、CustomTypefaces 遍历之前       | ⏭️ |
| 7D-P1-4 | 添加 `KnownExternalFontAliases` 硬编码表     | EraMonoSC/等距时代黑体SC、EraPixel/eraPixel | ⏭️ |
| 7D-P1-5 | 编译+运行验证                                | —                                    | ⏭️ |

#### 7D-Phase2：完整方案（可选扩展）

| #       | 操作                        | 说明                                     | 状态 |
| ------- | ------------------------- | -------------------------------------- | -- |
| 7D-P2-1 | 移植 `StripVersionSuffix()` | `EraMonoSC-v2` → `EraMonoSC`           | ⏭️ |
| 7D-P2-2 | 移植 `FontNameMapping` 双向字典 | 日文名/英文名互转                              | ⏭️ |
| 7D-P2-3 | 添加 FamilyName 去重          | `AllModels.ContainsKey(familyName)` 检查 | ⏭️ |
| 7D-P2-4 | 添加字体启用/禁用排序               | `EnabledList`/`DisabledList` 优先级控制     | ⏭️ |
| 7D-P2-5 | 添加字体选择 UI                 | `FontSettingsPage`                     | ⏭️ |
| 7D-P2-6 | 编译+运行验证                   | —                                      | ⏭️ |

**前置条件**：Phase 3D（FontFactory 完善）完成 ✅

### 切片 7E：本地化系统（框架，P2）

> **研判（2026-05-16 深化）**：内核层已完成，仅需 UI 层。
>
> **LazyLoading 现状**：`EvilMask.Emuera.Lang` 完整本地化框架已就绪：
>
> - `TranslatableString` + 反射注册 + XML 翻译文件加载
> - 已有嵌入资源：`emuera-zhs.xml`（简体中文）、`emuera-eng.xml`（English）
> - 已有 `<ExeDir>/lang/` 目录扫描 + 嵌入资源扫描
> - `Config.EmueraLang` 配置项控制语言选择
> - **缺失**：无移动端 UI 选择器、无 `CultureInfo.CurrentUICulture` 联动
>
> **无需移植** **`LanguageModel`**：LazyLoading 的 `Lang` 类翻译加载机制比 XEmuera 更完整，
> 只需添加 UI 选择器调用 `Config.EmueraLang` + `Lang.SetLanguage()` 即可。

| #    | 操作                                   | 说明                                                    | 状态 |
| ---- | ------------------------------------ | ----------------------------------------------------- | -- |
| 7E-1 | 移植 `LanguagePage`                    | ListView 选择语言 + 重启提示                                  | ⏭️ |
| 7E-2 | 适配持久化方式                              | XEmuera 用 `Preferences`，LazyLoading 用 `emuera.config` | ⏭️ |
| 7E-3 | 添加 `CultureInfo.CurrentUICulture` 联动 | 语言切换时同步设置                                             | ⏭️ |
| 7E-4 | 编译+运行验证                              | —                                                     | ⏭️ |

**前置条件**：Phase 4A-Xamarin 完成

### 切片 7F：解释器日志查看器（框架，P2）

> **研判（2026-05-16 深化）**：实现简单，可直接移植。
>
> **LazyLoading 现状**：`EmueraConsole.OutputLog()`/`OutputSystemLog()` 写入 `Program.ExeDir + "emuera.log"`，
> 错误/崩溃时自动输出。桌面端直接用文本编辑器打开，移动端需查看器。
>
> **XEmuera 的** **`TxtFilePage`** 是通用文本查看器（`Func<string>` 加载器 + Copy + Refresh），
> 可直接移植。日志读取用 `EncodingHandler.DetectEncoding()` 解码。

| #    | 操作               | 说明                                                                  | 状态 |
| ---- | ---------------- | ------------------------------------------------------------------- | -- |
| 7F-1 | 移植 `TxtFilePage` | 通用文本查看器 + SelectableLabel + Copy + Refresh                          | ⏭️ |
| 7F-2 | 在侧边菜单添加"解释器日志"入口 | `Func<string>` 读取 `emuera.log` + `EncodingHandler.DetectEncoding()` | ⏭️ |
| 7F-3 | 编译+运行验证          | —                                                                   | ⏭️ |

**前置条件**：Phase 4A-Xamarin 完成

### 切片 7G：游戏列表+侧边菜单（框架，P0）

> **研判（2026-05-16 深化）**：移动端导航基础，与 4A-X6 合并。
>
> **XEmuera 导航架构**：
>
> ```
> App.MainPage (FlyoutPage)
> ├── Flyout: MainMenuPage (侧边菜单)
> │   ├── 设置 → PushAsync(SettingsPage)
> │   ├── 解释器日志 → PushAsync(TxtFilePage)
> │   ├── 重启 → MainWindow.MainMenu_Reboot()
> │   └── 回到标题 → MainWindow.MainMenu_GotoTitle()
> └── Detail: NavigationPage
>     ├── [0] GameItemPage (游戏列表，始终在栈底)
>     ├── [1] MainWindow (游戏运行页，PushAsync 进入)
>     └── [2] SettingsPage / TxtFilePage (从侧边菜单进入)
> ```
>
> **LazyLoading 现状**：桌面版直接创建 WinForms `MainWindow`，无游戏选择界面，无 Flyout/导航栈概念。
> `GameItemModel` 的整个概念是移动端特有的——移动端需要在一个根目录下管理多个游戏。
>
> **实施要点**：
>
> - `GameItemModel.Load()` 扫描根目录下含 ERB/erb 子目录的文件夹
> - `GameUtils.StartEmuera(path)` 设置游戏路径 → 加载字体 → 创建 MainWindow → PushAsync
> - `MainMenuPage` 动态构建菜单项（运行中显示重启/回到标题，始终显示设置）
> - 持久化用 `Xamarin.Essentials.Preferences`（收藏/最后游戏路径），需适配

| #    | 操作                                  | 说明                                                      | 状态 |
| ---- | ----------------------------------- | ------------------------------------------------------- | -- |
| 7G-1 | 移植 `GameItemModel` + `GameItemPage` | 游戏列表 + 长按配置编辑                                           | ⏭️ |
| 7G-2 | 移植 `MainMenuPage`                   | 侧边菜单（重启/回到标题/设置/解释器日志）                                  | ⏭️ |
| 7G-3 | 移植 `GameUtils` 游戏管理                 | `StartEmuera()`/`CurrentGamePath`/`PrefKeyLastGamePath` | ⏭️ |
| 7G-4 | 编译+运行验证                             | —                                                       | ⏭️ |

**前置条件**：Phase 4A-Xamarin 完成
**与 4A-X6 的关系**：7G 是 4A-X6 的细化，合并执行

***

## 切片 9F：Android 兼容性补全（自拷问产出）

> **来源**：2026-05-19 系统性自拷问（grill session）
> **知识库**：[maui-android-compat.md §11-17](file:///d:/emuera/shared-trae/knowledge/erabasic/maui-android-compat.md)
> **前置条件**：9E ✅（引擎完整启动）

### 切片 9F-P0-A：AdaptiveFont 默认值修复（字体偏大根因）

> **根因**：`AdaptiveFont=true`（默认值）导致 `FontSize` 被 `AdaptiveFontSize`（默认 32）覆盖，用户设置 `FONT SIZE:14` 实际生效为 32
> **修复方案**：修改 ConfigData.cs 中 AdaptiveFont 默认值为 false
> **参考**：[maui-android-compat.md §16](file:///d:/emuera/shared-trae/knowledge/erabasic/maui-android-compat.md)
> **优先级理由**：一行代码修复，影响面最大（字体/图片/界面尺寸全部偏大），应最先做

| # | 操作 | 说明 | 状态 |
|---|------|------|------|
| 9F-P0-A1 | 修改 AdaptiveFont 默认值为 false | ConfigData.cs 中 `AdaptiveFont` 默认从 `true` 改为 `false` | ✅ |
| 9F-P0-A2 | ADB 验证 | 确认 FontSize 生效为 14 而非 32，字体/界面尺寸与 XEmuera 对齐 | ⏭️ |

### 切片 9F-P0-B：TOFULL/TOHALF 跨平台替代

> **根因**：Microsoft.VisualBasic 在 Android 不可用，TOFULL/TOHALF 调用 StrConv 抛出 MissingMethodException
> **修复方案**：跨平台全角/半角转换，不依赖 Microsoft.VisualBasic
> **参考**：[maui-android-compat.md §7](file:///d:/emuera/shared-trae/knowledge/erabasic/maui-android-compat.md)

| # | 操作 | 说明 | 状态 |
|---|------|------|------|
| 9F-P0-1 | 实现 `ToFullWidth(string)` | 数字→全角数字，A-Z→全角大写，a-z→全角小写 | ✅ |
| 9F-P0-2 | 实现 `ToHalfWidth(string)` | 全角数字→半角，全角字母→半角 | ✅ |
| 9F-P0-3 | 条件编译切换 | `#if WINDOWS` 用 StrConv，`#else` 用跨平台实现 | ✅ |
| 9F-P0-4 | 编译+ADB 验证 | eratw-chs 中 TOFULL/TOHALF 调用不再崩溃 | ⏭️ |

### 切片 9F-P0-C：CALLPLUGIN null guard

> **根因**：PluginManager.GetMethod(name) 返回 null，CALLPLUGIN_Instruction 可能 NullReferenceException
> **修复方案**：在 CALLPLUGIN_Instruction 中添加 null 检查，null 时跳过并设置 RESULT=0
> **风险**：eratw-chs 不使用 CALLPLUGIN，风险极低

| # | 操作 | 说明 | 状态 |
|---|------|------|------|
| 9F-P0-5 | CALLPLUGIN null guard | `if (method == null) { result = 0; return; }` | ✅ |
| 9F-P0-6 | 编译验证 | — | ✅ |

### 切片 9F-P1-B：IsActive + InputState 平台抽象 + BadApple 触发（✅ 已验证无需修改）

> **验证结果**：xamarin 版 IsActive 已正确（Created→IsEnabled→true），WinInput 已跨平台（内部数组，不依赖 user32.dll）
> **结论**：GETKEYTRIGGERED/BadApple 的前置条件已满足，无需额外修改

| # | 操作 | 说明 | 状态 |
|---|------|------|------|
| 9F-P1-1 | ~~实现 InputState 平台抽象~~ | xamarin WinInput 已跨平台，无需额外抽象 | ✅ 无需修改 |
| 9F-P1-2 | ~~修复 IsActive 返回 true~~ | xamarin 版 IsActive 已返回 true（Created→IsEnabled→true） | ✅ 无需修改 |
| 9F-P1-3 | BadApple 结束触发 | 基于 InputState 检测任意按键→退出演示 | ⏭️ |
| 9F-P1-4 | ADB 验证 | BadApple 演示可通过键盘输入退出 | ⏭️ |

### 切片 9F-P1-A：emuera.config 外部存储复制

> **根因**：Android 端 emuera.config 仅从 Context.FilesDir 加载，MuMu 共享文件夹中的配置文件不在加载路径
> **修复方案**：AndroidHost 初始化时检查外部存储是否有 emuera.config，如有则复制到内部存储

| # | 操作 | 说明 | 状态 |
|---|------|------|------|
| 9F-P1-5 | 实现外部存储配置复制 | AndroidHost.InitEmuera() 中检查并复制 | ✅ |
| 9F-P1-6 | 验证配置覆盖 | 确认外部存储的 emuera.config 能正确覆盖默认配置 | ⏭️ |

### 切片 9F-P2：FontScale/AdaptiveFontSize 显示差异调整

> **根因**：FontScale 叠加 + AdaptiveFontSize + ScreenDensity 三重缩放导致字体偏大
> **修复方案**：系统性调整缩放参数

| # | 操作 | 说明 | 状态 |
|---|------|------|------|
| 9F-P2-1 | 调整 FontScale 默认值 | 确保默认为 1.0，不叠加放大 | ⏭️ |
| 9F-P2-2 | 审查 AdaptiveFontSize 逻辑 | 确认是否在 Android 端过度放大 | ⏭️ |
| 9F-P2-3 | ADB 真机验证 | 对比 XEmuera 和 LazyLoading 的字体大小 | ⏭️ |

### 切片 9F-P1-C：移植运行时缩放 + 虚拟控制器

> **根因**：当前项目缺失 XEmuera 的运行时缩放和虚拟控制器功能
> **参考**：[maui-android-compat.md §17](file:///d:/emuera/shared-trae/knowledge/erabasic/maui-android-compat.md)
> **XEmuera 参考**：XEmuera/Emuera/Forms/MainWindow.xaml + MainWindow.xaml.cs

#### 9F-P1-RT：运行时缩放

| # | 操作 | 说明 | 状态 |
|---|------|------|------|
| 9F-P1-RT1 | 移植 `Config.SetRuntimeFontScale()` | 运行时修改 FontScale 并重新计算 FontSize/LineHeight | ✅ |
| 9F-P1-RT2 | 移植 `Config.ApplyRuntimeDisplaySettings()` | 重新读取 AdaptiveFont/AdaptiveFontSize/FontScale 并应用 | ✅ |
| 9F-P1-RT3 | 移植 `MainWindow.ApplyRuntimeDisplayScale()` | 运行时动态调整 FontScale + 重算 ScaledWindowX + Reflow | ✅ |
| 9F-P1-RT4 | 移植 `EmueraConsole.ReflowDisplayLinesForCurrentScale()` | 缩放后重新排版所有显示行 | ✅ |
| 9F-P1-RT5 | 移植 `MainWindow.ToLogicalTouchPoint()` | 运行时缩放后触摸坐标转换（预留，当前 no-op） | ✅ |
| 9F-P1-RT6 | 编译+ADB 验证 | A+/A- 缩放功能正常 | ⏭️ ADB 验证 |

#### 9F-P1-VC：虚拟控制器

| # | 操作 | 说明 | 状态 |
|---|------|------|------|
| 9F-P1-VC1 | 移植 MainWindow.xaml 虚拟控制器 XAML | virtualControllerLayout Grid + 方向键 + 数字键盘 + AVG 按钮 + 确认/返回 | ✅ C# 代码构建 |
| 9F-P1-VC2 | 移植 Style（VirtualControlButtonStyle/VirtualRoundButtonStyle/ToolActionButtonStyle） | 3 个缺失的按钮样式 | ✅ C# 样式 |
| 9F-P1-VC3 | 移植 virtual_controller_button + zoom_in/zoom_out 按钮 | 工具栏中的 3 个缺失按钮 | ✅ 🎮 按钮 + A+/A- |
| 9F-P1-VC4 | 移植虚拟控制器事件处理器（×20+） | 方向键/数字键盘/AVG/确认返回/缩放 | ✅ |
| 9F-P1-VC5 | 移植 `RefreshVirtualControllerState()` | 根据输入状态更新虚拟控制器按钮可见性 | ✅ |
| 9F-P1-VC6 | 编译+ADB 验证 | 虚拟控制器功能正常 | ⏭️ ADB 验证 |

#### 9F-P1-SM：智能按钮映射（INPUT→虚拟控件）

> **核心差异化功能**：XEmuera 能扫描当前屏幕上的 INPUT 按钮，按关键词匹配映射为右下角的虚拟确认/返回/AVG 按钮，使触控操作更便捷
> **当前项目**：仅有 QuickButton（静态显示 INPUT 选项），无智能关键词匹配和虚拟控件映射

| # | 操作 | 说明 | 状态 |
|---|------|------|------|
| 9F-P1-SM1 | 移植 `EmueraConsole.NormalizeVirtualButtonText()` | 标准化按钮文本（全角→半角等） | ✅ |
| 9F-P1-SM2 | 移植 `EmueraConsole.CollectVisibleSelectionCandidates()` | 收集当前屏幕上所有可选按钮候选 | ✅ |
| 9F-P1-SM3 | 移植 `EmueraConsole.FindVisibleButtonByKeywords()` | 按关键词模糊匹配可见按钮 | ✅ |
| 9F-P1-SM4 | 移植 `EmueraConsole.FindVisibleButtonByLabels()` | 按标签精确匹配可见按钮 | ✅ |
| 9F-P1-SM5 | 移植 `EmueraConsole.GetVisibleButtonKeywordLabel()` | 获取含关键词的按钮文本 | ✅ |
| 9F-P1-SM6 | 移植 `EmueraConsole.HasRecentDisplayLineKeyword()` | 在最近 N 行中搜索关键词（检测数字键盘模式） | ✅ |
| 9F-P1-SM7 | 移植 `EmueraConsole.TrySelectVisibleButtonByKeywords/Labels()` | 选中匹配的按钮 | ✅ |
| 9F-P1-SM8 | 移植 `MainWindow.TryActivateKeywordButton()` | 虚拟按钮点击→激活匹配的 INPUT 按钮 | ✅ |
| 9F-P1-SM9 | 集成到 RefreshVirtualControllerState | 确认/返回/AVG/数字键盘的智能映射 | ✅ |
| 9F-P1-SM10 | 编译+ADB 验证 | INPUT 按钮能正确映射到虚拟控件 | ⏭️ ADB 验证 |

---

## Phase 9G：v0.30 ADB 验证反馈修复

> **来源**：v0.30 ADB 真机测试用户反馈的 6 个问题
> **知识库**：[maui-android-compat.md §18-§23](file:///d:/emuera/shared-trae/knowledge/erabasic/maui-android-compat.md)
> **优先级**：P0（阻塞游戏）> P1（影响可玩性）> P2（功能缺失）

### 切片 9G-P0：ProcessState.Return NRE + SQL 嵌套事务 ✅

> **问题**：无法进入正常游戏，函数结尾发生错误
> **根因**：`ProcessState.ReturnF` 在 `functionList.Count=0` 时崩溃 + SQL 嵌套事务
> **修复**：ReturnF 添加 `functionList.Count == 0` 空检查 + 3个Import方法添加嵌套事务保护（try BeginTransaction / catch InvalidOperationException）

| # | 操作 | 说明 | 状态 |
|---|------|------|------|
| 9G-P0-1 | 确认 ProcessState.Return 修复已包含 | Return 已有空检查，ReturnF 缺少 → 已添加 | ✅ |
| 9G-P0-2 | 修复 SQL 嵌套事务 | ImportMapXml/ImportDtXml/ImportXmlCustom 添加 ownsTransaction 保护 | ✅ |
| 9G-P0-3 | 完整分析 emuera.log 错误链 | Return NRE 是主要阻塞原因 | ✅ |
| 9G-P0-4 | 重建 APK + ADB 验证 | 能正常进入游戏 | ⏭️ |

### 切片 9G-P1：BADAPPLE AWAIT 16 帧同步失效 ✅

> **问题**：循环和动画以最快速度进行，音画不同步，无法关闭
> **根因**：`AWAIT` 不带参数时 `Thread.Sleep` 不执行，ERB 循环以最快速度运行，UI 线程无时间处理重绘
> **修复**：AWAIT 不带参数时添加 `Thread.Sleep(1)` 让出 UI 线程时间片

| # | 操作 | 说明 | 状态 |
|---|------|------|------|
| 9G-P1-1 | 修复 AWAIT 不带参数时无等待 | `else Thread.Sleep(1)` 让出时间片 | ✅ |
| 9G-P1-2 | 编译验证 | Android 项目编译通过 | ✅ |
| 9G-P1-3 | ADB 验证 BADAPPLE 播放 | 确认帧同步和退出 | ⏭️ |

### 切片 9G-P2：运行时字体缩放渲染错误 ✅

> **问题**：A+ 放大文字重叠，A- 缩小出现随机大空隙
> **根因**：`DrawTextUtils.FontCache.ValidChar` 缓存了按旧 TextSize 计算的 Advance 值，缩放后未清除
> **修复**：`ApplyRuntimeDisplaySettings` 中调用 `DrawTextUtils.Reset()` 清除旧缓存

| # | 操作 | 说明 | 状态 |
|---|------|------|------|
| 9G-P2-1 | 对比 XEmuera `ApplyRuntimeDisplayScale()` | 找出差异：缺少 DrawTextUtils.Reset() | ✅ |
| 9G-P2-2 | 修复 FontCache 缓存失效 | ApplyRuntimeDisplaySettings 中添加 DrawTextUtils.Reset() | ✅ |
| 9G-P2-3 | 编译验证 | Android 项目编译通过 | ✅ |
| 9G-P2-4 | ADB 验证 A+/A- 缩放 | 文字不重叠不空隙 | ⏭️ |

### 切片 9G-P3：HTML 交互控件在向上滚动时失效（✅ 已修复）

> **问题**：向上滚动触发历史模式，按钮变灰不可选，屏幕外按钮无法选中
> **根因**：Android 端 `MouseReleased` bug——`MoveMouse` 在 `isBackLog` 时跳过，导致 `SelectedString` 为 null
> **用户确认**：桌面端在历史模式下按钮仍然可用，这是 Android 端自身问题
> **修复**：滚回底部后重新 `MoveMouse` 更新 `selectingButton`，然后重新获取 `str`

| # | 操作 | 说明 | 状态 |
|---|------|------|------|
| 9G-P3-1 | 修复 MouseReleased isBacklog 逻辑 | 滚回底部后重新 MoveMouse + 获取 SelectedString | ✅ |
| 9G-P3-2 | 编译验证 | Android 项目编译通过 | ✅ |
| 9G-P3-3 | ADB 验证 | 屏幕外按钮可通过滚动后点击选中 | ⏭️ |

### 切片 9G-P4：虚拟控制器默认可见 ✅

> **问题**：用户反馈"没有看见控件按钮"，实际是默认隐藏
> **修复**：`virtualControllerVisible` 默认 `true` + `IsVisible = true` + `SetConsole` 时调用 `RefreshVirtualControllerState`

| # | 操作 | 说明 | 状态 |
|---|------|------|------|
| 9G-P4-1 | 修改默认值 + IsVisible + SetConsole 刷新 | AndroidMainWindow.cs | ✅ |
| 9G-P4-2 | ADB 验证 | 虚拟控制器默认可见 | ⏭️ |

### 切片 9G-P5：移植收起工具栏按钮 + 滚动按钮 ✅

> **问题**：缺少 XEmuera 的收起工具栏按钮和滑块移动窗口按钮
> **修复**：添加 ▲收起/▼展开工具栏 + ↑↓半页滚动按钮

| # | 操作 | 说明 | 状态 |
|---|------|------|------|
| 9G-P5-1 | 移植收起/展开工具栏按钮 | ▲隐藏 → ▼恢复 | ✅ |
| 9G-P5-2 | 移植滚动按钮 | ↑↓半页滚动（DisplayUtils.PicBoxHeight / LineHeight / 2） | ✅ |
| 9G-P5-3 | ADB 验证 | 两个按钮功能正常 | ⏭️ |
