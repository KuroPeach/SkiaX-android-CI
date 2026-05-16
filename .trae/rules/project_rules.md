# Project Rules — LazyLoading

## 项目类型

.NET C# 解决方案（Emuera 原版派生的懒加载修改版）

- 解决方案：`Emuera.sln`
- 构建：`dotnet build "D:\emuera\emuera_lazyloading_selfmodified_version\Emuera.sln" 2>&1`
- 测试：`dotnet test 2>&1`

## 通用语言与技能

通用语言：[CONTEXT.md](file:///d:/emuera/shared-trae/CONTEXT.md)
技能有效性反馈：[SKILL-EFFECTIVENESS-LOG.md](file:///d:/emuera/shared-trae/skills/SKILL-EFFECTIVENESS-LOG.md)

### 默认技能

| 技能 | 激活条件 |
|------|---------|
| **erabasic** | .cs 中 ERABASIC 解析/运行时逻辑，或 .erb/.erh/.csv 文件操作 |
| **powershell-git** | 终端命令、Git 操作 |
| **knowledge-builder** | 发现新洞见时 |

### 辅助技能

| 技能 | 激活条件 |
|------|---------|
| **xemuera** | Xamarin 移植工作（IO/字体/AWAIT/图形渲染/MainWindow 集成） |
| **grill-with-docs** | 对齐设计决策、锐化术语 |
| **zoom-out** | 不熟悉代码区域 |
| **diagnose** | 引擎 Bug（Emuera 是 GUI，用 `dotnet build` 替代运行） |
| **tdd** | C# 层测试驱动开发 |
| **improve-codebase-architecture** | 架构重构、浅模块深化 |
| **handoff** | 结束会话 |
| **caveman** | 节省 token |

## ERB 脚本编写规则

编写 ERB/ERH 脚本前必须查阅：[syntax-quickref.md](file:///d:/emuera/shared-trae/knowledge/erabasic/syntax-quickref.md)

- `#DIM` 是预处理指令，`#` 不可省略
- 字符串字面量需用 `""` 包裹，否则被当变量名
- A-Z 单字母变量是引擎保留变量，不可用于 `#DIM`

### API 确认流程（强制）

1. 先查知识库是否有该指令的 API 文档
2. 若无，查源码 `DoInstruction` / `ArgumentBuilder` 确认参数个数和类型
3. **禁止凭猜测编写参数**

## Survey-First 协议（强制）

> **动手写代码前，必须先勘测。禁止收到任务后直接编码。**

### 触发条件

以下场景必须执行自拷问（grill-with-docs 模式 B）：

1. **首次接触某移植领域**（如 XEmuera/Godot 适配）
2. **跨多个文件的修改**，需要理解依赖关系
3. **修改内核 Runtime/ 代码**，需确认不影响主项目编译

### 自拷问清单

1. **接口理解**：LazyLoading 源码中该功能的完整接口是什么？
2. **目标平台限制**：目标平台（XEmuera/Godot）有哪些硬限制？
3. **移植策略**：直接移植 vs 重新设计？为什么？
4. **风险 Top 3**：最可能出问题的 3 个点是什么？
5. **知识库覆盖**：`xemuera/` 或 `gemuera/` 域是否有相关条目？

### 垂直切片

自拷问完成后，将任务拆分为垂直切片。每个切片：
- ≤ 1 个概念
- 可独立验证（编译/运行）
- 完成后立即反思，更新知识库

## 会话结束规则

1. 更新 Xamarin 移植项目文档（涉及移植工作时）：
   - `emuera.xamarin/CHANGELOG.md` — 已完成工作的历史记录
   - `emuera.xamarin/TODO.md` — 当前和未来任务（已完成项迁移到 CHANGELOG）
2. 如涉及 DotNet 移植，更新 `docs/comparison/emumeradotnet-portability-assessment.md`
3. 按 knowledge-builder 方法论同步知识库
