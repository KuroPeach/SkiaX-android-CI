# Handoff: G6 GDI 注释残留清理完成

## 会话目标

继续推进移植任务，包括一至两次完整提交。

## 已完成

- [x] G6 第一次提交（删除 9402 行）：
  - 删除 5 组完全注释掉的 GDI 对话框文件（15 个文件）：ConfigDialog/DebugDialog/DebugConfigDialog/ClipBoardDialog/ColorBox
  - 清理 4 处 `//using System.Drawing` / `//using System.Drawing.Imaging` 注释残留
  - 清理 8 处 `//xxx.Dispose()` 注释残留
  - 清理 Program.cs 图标加载注释块、AppContents.cs 超大图片注释块、Config.cs Font Dispose 注释块
  - 清理 EmueraConsole.cs stopTimer() DoEvents 注释块、timer.Dispose 注释
  - 清理 StringMeasure.cs GDI 释放注释块
  - 清理 GraphicsImage.cs UnlockGraphics/LockGraphics/created/locked/fontStyle 注释块
- [x] G6 第二次提交：
  - 清理 EmueraConsole.cs 旧 ConsoleState 枚举值（WaitKey/WaitInteger 等 13 个）
  - 清理 EmueraConsole.cs `getGraphics()` 方法注释块（GDI CreateGraphics）
  - 清理 EmueraConsole.cs `selectingButton`/`lastSelectingButton` 注释字段声明
  - 清理 GraphicsImage.cs LockGraphics/UnlockGraphics/created/locked/fontStyle 注释块
- [x] 编译验证：0 错误，72 警告
- [x] 文档更新：TODO.md + CHANGELOG.md + HANDOFF.md

## 未完成

- [ ] G5：MainThread (Xamarin.Essentials) 残留引用（7 个 using + 6 处调用，仅影响 Xamarin 层，Desktop 路径不受影响）
- [ ] Phase 5A-4/5/6：音频路径构建 + IPathProvider Android/iOS + 资源解压（需 TFM 切换）
- [ ] Phase 4A-Xamarin：启用 ContentPage MainWindow（未来 TFM 切换）
- [ ] Phase 4B-Xamarin：配置页面体系
- [ ] Phase 7C-P1：静态字体缩放
- [ ] Phase 7E-G：本地化/日志查看器/游戏列表
- [ ] ToolTip OwnerDraw（Draw/Popup 事件）：需要 SKBitmap→Bitmap 转换

## 关键决策

- **删除 vs 保留注释掉的对话框**：ConfigDialog/DebugDialog/DebugConfigDialog/ClipBoardDialog/ColorBox 已被 Desktop* 版本完全替代，且 `Platform\Forms\` 目录已被 csproj 排除编译，删除无风险
- **保留 MainWindow.cs/EraPictureBox.cs 中的 `//using System.Drawing`**：这些文件仍在 `Platform\Forms\` 目录中，可能在未来 TFM 切换时需要参考，暂不删除
- **保留 `dTraceLogChanged`/`_tooltipDuration` 未使用字段**：虽然编译器警告赋值未使用，但删除可能引入功能回归风险，暂不处理

## 勘测发现

### MainThread 残留引用（G5，需 TFM 切换后处理）

| 文件 | 调用点 | 说明 |
|------|--------|------|
| MessageBox.cs:16,46 | `MainThread.BeginInvokeOnMainThread` | Xamarin 前端消息框 |
| MainWindow.cs:145,648,926 | `MainThread.BeginInvokeOnMainThread` | ChangeTextBox/Reboot/clear_richText |
| MainWindow.xaml.cs:234,467 | `MainThread.BeginInvokeOnMainThread` | Close/Refresh |

### 其他 stub/未实现项

| 文件 | 方法 | 说明 |
|------|------|------|
| EmueraConsole.cs:1649 | `DebugDialog { get { return null; } }` | DesktopDebugDialog 已通过 DesktopHost 接入 |
| PrintStringBuffer.cs:303 | `PrintHtml()` 抛 NotImplementedException | HTML 打印功能未实现 |

## 阻塞项

- 无代码阻塞

## 下一步建议

1. **Phase 7C-P1**：静态字体缩放（可在当前 TFM 下实施）
2. **Phase 5A-4**：替换音频路径构建（`IPathProvider.MusicDir`）
3. **Phase 4A-Xamarin**：TFM 切换到 net8.0-android 或多目标框架
4. **G5**：MainThread 残留清理（需与 TFM 切换同步）

## 关键文件

### 本次修改的文件

| 文件 | 变更说明 |
|------|---------|
| `Platform/Program.cs` | 删除 `//using System.Drawing` + 图标加载注释块 |
| `Platform/GameView/EmueraConsole.cs` | 删除旧枚举 + getGraphics + stopTimer GDI 注释 + selectingButton 注释 |
| `Platform/GameView/PrintStringBuffer.cs` | 删除 `//using System.Drawing` |
| `Platform/GameView/StringMeasure.cs` | 删除 `//using System.Drawing` + GDI 释放注释块 |
| `Platform/Config/Config.cs` | 删除 Font Dispose 注释块 |
| `Platform/Content/AppContents.cs` | 删除超大图片 Dispose/return-null 注释块 |
| `Platform/Content/GraphicsImage.cs` | 删除 LockGraphics/UnlockGraphics/created/locked/fontStyle 注释块 |
| `Platform/Forms/ConfigDialog.*` | 删除（3 文件） |
| `Platform/Forms/DebugDialog.*` | 删除（3 文件） |
| `Platform/Forms/DebugConfigDialog.*` | 删除（3 文件） |
| `Platform/Forms/ClipBoardDialog.*` | 删除（3 文件） |
| `Platform/Forms/ColorBox.*` | 删除（3 文件） |
