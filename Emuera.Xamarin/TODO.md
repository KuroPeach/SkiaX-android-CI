# Emuera.Xamarin 任务计划

> **策略核心**：LazyLoading 内核直接编译（net8.0-windows），UI 层用 XEmuera-1 Skia 版本，逐项解决类型冲突和 API 不完整
> **创建日期**：2026-05-15
> **最后更新**：2026-05-15

## 当前状态

- ✅ 项目结构已创建（csproj + 目录 + 文件复制）
- ✅ 已添加到解决方案
- ✅ TFM 升级到 net8.0-windows
- ✅ LazyLoading 主项目编译通过（0 错误）
- ✅ **Emuera.Xamarin 编译通过（0 错误）**（818→336→0）
- ✅ 知识库已更新（9 个条目完整覆盖）
- 🔄 **Phase 2 运行时适配 — 启动中**
  - ⚠️ 关键发现：编译通过≠可运行，三大运行时障碍待解决
  - ⚠️ 执行顺序调整：2C→2B→2A→2D→2E（原计划 2A→2B→2C→2D→2E）
  - 详见 [phase2-readiness.md](file:///d:/emuera/shared-trae/knowledge/xemuera/phase2-readiness.md)

---

## Phase 0：项目骨架 — ✅ 已完成

## Phase 1：编译错误收敛 — ✅ 已完成（818→336→0）

### Round 1：类型冲突 + 存根（818→336）— ✅ 已完成

### Round 2：剩余错误收敛（336→0）— ✅ 已完成

| 修复类别 | 操作 | 消除错误 |
|---------|------|---------|
| 全局 using alias | `FontStyle`/`Brush`/`Pen`/`Font`/`SolidBrush`/`GraphicsUnit` → `XEmuera.Drawing` | ~140 |
| XAML 页面排除 | 10 个 XAML 页面 + App.xaml.cs + MainPage.xaml.cs | ~80 |
| ConfigCode 枚举扩展 | 内核 ConfigCode 添加 14 个 XEmuera 成员 | ~116 |
| Nullable disable | csproj `<Nullable>disable</Nullable>` + NoWarn | ~22 |
| 存根补全 | Sound/Bgm/MainWindow/EmueraConsole/GraphicsImage/AppContents/FontFactory/Sys/App/ColorMatrixHelper | ~100 |
| 内核修复 | ScriptPosition?.Value、ConfigData global::、SkiaSharp 枚举类型、dynamic lambda | ~33 |
| 隐式转换 | XEmuera.Drawing ↔ System.Drawing (Pen/Brush/SolidBrush/Font/SKBitmap→GraphicsImage) | ~20 |

---

## Phase 2：运行时适配 — 编译通过后的首个可执行阶段

> **知识库参考**：[io-cross-platform.md](file:///d:/emuera/shared-trae/knowledge/xemuera/io-cross-platform.md)、[await-async.md](file:///d:/emuera/shared-trae/knowledge/xemuera/await-async.md)、[phase2-readiness.md](file:///d:/emuera/shared-trae/knowledge/xemuera/phase2-readiness.md)
> **前置条件**：Phase 1 Round 3 完成（0 编译错误）
> **执行顺序调整**：原计划 2A→2B→2C→2D→2E，依赖分析后调整为 **2C→2B→2A→2D→2E**（IPathProvider 是基础设施，应最先实现）

### 任务来源分析

Phase 2 的任务来源于三个层次：

| 层次 | 来源 | 示例 |
|------|------|------|
| **L1：内核硬依赖** | 内核代码直接引用 WinForms/Desktop 专用 API | `WinInput.GetKeyState`(user32.dll)、`Control.MousePosition`、`PrivateFontCollection` |
| **L2：平台初始化** | LazyLoading 主项目 `Program.Main()` 的启动序列 | GlobalStatic 初始化、Config 加载、ERB 文件扫描 |
| **L3：存根升级** | Phase 1 创建的空壳存根需要真实实现 | `SoundAndBgm.cs` 的 play/stop、`ImageLayerManager.cs` 的 DrawAll、`MainWindow.cs` 的各种方法 |

### 垂直切片

#### 切片 2A：入口点适配

| # | 操作 | 说明 |
|---|------|------|
| 2A-1 | 替换 Platform/Program.cs | 从 `static void Main()` 改为 Xamarin.Forms 入口 |
| 2A-2 | GlobalStatic 初始化序列 | 复制 LazyLoading `Program.Main()` 中的初始化逻辑 |
| 2A-3 | Config 加载 | 连接 Platform/Config/ 到内核 Config |
| 2A-4 | 编译+运行验证 | 确认启动不崩溃 |

**风险**：LazyLoading 的 `Program.Main()` 包含大量 WinForms 初始化（窗口创建、事件绑定），需逐项替换。

#### 切片 2B：Pfc (PrivateFontCollection) 替换

| # | 操作 | 说明 |
|---|------|------|
| 2B-1 | 创建 SkiaSharp 字体集合存根 | 替代 `System.Drawing.Text.PrivateFontCollection` |
| 2B-2 | 实现 `Families` 属性 | 返回 `SKTypeface[]` 而非 `FontFamily[]` |
| 2B-3 | 实现 `AddFontFile()` | 加载字体文件到 `SKTypeface` |
| 2B-4 | 修改 Creator.Method.cs 中的 Pfc 使用 | `FontFamily` → `SKTypeface`（csproj 排除+替代） |
| 2B-5 | 编译验证 | — |

**风险**：Creator.Method.cs 是内核文件，不能直接修改。需 csproj 排除原文件 + 创建 Platform 替代版本。涉及 2 处调用（行 3221、7137）。

#### 切片 2C：路径系统适配

| # | 操作 | 说明 |
|---|------|------|
| 2C-1 | 创建 `IPathProvider` 接口 | 参考 [io-cross-platform.md](file:///d:/emuera/shared-trae/knowledge/xemuera/io-cross-platform.md) |
| 2C-2 | 实现 `DefaultPathProvider` | 封装现有 `Program.*` 路径 |
| 2C-3 | 替换 `Program.ExeDir` 等硬编码 | 逐步替换，先替换 IO 函数内部 |
| 2C-4 | 编译验证 | — |

**风险**：路径替换影响面广，需分阶段执行。

#### 切片 2D：键鼠输入抽象

| # | 操作 | 说明 |
|---|------|------|
| 2D-1 | 创建 `IInputProvider` 接口 | `GetKeyState(keycode)`、`GetMousePosition()`、`GetMouseButton()` |
| 2D-2 | 实现 `WinInputProvider` | 封装 `WinInput.GetKeyState`（Windows） |
| 2D-3 | 实现 `TouchInputProvider` | 触摸屏映射（短按=左键、长按=右键、返回键=ESC） |
| 2D-4 | 替换 Creator.Method.cs 中的 WinInput 调用 | csproj 排除+替代 |
| 2D-5 | 编译验证 | — |

**风险**：Creator.Method.cs 中 `GetKeyStateMethod` 和 `MouseButtonMethod` 直接使用 `WinInput.GetKeyState` 和 `Control.MousePosition`，必须替换为跨平台实现。

#### 切片 2E：AWAIT/INPUT 验证

| # | 操作 | 说明 |
|---|------|------|
| 2E-1 | 验证 AWAIT 在后台线程的行为 | `Thread.Sleep` 不阻塞 UI |
| 2E-2 | 验证 TINPUT/TINPUTS Timer | Android 上 Timer 行为 |
| 2E-3 | 验证 GETKEYTRIGGERED/MOUSEB 触摸映射 | 配合 2D 的 TouchInputProvider |
| 2E-4 | 编译+运行验证 | — |

**知识库确认**：[await-async.md](file:///d:/emuera/shared-trae/knowledge/xemuera/await-async.md) 已分析 AWAIT 在 XEmuera 上的行为，结论是"保持现状即可"。

---

## Phase 3：Skia 渲染集成 — 存根升级为真实实现

> **知识库参考**：[graphics-rendering.md](file:///d:/emuera/shared-trae/knowledge/xemuera/graphics-rendering.md)、[font-rendering.md](file:///d:/emuera/shared-trae/knowledge/xemuera/font-rendering.md)
> **前置条件**：Phase 2 完成（程序可启动）

### 任务来源分析

| 层次 | 来源 | 示例 |
|------|------|------|
| **L1：内核功能依赖** | 内核 Instraction.Child.cs 调用 SETIMAGELAYER/CBG 系列指令 | `ImageLayerManager.SetLayer/RemoveLayer/Clear/DrawAll` |
| **L2：渲染管线** | LazyLoading 主项目的 OnPaint 渲染循环 | `EmueraConsole.OnPaint` → `SKCanvas` 绘制 |
| **L3：字体渲染** | GDI+ TextRenderer 回退路径不可移植 | 需纯 SkiaSharp 方案 |

### 垂直切片

#### 切片 3A：SETIMAGELAYER 图层管理

| # | 操作 | 说明 |
|---|------|------|
| 3A-1 | 升级 `ImageLayerManager.cs` 存根 | 从空方法 → 真实实现（ZDepth 排序 + SKPicture 烘焙） |
| 3A-2 | 实现 `ImageLayer` 数据结构 | 直接移植 LazyLoading 版本 |
| 3A-3 | 实现 `DrawAll(SKCanvas, scrollY)` | 视口裁剪 + 烘焙绘制 |
| 3A-4 | 编译+运行验证 | — |

**知识库确认**：[graphics-rendering.md](file:///d:/emuera/shared-trae/knowledge/xemuera/graphics-rendering.md) 已记录 LazyLoading 的 ImageLayerManager 架构。

#### 切片 3B：ColorMatrix 颜色矩阵

| # | 操作 | 说明 |
|---|------|------|
| 3B-1 | 升级 `ColorMatrixHelper.cs` 存根 | GDI+ float[5][5] → SkiaSharp float[20] 转换 |
| 3B-2 | 修改 HtmlManager 解析 `cm` 属性 | `ConsoleImagePart._cmVariableName` |
| 3B-3 | 实现 `GSETCOLORMATRIX` 指令 | 新增到 BuiltInFunctionCode |
| 3B-4 | 编译+运行验证 | — |

#### 切片 3C：字体渲染

| # | 操作 | 说明 |
|---|------|------|
| 3C-1 | 移植 LazyLoading 字体回退链 | 4 层回退（自定义→安全字体→MatchCharacter→Default） |
| 3C-2 | 升级 `FontFactory.cs` 存根 | 实现字体加载+缓存 |
| 3C-3 | 去掉 GDI+ 回退路径 | Android/iOS 不可用 |
| 3C-4 | 新增 ConfigCode 配置项 | SkiaSharpFontHinting/SkiaSharpFontEdging |
| 3C-5 | 编译+运行验证 | — |

**知识库确认**：[font-rendering.md](file:///d:/emuera/shared-trae/knowledge/xemuera/font-rendering.md) 已记录完整移植策略。

#### 切片 3D：增量渲染（可选优化）

| # | 操作 | 说明 |
|---|------|------|
| 3D-1 | 移植 `DirtyLineTracker` | 脏行追踪 |
| 3D-2 | 移植 `IncrementalRenderer` | 增量渲染 |
| 3D-3 | 移植 `RenderLoop` | 渲染循环 |
| 3D-4 | 性能对比测试 | 全屏重绘 vs 增量渲染 |

**优先级**：中。全屏重绘在简单场景可接受，增量渲染是后续优化。

---

## Phase 4：Xamarin.Forms UI 集成

> **前置条件**：Phase 3 完成（渲染管线可用）

### 任务来源分析

| 层次 | 来源 | 示例 |
|------|------|------|
| **L1：平台 UI 框架** | XEmuera-1 的 Xamarin.Forms 页面 | `MainPage.xaml`、`App.xaml`、`EraPictureBox` |
| **L2：交互控件** | WinForms 控件的 Xamarin 等价物 | `TextBox` → `Entry`、`RichTextBox` → 自定义渲染 |
| **L3：平台服务** | Android/iOS 特定功能 | 文件选择器、权限请求、通知 |

### 垂直切片

#### 切片 4A：主页面集成

| # | 操作 | 说明 |
|---|------|------|
| 4A-1 | 连接 `MainPage.xaml` 到 `EmueraConsole` | SKCanvasView 渲染 |
| 4A-2 | 实现触摸事件转发 | `EraPictureBox_Touch` → `MoveMouse`/`PressEnterKey` |
| 4A-3 | 实现文本输入 | `Entry` → `GETTEXTBOX`/`SETTEXTBOX` |
| 4A-4 | 编译+运行验证 | — |

#### 切片 4B：配置页面

| # | 操作 | 说明 |
|---|------|------|
| 4B-1 | 替换 WinForms ConfigDialog | Xamarin.Forms 配置页面 |
| 4B-2 | 连接 ConfigModel 到内核 Config | 双向绑定 |
| 4B-3 | 编译+运行验证 | — |

#### 切片 4C：调试窗口

| # | 操作 | 说明 |
|---|------|------|
| 4C-1 | 替换 WinForms DebugDialog | Xamarin.Forms 调试页面 |
| 4C-2 | 实现变量查看/修改 | — |
| 4C-3 | 编译+运行验证 | — |

---

## Phase 5：平台特定功能

> **前置条件**：Phase 4 完成（UI 完整可用）

### 任务来源分析

| 层次 | 来源 | 示例 |
|------|------|------|
| **L1：跨平台 IO** | Android 大小写敏感、iOS 只读资源 | [io-cross-platform.md](file:///d:/emuera/shared-trae/knowledge/xemuera/io-cross-platform.md) |
| **L2：音频系统** | NAudio/WMP 不可用 | 需跨平台音频方案 |
| **L3：SQL 系统** | SQLite 路径适配 | `IPathProvider.SqlDir` |
| **L4：LazyLoading 二进制缓存** | 跨平台指纹校验 | 平台不匹配时自动重建 |

### 垂直切片

#### 切片 5A：IO 跨平台适配

| # | 操作 | 说明 |
|---|------|------|
| 5A-1 | 封装 `Program.FileExists/DirectoryExists/GetFiles` | 大小写不敏感查找 |
| 5A-2 | 替换所有 `File.Exists`/`Directory.GetFiles` 调用 | — |
| 5A-3 | 实现 `IPathProvider` 的 Android/iOS 版本 | — |
| 5A-4 | 编译+运行验证 | — |

#### 切片 5B：音频系统

| # | 操作 | 说明 |
|---|------|------|
| 5B-1 | 调研跨平台音频方案 | Xamarin.Essentials / Plugin.Audio / 原生 |
| 5B-2 | 升级 `SoundAndBgm.cs` 存根 | 实现真实的 play/stop/setVolume |
| 5B-3 | 实现 BGM 循环播放 | — |
| 5B-4 | 编译+运行验证 | — |

#### 切片 5C：SQL 跨平台

| # | 操作 | 说明 |
|---|------|------|
| 5C-1 | SQLite 路径适配 | `IPathProvider.SqlDir` |
| 5C-2 | 验证 SQLite 跨平台兼容性 | — |
| 5C-3 | 编译+运行验证 | — |

#### 切片 5D：LazyLoading 二进制缓存

| # | 操作 | 说明 |
|---|------|------|
| 5D-1 | 添加平台指纹校验 | `RuntimeInformation.FrameworkDescription` |
| 5D-2 | 路径分隔符规范化 | 写入时统一 `/` |
| 5D-3 | 降级策略验证 | 缓存失效时自动重建 |
| 5D-4 | 编译+运行验证 | — |

---

## 存根清单与升级映射

Phase 1 创建的存根文件及其在 Phase 2-5 中的升级目标：

| 存根文件 | 当前状态 | 升级目标 | 目标 Phase |
|---------|---------|---------|-----------|
| `SoundAndBgm.cs` | 空方法 | 真实音频播放 | Phase 5B |
| `ImageLayerManager.cs` | 空方法 | ZDepth 排序 + SKPicture 烘焙 | Phase 3A |
| `MainWindow.cs` | 属性+空方法 | 真实 Xamarin 窗口 | Phase 4A |
| `WinForms.cs` | Application.DoEvents 空实现 | 保持空实现（正确行为） | 无需升级 |
| `IPluginMethod.cs` | 完整空壳 | 保持（PluginSystem 不移植） | 无需升级 |
| `AConsoleDisplayNode.cs` | 抽象类 | 保持（GameView 层使用） | 无需升级 |
| `EvilMaskUtils.cs` | 完整实现（MixedNum/StyledBoxModel/ConsoleDivPart） | 保持 | 无需升级 |
| `FontFactory.cs` | 存根 | 真实字体加载+缓存+回退链 | Phase 3C |
| `ColorMatrixHelper.cs` | 存根 | GDI+→SkiaSharp 转换 | Phase 3B |
| `EraPictureBox.cs` | 存根 | 真实触摸事件处理 | Phase 4A |
| `SKMouseButton.cs` | 枚举定义 | 保持 | 无需升级 |
| `Dialog.cs` | 存根 | Xamarin.Forms 弹窗 | Phase 4B |
| `EnumsNET.cs` | 工具类 | 保持 | 无需升级 |
| `UIImageStubs.cs` | 抽象类 | 保持 | 无需升级 |
| `WinmmTimer.cs` | 存根 | 跨平台 Timer | Phase 2E |

## 内核硬依赖清单（需 csproj 排除+替代）

以下内核代码直接引用 WinForms/Desktop 专用 API，不能在 Xamarin 上编译，必须排除后创建 Platform 替代版本：

| 内核文件 | 行号 | 硬依赖 | 替代方案 | 目标 Phase |
|---------|------|--------|---------|-----------|
| `Creator.Method.cs` | 3221, 7137 | `GlobalStatic.Pfc.Families` (PrivateFontCollection) | SkiaSharp 字体集合 | Phase 2B |
| `Creator.Method.cs` | 8808 | `WinInput.GetKeyState` (user32.dll) | `IInputProvider.GetKeyState` | Phase 2D |
| `Creator.Method.cs` | 8855 | `Control.MousePosition` (WinForms) | `IInputProvider.GetMousePosition` | Phase 2D |
| `WinInput.cs` | 6 | `[DllImport("user32.dll")]` | 跨平台输入抽象 | Phase 2D |

## 验证节奏

每个 Phase 的每个切片完成后：
1. `dotnet build` 记录错误数
2. 运行验证（Phase 2+）
3. 更新知识库对应条目
4. 更新本文档状态

---

## IO 子系统移植现状矩阵（2026-05 审计）

> **核心发现**：XEmuera-1（旧版 EMv15+EEv24fix）和 LazyLoading（新版 Skia）的 IO 子系统差距巨大。XEmuera 缺少 SQL、图片 LazyLoading、LRU 缓存三大子系统，SAVETEXT/LOADTEXT 已有实现但需路径适配。
> **详细审计**：[io-cross-platform.md](file:///d:/emuera/shared-trae/knowledge/xemuera/io-cross-platform.md) IO 子系统移植现状矩阵

### 逐子系统审计

| # | 子系统 | LazyLoading 实现 | XEmuera-1 现状 | 移植差距 | 优先级 | 目标 Phase |
|---|--------|-----------------|---------------|---------|--------|-----------|
| 1 | **函数 LazyLoading** | `Process.LazyLoading.cs`：二进制字典 `lazyloading.bin` + `lazyloadingfiles.bin`，魔数校验，增量更新 | **完全缺失** | 需整体移植 Process.LazyLoading.cs + ErbLoader.cs | 中 | Phase 5D |
| 2 | **图片 LazyLoading** | `AppContents.cs`：SQLite 内存数据库 `metaDb` 存 SpriteMeta，按需从 CSV 加载 | **完全缺失**。`AppContents.cs` 用 `Dictionary<string, ASprite>` 全量加载 | 需移植 metaDb + LoadSpriteFromMeta 按需加载 | 中 | Phase 3A+ |
| 3 | **SQL 系统** | `SqlManager.cs`：27 个 SQL_* 函数，`Microsoft.Data.Sqlite`，WAL 模式 | **完全缺失**。Creator.cs 无 SQL_* 注册 | 需整体移植 SqlManager.cs + 27 个 SQL 方法 | 高 | Phase 5C |
| 4 | **LRU 图片缓存** | `AnimSpriteCache`：LRU 淘汰 + `IFileBacked` 接口（Evict/Reload），`SpriteAnimated` 实现 | **完全缺失**。`SpriteAnime` 无 IFileBacked | 需移植 IFileBacked + AnimSpriteCache + SpriteAnimated | 低 | Phase 3A+ |
| 5 | **SAVETEXT/LOADTEXT** | `Creator.Method.cs`：支持字符串路径重载 | **已有实现**。行 6617/6725，但路径硬编码 | 需路径适配（`IPathProvider` 替换） | 高 | Phase 2C |
| 6 | **EXISTFILE/ENUMFILES** | `Creator.Method.cs`：`File.Exists` + `Directory.EnumerateFiles` | **已有实现**。行 1226/224，Android 大小写敏感 | 需封装大小写不敏感查找 | 高 | Phase 5A |
| 7 | **XML 系统** | `Creator.cs`：20 个 XML_* 函数 + MAP_TOXML/MAP_FROMXML | **已有实现**。行 240-272，`System.Xml.XmlDocument` | `System.Xml` 跨平台可用，无需修改 | 低 | 无需 |
| 8 | **SharedBitmapCache** | `AppContents.cs`：`ConcurrentDictionary` + `SpriteDisposeAll` | **已有实现**。`Dictionary<int, GraphicsImage>` | 线程安全差异 | 低 | 无需 |

### 移植策略判定

| 子系统 | 策略 | 理由 |
|--------|------|------|
| SQL | **全新移植** | XEmuera 无任何 SQL 代码，需从 LazyLoading 整体移植 + 添加 `Microsoft.Data.Sqlite` NuGet |
| 函数 LazyLoading | **全新移植** | 独立子系统（300+ 行），与现有代码无冲突，但依赖 `IPathProvider` |
| 图片 LazyLoading | **重构 AppContents** | 需将 Dictionary 改为 SQLite 索引 + 按需加载，依赖 SQL 子系统 |
| LRU 缓存 | **新增接口+实现** | 在 SpriteAnime 上新增 IFileBacked，新增 AnimSpriteCache，依赖 SpriteAnimated |
| SAVETEXT/LOADTEXT | **路径适配** | 已有实现，仅需 IPathProvider 替换 |
| EXISTFILE/ENUMFILES | **封装适配** | 已有实现，需封装大小写不敏感查找 |
| XML | **无需修改** | System.Xml 跨平台可用 |

### 依赖关系图

```
IPathProvider (Phase 2C)
  ├── SAVETEXT/LOADTEXT 路径适配
  ├── EXISTFILE/ENUMFILES 大小写封装
  ├── SQL 路径适配 (Phase 5C)
  │     └── 图片 LazyLoading metaDb (Phase 3A+)
  └── 函数 LazyLoading 二进制缓存 (Phase 5D)

SpriteAnimated (Phase 3A)
  └── IFileBacked + AnimSpriteCache (LRU 缓存)
```

---

## 从 Issue-Driven 到 Feature-Driven：执行模式转型

> **核心转变**：Phase 1 是 issue-driven（消除编译错误），Phase 2+ 应转为 feature-driven（按功能模块逐个实现跨平台能力）。

### Issue-Driven（Phase 1）

- **驱动力**：编译错误列表
- **操作**：逐个修复 CS0117/CS1061/CS1503 等错误
- **验证**：错误数下降
- **局限**：修完错误不等于功能可用

### Feature-Driven（Phase 2+）

- **驱动力**：功能模块的跨平台能力
- **操作**：按功能模块逐个审查 → 判定实现方式 → 实现 → 验证
- **验证**：功能在目标平台上可用
- **审查判定**：

| 判定 | 含义 | 示例 |
|------|------|------|
| **直接可用** | 已有跨平台实现，无需修改 | XML 系统（System.Xml 跨平台） |
| **路径适配** | 已有实现，仅需 IPathProvider 替换 | SAVETEXT/LOADTEXT |
| **存根升级** | Phase 1 创建的空壳需真实实现 | ImageLayerManager、FontFactory |
| **全新移植** | XEmuera 完全缺失，需从 LazyLoading 整体移植 | SQL 系统、函数 LazyLoading |
| **空实现保留** | 功能不可移植或不需要，保持空实现 | PluginSystem、WinForms.DoEvents |
| **转变实现方式** | 功能需要，但原实现方式不可移植，需重新设计 | AWAIT（DoEvents→Thread.Sleep）、GETKEY（user32.dll→IInputProvider） |

### AWAIT 系列的审查判定

| 指令 | 原实现 | XEmuera 现状 | 判定 | 说明 |
|------|--------|-------------|------|------|
| AWAIT | `Application.DoEvents()` + `Thread.Sleep()` | `App.DoEvents()` + `Thread.Sleep()` | **转变实现方式** | DoEvents 在 Xamarin 上不可用，但后台线程模型下 Thread.Sleep 不阻塞 UI，已正确 |
| WAIT | `Console.ReadAnyKey()` | `Console.ReadAnyKey()` | **直接可用** | 后台线程模型已处理 |
| TWAIT | `Console.ReadAnyKey()` + Timer | `Console.WaitInput(req)` + Timer | **直接可用** | Timer 跨平台 |
| INPUT/TINPUT | `Console.WaitInput()` | `Console.WaitInput()` | **直接可用** | 后台线程模型已处理 |
| GETKEY | `WinInput.GetKeyState(user32.dll)` | 已注释掉，返回 0 | **存根升级** | 需 IInputProvider |
| MOUSEX/MOUSEY | `Control.MousePosition` | 已注释掉，返回空 Point | **存根升级** | 需 IInputProvider |
| MOUSEB | `Control.MouseButtons` | 未实现 | **存根升级** | 需 IInputProvider |

### 键鼠交互系列的审查判定

| 功能 | 原实现 | 跨平台替代 | 判定 |
|------|--------|----------|------|
| 键盘状态检测 | `WinInput.GetKeyState(user32.dll)` | `IInputProvider.GetKeyState()` | **转变实现方式** |
| 鼠标位置 | `Control.MousePosition` (WinForms) | `IInputProvider.GetMousePosition()` | **转变实现方式** |
| 鼠标按钮 | `Control.MouseButtons` (WinForms) | `IInputProvider.GetMouseButton()` | **转变实现方式** |
| 触摸映射 | 不存在 | 短按=左键、长按=右键、返回键=ESC | **全新移植** |

### 交互函数移植矩阵（2026-05 审计）

> **详细分析**：[await-async.md](file:///d:/emuera/shared-trae/knowledge/xemuera/await-async.md) 交互函数移植矩阵章节
> **核心发现**：输入交互函数分三层——阻塞等待层（INPUT/WAIT，已可用）、轮询检测层（GETKEY/MOUSEB，需触屏映射）、桌面深度耦合层（INPUTMOUSEKEY/BINPUT/FLOWINPUT，需空实现）

#### 全部输入交互函数清单

| 系列 | 函数 | XEmuera 注册 | XEmuera 实现 | eratw 使用量 | 移植判定 |
|------|------|:---:|:---:|:---:|------|
| AWAIT | `AWAIT time` | ✅ | ✅ | 16次/11文件 | ✅ 保持现状 |
| WAIT | `WAIT/WAITANYKEY/TWAIT/FORCEWAIT` | ✅ | ✅ | 大量 | ✅ 保持现状 |
| INPUT | `INPUT/S(default, canClick, allowSkip)` | ✅ | ✅ | 大量 | ✅ 保持现状 |
| TINPUT | `TINPUT/S time, default, ...` | ✅ | ✅ Timer | 45次/27文件 | ✅ 保持现状 |
| ONEINPUT | `ONEINPUT/S default` | ✅ | ✅ | 6次/6文件 | ✅ 保持现状 |
| TONEINPUT | `TONEINPUT/S time, default, ...` | ✅ | ✅ | 0次 | ✅ 保持现状 |
| INPUTANY | `INPUTANY` | ✅ | ✅ | 0次 | ✅ 保持现状 |
| GETKEY | `GETKEY keyCode` | ✅ | ⚠️ 返回0 | 24次/4文件 | **P0 触屏映射** |
| GETKEYTRIGGERED | `GETKEYTRIGGERED keyCode` | ✅ | ⚠️ 返回0 | 18次/7文件 | **P0 触屏映射** |
| MOUSEB | `MOUSEB` | ❌ | ❌ | 14次/3文件 | **P0 触屏映射** |
| MOUSEX/Y | `MOUSEX/MOUSEY` | ✅ | ⚠️ 返回0 | 0次 | **P1 触屏映射** |
| GETTEXTBOX | `GETTEXTBOX/SETTEXTBOX` | ✅ | ✅ | 3次/3文件 | ✅ 保持现状 |
| INPUTMOUSEKEY | `INPUTMOUSEKEY timeLimit` | ✅ | ⚠️ | 1次/1文件 | **P2 降级：仅触屏点击+长按右键** |
| BINPUT | `BINPUT/S(default, ...)` | ❌ | ❌ | 10次/2文件 | **P1 降级为 INPUT/INPUTS** |
| FLOWINPUT | `FLOWINPUT/S default, ...` | ❌ | ❌ | 0次 | **P1 降级为 INPUT/INPUTS** |
| ONEBINPUT | `ONEBINPUT/S default` | ❌ | ❌ | 0次 | **P3 降级为 ONEINPUT/ONEINPUTS** |

#### 触屏映射方案

| keycode | WinForms 语义 | 触摸屏映射 |
|---------|-------------|-----------|
| 1 (VK_LBUTTON) | 鼠标左键 | 短按触摸 |
| 2 (VK_RBUTTON) | 鼠标右键 | 长按（XEmuera 已有 LongPressTimer） |
| 4 (VK_MBUTTON) | 鼠标中键 | 无映射，永远返回0 |
| 13 (VK_RETURN) | 回车 | Entry Completed 事件 |
| 27 (VK_ESCAPE) | ESC | Android 返回键 |
| 16/17/18 | Shift/Ctrl/Alt | 无映射，永远返回0 |

#### 脚本实例验证

| 脚本 | 交互模式 | 依赖函数 | 安卓可用性 |
|------|---------|---------|-----------|
| BADAPPLE | 帧同步播放器 | AWAIT+GETKEYTRIGGERED+GETTEXTBOX | ✅ GETKEYTRIGGERED映射后完全可用 |
| qol_MAP | AWAIT+TINPUTS混合状态机 | AWAIT+MOUSEB+GETKEYTRIGGERED+TINPUTS | ⚠️ 悬停模式降级为动画一直播放（正确行为），快捷键Z/X/C/Y无映射 |

### 渲染系列的审查判定

| 功能 | 原实现 | XEmuera 现状 | 判定 |
|------|--------|-------------|------|
| GCREATE/GDRAWLINE 等 | GDI+ Graphics | SkiaSharp SKCanvas | **已有实现** |
| SETIMAGELAYER | ImageLayerManager (SKPicture) | 空存根 | **存根升级** |
| ColorMatrix | GDI+ float[5][5] | 空存根 | **存根升级**（需转变实现方式） |
| 字体渲染 | GDI+ TextRenderer + PrivateFontCollection | FontFactory 存根 | **存根升级**（需转变实现方式） |
| 增量渲染 | DirtyLineTracker + IncrementalRenderer | 不存在 | **全新移植**（可选优化） |
| 动画 GIF | SpriteAnimated (IFileBacked + LRU) | SpriteAnime（无 IFileBacked） | **存根升级** |
