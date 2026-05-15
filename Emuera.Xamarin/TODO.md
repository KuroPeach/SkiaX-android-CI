# Emuera.Xamarin 任务计划

> **策略核心**：LazyLoading 内核直接编译（net8.0-windows），UI 层用 XEmuera-1 Skia 版本，逐项解决类型冲突和 API 不完整
> **创建日期**：2026-05-15
> **最后更新**：2026-05-15

## 当前状态

- ✅ 项目结构已创建（csproj + 目录 + 文件复制）
- ✅ 已添加到解决方案
- ✅ TFM 升级到 net8.0-windows
- ✅ LazyLoading 主项目编译通过（0 错误）
- 🔄 Emuera.Xamarin：818 个编译错误（类型冲突层面）
- ✅ 知识库已更新（porting-methodology/architecture/type-conflicts/net8-migration）

---

## Phase 0：项目骨架 — ✅ 已完成

---

## Phase 1：编译错误收敛 — 🔄 进行中

### Step 1：解决类型冲突

| 任务 | 状态 | 说明 |
|------|------|------|
| 1.1 ConfigCode 枚举冲突 | ⬜ | 排除 Platform/Config/ConfigCode.cs，使用内核版本 |
| 1.2 MixedNum 类型冲突 | ⬜ | 排除 stub 中的 MixedNum，使用内核版本 |
| 1.3 FontStyle/GraphicsUnit/SolidBrush 歧义 | 🔄 | 17 个文件已添加 Font/FontStyle/Brush/Pen 别名，还需 GraphicsUnit/SolidBrush |

### Step 2：补全 EmueraConsole 缺失方法

| 任务 | 状态 | 说明 |
|------|------|------|
| 2.1 HTML Island 方法 | ⬜ | PrintHTMLIsland/ClearHTMLIsland |
| 2.2 背景图管理方法 | ⬜ | AddBackgroundImage/RemoveBackground/ClearBackgroundImage |
| 2.3 其他缺失方法 | ⬜ | TextBackgroundColor/PrintHtmlC/PrintImg/printCustomBar/Window/PrintErrorButton |

### Step 3：补全缺失类型和命名空间

| 任务 | 状态 | 说明 |
|------|------|------|
| 3.1 DisplayLineAlignment 枚举 | ⬜ | 在 Stubs 中定义 |
| 3.2 AppContents 命名空间 | ⬜ | using 别名 |
| 3.3 HtmlManager 命名空间 | ⬜ | using 别名 |
| 3.4 GlobalStatic.ctrlZ | ⬜ | 添加属性 |
| 3.5 ScriptPosition nullable | ⬜ | 添加 `!` 操作符 |
| 3.6 InputType 枚举 | ⬜ | 映射到 LazyLoading 命名空间 |

### Step 4：迭代编译直到错误收敛

每轮编译后分析错误分布，按优先级修复。

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
