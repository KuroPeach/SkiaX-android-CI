# SkiaX — Emuera Skia 内核的 Android 端

> **别名**：SkiaX
> **分支**：`feature/xamarin`（从 LazyLoading develop-skiasharp 创建）
> **当前版本**：v0.51.0
> **历史记录**：[CHANGELOG.md](CHANGELOG.md)

## 项目定位

SkiaX 是以 Skia 变体的逻辑为内核开发的 Android 端 Xamarin 引擎，框架移植自 [Future-R/XEmuera](https://github.com/Future-R/XEmuera)。

由于 Skia 内核与 XEmuera 原始内核在渲染层面发生了根本性变化——最初的 XEmuera 内核仅支持 Emuera 本体，之后由 CKRainbow 和 Future-R 逐步适配了 EM+EE 内核的渲染逻辑——因此除了框架、页面导航和触屏交互逻辑两者一致外，渲染实现几乎完全不同。

**唯一目标产物是 Android APK**。桌面端（net8.0-windows）仅作为功能验证手段，不是目的。

## 致谢

SkiaX 的框架基础来自 XEmuera 三代开发者的工作：

- **Fegelein21** — XEmuera 初代创建者，奠定 Android 端框架基础
- **CKRainbow** — 适配 EM+EE 内核渲染逻辑
- **Future-R** — XEmuera 现维护者，持续适配 EM+EE 渲染更新

详细沿革参见 [Skia 功能概要 — 沿革与开发背景](https://gitgud.io/qrchen/emuera.sk.doc/-/blob/main/docs/Skia/Skia_Summary.zh.md)。

## 项目结构

```
Emuera.Xamarin/                   (net8.0-windows) — 原项目（仍可独立编译）
├── Platform/                      Skia 平台层（源自 XEmuera）
├── Drawing/                       绘图工具
├── Models/                        数据模型
├── Views/                         Xamarin 页面
├── Resources/                     资源文件
├── App.xaml / MainPage.xaml       XF 入口
├── CHANGELOG.md                   变更记录
└── README.md                      本文件

Emuera.Xamarin.Core/              (net8.0;net8.0-windows) — 共享代码 + System.Drawing 垫片
Emuera.Xamarin.Desktop/           (net8.0-windows) — WinForms 桌面验证
Emuera.Xamarin.Android/           (net8.0-android) — APK 产出
```

## 内核引用方式

csproj 通过 `<Compile Include>` 直接引用 LazyLoading 源码目录：

```xml
<Compile Include="..\Emuera\Runtime\**\*.cs" LinkBase="Runtime" />
```

**排除的内核文件**（WinForms/Desktop 专用）：Sound.NAudio/WMP、PluginSystem、JSON配置、Shape、Lang(GeneratedRegex)、Utils(System.Drawing)、Clipboard

## 编译命令

```bash
# 桌面端验证
dotnet build Emuera.Xamarin\Emuera.Xamarin.csproj

# Android APK
dotnet publish Emuera.Xamarin.Android\Emuera.Xamarin.Android.csproj -f net8.0-android -c Debug
```

## 分支策略

```
main-skiasharp ← 发布（线性）
  └─ develop-skiasharp ← 内核开发（线性）
       └─ feature/xamarin ← SkiaX Android 移植（单向接收 develop 更新）
```

- develop 更新单向流入 feature/xamarin
- xamarin 仅作为一个 feature，不独立更新内核
- 内核修改分类与回流策略详见 [kernel-audit.md](../../.trae/rules/kernel/modification-classification.md)

## 关联项目

| 项目 | 说明 |
|------|------|
| [LazyLoading](../) | 内核来源（Emuera Skia 变体） |
| [XEmuera](https://github.com/Future-R/XEmuera) | 框架移植来源 |
| [emuera.em](https://gitlab.com/EvilMask/emuera.em) | 上游（EM+EE） |
| [在线帮助手册](https://gitgud.io/qrchen/emuera.sk.doc) | Skia 版功能文档 |
