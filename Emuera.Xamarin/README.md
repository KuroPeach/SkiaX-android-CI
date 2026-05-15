# Emuera.Xamarin — LazyLoading 的 Skia/Xamarin 平台端

> **分支**：`feature/xamarin`（从 LazyLoading 主分支创建）
> **策略**：保留 LazyLoading 内核文件（`Runtime/` + `UI/`），复制 XEmuera-1 的 Skia 平台层，适配命名空间后编译通过
> **创建日期**：2026-05-15

## 项目定位

Emuera.Xamarin 是 LazyLoading 内核的 **Xamarin.Forms + SkiaSharp 跨平台前端**。

与之前在 XEmuera-1 仓库中"逐文件 diff 合并"的策略不同，本项目的核心思路是：

```
LazyLoading 仓库内新建 Emuera.Xamarin/ 项目
├── 内核：直接编译 LazyLoading 的 Runtime/ + UI/ 目录（不复制文件）
└── 平台层：从 XEmuera-1 复制的 Skia/Xamarin 文件（Platform/ 目录）
```

**为什么不继续在 XEmuera-1 仓库工作？**

| 维度 | XEmuera-1 仓库 | LazyLoading feature/xamarin |
|------|---------------|---------------------------|
| 内核同步 | 逐文件 diff + 三分类过滤 | 直接编译，零同步成本 |
| 上游一致性 | 偏离 LazyLoading 命名空间 | 保持 LazyLoading 原生命名空间 |
| Float/SQL/LazyLoading | 需要手动移植 | 内核已有，直接可用 |
| 编译验证 | 每步需验证 | 最终验证即可（内核已验证） |
| 文件架构 | GameData/GameProc/Sub | Runtime/Script（与 emuera.em 上游一致） |

## 项目结构

```
Emuera.Xamarin/
├── Emuera.Xamarin.csproj      # 项目文件（引用 Runtime/ + UI/ + Platform/）
├── Platform/                   # 从 XEmuera-1 复制的 Skia 平台层
│   ├── Config/                 # 配置系统（适配 LazyLoading 命名空间）
│   ├── Content/                # 图像资源管理（GraphicsImage 等）
│   ├── Forms/                  # WinForms 残留 → Xamarin 适配
│   ├── GameView/               # 控制台渲染（EmueraConsole 等）
│   ├── GlobalStatic.cs         # 全局静态引用
│   ├── Program.cs              # 启动入口
│   ├── Lang.cs                 # EvilMask 多语言（XEmuera-1 旧版，无 GeneratedRegex）
│   └── EvilMask_Utils.cs       # EvilMask 工具（XEmuera-1 旧版，无 System.Drawing）
├── Drawing/                    # 绘图工具
├── Models/                     # 数据模型
├── Views/                      # Xamarin 页面
├── Forms/                      # 接口层
├── Resources/                  # 资源文件
├── App.xaml / MainPage.xaml    # XF 入口
├── README.md                   # 本文件
├── TODO.md                     # 任务计划
└── CHANGELOG.md                # 变更记录
```

## 内核引用方式

csproj 通过 `<Compile Include>` 直接引用 LazyLoading 源码目录：

```xml
<Compile Include="..\Emuera\Runtime\**\*.cs" LinkBase="Runtime" />
```

**排除的内核文件**（WinForms/Desktop 专用）：
- `Sound.NAudio.cs` / `Sound.WMP.cs` — NAudio/WMP 音频
- `NAudio_LoopStream.cs` / `SoundTouch_byMarkHeath/**` — 音频处理
- `PluginSystem/**` — WinForms 插件
- `Config/JSON/**` — JSON 配置（netstandard2.1 不兼容）
- `EvilMask/Shape.cs` — System.Drawing.Graphics 专用
- `EvilMask/Lang.cs` — GeneratedRegex（.NET 7+）
- `EvilMask/Utils.cs` — System.Drawing.Bitmap/Icon
- `Clipboard.cs` — System.Windows.Forms + GeneratedRegex

## 与 XEmuera-1 手册的关系

| 手册 | 是否仍需要 | 说明 |
|------|-----------|------|
| PORTING-MANUAL-v2.md | ❌ 废弃 | 逐文件 diff 策略已不适用 |
| WORKLOG-v2.md | ⚠️ 参考历史 | 旧工作记录可查，但不再更新 |
| MIGRATION-TRACKER-v2.md | ❌ 废弃 | 迁移追踪已不适用 |
| EM-EE-FEATURE-DELTA.md | ✅ 保留 | 特性增量信息仍有效 |
| SKIA-SYNC-EMEE.md | ✅ 保留 | Skia 特性清单仍有效 |
| EM-EE-MIGRATION-TRACKER.md | ✅ 保留 | Feature 级追踪仍有效 |

## 编译命令

```bash
dotnet build Emuera.Xamarin\Emuera.Xamarin.csproj
```

## 关联项目

- **LazyLoading** (`emuera_lazyloading_selfmodified_version`)：内核来源
- **XEmuera-1** (`XEmuera-1`)：平台层来源
- **emuera.em** (`emuera.em`)：上游
