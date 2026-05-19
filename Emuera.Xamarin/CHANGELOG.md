# Emuera.Xamarin 变更记录

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
- 生成 `com.xemuera.app-Signed.apk`（~30.5 MB）
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
