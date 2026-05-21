---
alwaysApply: true
---

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

## Xamarin Android ADB 测试规则

> **进行 Xamarin Android 开发和修复时，自动连接 ADB 进行测试。**

### 激活条件

- 修改了 `Emuera.Xamarin/` 或 `Emuera.Xamarin.Android/` 下的代码
- 用户要求构建 APK / 真机测试
- 需要查看渲染诊断日志
- **Android 端运行时报错（NRE/Exception）需要拉取 logcat 堆栈**
- **任何需要使用 `adb` 命令的场景**（adb 不在 PATH 中，必须先查阅知识库获取路径）

### 测试流程

> **⚠ ADB 不在系统 PATH 中！每次使用前必须先定义路径变量：**
> ```powershell
> $adb = "D:\Program Files (x86)\Microsoft Visual Studio\Shared\Android\android-sdk\platform-tools\adb.exe"
> ```
> 完整流程见知识库 [adb-testing.md](file:///d:/emuera/shared-trae/knowledge/xemuera/adb-testing.md)，powershell-git 技能 §10 有命令速查。

1. **构建 APK**：`dotnet publish "D:\emuera\emuera_lazyloading_selfmodified_version\Emuera.Xamarin.Android\Emuera.Xamarin.Android.csproj" -f net8.0-android -c Debug 2>&1`
2. **连接模拟器**：`& $adb connect 127.0.0.1:16384`（MuMu）或 `127.0.0.1:5555`（华为）
3. **安装 APK**：`& $adb -s $device install -r "APK路径" 2>&1`
4. **清除 logcat + 启动**：`& $adb -s $device logcat -c` + `am start`
5. **导航到引擎**：等待 8 秒 → `input tap 540 300`（点击第一个游戏）
6. **等待加载**：eratw-chs 约 23 秒
7. **拉取诊断日志**：`& $adb -s $device logcat -d | Select-String "EmueraRender"`

### 诊断日志查看

```powershell
# 渲染诊断（坐标系 + 行坐标 + 按钮坐标）
& $adb -s $device logcat -d | Select-String "EmueraRender"

# 引擎初始化
& $adb -s $device logcat -d | Select-String "EmueraHost"
```

### 知识库

- [adb-testing.md](file:///d:/emuera/shared-trae/knowledge/xemuera/adb-testing.md) — 完整 ADB 测试流程
- [coordinate-system.md](file:///d:/emuera/shared-trae/knowledge/xemuera/coordinate-system.md) — 坐标系与渲染管线

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
