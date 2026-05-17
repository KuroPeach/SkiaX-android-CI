# Handoff: Phase 9D 完成，eratw-chs 在 Android 上完整启动

## 会话目标

修复 OOM 崩溃 + Config 同步 + 工具栏/滚动/渲染修复，实现 eratw-chs 完整启动

## 已完成

- [x] Phase 9D：SQL 懒加载 + Config 同步 + 工具栏/滚动/渲染修复
  - **OOM 修复**：重写 AppContents.cs，使用 SQLite 内存数据库存储 25421 个 sprite 元数据，启动时 0 图片解码
  - **Config 双类同步**：Xamarin Config.SetConfig() 中同步 SystemSaveInBinary 到 Runtime.Config.Config
  - **GetConfigItem 大小写 Bug**：key.ToUpper() 后与 item.EngText.ToUpper() 比较
  - **工具栏布局**：☰ 移入悬浮工具栏，移除 AndroidApp 浮动 ☰ 按钮
  - **滚动修复**：移除本地 scrollBarValue/scrollBarMaximum，直接用 ScrollBar stub 属性
  - **渲染范围**：canvasView.IgnorePixelScaling = true
  - **真机验证**：eratw-chs 完整启动（Process.Initialize: SUCCESS, starting TITLE）

## 当前状态

### 引擎初始化流程（真机验证通过）

```
Program.Init() start
→ PathProvider: ExeDir=/storage/emulated/0/emuera/eratw-chs/
→ Language files loaded
→ Config.SystemSaveInBinary = True
→ Config loaded
→ Fonts loaded
→ Process.Initialize: start
→ AppContents.LoadContents: 25421 sprite metadata entries written to SQLite (no images decoded)
→ KeyMacro+Replace+Rename...
→ Loading GAMEBASE.CSV...
→ Loading other CSVs...
→ ERH loaded OK: 天候システム.ERH, DIM.ERH, DIM_als.ERH
→ Loading ERB files...
→ Process.Initialize: SUCCESS, starting TITLE
→ callEmueraProgram done
```

### 已知问题

1. **🔴 UI 交互验证**：滚动/点击/输入是否正常工作？用户反馈无法拖动或滚动 UI
2. **🔴 渲染范围**：文本最后一行在屏幕中间偏下，而不是在屏幕底部
3. **🟡 Config 双类同步不完整**：只同步了 SystemSaveInBinary，其他配置值（如 LineHeight、MaxLog 等）可能也需要同步
4. **🟡 ConstImage 延迟解码不完整**：当前 CreateFrom 仍然用 SKBitmap.Decode 解码图片，只是不在 LoadContents 时调用。需要实现真正的延迟解码（CreateFrom 只读尺寸，Bitmap 属性按需解码）

### ADB 连接信息

- 设备地址：`127.0.0.1:16384`
- 包名：`com.xemuera.app`
- Activity：`crc64eb58f14690a3a661.MainActivity`
- 日志过滤：`adb -s 127.0.0.1:16384 logcat -d -t 500 2>&1 | Select-String "\[Emuera\]"`
- emuera.log 位置：`/sdcard/emuera/eratw-chs/emuera.log`

## 未完成

- [ ] 9E：UI 交互验证（滚动/点击/输入）
- [ ] 渲染范围验证（文本是否填满屏幕底部）
- [ ] Config 双类完整同步（不只是 SystemSaveInBinary）
- [ ] ConstImage 真正延迟解码（SharedBitmapCache LRU）
- [ ] 7C-P2：动态缩放（运行时，高难度 — ReflowDisplayLines）
- [ ] 7E：本地化系统（LanguagePage + CultureInfo 联动）
- [ ] 7F：解释器日志查看器（TxtFilePage + 侧边菜单入口）
- [ ] 清理调试日志 + 修复编译警告

## 关键架构发现

### Config 双类问题

Xamarin 项目有两个独立的 Config 类：
- `MinorShift.Emuera.Config`（Xamarin 版，在 `Emuera.Xamarin\Platform\Config\Config.cs`）
- `MinorShift.Emuera.Runtime.Config.Config`（Runtime 版，在 `Emuera\Runtime\Config\Config.cs`）

Runtime 代码（如 `UserDefinedVariable.cs`）引用 `Config.Config.SystemSaveInBinary`，解析为 Runtime 版。但只有 Xamarin 版的 `Config.SetConfig()` 被调用，Runtime 版的值保持默认。

**当前修复**：在 Xamarin Config.SetConfig() 中手动同步关键值。**长期方案**：统一为单一 Config 类，或让 Xamarin 版继承 Runtime 版。

### SQL 懒加载架构

Xamarin 版 AppContents.cs 现在使用 SQLite 内存数据库：
- `LoadContents()`：只解析 CSV 写入 SpriteMeta 表，0 图片解码
- `GetSprite()`：先查内存缓存，缓存 miss 时从 SQLite 查询元数据，按需解码图片
- `GetSprite_OnlyCheckExists()`：只查 SQLite，不触发解码

与 WinForms 版的差异：
- WinForms 版有 SharedBitmapCache（LRU，200 张上限）+ AnimSpriteCache（LRU，6 个上限）
- Xamarin 版暂无 LRU 缓存，解码后的图片常驻内存
- WinForms 版的 ConstImage.CreateFrom 只读尺寸（SKCodec.Info），Bitmap 属性按需解码
- Xamarin 版的 ConstImage.CreateFrom 仍然全量解码

## 编译验证

| 项目 | 错误 | 警告 |
|------|------|------|
| Emuera.Xamarin.Core | 0 | ~30 |
| Emuera.Xamarin.Android | 0 | ~8 |

## 下一步建议

1. **9E：UI 交互验证** — 测试滚动、点击、输入是否正常
2. **渲染范围修复** — 检查 OnPaint 中的 ClientHeight 计算
3. **Config 完整同步** — 同步所有关键配置值到 Runtime Config
4. **ConstImage 延迟解码** — 实现 SharedBitmapCache LRU
5. **7C-P2**：动态缩放（高难度，需 ReflowDisplayLines）
