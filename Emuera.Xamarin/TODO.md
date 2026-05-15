# Emuera.Xamarin 任务计划

> **策略核心**：LazyLoading 内核直接编译（net8.0-windows），UI 层用 XEmuera-1 Skia 版本，逐项解决类型冲突和 API 不完整
> **创建日期**：2026-05-15
> **最后更新**：2026-05-15

## 当前状态

- ✅ 项目结构已创建（csproj + 目录 + 文件复制）
- ✅ 已添加到解决方案
- ✅ TFM 升级到 net8.0-windows
- ✅ LazyLoading 主项目编译通过（0 错误）
- 🔄 Emuera.Xamarin：336 个编译错误（从 818 收敛）
- ✅ 知识库已更新（porting-methodology/architecture/type-conflicts/net8-migration）

---

## Phase 0：项目骨架 — ✅ 已完成

---

## Phase 1：编译错误收敛 — 🔄 进行中

### Step 1：解决类型冲突 — ✅ 已完成

| 任务 | 状态 | 说明 |
|------|------|------|
| 1.1 ConfigCode 枚举冲突 | ✅ | csproj `<Compile Remove>` 排除 Platform 版本 |
| 1.2 MixedNum 类型冲突 | ✅ | 删除 ConsoleImagePart.cs 内联定义，添加 using 别名 |
| 1.3 FontStyle/GraphicsUnit/SolidBrush 歧义 | ✅ | csproj 全局 `<Using Remove>` + 局部 using 别名 |

### Step 2：补全 EmueraConsole 缺失方法 — ✅ 已完成

| 任务 | 状态 | 说明 |
|------|------|------|
| 2.1 HTML Island 方法 | ✅ | PrintHTMLIsland/ClearHTMLIsland 存根 |
| 2.2 背景图管理方法 | ✅ | AddBackgroundImage/RemoveBackground/ClearBackgroundImage 存根 |
| 2.3 其他缺失方法 | ✅ | TextBackgroundColor/PrintHtmlC/PrintImg/printCustomBar/Window/PrintErrorButton/PrintHtml/PrintBuffer/DisplayLineList/LastButtonGeneration/EscapedParts/GetLineNo/ClearText |
| 2.4 其他缺失成员 | ✅ | MainWindow（ApplyTextBoxChanges/Load/update_lastinput/clear_richText）、AppContents（UnloadTemp*）、GlobalStatic（ctrlZ/Pfc/Sound/Bgm）、Program（rebootFlag/WorkingDir）、Config（StringComparison/StrComper/ImageQuality/FontHinting/FontEdging）、Lang（LoadLanguageFile） |

### Step 3：补全缺失类型和命名空间 — 🔄 部分完成

| 任务 | 状态 | 说明 |
|------|------|------|
| 3.1 DisplayLineAlignment 枚举 | ✅ | 已在 ConsoleDisplayLine.cs 中定义（GameView 命名空间） |
| 3.2 全局 using 命名空间转发 | ✅ | csproj 中添加 12 个全局 using |
| 3.3 Creator.Method.cs 恢复 | ✅ | 误排除，恢复后消除 ~300 个错误 |
| 3.4 Sound/Bgm/WinmmTimer 存根 | ✅ | Platform/Stubs/ 中添加 |
| 3.5 EraStreamReader | 🔄 | 内核已有，需确认 using |
| 3.6 FontFactory | 🔄 | 内核已有，级联错误导致不可见 |
| 3.7 ConfigValuePopup XAML | 🔄 | UseLanguage/ReduceArgumentOnLoadFlag/DisplayWarningFlag using 已添加 |

### Step 4：迭代编译直到错误收敛

当前 336 个错误，主要类别：
- CS0103（名称不存在）：~150 — 主要是 XAML InitializeComponent、FontFactory、Sys 等
- CS1061（方法不存在）：~80 — 主要是 Creator.Method.cs 中的 dynamic 操作
- CS0246（类型未找到）：~60 — 主要是 EraStreamReader、_Library 等
- 其他：~46 — Nullable、类型转换等

### Step 5：提交 Phase 1 完成版本

编译错误收敛到 0（或接近 0）时提交。

---

## Phase 2：运行时适配（不在本轮范围）

- 入口点适配（Program.cs → Xamarin 入口）
- GlobalStatic 初始化
- Config 加载
- 路径系统适配

## Phase 3：Skia 渲染集成（不在本轮范围）

- EmueraConsole.OnPaint → SKCanvas
- 图像加载适配
- 字体渲染适配

## Phase 4：Xamarin.Forms UI 集成（不在本轮范围）

## Phase 5：平台特定功能（不在本轮范围）

- 音频系统
- IO 路径抽象
- SQL 跨平台

---

## 关键技术发现

1. **全局 using 命名空间转发**：csproj `<Using Include>` 可解决内核文件找不到 Platform 类型的问题，但有冲突的命名空间（如 `Runtime.Config` vs `Config`）不能用全局 using
2. **Creator.Method.cs 误排除**：注释说引用 PluginSystem，实际不引用，恢复后消除 ~300 个错误
3. **命名空间迁移反模式**：不能移动 Platform 类型的命名空间来匹配内核期望，会导致级联错误
