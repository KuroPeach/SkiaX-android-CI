# Emuera.Xamarin 任务计划

> **最后更新**：2026-05-21
> **历史记录**：[CHANGELOG.md](file:///d:/emuera/emuera_lazyloading_selfmodified_version/Emuera.Xamarin/CHANGELOG.md)
> **当前版本**：v0.46.0

## 当前状态

### 已完成阶段总览

| 阶段 | 内容 | 版本 |
|------|------|------|
| Phase 0-2 | 项目骨架 + 编译通过 + 桌面端可启动 + 路径/字体/入口适配 | 0.1-0.3 |
| Phase 3-3.5 | Skia 渲染集成 + 渲染管线补全 + 桌面端修复 | 0.4-0.5 |
| Phase 4A-4C | Stub 扩展 + 鼠标交互 + ConfigDialog + ToolTip + QuickButton + DebugDialog | 0.6-0.8 |
| Phase 4A-D20~D23 | P0-P1修复 + Rikaichan + AnimeTimer + GotoTitleAndLoadAndRepeatInput | 0.9-0.10 |
| Phase 5 | 音频 + IO + SQL + 资源清理 + 退出流程 | 0.11-0.12 |
| Phase 6 | EM 私家版 HTML 扩展（div + PrintHtmlC 全量版） | 0.6 |
| Phase 7 | 外部字体 + 运行时缩放 + 本地化 + 日志查看器 | 0.11-0.19 |
| Phase 8A-8E | Core/Desktop/Android 项目创建 + Android 功能补全 + 启动修复 | 0.14-0.20 |
| Phase 9A-9D | Preload + 工具栏 + AOT修复 + SQL懒加载 + eratw-chs完整启动 | 0.21-0.23 |
| Phase 9G-9I | NRE修复 + AWAIT同步 + 缩放渲染 + HTML控件 + 虚拟控制器 + 图片LRU | 0.29-0.31 |
| Phase 9J-9M | APK构建 + 跨平台编译保护 + 运行时崩溃保护 + 动态缩放 + 字体扫描 | 0.32-0.34 |
| Phase 9N-9O | 真机测试反馈修复 Round 1-2（缺字/越界/div错位/缩放过大） | 0.35-0.36 |
| Phase 9P-9Q | 坐标系统一修复 + 回归基准版本坐标系 | 0.37-0.38 |
| Phase 9T | 移除 graph.Scale(density) + 物理像素坐标系 | 0.39 |
| Phase 9R | SimHei 字体嵌入 + 黑体别名映射 | 0.40 |
| Phase 9U | HTML img/space 内联元素与文字行偏移修复 | 0.44 |
| Phase 9V | 字形回退 + Mapping双向字典陷阱 + 移除诊断日志 | 0.45 |
| Phase 9W | 修复 GETCONFIG/FPS 移动端支持 + 画廊 QuickButton HTML 过滤 + 旋转锁定按钮 + SVG 资源补全 | 0.47 |
| Phase 10A | Release 发布（版本号 + 签名 + GitHub Release） | 0.46 |

---

## 项目定位

> **唯一目标产物是 Android APK**。桌面端（net8.0-windows）仅作为功能验证手段，不是目的。
> 知识库：[android-migration.md](file:///d:/emuera/shared-trae/knowledge/xemuera/android-migration.md)

### 当前项目结构

```
Emuera.Xamarin.sln
├── Emuera.Xamarin.Core      (net8.0;net8.0-windows) — 共享代码 + System.Drawing 垫片
├── Emuera.Xamarin.Desktop   (net8.0-windows)         — WinForms 桌面验证
├── Emuera.Xamarin.Android   (net8.0-android)         — APK 产出（.NET 8 SDK 风格）
└── Emuera.Xamarin           (net8.0-windows)         — 原项目（仍可独立编译）
```

---

## 待推进任务

### 🔴 P0：ADB 真机验证（已完成）

> v0.45.0-v0.46.0 字体修复 + Release 发布准备已完成 ✅

| # | 操作 | 说明 | 状态 |
|---|------|------|------|
| V1 | ADB 真机验证 ❤ 字形 | eratw-chs HEARTMARK 正常显示 ❤ | ✅ |
| V2 | 验证 NRE 崩溃修复 | 启动/运行时无 NullReferenceException | ✅ |
| V3 | Release 构建 | v0.46.0 签名 APK 构建成功 | ✅ |

### 🟡 P1：已知遗留问题

| # | 问题 | 说明 | 优先级理由 |
|---|------|------|-----------|
| P1-1 | AndroidSound 变速 | setSpeed/getSpeed/SetPreservePitch 为 stub，需 ExoPlayer | 音频变速非核心功能 |
| P1-2 | HTML div 触屏定位 | QOL_USERCOM.ERB @COM_OPTION 弹窗位置错误（div 坐标基于鼠标位置，Android 无鼠标） | 影响特定 ERB 脚本交互 |
| P1-3 | FontScale/AdaptiveFontSize 显示差异 | 三重缩放叠加问题系统性调整 | P0 验证后确认是否仍存在 |
| P1-4 | 动图渲染验证 | GIF/WebP 在 Android 上是否正确播放 | 需 ADB 验证 |
| P1-5 | 内存占用对比测试 | SharedBitmapCache LRU 效果 | 性能优化 |
| P1-6 | BADAPPLE 无法退出 | 模拟器渲染慢导致，考虑禁用 demo | 非阻塞 |

### 🟢 P2：待推进

| # | 操作 | 说明 | 状态 |
|---|------|------|------|
| 11A | AndroidSound 变速实现 | ExoPlayer 替代 MediaPlayer | ⏭️ |
| 11B | APK 体积优化 | AOT/Linker/资源压缩 | ⏭️ |
| 11C | HTML div 触屏定位修复 | QOL_USERCOM.ERB 弹窗坐标 | ⏭️ |
| 11D | 动图渲染验证 | GIF/WebP Android 播放确认 | ⏭️ |
| 11E | 内存占用对比测试 | SharedBitmapCache LRU 效果 | ⏭️ |

---

## 历史详细记录（已完成）

> 以下为已完成任务的详细切片记录，供回溯参考。新增已完成项不再追加到此处，统一记录到 CHANGELOG.md。

### Phase 9F：Android 兼容性补全 ✅

| 切片 | 内容 | 状态 |
|------|------|------|
| 9F-P0-A | AdaptiveFont 默认值修复 | ✅ |
| 9F-P0-B | TOFULL/TOHALF 跨平台替代 | ✅ |
| 9F-P0-C | CALLPLUGIN null guard | ✅ |
| 9F-P1-A | emuera.config 外部存储复制 | ✅ |
| 9F-P1-B | IsActive 已验证无需修改 | ✅ |
| 9F-P1-C | 运行时缩放 + 虚拟控制器 + 智能按钮映射 | ✅ |
| 9F-P2 | FontScale 显示差异调整 | 🟡 待 ADB 验证 |

### Phase 9G：v0.30 ADB 验证反馈修复 ✅

| 切片 | 内容 | 状态 |
|------|------|------|
| 9G-P0 | ProcessState.Return NRE + SQL 嵌套事务 | ✅ |
| 9G-P1 | BADAPPLE AWAIT 帧同步 | ✅ |
| 9G-P2 | 运行时字体缩放渲染错误 | ✅ |
| 9G-P3 | HTML 交互控件滚动失效 | ✅ |
| 9G-P4 | 虚拟控制器默认可见 | ✅ |
| 9G-P5 | 收起工具栏 + 滚动按钮 | ✅ |

### Phase 9H：XEmuera 风格 UI 控件 ✅

| 切片 | 内容 | 状态 |
|------|------|------|
| 9H-P1 | XEmuera 风格工具栏 | ✅ |
| 9H-P2 | 画廊视图 QuickButton 面板 | ✅ |
| 9H-P3 | 旋转90° Slider 滚动条 | ✅ |
| 9H-P4 | Return(0) NRE 修复 | ✅ |
| 9H-P5 | 滚动后点击错误按钮修复 | ✅ |
| 9H-P6 | 工具栏按钮不可见修复 | ✅ |
| 9H-P7 | BADAPPLE 无法退出 | 🟡 用户确认模拟器渲染慢 |

### Phase 9I：图片 LazyLoading + LRU ✅

| 切片 | 内容 | 状态 |
|------|------|------|
| 9I-P1~P8 | SharedBitmapCache/ConstImage/AnimatedImageHelper/SpriteAnimated/AnimSpriteCache/AppContents/Skia兼容/编译验证 | ✅ |

### Phase 9K-9M ✅

| 切片 | 内容 | 状态 |
|------|------|------|
| 9K-P1~P6 | GSAVE/ShowQuestion/跨平台编译保护/APK构建/ConsoleStyledString | ✅ |
| 9L | 运行时崩溃保护 | ✅ |
| 9M-P1~P2 | 动态缩放完善 + 字体扫描修复 | ✅ |

### Phase 9N：真机测试反馈修复 Round 1 ✅

| 切片 | 内容 | 状态 |
|------|------|------|
| 9N-P1 | 文字渲染全黑修复 | ✅ |
| 9N-P2 | 游戏列表页 Language 崩溃修复 | ✅ |
| 9N-P3 | 菜单栏中文化 | ✅ |
| 9N-P4 | 工具栏 SVG 图标 | ✅ |
| 9N-P5 | 汉堡菜单按钮分离 | ✅ |
| 9N-P6 | 英文翻译文件 | ✅ |
| 9N-P7 | 应用标识 | ✅ |

### Phase 9O：真机测试反馈修复 Round 2 ✅

| 切片 | 内容 | 状态 |
|------|------|------|
| 9O-P0-1 | 缺字方框修复 | ✅ (v0.45.0 MatchCharacter 回退) |
| 9O-P0-2 | 最后一行越界 + 按钮错位 | ✅ (v0.44.0 基线修复) |
| 9O-P1-3 | HTML div 弹窗位置错位 | 🟡 已知遗留 |
| 9O-P0-4 | 缩放比例过大 | ✅ (v0.38 回归基准坐标系) |

---

## 知识库索引

| 条目 | 路径 |
|------|------|
| 字体渲染 | [font-rendering.md](file:///d:/emuera/shared-trae/knowledge/xemuera/font-rendering.md) |
| 坐标系统 | [coordinate-system.md](file:///d:/emuera/shared-trae/knowledge/xemuera/coordinate-system.md) |
| 图形渲染 | [graphics-rendering.md](file:///d:/emuera/shared-trae/knowledge/xemuera/graphics-rendering.md) |
| ADB 测试 | [adb-testing.md](file:///d:/emuera/shared-trae/knowledge/xemuera/adb-testing.md) |
| Android 迁移 | [android-migration.md](file:///d:/emuera/shared-trae/knowledge/xemuera/android-migration.md) |
| 音频系统 | [audio-system.md](file:///d:/emuera/shared-trae/knowledge/xemuera/audio-system.md) |
| IO 跨平台 | [io-cross-platform.md](file:///d:/emuera/shared-trae/knowledge/xemuera/io-cross-platform.md) |
| HTML 渲染 | [html-rendering.md](file:///d:/emuera/shared-trae/knowledge/xemuera/html-rendering.md) |
| AWAIT 异步 | [await-async.md](file:///d:/emuera/shared-trae/knowledge/xemuera/await-async.md) |
| 移植方法论 | [porting-methodology.md](file:///d:/emuera/shared-trae/knowledge/xemuera/porting-methodology.md) |
| XEmuera 上游 | [xemuera-upstream-tracking.md](file:///d:/emuera/shared-trae/knowledge/xemuera/xemuera-upstream-tracking.md) |
