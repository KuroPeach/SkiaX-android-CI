# Emuera.Xamarin 任务计划

> **最后更新**：2026-05-16
> **历史记录**：[CHANGELOG.md](file:///d:/emuera/emuera_lazyloading_selfmodified_version/emuera.xamarin/CHANGELOG.md)

## 当前状态

- ✅ Phase 0-2 已完成（编译通过 + 桌面端可启动）
- ✅ **Phase 4A-Desktop** 已完成（Stub 扩展 + 30+ 处注释代码恢复）
- 🔄 **Phase 3** — 下一步（Skia 渲染集成）
- ⏭️ Phase 4A-Xamarin / Phase 4B / Phase 5

---

## Phase 2（剩余延后项）

### 切片 2D：键鼠输入抽象 — ⏭️ 延后至移动端

> 延后理由：当前 TFM `net8.0-windows` 下 `WinInput.GetKeyState` 和 `Control.MousePosition` 均可正常工作

| # | 操作 | 说明 |
|---|------|------|
| 2D-1 | 创建 `InputState` 具体类 | `lock` 保护的按键状态数组 + 鼠标位置 + 鼠标按钮值 |
| 2D-2 | 修改 Creator.Method.cs 中 3 个方法 | `GetKeyStateMethod`/`MouseButtonMethod`/`MousePosMethod` 从 InputState 读取 |
| 2D-3 | 触摸事件写入 InputState | `EraPictureBox_Touch` → `SetKeyDown/Up` + `SetMousePosition` + `SetMouseButtonValue` |
| 2D-4 | Windows 事件写入 InputState | `KeyDown/Up` + `MouseDown/Up` → `SetKeyDown/Up`（保持 WinInput 兼容） |
| 2D-5 | 编译验证 | — |

**方案决策**：采用 InputState 具体类方案（方案 C）。详见 [await-async.md](file:///d:/emuera/shared-trae/knowledge/xemuera/await-async.md)。

### 切片 2E（移动端待验证项）

| # | 操作 | 说明 |
|---|------|------|
| 2E-2 | 验证 TINPUT/TINPUTS Timer | Android 上 Timer 行为 |
| 2E-3 | 验证 GETKEYTRIGGERED/MOUSEB 触摸映射 | 配合 2D 的 InputState 触屏映射 |

---

## Phase 3：Skia 渲染集成

> **知识库**：[graphics-rendering.md](file:///d:/emuera/shared-trae/knowledge/xemuera/graphics-rendering.md)、[font-rendering.md](file:///d:/emuera/shared-trae/knowledge/xemuera/font-rendering.md)
> **前置条件**：Phase 2 + 4A-Desktop 完成 ✅
> **勘探日期**：2026-05-16
> **关键发现**：
> 1. LazyLoading 内核渲染管线**已完全 Skia 化**，GDI+ 仅在光栅字体回退路径残留
> 2. xamarin OnPaint 是**旧版简化**，缺少背景烘焙/图层混合/EscapedParts/Rikaichan
> 3. Drawing/ 目录的 DrawBitmapUtils + DrawTextUtils 是**完整实现**，可直接使用
> 4. ImageLayerManager 和 ColorMatrixHelper 是**P0 级 stub**，必须优先实现
> 5. FontFactory 需要从简化版升级为完整版（4层回退链+缓存）

### 切片 3A：ImageLayerManager 图层管理（P0）

> **目标**：实现 SETIMAGELAYER 系列指令的图层管理
> **参考**：LazyLoading `Emuera/UI/Game/ImageLayerManager.cs`（117行，完全 Skia）

| # | 操作 | 说明 | 状态 |
|---|------|------|------|
| 3A-1 | 升级 `ImageLayer` 数据结构 | 添加 `ASprite Image`（替换 `object`）、`SKColorFilter?`、`SpriteAnime` 动画管理 | ✅ |
| 3A-2 | 实现 `SetLayer()` | 创建 ImageLayer 并存入 `_layers` 字典，查找 ASprite | ✅ |
| 3A-3 | 实现 `DrawAll()` → 改名 `DrawTo()` | 按 Depth 升序绘制，视口裁剪，离屏暂停动画，FollowScroll 偏移 | ✅ |
| 3A-4 | 集成到 EmueraConsole.OnPaint | 添加 `_imageLayerManager.DrawTo(graph, ...)` 调用 | ✅ |
| 3A-5 | 编译+运行验证 | 0 编译错误 | ✅ |

**隐含依赖**：`AppContents.CreateSprite<XXX>()` 系列方法需能正确返回 ASprite（当前 `CreateSpriteFromFileDynamic` 返回 false）

### 切片 3B：ColorMatrix 颜色矩阵（P0）

> **目标**：实现 GSETCOLORMATRIX 指令和 HTML img cm 属性
> **参考**：LazyLoading `Emuera/UI/Game/image/ColorMatrixHelper.cs`（84行，完全 Skia）

| # | 操作 | 说明 | 状态 |
|---|------|------|------|
| 3B-1 | 重写 `CreateColorMatrix()` | 移除（无调用者，直接用 SKColorFilter.CreateColorMatrix） | ✅ |
| 3B-2 | 实现 `ReadFromVariableTerm()` | 从 ERB 变量表达式读取 5x5 矩阵，调用 `ToSkia()` 转换 | ✅ |
| 3B-3 | 实现 `ReadFromVariable()` | 从 VariableToken 读取 2D/3D 数组，支持角色变量 | ✅ |
| 3B-4 | 验证 `SKColorFilter.CreateColorMatrix()` | 确认 offset × 255 转换正确 | ✅ |
| 3B-5 | 编译+运行验证 | 0 编译错误 | ✅ |

### 切片 3C：OnPaint 升级（P0）

> **目标**：将 xamarin OnPaint 从旧版简化升级为 LazyLoading 完整版
> **这是 Phase 3 的核心工作量**

| # | 操作 | 说明 | 状态 |
|---|------|------|------|
| 3C-1 | 移植 `ValidateBackground()` + `bakedBackground` | 背景预烘焙机制 | ⏳ |
| 3C-2 | 移植 EscapedParts + CBG 混合渲染 | 按 zdepth 降序排列，depth=0 时绘制文本行 | ⏳ |
| 3C-3 | 集成 `_imageLayerManager.DrawTo()` | 在背景和文本之间绘制图层 | ⏳ |
| 3C-4 | 移植 `rikaichan.OnPaint()` | Rikaichan 悬浮提示渲染 | ⏳ |
| 3C-5 | 移植 `_htmlElementList` HTML 元素绘制 | HTML_PRINT 岛屿输出 | ⏳ |
| 3C-6 | 编译+运行验证 | — | ⏳ |

**风险**：OnPaint 升级涉及大量行级修改，需逐段 diff LazyLoading 版和 xamarin 版

### 切片 3D：FontFactory 完善（P1）

> **目标**：从简化版升级为完整版字体工厂
> **参考**：LazyLoading `Emuera/UI/FontFactory.cs`（294行）

| # | 操作 | 说明 | 状态 |
|---|------|------|------|
| 3D-1 | 移植字体回退链 | 4 层回退：自定义→安全字体→MatchCharacter→Default | ⏳ |
| 3D-2 | 实现 `GetFont()` 完整参数 | 应用 style/edging/hinting 参数 | ⏳ |
| 3D-3 | 实现 `IsRasterFont()` | 检测 MS Gothic 等光栅字体 | ⏳ |
| 3D-4 | 实现字体缓存 | `fontDic` + `fallbackTypefaceCache` | ⏳ |
| 3D-5 | 实现 `GetFallbackFontForChar()` | 按字符获取回退字体 | ⏳ |
| 3D-6 | 编译+运行验证 | — | ⏳ |

**注意**：光栅字体的 GDI 桥接路径（`TextRenderer.DrawText` → `SKBitmap`）在 Android/iOS 不可用，需条件编译或替代方案

### 切片 3E：ConsoleDivPart 实现（P1）

> **目标**：实现 div 容器的渲染和宽度计算

| # | 操作 | 说明 | 状态 |
|---|------|------|------|
| 3E-1 | 移植 `ConsoleDivPart.DrawTo()` | div 容器绘制逻辑 | ⏳ |
| 3E-2 | 移植 `ConsoleDivPart.SetWidth()` | div 宽度计算 | ⏳ |
| 3E-3 | 编译+运行验证 | — | ⏳ |

### 切片 3F：GraphicsImage 缺失方法（P2）

> **目标**：补全 GBitmapToInt64Array/GByteArrayToBitmap 等像素操作

| # | 操作 | 说明 | 状态 |
|---|------|------|------|
| 3F-1 | 实现 `GBitmapToInt64Array()` | SkiaSharp 像素读取 | ⏳ |
| 3F-2 | 实现 `GByteArrayToBitmap()` | SkiaSharp 像素写入 | ⏳ |
| 3F-3 | 修复 `GDrawLine()`/`GDrawPolygon()` | surface 未写回 Bitmap 的 bug | ⏳ |
| 3F-4 | 编译+运行验证 | — | ⏳ |

### 切片 3G：增量渲染（可选优化，P3）

| # | 操作 | 说明 | 状态 |
|---|------|------|------|
| 3G-1 | 移植 `DirtyLineTracker` | 脏行追踪 | ⏭️ 可选 |
| 3G-2 | 移植 `IncrementalRenderer` | 增量渲染 | ⏭️ 可选 |
| 3G-3 | 移植 `RenderLoop` | 渲染循环 | ⏭️ 可选 |
| 3G-4 | 性能对比测试 | 全屏重绘 vs 增量渲染 | ⏭️ 可选 |

---

## Phase 4：Xamarin.Forms UI 集成

> **知识库**：[ui-integration.md](file:///d:/emuera/shared-trae/knowledge/xemuera/ui-integration.md)
> **前置条件**：Phase 3 完成（4A-Desktop 已提前完成）

### 切片 4A：主页面集成

#### 子阶段 4A-Desktop：扩展 Stub + DesktopMainWindow（当前 TFM）

> **目标**：取消注释 xamarin EmueraConsole 中更多 `window.*` 引用，逐步恢复 LazyLoading EmueraConsole 完整功能

| # | 操作 | 说明 | 状态 |
|---|------|------|------|
| 4A-D1 | 扩展 `MainPicBoxStub` | 添加 `Width`/`Height`（连接实际尺寸）、`PointToClient(Point)`、`ClientRectangle` | ✅ |
| 4A-D2 | 扩展 `TextBoxStub` | 添加 `ForeColor`、`Font` 属性；DesktopMainWindow 连接 WinForms TextBox | ✅ |
| 4A-D3 | 实现 `window.Invoke(Action)` 同步版本 | Stub 添加 virtual 方法；DesktopMainWindow 用 `ManualResetEventSlim` + `form.BeginInvoke` 实现 | ✅ |
| 4A-D4 | 实现 `window.Created` 替代 | Stub 添加 `Created` 属性（别名 `IsEnabled`）；DesktopMainWindow override `!form.IsDisposed` | ✅ |
| 4A-D5 | 创建 `ToolTipStub` 空实现 | Show/RemoveAll/InitialDelay/AutoPopDelay/ForeColor/BackColor/OwnerDraw/Draw/Popup 全部空实现 | ✅ |
| 4A-D6 | 补充其他缺失接口 | `Text`（别名 Title）、`ShowConfigDialog()`（空）、`Focus()`（空）、`TextBoxPosChanged`/`ResetTextBoxPos()`/`TextBoxIgnoreScrollBarChanges`（空） | ✅ |
| 4A-D7 | 取消注释 EmueraConsole 中 `window.MainPicBox.*` 调用 | PointToClient/ClientRectangle/Width/Height，替换 `Control.MousePosition` 为 `System.Windows.Forms.Control.MousePosition` | ✅ |
| 4A-D8 | 取消注释 EmueraConsole 中 `window.ToolTip.*` 调用 | ToolTip stub 化后可安全取消注释 | ✅ |
| 4A-D9 | 编译+运行验证 | 0 编译错误，60 警告（均为既有警告） | ✅ |

**Stub 缺失接口清单（4A-D1~D6 已全部补充）**：

| 缺失接口 | 严重程度 | 目标切片 | 状态 |
|----------|---------|---------|------|
| `window.Created` | **高** | 4A-D4 | ✅ |
| `window.Invoke(Action)` | **高** | 4A-D3 | ✅ |
| `window.ToolTip.*` | **高** | 4A-D5 | ✅ |
| `window.MainPicBox.PointToClient()` | **高** | 4A-D1 | ✅ |
| `window.MainPicBox.ClientRectangle` | **中** | 4A-D1 | ✅ |
| `window.MainPicBox.Width/Height` | **中** | 4A-D1 | ✅ |
| `window.TextBox.ForeColor/Font` | **中** | 4A-D2 | ✅ |
| `window.TextBoxPosChanged`/`ResetTextBoxPos()` | **中** | 4A-D6 | ✅ |
| `window.TextBoxIgnoreScrollBarChanges` | **中** | 4A-D6 | ✅ |
| `window.Text` | **低** | 4A-D6 | ✅ |
| `window.ShowConfigDialog()` | **低** | 4A-D6 | ✅ |
| `window.Focus()` | **低** | 4A-D6 | ✅ |

**风险 Top 3**：
1. `window.Invoke(Action)` 同步语义 — WinForms `Invoke` 是同步的，`MainThread.BeginInvokeOnMainThread` 是异步的
2. 取消注释后引入新编译错误 — 注释代码引用 `Control.MousePosition`/`Cursor.Position` 等 WinForms API
3. MainPicBoxStub 与 EraPictureBox 双轨并存 — 未来切换 TFM 时需确保不冲突

#### 子阶段 4A-Xamarin：启用 ContentPage MainWindow（未来 TFM 切换）

> **前置条件**：4A-Desktop 完成 + TFM 切换到 net8.0-android 或多目标框架

| # | 操作 | 说明 | 状态 |
|---|------|------|------|
| 4A-X1 | 移除 csproj 中 `Platform\Forms\**` 排除项 | 启用 XAML 代码生成 | ⏭️ |
| 4A-X2 | 移除 csproj 中 XAML 页面排除项 | Views/*.xaml.cs + MainPage.xaml.cs + App.xaml.cs | ⏭️ |
| 4A-X3 | 连接 SKCanvasView 到 EmueraConsole.OnPaint | `mainPicBox_Paint` → `console.OnPaint(canvas)` | ⏭️ |
| 4A-X4 | 实现触摸事件转发 | `EraPictureBox_Touch` → `console.MouseDown`/`PressEnterKey` | ⏭️ |
| 4A-X5 | 实现文本输入 | `Entry` → `console.PressEnterKey`（ONEPHRASE/ANYKEY 自动提交） | ⏭️ |
| 4A-X6 | 移植 App.xaml + MainPage.xaml | FlyoutPage 导航框架 + GameItemPage 游戏列表 | ⏭️ |
| 4A-X7 | 编译+运行验证 | — | ⏭️ |

### 切片 4B：配置页面

| # | 操作 | 说明 | 决策 |
|---|------|------|------|
| 4B-1 | 移植 XEmuera-1 ConfigPage 体系 | `SettingsPage` → `ConfigSettingsPage` → `ConfigPage` → `ConfigValuePopup` | 方案 A：移植 XEmuera-1 |
| 4B-2 | 移植 ConfigModel | 配置分组管理 + MVVM 绑定 | — |
| 4B-3 | 扩展 ConfigCodeGroups 添加 LazyLoading 独有分组 | CB 配置 / Rikaichan / Rendering / Debug | — |
| 4B-4 | 保持 LazyLoading 的 Copy+SaveConfig 保存策略 | 不改用 Preferences，保持 emuera.config 文件格式 | 方案 A |
| 4B-5 | 实现 DebugConfigDialog（低优先级） | `GetDebugItem()` 通道 + `SaveDebugConfig()` | — |
| 4B-6 | 编译+运行验证 | — | — |

**ConfigData 双通道设计**：
- 主配置：`GetConfigItem(ConfigCode)` → `Copy() → SaveConfig()`
- 调试配置：`GetDebugItem(ConfigCode)` → 直接修改 → `SaveDebugConfig()`
- JSON 配置：`JSONConfig.Data` → `JSONConfig.Save()`

### 切片 4C：调试窗口

| # | 操作 | 说明 | 决策 |
|---|------|------|------|
| 4C-1 | 保持 DebugDialog 注释状态 | 移动端调试窗口优先级低 | 方案 D：暂不实现 |
| 4C-2 | 确保 EmueraConsole 中 DebugDialog 引用不报错 | `DebugDialog` 属性返回 null | — |
| 4C-3 | （可选）Tab 页方案实现 | 在 MainWindow 中添加调试 Tab | 低优先级 |

---

## Phase 5：平台特定功能

> **知识库**：[io-cross-platform.md](file:///d:/emuera/shared-trae/knowledge/xemuera/io-cross-platform.md)、[audio-system.md](file:///d:/emuera/shared-trae/knowledge/xemuera/audio-system.md)
> **前置条件**：Phase 4 完成

### 切片 5A：IO 跨平台适配

| # | 操作 | 说明 | 决策 |
|---|------|------|------|
| 5A-1 | 移植 XEmuera `FileUtils` 到 emuera.xamarin | 大小写不敏感文件查找（`Exists(ref)`/`GetFiles()`） | 方案 B：封装到 Program 静态方法 |
| 5A-2 | 添加 `Program.FileExists(ref)`/`Program.DirectoryExists(ref)`/`Program.GetFiles()` | 内部委托 FileUtils | — |
| 5A-3 | 替换内核中所有 `File.Exists`/`Directory.GetFiles` 调用 | Instraction.Child.cs/Creator.Method.cs/Process.LazyLoading.cs/Config.cs/ErbLoader.cs | — |
| 5A-4 | 替换音频路径构建 | `Path.GetFullPath(".\\sound\\")` → `IPathProvider.MusicDir` | — |
| 5A-5 | 实现 `IPathProvider` 的 Android/iOS 版本 | Android: `Context.FilesDir`; iOS: `NSBundle` + `Documents/` | — |
| 5A-6 | Android 资源解压 | APK assets → Context.FilesDir（首次启动全量解压） | 方案 A |
| 5A-7 | 编译+运行验证 | — | — |

### 切片 5B：音频系统

| # | 操作 | 说明 | 决策 |
|---|------|------|------|
| 5B-1 | 扩展 `IPlayer` 接口 | 添加 `Pause()`/`Resume()`/`GetCurrentTime()`/`GetTotalTime()`/`GetSpeed()` | 方案 A：扩展 IPlayer |
| 5B-2 | 升级 `SoundAndBgm.cs` 存根 | 委托到 `IPlayer` 实现，替换 NAudio 依赖 | — |
| 5B-3 | 实现 Android `MobileMediaPlayer` 扩展 | `MediaPlayer.Pause()`/`Resume()`/`CurrentPosition`/`Duration` | — |
| 5B-4 | 升级 PLAYSOUND ArgumentBuilder | `STR_EXPRESSION` → 支持 repeat 参数（移植 `SpHtmlPrint`） | 方案 A：升级 ArgumentBuilder |
| 5B-5 | 替换音频路径构建 | `Path.GetFullPath(".\\sound\\")` → `IPathProvider.MusicDir` + `Program.FileExists()` | — |
| 5B-6 | 移植 GETSOUNDSTATUS/ISPLAYINGSOUND | `Creator.Method.cs` 新增方法 | 低优先级 |
| 5B-7 | 编译+运行验证 | — | — |

### 切片 5C：SQL 系统

| # | 操作 | 说明 |
|---|------|------|
| 5C-1 | SQLite 路径适配 | `IPathProvider.SqlDir` |
| 5C-2 | 编译+运行验证 | — |
