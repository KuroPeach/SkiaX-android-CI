# ADR-001: 键鼠抽象采用 InputState 具体类而非 IInputProvider 接口

> 日期：2026-05-16
> 状态：已决定
> 上下文：Phase 2D 键鼠输入抽象

## 决策

采用 **InputState 具体类方案**（方案 C），不采用 IInputProvider 接口方案（方案 A）。

## 背景

Creator.Method.cs 中的 `GetKeyStateMethod`/`MouseButtonMethod`/`MousePosMethod` 三个 ERB 函数依赖 `WinInput.GetKeyState`（user32.dll）和 `Control.MousePosition`（WinForms），在非 Windows 平台不可用。

## 方案对比

| 维度 | 方案 A: IInputProvider | 方案 C: InputState |
|------|----------------------|-------------------|
| 架构 | 抽象接口 + 平台实现 | 具体类 + 事件写入 |
| 改动范围 | csproj 排除 Creator.Method.cs + 新建替代文件 | 新增 1 类 + 修改 3 个方法 |
| 编译风险 | 高（排除 8000+ 行文件） | 低（不排除文件） |
| 未来扩展 | ✅ 新平台只需新实现 | ⚠️ 需要时再抽接口 |
| 维护成本 | 高（两份 Creator.Method.cs 副本） | 低（修改原文件 3 个方法） |

## 理由

1. Creator.Method.cs 是 8000+ 行的巨大文件，排除后需要维护两份副本，diff 合并成本极高
2. InputState 是深模块——接口简单（读/写状态），内部封装平台差异
3. Windows 和 Android 都通过同一个 InputState 读写状态，行为一致
4. 将来需要多平台时可轻松从 InputState 抽出 IInputProvider 接口（YAGNI 原则）

## 后果

- 实施时修改 Creator.Method.cs 中 3 个方法的实现（而非排除文件）
- InputState 需要 lock 保护线程安全（UI 线程写入，后台线程读取）
- MOUSEB 的 MoveMouse 副作用必须保留（按钮高亮渲染依赖）

## 参考

- [await-async.md 键鼠抽象实施预案](file:///d:/emuera/shared-trae/knowledge/xemuera/await-async.md)
