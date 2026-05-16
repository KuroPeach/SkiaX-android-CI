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

### 切片 3A：SETIMAGELAYER 图层管理

| # | 操作 | 说明 |
|---|------|------|
| 3A-1 | 升级 `ImageLayerManager.cs` 存根 | 从空方法 → 真实实现（ZDepth 排序 + SKPicture 烘焙） |
| 3A-2 | 实现 `ImageLayer` 数据结构 | 直接移植 LazyLoading 版本 |
| 3A-3 | 实现 `DrawAll(SKCanvas, scrollY)` | 视口裁剪 + 烘焙绘制 |
| 3A-4 | 编译+运行验证 | — |

### 切片 3B：ColorMatrix 颜色矩阵

| # | 操作 | 说明 |
|---|------|------|
| 3B-1 | 升级 `ColorMatrixHelper.cs` 存根 | GDI+ float[5][5] → SkiaSharp float[20] 转换 |
| 3B-2 | 修改 HtmlManager 解析 `cm` 属性 | `ConsoleImagePart._cmVariableName` |
| 3B-3 | 实现 `GSETCOLORMATRIX` 指令 | 新增到 BuiltInFunctionCode |
| 3B-4 | 编译+运行验证 | — |

### 切片 3C：字体渲染

| # | 操作 | 说明 |
|---|------|------|
| 3C-1 | 移植 LazyLoading 字体回退链 | 4 层回退（自定义→安全字体→MatchCharacter→Default） |
| 3C-2 | 升级 `FontFactory.cs` 存根 | 实现字体加载+缓存 |
| 3C-3 | 去掉 GDI+ 回退路径 | Android/iOS 不可用 |
| 3C-4 | 新增 ConfigCode 配置项 | SkiaSharpFontHinting/SkiaSharpFontEdging |
| 3C-5 | 编译+运行验证 | — |

### 切片 3D：增量渲染（可选优化）

| # | 操作 | 说明 |
|---|------|------|
| 3D-1 | 移植 `DirtyLineTracker` | 脏行追踪 |
| 3D-2 | 移植 `IncrementalRenderer` | 增量渲染 |
| 3D-3 | 移植 `RenderLoop` | 渲染循环 |
| 3D-4 | 性能对比测试 | 全屏重绘 vs 增量渲染 |

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
