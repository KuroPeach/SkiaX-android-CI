# Emuera-SKIA

> **画蛇又添足改造版** — 基于 Emuera 1.824 + EMv18 + EEv53 + Lazyloading v2.4 私家改造。

[![Base](https://img.shields.io/badge/base-Emuera%201.824%20%2B%20EEv53-blue)](#)
[![Lang](https://img.shields.io/badge/lang-C%23%20%2F%20ERABASIC-green)](#)
[![Render](https://img.shields.io/badge/render-SkiaSharp%20%2B%20OpenGL-orange)](#)
[![Status](https://img.shields.io/badge/status-active%20development-brightgreen)](#)

---

## 新增功能

| 类别 | 功能 |
|:---|:---|
|  **渲染引擎** | SkiaSharp 替代 GDI+，OpenGL 硬件加速，SRGB 色彩空间修复 |
|  **浮点类型** | `#DIMF` / `#FUNCTIONF` / `LOCALF` / `ARGF` / `RESULTF`，三角函数与端数处理 |
|  **运行时** | ExecutionContext 栈式上下文 + SparseArray 稀疏存储 + SafeArithmetic 溢出保护 |
|  **新指令** | EVAL/EVALS 动态求值、CALLSTR 动态调度、HTML_PRINTC 像素制表、SQL 全套、MAP 增强 |
|  **语法扩展** | 可变参数声明 `VARIADIC` / 可变参数长度`ARGLEN()`、`#REF`/`#REFS` 引用声明、`OUT` 输出参数 |
|  **图层渲染** | `SETIMAGELAYER` 独立图层系统、视口裁剪、动图离窗暂停、颜色矩阵 |
|  **性能优化** | `SELECTCASE` 编译期跳转表（O(1) 查找）、SQL图片缓存、LRU管理 |
|  **Bug 修复** | 懒加载 EXISTFUNCTION、.als 指针、SPRITECREATE、SpriteG 快照、字体泄漏等 20+ 项 |
|  **Android 端** | SkiaX — 基于 Skia 内核的 Xamarin.Android 移植，详见 [Emuera.Xamarin/](Emuera.Xamarin/) |

---

## 快速开始

1. 将 `Emuera.exe` 放置于游戏目录
2. 配置 `lazyloading.cfg`（可选，详见 [懒加载说明](Readme/参考手册/懒加载LazyLoading.zh.md)）
3. 启动引擎 — ERB 脚本无需任何修改

---

## 文档导航

| 文档 | 说明 |
|:---|:---|
| [CHANGELOG.md](CHANGELOG.md) | 版本更新日志（Release Notes） |
| [Readme/参考手册/](Readme/参考手册/) | 各功能模块详细参考手册 |
| [在线帮助手册](https://gitgud.io/qrchen/emuera.sk.doc) | SkiaSharp 版本在线文档 |
| [Readme/画蛇又添足版自改emuera相关说明.txt](Readme/画蛇又添足版自改emuera相关说明.txt) | 原始开发日志（历史参考） |

---

## 参考

| 项目 | 来源 | 说明 |
|:---|:---|:---|
| **Emuera EE** | [Emuera EE](https://gitlab.com/EvilMask/emuera.em) | 基础代码框架 |
| **Lazyloading** | [CRER/emuera.em](https://gitlab.com/CRER/emuera.em) | 懒加载功能实现（lazyloading分支） |
| **SkiaSharp** | [VVIIlet/emuera](https://gitlab.com/VVIIlet/emuera/-/commit/423fb6eb19f5f33af653a780e084bdd40b6efef1) | 渲染引擎替换 |
| **SoundTouch** | [markheath/naudio](https://github.com/naudio/varispeed-sample) | 音频变速处理 |
| **XEmuera** | [Future-R/XEmuera](https://github.com/Future-R/XEmuera) | SkiaX Android 框架移植来源 |

**SkiaSharp 移植说明**：
- 初始实现来自 VVII 的 "SkiaSharpへの置き換え" 提交
- 本项目引用 ee+em 的 b2fd164 版本作为起点
- 在 1.0.0 版本完成完整的 SkiaSharp 功能集成，并进行了大量重构优化

**SkiaX (Android) 移植说明**：
- SkiaX 是以 Skia 变体的逻辑为内核开发的 Android 端 Xamarin 引擎
- 框架移植自 [Future-R/XEmuera](https://github.com/Future-R/XEmuera)，页面导航和触屏交互逻辑与 XEmuera 一致
- 渲染实现因 Skia 内核架构差异而完全重写
- 致谢 XEmuera 三代开发者：
  - **Fegelein21** — XEmuera 初代创建者，奠定 Android 端框架基础
  - **CKRainbow** — 适配 EM+EE 内核渲染逻辑
  - **Future-R** — XEmuera 现维护者，持续适配 EM+EE 渲染更新

---

> 版本 V3.3
