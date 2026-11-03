# Changelog

All notable changes to Emuera-SKIA will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

## [16.6.14] — 内存优化：源码缓存释放 / 位图缓存字节预算 / 动画缓存预算

### Changed

- ERB/CSV 编译完成后调用 `Preload.ClearAndCompact()`（清缓存＋一次完整压缩 GC，并显式请求 LOH `CompactOnce`）；全量 `ReloadErb` 重新加载后同样释放。`ReloadPartialErb` 只丢弃本次重载文件的源码缓存，不再触发全量 GC。懒加载与重载按需回读磁盘（回退读取与 Preload 一致使用 ReadWrite 共享）。eraTW 实测：标题界面工作集 1.08 GB → 0.67 GB，读档后 1.36 GB → 1.01 GB（4555 文件 / 4.44M 行）
- `SharedBitmapCache` 由「最多 200 张」改为「256 MB 硬字节预算」的 LRU，LRU 节点字典化（命中 O(1)）。eraTW 素材平均 13.9 MB/张，旧上限最坏可达 2.8 GB；不再保留条目下限（否则 32 张大图即可突破 445 MB），实测 40 张 9 MB 图解码后常驻约 256 MB，被淘汰条目再次取用可正常重解码。淘汰按入缓存时记录的字节数结算，不再二次读取位图；同 key 重入与 LRU 节点失步都有防御
- `AnimSpriteCache` 在原有 6 个上限外增加 256 MB 帧字节预算：超预算时淘汰 1 秒内未被绘制的动画（至少保留 1 个），避免大动画互相挤兑导致每帧重解码；同一文件路径的新实例接管缓存时先淘汰旧实例。现有素材（单动画 ≤36 MB、6 个上限≈216 MB）不会触发该预算，属安全网
- `SharedBitmapCache.Get` 的 `SKBitmap.Decode` 移出全局锁（字节预算下命中率下降、该路径更常走），并发重复解码由 `add` 的同 key 检查去重；`CroppedImage.GraphicsDraw` 的颜色过滤器引用改为 `finally` 清理（原实现异常路径会把已释放的 filter 留在共享 `SKPaint` 上）

### 未验收（develop 暂存，⛔ 不随 16.6.14 发版）

- **R-IMG-1a：CBG 合成快照导出**（cap `bg`，提交 `488eb2de`，作者标注「在建功能，未验收」）：`bg.set`／`bg.data`／`bg.clear`／`bg.fail` 事件、`bg.req` 按 rev 重取、`legacy.textbgc` 全局底色事件、`sys.state.bg`／`snapshot.full.bg` 加法字段。限制：快照只含「清底色＋SETBGIMAGE 烘焙层＋CBG（zdepth≠0）」，⛔ 不含文字行、行内 `escapedParts`、`SETIMAGELAYER` 图层（由 EWP 语义事件单独承载）

## [16.6.13] — EWP audio v1（P3-E：AudioLedger v2 / 6 ERB 内建）

### Added

- **v1 账本**（cap `audio.instances`）：单调句柄（会话内不复用／0＝拒绝）、8 态机与终态保留、`priority`／`steal:none`／`maxVoices`／`maxInstances` 准入与抢占、`stolenBy`；旧 11 槽保留容量不计入上限、不可被抢
- **总线**：`master`／`bgm`／`se`／`voice` 的 `volume`／`mute`／`stop_all`；duck 规则（多源取 min；默认 `voice→bgm` −12 dB 仅在 `audio.bus` 时下发）
- **过渡**：`fadeIn`／`crossfade`／`seek`／`if_changed`；`audio.sync` 全量重建（会话首条 v1 事件，≤128 KiB）
- **v1 事件**：`audio.play`／`ctl`／`bus`／`duck`／`sync`；回供 `audio.state`／`fail` 的 handle 形＋`audio.device`／`audio.bus.state`；`sys.state.audio` v1 加法字段
- **6 个 ERB 内建**：`AUDIO_PLAY`／`AUDIO_CTL`／`AUDIO_BUS`／`AUDIO_DUCK`／`AUDIO_QUERY`／`AUDIO_BUS_QUERY`（v0.2 会话按 spec §1.5 fail-closed，不回退旧通道）
- v1 模式下旧 6 指令／5 方法映射为 v1 事件；`STOPSOUND` 只停旧 10 槽的 `playing`／`deferred`；零 `audio.bgm`／`audio.se` 双发

### Changed

- `hello.caps` 增 `audio.instances`／`audio.bus`／`audio.transitions`／`audio.meta`（旧 FE 忽略）
- 握手前（hello.ok 前）音频意图记入 v1 shadow，v1 会话由 `audio.sync` 重建；v0.2／无 audio 仍按冻结语义丢弃（不补发）

### Fixed（2026-11-01）

- **`audio.play.fadeInMs` 在普通 play 被丢弃**：`AudioV1Ledger.Play` 原先只在 `crossfadeFrom` 生效时才带 `fadeIn`（`doCross ? fadeIn : 0`），导致 `AUDIO_PLAY(..., fadeMs>0)` 的淡入在非交叉场景静默失效，与 v1.0 §8.2「`fadeMs`＝淡入」不符（§1.4 只允许在缺 `transitions` cap 时省略，该分支本就把 `fadeIn` 清 0 并计 `degraded`）。现改为直传 `fadeIn`。
- 门禁同步：G1 v1 新增 `P2.fade_in_plain`（普通 play `fadeInMs==120`）与 `P2.fade_in_no_crossfade`；`r_aud10_macro_gate` 的 `v1.play0 fadeInMs` 由「容忍 0/300」收紧为精确 300；`r_aud5_live_gate` 的 `v1macro wire CF1` 增加 `fadeInMs==300`，两处 `knownDeviations` 清空。

### 验收（2026-10-31）

- eraWire `r_aud_wire.py --g1v1`：55 句柄（44 新＋11 旧）晚连接 sync 75,031 B ≤128 KiB、sync 为会话首条 v1 事件、抢占／`steal:none`／曲目上限、`STOPSOUND`×`deferred`、未知句柄回供、`no_device` 闭锁与 `restored`、缺 `bus`／`transitions`／`meta` 的降级矩阵
- v0.2 零回归：`--g1` 120/120；`--g4` 旧 FE 0 条 v1 事件
- 真实设备／听感／PCM 仍由用户冒烟，⛔ 不宣称音频可用
- 版本签名：`Skiav16.6.12` → `Skiav16.6.13`

***

## [16.6.12] — EWP div.children 内的动画帧表（N-4 收口）

### Changed

- **div 子节点 img 增可选 `anim{count,delays}`**：与顶层 span 同形（cap `sprite.export`）。此前只有顶层行内 `<img>` 带帧表，`div.children` 里的动画精灵被当作静态图；旧 FE 忽略该键，无协议版本变化
- `sprite.req` 负 rev 语义不变（`frame = -rev-1`），div 内动画复用同一按需取帧路径

### 验收（2026-10-29）

- eraWire `r3c_anim_gate` div 用例：真实 `<div><img anim></div>` 的 child span 带 `anim{count,delays}`，按伪 rev 取帧与源帧逐像素一致；Godot FE 离线断言 div-child 动画请求/命中/相位独立
- 版本签名：`Skiav16.6.11` → `Skiav16.6.12`

***

## [16.6.11] — EWP 动画帧导出（R-3c：ANIME 多帧 / 动图 webp）

### Added

- **span `anim{count,delays}`**（cap `sprite.export`）：动画精灵的帧表。两种类型统一口径——`SpriteAnime`（ANIME CSV：`name,ANIME,w,h`＋逐帧行，如 `AS06MARKIV____服_笑顔动_25` 121 帧×500×500×42 ms）与 `SpriteAnimated`（动图文件多帧）。旧 FE 忽略该键
- **`sprite.req` 支持负 rev＝动画帧伪键**（`frame = -rev-1`）：按需把该帧捕获进既有 LRU/rev/失败语义（96 项 / 32 MiB），成功回 `sprite.data{..., "frame":k}`、`rev` 回显伪键；失败走 `sprite.data.fail`（含 `frame` 语义同 bad_request/not_exportable）
- `SpriteAnime` / `SpriteAnimated` 增 `FrameCount`、`GetDelays()`、按帧绘制（`GraphicsDrawFrame` / 既有 `GetFrame`）

### 验收（2026-10-29）

- eraWire `r3c_anim_gate` **PASS（两型都验）**：①**动图 webp（`SpriteAnimated`）**＝`resources/l13/留琴/別立ち2_服_笑顔动_2.webp`（159 帧×270×270），帧 0/79/158 与 webp 自身帧逐像素（meanRgb 0.82／maxRgb ≤25／meanAlpha ≤0.01）；②**ANIME CSV（`SpriteAnime`）**＝`AS06MARKIV____服_笑顔动_25`（121 帧×500×500×42 ms），帧 0/60/120 与 CSV 裁片逐像素（meanRgb ≤2.1／maxRgb ≤33／alpha ≤0.09）；FE 离线渲染测试 PASS
- 更正：早前「l13 webp 在本构建走静态首帧」的观察**不成立**——那次用的是尚未包含本特性的 16.6.10 构建；16.6.11 实测 159 帧动图正常导出
- 版本签名：`Skiav16.6.10` → `Skiav16.6.11`

***

## [16.6.10] — EWP `sys.scroll`（程序化滚动）与快照滚动位置

### Added

- **FE→VM `sys.scroll{value|delta}`**：程序化移动引擎控制台滚动位置，走与鼠标滚轮同一状态路径（`ScrollValue → NotifyUserScrolled → RefreshStrings(true)`），clamp 到 `[0, ScrollMaximum]`；缺省/类型不符回 `protocol_violation`，滚动位置不变
- **`sys.state` / `snapshot.full` 增可选 `scroll{value,max,visible,top}`**：`visible = floor(RenderHeight/LineHeight)`，`top = max(0, min(value,max)-visible)`＝可见窗口首行（与引擎绘制口径一致）；旧 FE 忽略该键

### 验收（2026-10-29）

- eraWire `r3b2_visible_gate` PASS：clock 行 780/842（窗口外）→ `sys.scroll` 792（top 741），GUI⇄FE 逐边 0/0/0/0、dcov 0.0036；title 未回归
- 版本签名：`Skiav16.6.9` → `Skiav16.6.10`

***

## [16.6.9] — 按钮矩形导出/自检改用显示列表真底（rect 覆盖根因）

### Fixed

- **`GetSelectableButtonRects` / `HitTestButtonAt` 的底行改用 `displayLineList.Count-1`**：原用 `ScrollValue-1`，而 `ScrollMaximum` 只在重绘/ClearDisplay 同步，WaitInput 入口强滚的是陈旧值（真实状态屏落后 41 行、存档列表 50 行）⇒ 最新行 rect 被算到屏幕外、hit-test 够不到，生产自检全部扣发（rect 覆盖 1/54 的根因；fixture 日志浅无陈旧窗口，I3 门禁此前全绿）
- **`sys.state.rectDiag`**：最近一次 rect 导出的失败原因分布（noWindow/noStrArray/widthNonPositive/renderInvalid/... + renderW/H/bottomLine/scrollBottom/lineCount）；门禁语义仍是累计 `rectSelfCheckFailed`
- **`renderInvalid` 守卫**：窗口未完成布局的启动竞态 ⇒ 整轮干净降级，不发退化 rect

### 验收（2026-10-29）

- 真机：主菜单 **54/54**（前 1/54，inView 45）、存档 **39/46**、菜单屏 rect 真点击 PASS；`rect_gate` R1–R7 逐值不变；W2a 真机门禁与 vn_gate/C0/r3b1/integration/n3/c3/n2 全绿
- 版本签名：`Skiav16.6.8` → `Skiav16.6.9`

***

## [16.6.8] — 行内 `div.children` 与多 div 文本序列化修复

### Added

- **`div.children` 有界发射**：text/img/shape/div，坐标相对内容框；每 div ≤512 子 span、嵌套≤1、超限 `truncated`；复用行内 `rt/rev` 快照；`sys.state` 增 `spansChildNodes`/`spansChildTruncated`

### Fixed

- **多兄弟 div 的 `text` 序列化丢片段**：`ConsoleButtonString.ToString` 改局部 builder（static builder 重入导致 wire text 只剩最后一个 div；bodyGraph 行 213→1740 码点）
- **`ConsoleDivPart` 只读视图**：AltHeadTagLength/Border/Padding/颜色等供 spans 序列化

### 验收（2026-10-28）

- 真实 eratw `bodyGraph_*`：一行 11 div／37 子 span／5 运行期 rt；GUI⇄FE 5 图逐边 0、32 文本 dy=0/dx≤1
- 版本签名：`Skiav16.6.7` → `Skiav16.6.8`（README 同步）

***

## [16.6.7] — 行内运行期像素 W0 收口（cm 明确失败／快照重签 rev）

### Fixed

- **`CaptureInline` 接收实际 `ColorMatrix`**：带 cm 仍分配 rev，请求回 `not_exportable/color_matrix`，不再返回未滤镜成功图
- **`snapshot.full` 对 retained 且含 rt 的行重建 spans、重捕获并签发新 rev**；旧 rev 不可变，容量外明确 `expired`；`LiveLine.Line` 仅对 rt 行保留，CLEARLINE/ClearDisplay 随 liveLines 释放

### 验收（2026-10-28）

- G1/G2/G3：`r3b2_inline_gate`＋`r3b2_integration_gate` PASS；live `rerun-w0` PASS
- 版本位 `Skiav16.6.7`

***

## [16.6.6] — `legacy.spans` 行内节点/样式序列化

### Added

- **`legacy.line` / `snapshot.full.lines[]` 可选 `spans`**（cap `legacy.spans` 门控）：码点偏移；text/img/shape/div/unsupported；解析色/x/advance/draw；仅非纯默认文本/含非文本节点/align≠LEFT 时发；`text`/`AltText` 不变
- `ConsoleImagePart`/`ConsoleShapePart` 增 span 只读 getter；`sys.state` 增 `spansEnabled/spansEmitted/spansSkipped`

### 验收（2026-10-28）

- `r3b_spans_gate`／`r3b1_fe_gate`（含真实事件流）PASS；n2 文本 hash 不变
- 版本签名：`Skiav16.6.5` → `Skiav16.6.6`

***

## [16.6.5] — `meta.metrics` 增绘制原点/基线/默认色

### Added

- `originX`＝`DrawingParam_ShapePositionShift`（只用于文本/img/rect，div 背景/边框不加）；`baselineOffset`＝|font ascent|；`foreColor`/`bgColor`＝引擎实际默认色
- 不翻 `hello.proto`；旧 FE 忽略新字段

### 验收（2026-10-28）

- `text_pixel_gate`／`fdiv_gate`／V8 全回归 PASS
- 版本签名：`Skiav16.6.4` → `Skiav16.6.5`

***

## [16.6.4] — `HTML_PRINT_ISLAND` div 落画修复

### Fixed

- island 行独占 `DrawDivInline`：`ConsoleButtonString` 仅对 island 行放行 div，普通行仍走 escapedParts，避免二重绘；`PrintHTMLIsland` 给 island 行置 `DrawDivInline=true`
- 三端（桌面 GUI／桌面 pixel.frame／headless pixel.frame）bbox 逐边差 0

### 验收（2026-10-28）

- `island_probe` 三端 bbox 逐边 0；C2-island 卡收口
- 版本 16.6.4；验收记录＝工作区《2026-10-28-C2-island落画修复实施记录》

***

## [16.6.3] — div 开放标签序列化 `align`（供前端还原盒内对齐）

### Changed

- `ConsoleDivPart` 首个子行非左对齐时追加 `align='center'|'right'`（左对齐不写，旧文本不变）；仅属性加法，不改绘制/布局，旧前端忽略

### 验收（2026-10-28）

- eraWire `fdiv_gate` U/R/L/G PASS；V8-A/B 见 E4 卡 §12
- 版本签名：`Skiav16.6.2` → `Skiav16.6.3`（desktop/headless/README）

***

## [16.6.2] — 图层布局统一到 `RenderWidth/Height`（V8-B +2px 根因）

### Fixed

- `ImageLayer` 绘制原用 `graph.LocalClipBounds`（桌面实测 1802×923），与文字/CBG 的 `window.RenderWidth/Height`（1782×921）不同源 ⇒ 图层整体 +2px，`sprite.set` 的 `y+renderH-h` 语义与引擎实际落画不一致；改用 `window.RenderWidth/RenderHeight`
- V8-A/V8-B 判据转为 mask 100%、maxDiff ≤1、shift [0,0]

### 验收（2026-10-27）

- eraWire V8-A/B PASS；`r3a` 回归绿
- 版本签名：`Skiav16.6.1` → `Skiav16.6.2`（desktop/headless/README）

***

## [16.6.1] — `legacy.line` 保留 div 颜色 alpha（半透明 div 合成保真）

### Added

- **`HtmlManager.GetColorToStringArgb`**：文本通道的 ARGB 序列化——`A == 255` 输出 6 位 `#RRGGBB`（与旧文本逐字一致），半透明输出 8 位 `#AARRGGBB`
- **`Utils.AddColorParamArgb` / `AddColorParam4Argb`**：与现有 `AddColorParam` / `AddColorParam4` 同构的 ARGB 版本（additive，不动旧签名）

### Changed

- **`ConsoleDivPart` 的 `color` / `bcolor` 标签改写走 ARGB 序列化**：`legacy.line` / 剪贴板里的半透明 div 颜色不再丢 alpha（默认 Flan `0x807B0000` 此前被写成 `#7B0000`）。不透明 div 输出零变化；字体色/`ConsoleShapePart` 仍走 6 位（上游 EM/EE 共用路径不动）
- 引擎 HTML 解析器 `stringToColorInt32` 本就支持 8 位 ARGB，回读兼容

### Fixed

- **EWP / FE 无法还原半透明 div 的最终合成**：`legacy.line` 只带 `#RRGGBB`，FE 只能按不透明渲染；实测 eraWire E4-B 的 Flan 边框 mask 匹配 13.6%。修复后 `legacy.line` 携带 `#807B0000`，FE `EngineColor` 按引擎 ARGB 解析
- 影响面仅限 div 标签文本；GUI/pixel 绘制路径不变；`n2` 文本 hash 基线实测同值（标准流无半透明 div）

### 验收（2026-10-27）

- `dotnet build Emuera.sln -c Debug` 0 error；`n2_text_gate` 文本 hash 同基线；`fdiv_gate` U/R/L 全 PASS；E4-A（透明 div）回归 PASS
- eraWire E4-B：`legacy.line` 实测出现 `color='#807B0000'`；FE 半透明合成在 div 覆盖区非文字像素 99.98%（shift [0,-2] ≤2，抽检左/右边框逐点相等）。**严格全局 mask 仍未达标**（剩余为 followScroll 2px 非整数偏移与 div 内文字排版，F-div v1 不做）⇒ R-3a 仍 experimental

### Changed

- **版本签名**：`Skiav16.6.0` → `Skiav16.6.1`（`1824+v24+EMv18+EEv56+Skiav16.6.1`）

***

## [16.6.0] — EWP v0.3 定稿（运行期快照导出）＋模态超时竞态系统化收口

### Added

- **EWP v0.3（冻结）运行期精灵快照**：`sprite.req` / `sprite.data` / `sprite.data.fail`、`sprite.set.rev`、`snapshot.full.sprites[].rev`；引擎侧新增 `EwpSpriteExport`（`SETIMAGELAYER` 时冻结不可变快照，LRU 96 项/32 MiB，PNG 编码，失败码 `not_found`/`expired`/`not_exportable`/`too_large`/`bad_request`，capture/encode p50/p95 统计）。FE 未声明 `sprite.export` cap 时 `sprite.req` 直接忽略并计 `requestsIgnored`，旧 FE 事件流零变化
- **`sys.title`**：`console.GotoTitle()` 封装——整屏清场（`legacy.clear{-1}`/`sprite.clear{all}`/`overlay.island.clear`）＋快照缓存清空（旧 rev 此后为 `not_found`），回 `result{ok:true,detail:"title"}`
- **`ModalTimerGuard`（B 方案）**：统一模态闸（静态深度计数器＋`IDisposable` scope，内部复用 `PauseTimer/ResumeTimer`）；`DebugConfigDialog`、热键 toast×5 接入

### Changed

- **`hello.proto` → `EWP/0.3`**：命令面与 v0.2 同形（0.3 只新增 `sprite.export` 增量）；`0.x`（x≥2）未知 proto 按 v0.2 命令面处理，`EWP/1.0+` 仍显式拒绝
- **模态窗口期间带时限输入的超时推迟（C 方案）**：`tickTimer` 检测 `PlatformInterop.IsModalOpen`，模态打开时重启秒表、关窗后重新计满；`DisplayTime` 倒计时在模态期间冻结于满值。Headless / EWP / CliDebug 不注入 ⇒ 恒 false，行为不变

### Fixed

- **默认字体创建失败导致启动即 Error**：`FontFactory.GetFont` 原 `catch { return null; }` 使 `Config.DefaultFont` 为 null，`StringMeasure.GetDisplayLength` 解引用 NRE（`Process.Initialize → setStBar`，偶发、整轮作废）。现失败只记录一次（`[font] create failed …` 进 stderr）并由 `CreateEmergencyFont` 兜底；测量侧空守卫返回 `>0`（避免 `getStBar` 的 `while (width < DrawableWidth)` 死循环）
- **标题 DEMO 假死收口**：13 处模态点已由 16.5.1 逐点暂停；本版 B+C 让残余/未来模态（DebugConfigDialog、热键 toast）也不再抢跑

### 验收（2026-10-27）

- `dotnet build Emuera.sln` 0 error；headless `r3a_sprite_export_gate` 7 场景 PASS；`live_gate3 --proto EWP/0.3` PASS 2m08s；eraWire `r3a_gate`（离线 72＋渲染 17 断言）PASS
- 模态竞态人工验收：标题 20s 内打开「复制日志到剪贴板」停留 >20s → demo 不启动；关窗后闲置 20s → 自动 demo 仍正常触发

### Changed

- **版本签名**：`Skiav16.5.1` → `Skiav16.6.0`（`1824+v24+EMv18+EEv56+Skiav16.6.0`）

***

## [16.5.1] — 模态窗口期间暂停输入超时定时器（标题 DEMO 假死修复）

### Fixed

- **模态窗口打开期间，`TINPUT/TINPUTNF` 等带时限输入的超时定时器照常走完**：超时路径 `window.Invoke(RunEmueraProgram)` 会在模态消息循环里以默认输入抢跑脚本。实证：标题画面 `TINPUTNF 20000` 的 20 秒自动 DEMO 计时器在「复制日志到剪贴板」窗口打开期间到点，BADAPPLE（6573 帧 ≈ 3:37）在 UI 线程不可取消地播放——游戏窗口被模态禁用、焦点在模态窗使 `GETKEYTRIGGERED` 取消探针全灭 ⇒ 双窗假死，只能等播完或杀进程。现 MainWindow 全部 13 处模态点（剪贴板窗口两入口、保存/打开/文件夹对话框、确认 MessageBox×7）打开前 `PauseTimer()`、关闭后 `ResumeTimer()`（重启秒表，关窗后重新计满）；取消/校验错误路径与 catch 内错误弹窗同被覆盖。涉及 `UI/Framework/Forms/MainWindow.cs`（修复本体 `d7de7e62`）

### Changed

- **版本签名**：`Skiav16.5` → `Skiav16.5.1`（`1824+v24+EMv18+EEv56+Skiav16.5.1`）

***

## [16.5.0] — EWP v0.1 定稿：MessagePack 默认翻转（eraWire A2）

### Changed

- **`--ewp` 默认编码＝MessagePack**（spec §2 正本形态，A2 定稿）：4 字节大端长度前缀帧；`--ewp-json` 切 JSON-Lines（调试/抓包/哑前端零依赖形态）；`--ewp-msgpack` 保留为显式同义 no-op。B③ 过渡态（默认 JSON）终结
- hello 升版 `proto:"EWP/0.1"`、impl `ewp-b3`→`ewp-b4`

### 验收（2026-10-25）

- dumbfe 默认 msgpack 全链 PASS（与 JSON 模式事件数完全一致）；三门禁（--ewp-json 调试形态）回归 PASS；godot headless msgpack 门禁 PASS（六 fixtures 双 codec 逐字符一致）；live_gate3 真机 msgpack 直连 PASS
- 前置：C 轨 godot 侧 EwpMsgpack.gd 编解码落地（headless 自证全 PASS）

### Changed

- **版本签名**：`Skiav16.4` → `Skiav16.5`（`1824+v24+EMv18+EEv56+Skiav16.5`）

***

## [16.4.0] — channel 语义流（eraWire B② 余量，D0 契约消费）

### Added

- **channel 名挂钩（内核账本 #4，+2 行）**：`Process.State.IntoFunction` 末尾调 `UI.Ewp.EwpChannel.NotifyFunctionEntry`——`UI_CHANNEL_BEGIN/END/CLEAR` 三宏（eratw-chs `ERB/魔改内容/PRINT系/UI_CHANNEL.ERB`，D0 骨架）的**函数入口即通道边界**；实参从已绑定的形参变量读回；非 EWP 模式 Handler 为 null 立即短路，普通执行零语义零开销。事件（EWP §4.2）：`channel.create{id,kind,tier:"grid-locked"}`（id 首见）＋`channel.begin{id,rev}`（每次 BEGIN，rev 先递增）＋`channel.commit{id,rev}`（与配对 begin 同 rev 定稿）＋`channel.clear{id}`（清内容不清频道，rev 保持）；未配对 end/clear 静默忽略并计 `channelMismatch`（state 可观测）。新文件 `UI/Ewp/EwpChannel.cs`
- **hello caps 增 `channel`**；`snapshot.full` 增 `channels[]`（id/kind/rev/open，幂等重建数据源）；`state` 增 `channels`/`channelMismatch`
- **B② 至此收口**：余量仅 channel/node 词表的 node.* 部分（随 D1 包装面实测后定）与 colormatrix/followScroll（v1）

### Changed

- **版本签名**：`Skiav16.3` → `Skiav16.4`（`1824+v24+EMv18+EEv56+Skiav16.4`）

***

## [16.3.0] — 像素流＋MessagePack 编码（eraWire B3）

### Added

- **`pixel.frame` 像素流（EWP §4.4）**：`hello` caps 增 `pixel.frame`；FE 在 `hello.ok` 里 `mode.pixel=true` 即启用。管线＝内核新事件 `CliFramePainted`（`RefreshStrings` 有效通过点＝帧信号，内核账本第 3 笔，+7 行）→ UI 线程渲染离屏 raster `SKSurface`（尺寸＝`RenderWidth×RenderHeight`）直调 `console.OnPaint` → `ReadPixels` 拷贝 → 专用编码线程 PNG → `EwpOut` 独立像素队列写出。v0 整帧无 dirty-rect（A1-⑥）；过载策略＝捕获队列**丢旧保新**（cap 4）＋写出队列丢新计数（cap 8，EWP §6.3 像素流单独计数，与文本信用窗背压刻意不同——像素可丢、行不可丢）；载荷 `{frameId,w,h,fmt:"png",stats{dropped,encMs},data}`。新文件 `UI/Ewp/EwpPixel.cs`
- **L0.5 编码抽象 `IEwpCodec`（EWP §2）**：JSON-Lines（默认，换行定界）与 MessagePack（`--ewp-msgpack`，4 字节大端长度前缀帧）。MessagePack 为手写子集编解码器（不引包依赖）：编码＝`JsonElement` 递归打包（nil/bool/int/float/str/map/array），`pixel.frame` 直构 map（`data` 为 bin 不经 JSON 往返）；解码＝FE 命令 msgpack→JSON 文本回灌既有 `HandleLine`（零侵入）。`--ewp-json` 为显式别名；**默认仍 JSON-Lines（过渡态）**——A2 v0.1 定稿时连同 C 轨 FE 一起翻正本默认。新文件 `UI/Ewp/EwpCodec.cs`
- **`EwpOut` 双队列写手**：文本 8192 有界队列（满则阻塞生产者＝信用窗背压，B1 语义不变）＋像素小队列；写手 `TryTakeFromAny` 双路取件；`pixel.frame` 为独立帧型不计入 `frame.commit.events`（守恒口径：非 `frame.commit` 非 `pixel.frame` 的服务端事件 ⇔ Σ`frame.commit.events`）。`state` 命令增 `pixel{enabled,frames,dropped,written,writeDropped}` 观测块
- **`hello` 增补**：caps 加 `pixel.frame`；impl `ewp-b2`→`ewp-b3`
- **像素流防呆与观测**：①hello.ok 请求 pixel 但 `DRAWING INTERFACE:TEXTRENDERER` ⇒ 显性 `error{pixel_unavailable}`（该界面无 Skia 画布，不报则零帧静默——fixture 实锤）；②捕获遇 `render=0x0`（最小化窗口锚定布局未落地）一次性 `error{pixel_zero_size}` 诊断；③视口应用强制 `PerformLayout`＋微扰兜底（最小化窗 `ClientSize` 生效但 mainPicBox 恒 0 的 WinForms 行为，`render=0x0 client=1000x640` 实证）；④`state` 命令增 `winClient`/`winState` 字段

### Changed

- **版本签名**：`Skiav16.2` → `Skiav16.3`（`1824+v24+EMv18+EEv56+Skiav16.3`）

***

## [16.2.0] — EWP/0 wire 宿主（eraWire B1）

### Added

- **`--ewp` 启动模式**：eraWire EWP/0 stdio 宿主（新增 `UI/Ewp/EwpHost.cs`）。stdin/stdout JSON-Lines 事件流：事件＝`hello`/`ready`/`legacy.line`（行 ID 单调）/`legacy.clear`/`input.request`（reqId+generation+buttons，TINPUT 超时以 `timeoutMs` 透出）/`meta.state`/`dialog.request`/`frame.commit`/`snapshot.full`/`exit`；命令＝`input.submit`（text/button/enter/skip 四路，**stale 世代拒收**）/`view.ack`/`state`/`snapshot.req`/`sys.quit`。最小化窗口保消息循环（与 `--CliDebug` 同法，非 headless）。涉及 `Program.cs`
- **信用窗背压（EWP §6.3）**：`EwpOut` 专用写手线程＋8192 有界队列（队列满时阻塞生产者＝脚本暂停），未 ack 帧数 ≥8 时 writer 暂停发送（5s 超时兜底＋`ackTimeouts` 计数，`frame.commit` 附 `pending` 积压数）——替换 `--CliDebug` 同步写 stdout 的管道反压死锁路径（知识库 cli-debug-automation §3.1 实证问题）
- **`CliLinesRemoved` 内核事件**：`CLEARLINE` 删行后在 `deleteLine` 触发（参数＝请求删除的逻辑行数），无订阅者零开销；WireHost 据此发 `legacy.clear`（`fromLineId`+`count`，尾部裁剪）。涉及 `EmueraConsole.cs` / `EmueraConsole.Print.cs`
- **B2 首批（eraWire）**：`hello.metrics` 网格保真基准（cellW/cellH/lineH/font，StringMeasure 同源测量）；FE 视口回传（`hello.ok.viewport` → 窗体尺寸，`CLIENTWIDTH()/CLIENTHEIGHT()` 随之生效，零内核改动）；`frame.commit` 附 `ackTimeouts`、信用超时以非致命 `error{ack_timeout}` 外显；结构化 `snapshot.full`（lineId+text 数组＋世代＋挂起输入，断线重连幂等重建）；TCP 传输模式（`--ewp-port`，单客户端，接入前事件在 EwpOut 队列缓冲）
- **B2-3（eraWire）sprite/overlay 语义流**：内核新增 5 事件（`CliImageLayerSet/Cleared/ClearedAll`、`CliIslandPrinted/Cleared`，`ImageLayerManager.SetLayer` 改返真＝仅实际添加时通知；B2 内核账本第 2 笔）；WireHost 发 `sprite.set/clear`（depth/texture/geometry/opacity/follow，colormatrix 留 v1）与 `overlay.island(_clear)`（载荷＝原始 HTML 透传，v0 单实例）；snapshot 增 `sprites`/`island`。metrics 因字体时序改为 **`meta.metrics` 于字体就绪后补发**（Start 期测宽会在 FontFactory 初始化前触碰 SKFont，触发「不適正なフォント→強制終了」守卫——pixtest 实锤）
- **`Dialog` 全量改走 `PlatformInterop` 委托**：GUI 模式行为不变（MessageBox 同参数）；`--ewp/--CliDebug` 模式下原生弹窗（字体错误/无限循环警告等 35 处调用点）转为 `dialog.request` 事件——根治无人值守测试被原生弹窗阻塞与值守屏幕刷窗。涉及 `UI/Dialog.cs`

### Changed

- **版本签名**：`Skiav16.1` → `Skiav16.2`（`1824+v24+EMv18+EEv56+Skiav16.2`）

***

## [16.1.0] — 用户变量 ALS 别名按变量域隔离

### Fixed

- **不同用户变量域使用同一别名时误报「别名已被定义」（Lv1）**：用户定义变量的 CSV 由 `UserDefineLoadData` 借 `ERD_NAMES_INDEX` 槽位解析，`loadDataTo` 末尾无条件调 `loadAliases(aliasPath, ERD_NAMES_INDEX)`，把所有用户变量的 `.als` 灌进**同一张**只写不读的全局别名表；于是 `EI_BASE.als` / `EI_FLAG.als` 等共享旧名（如 `栄養`）时，后加载者逐条报重名。现 `loadDataTo` 跳过 `ERD_NAMES_INDEX` 的全局别名加载——用户变量别名本就由 `UserDefineLoadData → loadAliasesForUserDefined` 写入 per-variable 的 `erdNameToIntDics[varname]`，解析不受影响。实测 eratw-chs：11 条警告 → 0，2262 个文件加载与字典构建正常。涉及 `ConstantData.cs`

### Changed

- **版本签名**：`Skiav16` → `Skiav16.1`（`1824+v24+EMv18+EEv56+Skiav16.1`）
- **Android 版本**：`0.77.0` → `0.77.1`（versionCode 77，对应内核 Skiav16.1）

***

## [16.0.0] — 稳定性、字体渲染与插件加载

### Changed

- **版本签名**：`Skiav15.1` → `Skiav16`（`1824+v24+EMv18+EEv56+Skiav16`）
- **插件系统加载健壮性（MR-19 内核改动回流）**：`PluginManager.LoadPlugins` 逐插件 try/catch，单个插件 DLL 加载失败改为经 `PluginManager.warnPlugin` 打印错误后继续（旧行为：任一插件异常中断整个游戏初始化）；Plugins 目录中的**依赖 DLL（无 `PluginManifest`，如 `MathParser.org-mXparser.dll`）静默跳过**，待宿主插件需要时经 `AssemblyResolve` 兜底加载；插件目录解析改为 `Program.ExeDir` 下的 `Plugins`/`plugins` 优先、进程工作目录相对路径兜底（旧行为：仅 CWD 相对路径——以不同工作目录启动时插件将改从 exe 所在目录加载）；新增 `AppDomain.AssemblyResolve` 转发：插件引用的程序集名 `Emuera` 解析到内核程序集、插件依赖 DLL 从插件目录兜底加载（Windows 主程序集名本就是 `Emuera`，默认绑定命中，该转发实际仅作兜底）。涉及 `PluginManager.cs`
- **`GetFallbackFontForChar` 委派复用 Typeface 查找**：与 `GetFallbackTypefaceForChar` 共享同一回退层级（含衬线判定安全回退表）与 `fallbackTypefaceCache` 缓存，消除原内联查找每次新建单色 `SKTypeface` 且不入缓存的问题；`GDrawStringSkia` 的 BMP 字符与代理对统一按 codepoint 走 `GetFallbackTypefaceForCodepoint`，回退上下文统一继承 `GSETFONT` 当前字体族（原 BMP 固定用 `Config.FontName`，丢失衬线/非衬线倾向）。涉及 `FontFactory.cs` / `GraphicsImage.cs`

### Fixed

- **修复共享 `SKFont` 被提前释放导致的 use-after-free（coreclr 原生访问冲突根因）**：`GraphicsImage.GDispose()`（及其终结器路径）会对 `font` 字段调用 `Dispose()`，而该 `font` 是 `GSETFONT` 传入、由 `FontFactory.GetFont` 缓存并被**多方共享的同一 `SKFont` 包装实例**（`Config.DefaultFont`／其它 `GraphicsImage`／控制台复用同一对象）——一处释放即令其余持有者访问已释放的原生 Skia 对象，此后任意 `sk_font_get_typeface` 调用（如 `TEXTRENDERER` 模式下 `GDRAWTEXT` 走的 GDI 回退分支）都会命中空/悬垂句柄，触发 coreclr 原生访问冲突（`0xc0000005`）。修复：`GraphicsImage.GDispose()` 不再释放 `font`、仅置空引用（`GraphicsImage.cs`）；并收敛 `FontFactory.ClearFont()`，不再 `Dispose()` 可能被 `GlobalStatic.CustomTypefaces`（游戏自有字体）与 `SKTypeface.Default`（进程级静态）共享的缓存 typeface，改为仅清除引用、交由 GC／SkiaSharp 终结器回收（`FontFactory.cs`）。涉及 `GraphicsImage.cs` / `FontFactory.cs`
- **私有字体（`font/` 目录，如 game-icons）在 Skia 与 GDI 两条链上均无法命中（F002）**：`font/` 下字体启动时已加载进 `GlobalStatic.CustomTypefaces`（Skia）与 `GlobalStatic.Pfc`（GDI PrivateFontCollection），但两条渲染链都拿不到它。**Skia 链**：`FontFactory.CreateTypefaceWithFallback` 先调 `SKTypeface.FromFamilyName(name)` 并 `if (typeface != null) return`——而该 API 对**未安装到系统的字体名不会返回 null，而是返回默认字体**（实测 `FromFamilyName("game-icons")` → `'Segoe UI'`、`FromFamilyName("NoSuchFont_ZZZ")` → `'Segoe UI'`），使后面的"自定义字体"分支成为**死代码**。**GDI 链**：`GetGdiFont` 用 `new Font(string)`，`Font` 构造同样解析不到私有字体（实测 `new Font("game-icons")` → `Microsoft Sans Serif`）。现 Skia 链改为**先按族名匹配 `GlobalStatic.CustomTypefaces`（精确族名优先，其后宽松包含匹配）**，未命中再退回系统字体；GDI 链改为**先在 `GlobalStatic.Pfc.Families` 中按名查 `FontFamily`**、命中即用它构造，否则退回系统字体、再退回配置字体。修复后 `<font face='game-icons'>`、`GSETFONT …,"game-icons"` 以及 token 图标（`GCREATE`→`GSETFONT "game-icons"`→`GDRAWTEXT %UNICODE(0xXXXX)%`→`SPRITECREATE "CC_ICON_TOKEN_*"`→`<img src='CC_ICON_TOKEN_*'>`）链路恢复。涉及 `FontFactory.cs`
- **图形文本（GDRAWTEXT）对 BMP 以外字符（emoji 等代理对）无法回退 → 豆腐块/空白（F002）**：`GraphicsImage.GDrawStringSkia` 原按 `foreach (char c in text)` 逐 **UTF-16 码元**处理，代理对被拆成两个孤立代理项，`ContainsGlyph(char)` 与 `GetFallbackTypefaceForChar(char)` 均拿不到正确字形（实测 `GetFallbackTypefaceForChar('\uD83D')` → `'Segoe UI' glyph=0`）。现改为**代理对感知**：识别高/低代理组合、以 `char.ConvertToUtf32` 取得完整码位并走 `GetFallbackTypefaceForCodepoint`，且仅在回退字体确实含该字形时才切换（避免"无字形字体"画出豆腐块）。涉及 `GraphicsImage.cs`
- **emoji/符号回退落到彩色字体，无法以黑白字符呈现（F002）**：`SKFontManager.Default.MatchCharacter` 对 `U+1F441` 等会返回**彩色**字体 `Segoe UI Emoji`（实测渲染 `distinctColors=63`，即显示为彩色图片）。现于回退链中、`MatchCharacter` **之前**新增「单色符号字体」优先层（`Segoe UI Symbol`，实测含该字形且渲染 `distinctColors=1` 为单色），命中即用；未命中才退回 `MatchCharacter`。经验证 `GetFallbackTypefaceForCodepoint(0x1F441)` 现返回 `Segoe UI Symbol`。三个入口 `GetFallbackFontForChar` / `GetFallbackTypefaceForChar` / `GetFallbackTypefaceForCodepoint` 统一处理，控制台与图形文本表现一致。涉及 `FontFactory.cs`

***

## [15.1.0] — 变量写入监视调用栈

### Added

- **变量写入监视可选记录完整调用栈（调试窗口，MR-18）**：「变量监视」页签的控制行改为流式布局（与「打印监视」页同款，按文字实际占位排版、窗口过窄自动换行），新增「记录调用栈」勾选项。勾选后每条写入记录的位置字段由「函数 (文件:行号)」扩展为由内到外最多 20 帧的内联调用栈，格式与打印监视统一——两者各自的 `DescribeSite` / `DescribeCallStack` 抽取为共用的 `CallStackFormatter.cs`。开始监视时控制台提示附带「(含调用栈)」，日志文件首行同步标记该状态。涉及 `CallStackFormatter.cs`（新）/ `PrintWatch.cs` / `VarWriteWatch.cs` / `DebugDialog.cs` / `DebugDialog.Designer.cs`
- **CLI `watch` 命令的 `stack` 参数**：`{"cmd":"watch","action":"start","vars":[…],"stack":true}` 与调试窗口勾选项等价，结果事件回显 `"stack":true|false`。涉及 `CliDebugHost.cs`

### Fixed

- **CLI 启动变量写入监视时残留上一次调试窗口的勾选状态**：`IncludeCallStack` 为静态开关而 `watch start` 从不赋值它——先在 GUI 勾过「记录调用栈」再走 CLI 会静默按含调用栈记录（统计粒度不同），反之该新能力对 CLI 完全不可用。现按 `stack` 参数显式赋值，缺省 `false`。涉及 `CliDebugHost.cs`
- **变量写入监视报告行序不确定**：「按变量统计」段按 `Dictionary` 遍历顺序输出，同一份数据两次报告的行序可能不同，不利于日志 diff。现按写入次数降序、同次数按变量名 Ordinal，与打印监视（15.0.0）同规则。涉及 `VarWriteWatch.cs`

### Changed

- **变量写入监视的「脚本外写入」位置标记由 `<unknown>` 统一为 `<system>`**：与打印监视共用格式化器的必然结果；跨版本的日志该字段不可直接混排统计。涉及 `VarWriteWatch.cs`
- **版本签名**：`Skiav15` → `Skiav15.1`（`1824+v24+EMv18+EEv56+Skiav15.1`）

### Verified

- `dotnet build Emuera.sln -c Debug` 0 错误、无新增警告；勾选项与 CLI `stack` 参数的运行时表现待工作区实测点验。

***

## [15.0.0] — CLI 调试模式 + 打印监视

### Added

- **`--CliDebug` CLI 调试模式**（私家版新增，设计文档 `shared-trae/plans/plan-cli-debug-mode.md`）：stdin/stdout JSON Lines 协议，供 AI agent 等外部程序驱动解释器调试。隐含 `-Debug`；保留 WinForms 消息循环与渲染管线，仅最小化隐藏窗口。stdin 线程读取命令并经 `MainWindow.Invoke` 派发到 UI 线程执行；stdout 推送事件流。
  - 事件：`hello` / `ready` / `out`（新显示行，与 `ClipboardProcessor.AddLine` 同挂点，`StripHTML` 纯文本）/ `wait`（进入输入等待，含 `inputType`、`timeoutMs`、默认值、当世代可选 `buttons` 列表）/ `state`（Error/Quit 终态）/ `dialog`（模态对话框替代事件）/ `exit`。
  - 命令：`input` / `click`（按钮世代校验 + 鼠标点击语义字面提交）/ `key`（enter/skip）/ `debug`（等价调试窗口控制台，返回 `DebugConsoleLog` 增量）/ `watch`（变量写入监视 start/stop/status，直驱 `VarWriteWatch`）/ `trace` / `screen` / `state` / `buttons` / `log` / `quit` / `help`；另支持 `!表达式` 便捷输入。
  - 模态对话框（`PlatformInterop.ShowMessage`/`ShowQuestion` 等）在 CLI 模式下全部替换为 stdout 事件，避免无人值守卡死。
  - 内核新增并行订阅钩子：`EmueraConsole.CliLineAdded` / `CliWaitInputEntered` / `CliTerminalStateEntered` 事件与 `GetSelectableButtons()`（按钮筛选逻辑移植自 SkiaX `RefreshQuickButtonAsync`，与 `PressEnterKey` 世代校验同规则）。无订阅者时零开销，GUI 行为不变。
  - 涉及 `CliDebugHost.cs`（新）、`Program.cs`、`EmueraConsole.cs` / `EmueraConsole.Print.cs`
- **打印监视（调试窗口，MR-17）**：调试窗口新增「打印监视」页签（开始 / 停止并报告 / 记录调用栈勾选），记录每一次送往游戏控制台的文本输出及其触发位置（函数 + 文件:行号），明细写入 `debug\print_trace.log`（超 10 万条后仅统计），页签内 300ms 实时视图，停止时在控制台打印按位置统计的汇总报告（次数降序）。可选附加最多 20 帧的内联调用栈（由内到外，各层取调用点行）。常规 PRINT 族（`PrintStringBuffer` 各入口）、HTML_PRINT / HTML_PRINTC、系统单行输出与错误按钮均覆盖；调试窗口自身 ERB 执行的输出自动排除。未开启时每次打印仅多一次静态 bool 判断，零行为影响。涉及 `PrintWatch.cs`（新）/ `PrintStringBuffer.cs` / `EmueraConsole.Print.cs` / `DebugDialog.cs`

### Fixed

- **CLI `screen` 在 div 画面上返回空数组**：原实现调用 `GetDisplayLines(lines)`，其参数是「逻辑行」计数语义（无对应逻辑行时返回 `null`），而 div 类画面每轮 `CLEARLINE` + 重绘，逻辑行数远小于请求值 → 恒为空。新增 `EmueraConsole.GetCliTailLines(count)` 按物理行自底向上读取（`GetDisplayLines` 不动，GUI 无影响）。
- **div（HTML `rect` 覆盖层）内容无法读取**：div 内文字不进入 `displayLineList`，而是注册在静态 `ConsoleEscapedParts`。`screen` 结果新增 `divs` 字段（`[{top, lines[]}]`，画面位置自上而下），由 `EmueraConsole.GetCliDivContents()` + `ConsoleEscapedParts.CollectDivParts()` 提供。
- **div 内按钮无法枚举/点击**：`GetSelectableButtons()` 只遍历 `displayLineList`，而 div 按钮经 `ConsoleEscapedParts → ConsoleDivPart.Children` 注册（与 `BINPUT` / `IntButton`/`StrButton` 验证路径同源）→ `buttons` 恒为空、`click` 无从下手。现按同一路径补充收集（不加世代过滤，与 `BINPUT` 一致）。
- **CLI 首条 stdin 命令偶发 `invalid JSON`**：PowerShell 驱动偶发在流首写入 UTF-8 BOM 前导（`EF BB BF`），且 `Console.In` 在无控制台 WinExe 下回退到系统 ANSI 代码页（936）解码 → 首行变成 `鑘縁…` 杂字符、id 丢失。现锁定 stdin 为显式 UTF-8（含 BOM 自动侦测/剥离）读取，与协议约定（UTF-8 JSON Lines）一致；另对首尾 BOM/空白杂字符剥离重试，彻底失败时把原始行内容回显进错误消息便于诊断。
- 涉及 `EmueraConsole.cs` / `EmueraConsole.Print.cs`（无改动，仅复用）/ `ConsoleEscapedParts.cs` / `ConsoleDivPart.cs`（`CliPointY` 暴露 protected `PointY`）/ `CliDebugHost.cs`（stdin 读取 + 容错诊断）

### Verified

- 发布版（`发布.bat` → `Release-NAudio win-x64`）+ div 样例（`カラム機能(div).ERB`）双向交互全流程实测（`pub9`）：
  - `state` / `screen`（框+divs）/ `buttons`（div 与框内按钮同列）/ `click`（ div 按钮 `11` → `FLAG:0=11` 读回；框按钮 `101` 切第 2 列；列内 `21` → `FLAG:0=21`；`100` 切回第 1 列且值保持）/ `input 12`（`PRINT_IMG` 渲染进 `screen.lines`）/
  - `input 12` 触发 `PRINTFORMW` 链期间进入 `EnterKey` 无按钮 wait —— 用 `key enter` 逐个推进直到回到 `IntValue` 有按钮，再 `click 99`（`FLAG:0=12` 读回）→ 样例循环退出 → ERB `QUIT` → 进程自行退出（`state Quit` + 无 Emuera 残留，无需再发 `quit`）。


### Changed

- **版本签名**：`Skiav14` → `Skiav15`（`1824+v24+EMv18+EEv56+Skiav15`）

***

## [14.0.0] — 变量写入监视

### Added

- **变量写入监视（调试窗口）**：变量监视页新增"开始监视 / 停止监视并打印报告"按钮，以监视列表中的变量为对象集，记录监视期间每次 ERB 脚本写入的变量、新值、所在函数与文件行号，明细写入 `debug\var_write.log`（超 10 万条后仅统计），停止时在控制台打印汇总报告。单点赋值（`=` / `+=` 等）、`VARSET` / `CVARSET`、数组字面量赋值均覆盖；调试窗口自身的写入（锁定写回/值单元格赋值/控制台手动赋值）自动排除；关联数组与字符串下标暂不支持，开始监视时明确提示跳过原因。涉及 `VarWriteWatch.cs`（新）/ `VariableTerm.cs` / `VariableEvaluator.cs` / `DebugDialog.cs`

### Changed

- **版本签名**：`Skiav13` → `Skiav14`（`1824+v24+EMv18+EEv56+Skiav14`）

***

## [13.0.0] — 预设变量 ERD 扩展

### Added

- **预设变量同名 ERD 合并**：ERB 目录下与预设变量同名的 `.erd` 文件（如 `ITEM.erd`）可扩展对应预设变量的名表。ERD 只填 CSV 为空的槽（CSV 优先）；ITEM 额外支持第三列价格（CSV 出现过价格列即视为已设，含显式 0，ERD 不得覆盖）。合并发生在全部 CSV 加载后、逆引字典建立前，ERD 补入的名字同样可被 `GETNUM` / `ITEMNAME:n` 使用。涉及 `ConstantData.cs`（尊尼获加_ERD_PRESET）
- **`EXIST_IN_CSV` / `EXIST_IN_ERD` 表达式函数**：`EXIST_IN_CSV("ITEM", 6)` 查询指定槽位名字的有效来源（合并后来源，非"谁申请过"），CSV 则为 1；ERD 版同理。首参为字符串变量名（GETNUMB 风格，可反射拼接枚举）；第二参数只收整数索引，不支持的变量名/越界索引返回 0。被 CSV 顶掉的 ERD 名在运行时不可见（仅加载期警告），跨槽重名 ERD 行警告跳过。涉及 `Creator.Method.cs` / `Creator.cs`

### Changed

- **版本签名**：`Skiav12.2` → `Skiav13`（`1824+v24+EMv18+EEv56+Skiav13`）

***

## [12.2.0] — RAND 随机数入口安全保护

### Changed

- **RAND 参数非法不再弹窗中断**：`RAND:n` 参数 ≤ 0 时钳制为返回 `0`；`RAND(min,max)` / `RAND(max)`（含浮点重载）参数 `max ≤ min` 时钳制为返回下界 `min`。原行为为抛 CodeEE 弹窗并终止脚本，对玩家无操作价值且中断存档会话；现与兼容模式 RAND（CompatiRandToken）的归一化策略对齐。首次触发时向控制台输出一次错误色警告（调试模式下同时进入调试窗口），后续触发静默钳制，避免循环刷屏。涉及 `VariableToken.cs`（RandToken）/ `Creator.Method.cs`（RandMethod 整数/浮点两分支）
- **版本签名**：`Skiav12.1` → `Skiav12.2`（`1824+v24+EMv18+EEv56+Skiav12.2`）

***

## [12.1.0] — 多语言编码兼容 + CHKDATA 存档版本信息

### Added

- **CHKDATA 返回存档版本信息**：`EraDataResult` 新增 `Version` 字段；`CHKDATA` 的 `RESULT:1` 返回存档内版本号（文件不存在等其他错误时 `0`，版本不匹配或正常时返回实际版本）。涉及 `EraDataStream.cs` / `VariableEvaluator.cs` / `Creator.Method.cs`

### Fixed

- **多语言编码兼容性**：`LangManager` 的字节长度计算改为逐字符往返回退（当前语言编码无法往返的字符改用日文 932 编码计数），修复非日文编码（如 GBK）下 CJK 文本在 `GetStrlenLang`/`GetUFTIndex`/`GetSubStringLang` 中的长度与子串错乱

### Changed

- **版本签名**：`Skiav12` → `Skiav12.1`（`1824+v24+EMv18+EEv56+Skiav12.1`）

***

## [12.0.0] — 调试窗口：变量监视「锁定」与直接赋值

### Added

- **变量监视新增「锁定」列**：勾选后固定该变量的值，并每 200ms 在安全时机自动写回，防止其被脚本改动；表达式、函数、常量、只读变量等不可赋值对象无法锁定
- **「值」单元格可直接赋值**：点击编辑并确认即可赋值，与调试控制台语义一致
- 锁定状态为会话内状态，不持久化

### Fixed

- **调试窗口 UI 布局**：修复高 DPI 下底部按钮与面板重叠、按钮文字被裁切的问题

### Changed

- 变量监视改为「锁定 / 对象 / 值」三列布局，默认窗口宽度加大
- **版本签名**：`Skiav11.2` → `Skiav12`（`1824+v24+EMv18+EEv56+Skiav12`）

***

## [11.1.0] — ToolTip 异步回调 NRE 防御

### Fixed — OnPaint ToolTip 异步回调空引用崩溃

- **`context.Post` 回调空引用加固**（`EmueraConsole.cs` OnPaint ToolTip 块）：
  - `SynchronizationContext.Current` 判空：`context` 为 `null` 时跳过 ToolTip 调度，避免 `context.Post` 空引用崩溃
  - 窗口生命周期检查：回调内先检查 `window == null || window.IsDisposed || window.MainPicBox == null || window.MainPicBox.IsDisposed`，窗口销毁/控件重建间隙直接返回，放弃显示 ToolTip
  - `Cursor.Current` 判空：鼠标移出窗口后 `Cursor.Current` 为 `null`，原代码直接访问 `Cursor.Current.Size.Height` 触发 `NullReferenceException`；改为安全获取，缺失时回退默认高度 32px
  - **`Screen.FromPoint` 参数修正**：原代码传入 `mousePos`（窗口局部坐标），`Screen.FromPoint` 需要屏幕绝对坐标，改为传入 `absoluteP`（`Cursor.Position`），修正 ToolTip 在多显示器/缩放场景下的定位错误
- **影响场景**：鼠标悬停 ToolTip 延迟显示期间（`Task.Delay(InitialDelay)`），若玩家移开鼠标、关闭窗口或触发画面重建，高概率触发该崩溃；宴会等多人同屏场景按钮密集，复现频繁

### SkiaX Xamarin 端说明

- **Xamarin 端同步防御**：`Emuera.Xamarin/Platform/GameView/EmueraConsole.cs` ToolTip 块同步加 `context` 判空 + `window.MainPicBox` 生命周期检查（移动端无 `Cursor.Current`/`Screen.FromPoint`，无对应项）
- **Android 版本**：`0.69.0` → `0.70.0`（versionCode 69 → 70，对应内核 Skiav11.1）

### Changed

- **版本签名**：`Skiav11` → `Skiav11.1`（`1824+v24+EMv18+EEv56+Skiav11.1`）

***

## [11.0.0] — GC 配置回退 + 内存诊断门控

### Changed — GC 配置回退

- **ServerGC → WorkstationGC**：回退 [3.0.0] 上游同步（commit `4432ee9d`）引入的 `<ServerGarbageCollection>true</ServerGarbageCollection>`
  - 桌面 GUI 程序（单窗口、单线程交互）适合 WorkstationGC：内存占用低、及时归还 OS
  - 文字游戏对 GC 单次回收暂停不敏感，WorkstationGC 的回收更频繁但更短

### Added — 内存诊断工具（门控）

- **MemoryDiagnostic 工具门控化**：新增配置项 `MemoryDiagnosticEnabled`（默认 `false`），关闭时引擎不输出 `memory_diagnostic.log`
  - 该工具为临时诊断用途：关闭引擎时将内存快照（进程内存 / GC 堆细分 / 编译后脚本 / VariableData / FontFactory 缓存 / SQLite 连接等）写入游戏主目录
  - 日后需要诊断内存问题时，在 `emuera.config` 中添加 `MEMORYDIAGNOSTICLOG:YES` 即可开启

### Added — 诊断段落扩展

- **GC 堆细分**：`GC.GetGCMemoryInfo()` 的 HeapSizeBytes / FragmentedBytes / Gen0-2 堆大小 / LOH 大小
- **线程统计**：线程总数及 Running / Wait 状态分布
- **FontFactory 缓存**：fontDic / fallbackTypefaceCache / fallbackTypefaceCodepointCache / gdiFontDic 条目数
- **显示行缓存**：displayLineList 当前行数 / MaxLog

### SkiaX Xamarin 端说明

- **内存诊断不适用于 Xamarin 端**：MemoryDiagnostic.cs 引用桌面独有类型（UI.FontFactory / AppContents / Program.ExeDir），在 `Emuera.Xamarin.Core.csproj` 和 `Emuera.Xamarin.csproj` 中排除编译（Android/Xamarin 均不生成诊断日志）
- **ServerGC 回退仅影响 Desktop**：Android 端 `<ServerGarbageCollection>` 属性保留（Mono/ART 运行时不受该属性控制，移动端内存模型不同）
- **Android 版本**：`0.68.2` → `0.69.0`（versionCode 68 → 69，对应内核 Skiav11）

***

## [10.1.0] — 输入宏开关

### Added

- **DISABLE_INPUT_MACRO 函数**：关闭所有输入（textbox + SEQUENCEINPUT）的宏解析
  - 调用后，输入按字面整段喂入，不再解析 `(...)` 重复宏
  - 不再按 `\n` 拆分输入，也不再处理 `\e` MesSkip
  - 返回值始终为 `0`
  - Desktop 端实现于 `Emuera/UI/Game/EmueraConsole.cs`；SkiaX Xamarin 端实现于 `Emuera.Xamarin/Platform/GameView/EmueraConsole.cs`
- **ENABLE_INPUT_MACRO 函数**：恢复输入宏解析（默认行为）
  - 与原版 PressEnterKey 行为一致
  - 返回值始终为 `0`

### Changed

- **版本签名**：`Skiav10` → `Skiav10.1`（`1824+v24+EMv18+EEv56+Skiav10.1`）

***

## [10.0.0] — SEQUENCEINPUT 函数

### Added

- **SEQUENCEINPUT 函数**：接收一个字符串参数，将其排入队列，在下一次 `INPUT`/`TINPUT`/`INPUTS`/`TINPUTS` 等 WaitInput 时机作为用户输入自动提交
  - 调用 `SEQUENCEINPUT("输入内容")` 后，下一次进入 WaitInput 状态时，引擎调用 `PressEnterKey` 处理排队的字符串，行为与用户在文本框中输入并按回车一致
  - 字符串中的 `\n` 会被拆分为多段，每段喂入一个 ERB WaitInput
  - 字符串中的 `\e` 会被识别为 MesSkip（跳过等待），因为在 FORM 字符串解析中 `\e` 保留为 2 字符（`\` + `e`），让 SEQUENCEINPUT 路径能正确识别
  - 返回值始终为 `0`
  - 同时作用于 `WaitInput` 和 `WaitInputNoFocus`（NF 后缀指令）
  - Desktop 端实现于 `Emuera/UI/Game/EmueraConsole.cs`；SkiaX Xamarin 端实现于 `Emuera.Xamarin/Platform/GameView/EmueraConsole.cs`

***

## [9.1.0] — STRFORMCHECK 函数 + HTML_PRINT font valign 扩展

### Added

- **STRFORMCHECK 函数**：接收一个字符串参数，判断其是否能作为 `STRFORM` 展开；可展开返回 `1`，不可展开返回 `0`
  - 解析失败（语法错误）返回 `0`
  - 运行时求值失败（变量不存在等）返回 `0`
  - 与 `STRFORM` 共享同一套解析器，语义一致
- **HTML_PRINT `<font>` valign 属性**：在已有 `size` 属性基础上新增 `valign` 属性（`top`/`middle`/`bottom`），控制行内不同字号文本的垂直基准位置
  - `valign='top'`（默认）：文本顶部对齐，与现有行为一致
  - `valign='middle'`：文本垂直居中
  - `valign='bottom'`：文本底部对齐
  - 非法值（如 `center`）抛出 `CanNotInterpretAttribute` 错误
  - 嵌套 `<font>` 标签继承外层 `valign` 设置
  - Desktop 端与 SkiaX Xamarin 端同步实现

***

## [9.0.0] — F11 全屏比例缩放

### Added

- **F11 全屏比例缩放**：按 F11 时，游戏窗口内容（字体、图片、分割线、HTML shape、ImageLayer、CBG 背景图等所有可见元素）按配置中的宽度比例缩放到全屏，适配高分辨率显示屏
  - 核心机制：在 `SKCanvas` 绘制入口处应用一次 `canvas.Scale()` 变换，所有后续绘制操作自动按比例放大
  - 不修改 `Config.FontSize`、`Config.WindowX`、`Config.DrawableWidth`——引擎继续以逻辑尺寸布局
  - 不重排显示行——避免 HTML 往返 bug
  - 鼠标坐标自动转换为逻辑坐标
  - 输入框（WinForms RichTextBox）单独缩放字体和高度
  - 支持多显示器（`Screen.FromControl` 获取当前屏幕）
  - 引入 `RenderWidth`/`RenderHeight` 属性表示逻辑渲染尺寸，`EmueraConsole.OnPaint` 及相关方法使用此属性替代 `MainPicBox.Width`/`Height`
  - 安卓端零影响（不编译桌面 `MainWindow.cs` 和 `EmueraConsole.cs`）


***

## [8.2.0] — 调试窗口 WaitInput 状态下自定义函数求值修复

### Fixed

- **调试窗口 WaitInput 状态下监视自定义函数返回错误值**：游戏处于 WaitInput 状态时，`console.IsRunning` 返回 `False`，导致 `runScriptProc` 在循环开头立即退出，不执行函数体
  - 症状 1：返回 0（`MethodReturnValue` 为 null，克隆状态下初始值）
  - 症状 2：返回 HTML 字符串（`MethodReturnValue` 保留上一个 `#FUNCTIONS` 函数的残留值）
  - 修复：`Process.GetValue` 进入时设置 `forceRunning=true`，`runScriptProc` 检查 `(!console.IsRunning && !forceRunning)` 而非 `!console.IsRunning`
  - 修复：`Process.GetValue` 进入时 `state.MethodReturnValue = null`，清除残留值
  - 修复：`Process.GetValue` 的 finally 块中恢复 `forceRunning = savedForceRunning`

- **调试窗口表达式函数求值后 NullReferenceException**：`ReturnF` 后 `currentLine=null`，`ShiftNextLine` 访问 null
  - 修复：将 `ScriptEnd` 检查移至 `runScriptProc` 循环开头，`currentLine` null 安全检查

- **调试窗口 GetValue 失败路径私有变量状态泄漏**：`GetValue` 失败路径未调用 `ScopeOut`
  - 修复：检测 `functionList.Count < savedState.funcCount` 时显式调用 `ScopeOut`

## [8.1.0] — Float 类型错误消息修复 + CanReturnFloat 动态返回类型

### Fixed

- **Float 类型错误消息误报为"字符串型"**：原引擎只有 Integer/String 两种类型，错误消息使用二则判断（`== String ? Str消息 : Int消息`），Float 类型被误报为"字符串型"或"整型"
  - `#FUNCTION` 函数返回 Float 时报 `ReturnfStrInIntFunc`（"返回了字符串类型"）→ 区分 Float→`ReturnfFloatInIntFunc` / String→`ReturnfStrInIntFunc`
  - `#FUNCTIONS` 函数返回 Float 时同理 → 新增 `ReturnfFloatInStrFunc`
  - 函数参数 Float→Integer 转换报 `CanNotConvertStrToInt`（"不能从字符串型转换"）→ 报 `CanNotConvertFloatToInt`
  - 函数参数类型不匹配时 `String? Str : Int` 二则判断 → 三则：`String? Str : Float? Float : Int`（2处）
  - Ref 参数类型不匹配时缺少 Float 分支 → 添加 `Float? FloatVar : Var` 和 `Float? FloatArray : Array`
  - Float 变量赋 String 报 `SetIntToStr`（"向非整型赋整数"）→ 报 `SetStrToFloat`（"向浮点型赋字符串"）
  - Integer 变量赋 Float 报 `SetIntToStr` → 区分 Float→`SetFloatToInt` / String→`SetStrToInt`

### Changed

- **CanReturnFloat 动态返回类型机制**：POWER/SQRT/ABS 等函数根据参数类型动态返回 Integer 或 Float
  - 编译期 `GetEraType()` 检查参数中是否有 Float 类型来决定返回类型
  - 运行期 `GetReturnValue()` 通过 `HasFloatArg` 分派实际返回值
  - `FunctionMethod.CanReturnFloat` 属性标记动态返回函数
  - `FunctionMethodTerm.GetEraType()` 覆盖基类方法
  - 涉及函数：POWER, ABS, SQRT, CBRT, LOG, EXP, SIGN, LIMIT, MAX, MIN, SIN, COS, TAN, ASIN, ACOS, ATAN, FLOOR, CEIL, ROUND
- **版本签名**：`Skiav8` → `Skiav8.1`（`1824+v24+EMv18+EEv56+Skiav8.1`）

***

## [8.0.0] — ERD 系统 ALS 别名支持

### Added

- **用户定义变量 ALS 别名支持**：`#DIM` 声明的用户变量现在可以通过 `.als` 文件定义枚举别名
  - 之前：只有系统变量（ABL/TALENT/CFLAG 等）的 CSV 文件支持 `.als` 别名文件
  - 现在：用户定义变量（如 `BUFF`）的 CSV 文件也支持对应的 `.als` 文件
  - 用法：在 `CSV/BUFF.csv` 旁放置 `CSV/BUFF.als`，格式与系统变量的 ALS 文件相同（`index,别名`）
  - 示例：`BUFF.als` 中 `1,气力` 使得 `BUFF:2:气力` 等价于 `BUFF:2:1`
  - 多维变量支持：2D/3D 变量的 ALS 文件命名与 ERD 一致（如 `BUFF@1.als`、`BUFF@2.als`）
  - 别名不覆盖 CSV 中已有的同名定义（CSV 优先）
  - 新增 `loadAliasesForUserDefined()` 方法，将别名注入 `erdNameToIntDics` 字典
  - 3 端同步：LazyLoading Desktop + SkiaX Desktop + SkiaX Xamarin

### Changed

- **版本签名**：`Skiav7.0` → `Skiav8`（`1824+v24+EMv18+EEv56+Skiav8`）
- **Android versionCode**：65 → 66，versionName：0.65.0 → 0.66.0

***

## [7.3.2] — font 标签颜色继承修复 + SETIMAGELAYERL 注释修正

### Fixed

- **font 标签颜色继承失效**：`font.Color` 初始值已改为 `int.MinValue`，但继承检查仍用 `font.Color == -1`（死代码），导致嵌套 `<font>` 标签不继承外层颜色
  - 修复：`font.Color == -1` → `font.Color == int.MinValue`，`font.BColor == -1` → `font.BColor == int.MinValue`
- **SETIMAGELAYERL 过时注释**：`Instraction.Child.cs` 和 `ArgumentBuilder.cs` 中 "Always anchors to LINECOUNT" 修正为 "Always anchors to GetLineNo (current display line index)"

***

## [7.3.1] — 颜色哨兵值修复 + SETIMAGELAYERL GetLineNo 修复 + border 默认颜色

### Fixed

- **颜色哨兵值 `-1` 与 `0xFFFFFFFF` 冲突**：`stringToColorInt32("#FFFFFF")` 返回 `0xFFFFFFFF`（有符号 int = -1），被 `!= -1` / `>= 0` 守卫误判为"颜色未设置"
  - `HtmlManager` 中所有颜色哨兵值从 `-1` 改为 `int.MinValue`（`HtmlAnalzeStateFontTag.Color/BColor`、div/shape 解析中的 `color/bcolor` 局部变量）
  - `ConsoleDivPart` 构造函数中 `color != int.MinValue` 替代 `color != -1`
  - `box.color[i]` 直接 `Color.FromArgb()`，不再用 `!= -1` 守卫（`box.color != null` 已确认 bcolor 属性存在）
  - 3 端同步修复：LazyLoading Desktop + SkiaX Desktop + SkiaX Xamarin
- **SETIMAGELAYERL 行号锚定错误**：使用 `LineCount`（逻辑行号）而非 `GetLineNo`（显示行索引）锚定行号，导致 Y 坐标偏移 (displayLine - logicalLine) × LineHeight
  - 修复：改为 `exm.Console.GetLineNo`（displayLineList 索引）
- **HTML div border 无 bcolor 时边框不绘制**：`ConsoleDivPart` 只在有 `bcolor` 属性时初始化 `borderColors`，WinForms 原版在 `colors == null` 时用 `Config.ForeColor`
  - 修复：`box.border != null && box.color == null` 时，`borderColors` 默认使用 `Config.ForeColor`
- **`stringToColorInt32` ARGB 分支 `ToInt32` 溢出**：ARGB 分支先执行 `Convert.ToInt32(colorvalue, 16)`，9 位+ hex 溢出 int32，报错信息不准确
  - 修复：先判断 `colorvalue.Length`，≤6 走 `ToInt32`（RGB），>6 走 `ToInt64`（ARGB）
- **HTML div `border='1'` em 单位陷阱**：无 px 后缀被当作 em 单位，FontSize=18 时 `1*18/100=0`
  - 修复：ERB 层 `border='1'` → `border='1px'`

### Changed

- **SETIMAGELAYERL 锚定语义修正**：文档从"始终锚定当前行（LINECOUNT）"修正为"始终锚定当前显示行（GetLineNo）"
  - `LINECOUNT` 是逻辑行计数器，`GetLineNo` 是 displayLineList 索引，两者不一致
  - `GetLinePointY` 期望的参数是 displayLineList 索引

***

## [7.3.0] — SETIMAGELAYERL 行相对定位（xpos/ypos）+ ARGB_TO_HTML_COLOR 工具函数

### Fixed

- **SETIMAGELAYERL 参数重新设计**：API 简化为 `SETIMAGELAYERL spriteName, depth, xpos, ypos, width, height, opacity, CM_ARRAY`
  - 移除 `lineNo` 参数，始终锚定当前行（LINECOUNT），与 HTML `<img>` 参数约定一致
  - `xpos`：相对行位置的 X 偏移（与 HTML `<img>` 的 `xpos` 属性语义一致，自动包含 `ShapePositionShift`）
  - `ypos`：相对行顶边的 Y 偏移（与 HTML `<img>` 的 `ypos` 属性语义一致）
  - `xpos=0, ypos=0` 时渲染位置与同行的 `<img>` 完全一致
  - SETIMAGELAYER 与 SETIMAGELAYERL 的定位模型明确区分：绝对坐标 vs 行相对偏移


***

## [7.2.0] — 渲染管线统一 depth + SETIMAGELAYER 多精灵 + SETIMAGELAYERL + ARGB 透明度 + div height auto

### Fixed

- **渲染管线统一 depth**：SETIMAGELAYER、CBG、escapedParts（含 div）共享同一 depth 排序系统
  - 修复了 div 永远在 ImageLayer 之上的问题——原来 ImageLayer 在 Step 3 整批绘制，div 在 Step 4 绘制，两套独立 depth 系统
  - `ImageLayerManager` 新增 `DrawLayersAtDepth(canvas, viewportW, viewportH, scrollY, int? depth)` 和 `GetDepths()` 方法
  - `EmueraConsole.OnPaint` 将三个 depth 源（edepth + cbgList.zdepth + idepths）合并为统一降序列表，每个 depth 先绘制 ImageLayer 再绘制 CBG/div/文本
- **SETIMAGELAYER 多精灵支持**：`ImageLayerManager._layers` 从 `Dictionary<long, ImageLayer>` 改为 `List<ImageLayer>`，同 depth 多精灵按添加顺序渲染，不再覆盖
- **SETIMAGELAYERL 新增指令**：自动 followScroll=1 + 自动 GETLINEY y 轴转换，y 参数为行号（LINECOUNT），渲染位置与 HTML img 完全一致
- **SETIMAGELAYER 空参数支持**：第 3-9 参数为空时采用默认值，只有 spriteName/depth 不可为空
- **HTML div color ARGB 支持**：`stringToColorInt32` 支持 ARGB 透明度，6 位及以下解析为 RGB（Alpha=255），6 位以上解析为 ARGB；`ConsoleDivPart` 不再强制 Alpha=255
- **HTML div height auto**：`<div>` 的 `height` 属性改为可选，省略时自动从内容行数 × 行高 + padding/border 计算高度

***

## [7.1.0] — SETIMAGELAYER 多精灵支持 + SETIMAGELAYERL + ARGB 透明度 + div height auto

### Fixed

- **SETIMAGELAYER 多精灵支持**：`ImageLayerManager._layers` 从 `Dictionary<long, ImageLayer>` 改为 `List<ImageLayer>`，同 depth 多精灵按添加顺序渲染，不再覆盖
- **SETIMAGELAYERL 新增指令**：自动 followScroll=1 + 自动 GETLINEY y 轴转换，y 参数为行号（LINECOUNT），渲染位置与 HTML img 完全一致
- **SETIMAGELAYER 空参数支持**：第 3-9 参数为空时采用默认值，只有 spriteName/depth 不可为空
- **HTML div color ARGB 支持**：`stringToColorInt32` 支持 ARGB 透明度，6 位及以下解析为 RGB（Alpha=255），6 位以上解析为 ARGB；`ConsoleDivPart` 不再强制 Alpha=255
- **HTML div height auto**：`<div>` 的 `height` 属性改为可选，省略时自动从内容行数 × 行高 + padding/border 计算高度

***

## [7.0.0] — GETLINEY 表达式函数

### Added

- **GETLINEY 表达式函数** — 返回指定行号的物理 Y 坐标（左下原点，与 SETIMAGELAYER 坐标系一致）
  - `GETLINEY(lineNo)` 返回 `lineNo` 行在窗口中的物理 Y 坐标
  - 用于将 SETIMAGELAYER 图层与 HTML 文本流对齐
  - 底层复用 `EmueraConsole.GetLinePointY(int lineNo)` 方法
  - 负数参数抛出 `CodeEE`

***

## [6.2.0] — GETDISPLAYLINE 负数倒数索引

### Added

- **GETDISPLAYLINE 负数参数支持** — 负数参数表示从底部倒数索引
  - `GETDISPLAYLINE(-1)` 返回最后一行，`GETDISPLAYLINE(-2)` 返回倒数第二行
  - 原有正数索引行为不变（0=第一行）
  - 解决了 `GETDISPLAYLINE(LINECOUNT - 1)` 的语义不对齐问题：`LINECOUNT` 是逻辑行数，`GETDISPLAYLINE` 是显示行索引，两者不对齐；负数索引直接在显示行层面操作，绕过了不对齐问题
  - 修复 `long.MinValue` 和大负数 `long→int` 强转溢出问题

***

## [6.1.0] — IsFunctionMethod 边界检查 + FindContextByLabel 快照枚举

### Fixed — A 类跨平台 Bug 修复（双端受益）

- **Process.State.cs** — `IsFunctionMethod` 属性 `ArgumentOutOfRangeException`
  - `functionList[currentMin]` 在 `functionList` 被部分清除后索引越界
  - 修复：添加 `if (currentMin >= functionList.Count) return false;` 边界检查
  - 触发场景：异常路径（BEFORE_ERROR/BEFORE_THROW 处理）中 `RollbackToState()` 修改 `functionList` 但未同步 `currentMin`

- **Process.State.cs** — `FindContextByLabel` 方法 `InvalidOperationException`
  - `foreach (var ctx in stack)` 枚举 `_contextStack` 期间栈被 `ClearFunctionList()` 等操作修改
  - 修复：改用快照枚举 `foreach (var ctx in stack.ToArray())`
  - 触发场景：LOCAL/ARG 变量带 subID 访问时，`Return()` 异常触发 BEFORE_ERROR → `ClearFunctionList()` 修改 `_contextStack`

## [6.0.0] — 调试窗口修复：LOCAL@FUNCNAME + 调用栈保留 + 监视稳定性

### Fixed

- **LOCAL@FUNCNAME 检测失效** — `GetArrayLocal()` 忽略 `subID`，始终返回 `CurrentContext` 的数组。现在按 `subID` 在上下文栈中查找匹配的 `ExecutionContext`
- **错误/THROW 后调试窗口调用栈被清空** — `handleException` 后不再调用 `ClearFunctionList()`，保留调用栈供调试窗口查看。新增 `ClearFunctionListPreserveTrace()` 供 BEFORE_ERROR/BEFORE_THROW 内部使用
- **调试窗口监视含 LOCAL 变量的表达式报错** — `saveCurrentState` 克隆 state 后 `CurrentContext` 为 null，LOCAL 变量走 FallbackArray 返回空数组。现在 `Clone()` 保留对原始 `_contextStack` 的引用，`CurrentContext` 在自身栈为空时自动回退
- **调试窗口表达式函数求值后 currentLine 残留** — `Process.GetValue` 的 finally 块在成功路径下 `PopContext()` 但未恢复 `currentLine`。现在 `CaptureCallState` 同时保存 `currentLine`，成功/失败路径均恢复
- **调试窗口一个监视报错后其他监视全部失败** — 上述 currentLine 残留导致后续监视在错误函数上下文中解析私有变量。修复 currentLine 恢复后错误传播链断裂

### Changed

- `DisableBeforeErrorThrow` 配置项不再必要（错误后调用栈已默认保留），但保留以维持向后兼容
- 新增 `ProcessState.ContextStackCount` 属性

***

## [5.2.0] — DisableBeforeErrorThrow 配置项：保留调试函数栈

### Added

- **DisableBeforeErrorThrow 配置项** — 新增配置选项，启用后跳过 BEFORE_ERROR/BEFORE_THROW 事件函数，直接抛出异常。解决这两个事件在异常处理时清空函数栈的问题，使调试窗口能在异常发生时正确显示调用栈和局部变量。默认关闭以保持向后兼容。

### Fixed

- 调试窗口在 THROW 或错误发生时无法监视函数参数的问题（需启用 DisableBeforeErrorThrow）

### Xamarin 移植注意

- Xamarin 端 `ConfigData.SetDefault()` 已同步添加对应 ConfigItem（参考 v0.60.2 PluginAvailableWarn NRE 修复）

***

## [5.1.0] — EEv56 上游对齐：PluginAvailableWarn + TOOLTIP 回退

### Changed — 上游对齐（emuera.em EEv56）

- **PluginAvailableWarn 配置项**（源自 ee commit `0abdff8`）
  - 移除 `pluginsAware.txt` 安全检查机制（原机制：文件不存在时 throw ExeEE 阻止运行）
  - 新增 `ConfigCode.PluginAvailableWarn` 配置项（默认 true），检测到 Plugins 目录有 DLL 时打印警告
  - 新增 `GlobalStatic.ExistPlugin` 标志，`PluginManager.LoadPlugins()` 中根据 DLL 存在性设置
  - 新增 `Lang.PluginAvailable` 翻译字符串
  - 警告内容：「注意：外部プラグイン機能が有効になっています。この機能で生じた不具合等はEmueraのサポート対象外となります」
  - WinForms ConfigDialog 新增 checkBox36 UI 控件（仅 WinForms 端，SkiaX 不使用 ConfigDialog）

- **TOOLTIP 默认设置回退**（源自 ee commit `07e58ac`）
  - 当 ToolTip 三项设置均为默认值（OwnerDraw=false, InitialDelay=0, tooltip_duration=0）时，使用简单 `SetToolTip()` 而非异步 `Task.Run`
  - 修复默认配置下 TOOLTIP 显示延迟问题（异步路径的 InitialDelay 等待 + SynchronizationContext 调度开销）
  - 仅影响 Desktop 端（WinForms + SkiaX Desktop），Xamarin 端 TOOLTIP 实现不同无需修改

- **版本号更新**（源自 ee commit `26a35dc`）
  - EEv55 → EEv56
  - Skia 版本号 v5 → v5.1

### Fixed

- **设置窗口在 TINPUT 定时器运行时无法交互** — `ShowConfigDialog()` 使用模态 `ShowDialog()`，但 TINPUTNF 的 `System.Timers.Timer` 仍在运行，超时后触发 `RunEmueraProgram` 占用主线程导致设置窗口无响应
  - `EmueraConsole.cs`：新增 `PauseTimer()` / `ResumeTimer()` 方法，暂停/恢复 genericTimer 并重置计时起点
  - `MainWindow.cs`：`ShowConfigDialog()` 中打开对话框前暂停定时器，关闭后恢复

- **系统页 checkBoxUseLazyLoading 缺少说明文本** — 控件在 Designer 中创建但未设置 Text、未绑定 ConfigCode
  - `ConfigDialog.cs`：添加 `checkBoxUseLazyLoading.Text`（`Lang.UI.ConfigDialog.System.UseLazyLoading`）
  - `ConfigDialog.cs`：SetConfig 中绑定 `ConfigCode.UseLazyLoading`，SaveConfig 中保存值

- **PluginAvailableWarn 英文描述拼写错误** — `"If available pllugins, Show warning"` → `"Plugin available warning"`
  - `ConfigData.cs`：修正拼写（`pllugins` → `plugins`），改为名词性短语与其他条目风格一致

- **Program.cs bgm.close 注释恢复** — ee 恢复了被注释掉的 `bgm.close()` / `sound[].close()`，本仓库已在 A 类修复中取消注释，无需额外操作

***

## [5.0.0] — T 前缀 NF 后缀指令 + 自由滚动 + HOVER_PAUSE

### Added

- **NF 后缀指令**：`TINPUTNF`, `TINPUTSNF`, `TONEINPUTNF`, `TONEINPUTSNF`
  - 与原版 TINPUT/TINPUTS/TONEINPUT/TONEINPUTS 参数和返回值相同，但不强制滚动到底部
  - NF = NoFocus，进入 `ConsoleState.WaitInputNoFocus` 状态，不调用 `ApplyTextBoxChanges()`
  - 仅 T 前缀指令提供 NF 变体（INPUT/INPUTS 全阻塞无轮询，NF 无意义）
  - 用 `TINPUTSNF` 替代 AWAIT+GETKEYTRIGGERED 轮询 hack

- **ConsoleState.WaitInputNoFocus = 22**：与 `WaitInput` 唯一区别是不调用 `ApplyTextBoxChanges()`
- **InputRequest.NoFocus**：布尔标志，T 前缀 NF 变体通过 `noFocus` 构造参数设置
- **EmueraConsole.WaitInputNoFocus()**：设置 `WaitInputNoFocus` 状态
- **EmueraConsole.IsWaitInputState**：替代 28 处 `state == ConsoleState.WaitInput` 判断

### Fixed

- **NF 滚动机制**：`nfUserScrolledBack` 标志记录用户上滚意图；`nfScrollOffsetFromBottom` 保存偏移量；`WaitInput` 入口强制回底；`RefreshStrings` 跳过 NF 上滚时的中间渲染
- **HOVER_PAUSE 悬停暂停**：鼠标悬停按钮时暂停动画，离开时恢复。4 个地图函数统一应用

***

## [4.3.1] — GETKEY 解耦：Latch 泄漏修复

### Fixed — AWAIT 循环首次迭代虚假鼠标点击

- **EmueraConsole.cs** — `Await()` 方法在 `DoEvents()` 前调用 `WinInput.ClearLatches()`
  - 根因：INPUTS/TINPUTS 模式下 `MouseDown` 事件设置 `_keyLatch[1]=1`，但内置输入系统不调用 `GETKEYTRIGGERED` 消费 latch
  - 从 INPUTS 切换到 AWAIT 循环时，残留 latch 被 `GETKEYTRIGGERED(1)` 消费，产生虚假点击
  - 现象：qol_MAP 地图首次进入时大概率立即退出（鼠标左键虚假触发）
  - 修复：每次 `Await()` 迭代开始前清除所有残留 latch，`DoEvents()` 产生的新 latch 由 `GETKEYTRIGGERED` 正常消费

- **WinInput.cs** — 新增 `ClearLatches()` 方法
  - 原子清除 `_keyLatch` 数组所有元素为 0
  - 防止跨输入模式（INPUTS → AWAIT）的 latch 泄漏

***

## [4.3.0] — PRINTC 像素制表重构：跨平台列对齐统一

### Fixed — PRINTFORMLC 中文环境列错位（回归修复）

- **EmueraConsole.Print.cs** — `CreateTypeCString` 中 `Config.Encode.GetByteCount(str)` 默认 UTF-8 编码下 CJK 字符=3字节，但 `Config.PrintCLength` 以半角字符为单位（CJK=2）
  - A24 修复将编码从 Shift-JIS 改为 Config.Encode 后引入回归
  - 修复：改用 `LangManager.GetStrlenLang(str)`，基于语言设置的 ANSI 编码（中文=GB2312/936，日文=Shift-JIS/932）计算字节长度

### Changed — PrintC/PrintButtonC 从字节制表重构为像素制表

- **EmueraConsole.Print.cs** — 删除 `CreateTypeCString` 方法，`PrintC`/`PrintButtonC` 改用像素制表路径
  - 旧方案：字节长度计算补空格字符 → while 循环删空格微调 → 受编码和字体 hinting 影响
  - 新方案：`StringMeasure.GetDisplayLength` 测量内容像素宽度 → `ConsoleSpacePart` 像素矩形填充差值
  - 与 `PrintHtmlC` 共享同一套像素制表理念，确保 WinForms + SkiaX 双端对齐一致
  - 删除 `printCWidthL`/`printCWidthL2`（仅旧 while 循环使用）
  - 根因分析：等宽字体 `N × charWidth ≠ stringWidth`（字体 hinting/kerning 截断），WinForms GDI 整串绘制误差被吸收，SkiaX 逐字符绘制误差累积为可见偏移

### 同步修改

- **SkiaX Desktop** (`Emuera/UI/Game/EmueraConsole.Print.cs`) — 同步重构
- **SkiaX Xamarin** (`Emuera.Xamarin/Platform/GameView/EmueraConsole.Print.cs`) — 同步重构

***

## [4.3.0] — PRINTC 像素制表重构：跨平台列对齐统一

### Fixed — PRINTFORMLC 中文环境列错位（回归修复）

- **EmueraConsole.Print.cs** — `CreateTypeCString` 中 `Config.Encode.GetByteCount(str)` 默认 UTF-8 编码下 CJK 字符=3字节，但 `Config.PrintCLength` 以半角字符为单位（CJK=2）
  - A24 修复将编码从 Shift-JIS 改为 Config.Encode 后引入回归
  - 修复：改用 `LangManager.GetStrlenLang(str)`，基于语言设置的 ANSI 编码（中文=GB2312/936，日文=Shift-JIS/932）计算字节长度

### Changed — PrintC/PrintButtonC 从字节制表重构为像素制表

- **EmueraConsole.Print.cs** — 删除 `CreateTypeCString` 方法，`PrintC`/`PrintButtonC` 改用像素制表路径
  - 旧方案：字节长度计算补空格字符 → while 循环删空格微调 → 受编码和字体 hinting 影响
  - 新方案：`StringMeasure.GetDisplayLength` 测量内容像素宽度 → `ConsoleSpacePart` 像素矩形填充差值
  - 与 `PrintHtmlC` 共享同一套像素制表理念，确保 WinForms + SkiaX 双端对齐一致
  - 删除 `printCWidthL`/`printCWidthL2`（仅旧 while 循环使用）
  - 根因分析：等宽字体 `N × charWidth ≠ stringWidth`（字体 hinting/kerning 截断），WinForms GDI 整串绘制误差被吸收，SkiaX 逐字符绘制误差累积为可见偏移

### 同步修改

- **SkiaX Desktop** (`Emuera/UI/Game/EmueraConsole.Print.cs`) — 同步重构
- **SkiaX Xamarin** (`Emuera.Xamarin/Platform/GameView/EmueraConsole.Print.cs`) — 同步重构

***

## [4.2.0] — FONTBOLD/FONTITALIC/FONTREGULAR 跨平台修复 + GETPLATFORM API

### Fixed — 移动端字体样式指令空操作导致 Bold 泄漏

- **Instraction.Child.cs** — `FONTBOLD`/`FONTITALIC`/`FONTREGULAR` 在非 Windows 平台是空操作（`if (!OperatingSystem.IsWindows()) return;`）
  - `FONTSTYLE` 命令无此限制，在移动端正常执行
  - 不对称导致：`FONTSTYLE 1` 设置 Bold 后 `FONTREGULAR` 无法恢复 → Bold 泄漏 → `GETSTYLE()` 返回 1 → `HTMLWRAP(STYLE_FLAG=-1)` 生成 `<b>` 标签 → 嵌套 → SkiaX HtmlManager 抛出"标签重复使用"错误
  - 修复：移除三个指令的 `!IsWindows()` 限制，`FontStyle` 枚举是纯内存操作，不依赖平台 API

### Added — ERB 平台检测 API

- **GETPLATFORM()** — 返回当前运行平台的整数编码
  - 0=Windows, 1=Android, 2=iOS, 3=macOS, 4=Linux, 5=Unknown
  - `CanRestructure = true`（纯函数，编译期可常量折叠）
  - ERB 脚本可通过 `IF GETPLATFORM() == 0` 等方式做平台条件分支

***

## [4.1.4] — GETKEY/GETKEYTRIGGERED 鼠标按键修复

### Fixed — 跨平台回流引入的回归 Bug

- **MainWindow.cs** — `GETKEYTRIGGERED(1/2/4)` 鼠标按键永远返回 0（A35）
  - V4.1.0 将 `WinInput.GetKeyState` 从 Win32 `user32.dll GetKeyState` 改为事件驱动的 `_keyState` 数组
  - `SetKeyPressed`/`SetKeyReleased` 只在 `richTextBox1_KeyDown`/`KeyUp` 中调用，鼠标按键不触发 `KeyDown` 事件
  - 导致 `GETKEYTRIGGERED(1)` (VK_LBUTTON)、`GETKEYTRIGGERED(2)` (VK_RBUTTON)、`GETKEYTRIGGERED(4)` (VK_MBUTTON) 永远返回 0
  - 修复：在 `mainPicBox_MouseDown`/`mainPicBox_MouseUp` 中添加 `WinInput.SetKeyPressed`/`SetKeyReleased` 映射鼠标按键到 VK_LBUTTON/VK_RBUTTON/VK_MBUTTON
  - 对应 commit `63afa0d`（跨平台音频架构重构）引入的回归

- **WinInput.cs / Creator.Method.cs** — 快速鼠标点击在 AWAIT 循环中丢失（A35 补充修复）
  - 根因：`MouseDown` + `MouseUp` 可能在同一个 `DoEvents()` 中被处理，`SetKeyReleased` 立即清除 `_keyState`，导致 `GETKEYTRIGGERED` 读到 0
  - V3 的 Win32 `GetKeyState` 直接读硬件状态，不受消息队列时序影响
  - 修复：添加 `_keyLatch` 锁存数组，`SetKeyPressed` 时置 1，`GETKEYTRIGGERED` 优先消费 latch（`ConsumeKeyLatch`），确保即使按键已释放也能检测到按下事件

***

## [4.1.3] — 移除废弃的 #FUNCTION ... 可变参数语法

### Removed — 废弃语法清理

- **UserDefinedFunctionDataArgType** — 移除 `__Variadic = 0x80` 枚举值
  - `#FUNCTION` 声明中的 `...` 可变参数语法与词法分析器的浮点数解析冲突，已废弃
  - 可变参数统一通过 `VARIADIC` 关键字在函数定义中声明（`@FUNC(VARIADIC ARG:0)`），`#FUNCTION` 声明不包含可变参数信息
- **UserDefinedFunction.cs** — 移除 `case '.'` 解析代码和 `state == 7` 处理
- **UserDefinedRefMethod.cs** — 移除 `__Variadic` 标志匹配（2 处）

***

## [4.1.2] — debug_log 持续写入修复

### Fixed — debug_log 不再持续写入

- **Process.cs / Process.ScriptProc.cs** — `DebugLogEnabled` 一旦被设为 `true`（异常或 THROW 触发）后永不重置，导致后续正常执行流程持续写日志
  - 根因：`catch` 块和 `THROW` 指令中 `DebugLogEnabled = true`，但所有 `return`/`break` 路径均未重置为 `false`
  - 影响：WinForms 中 THROW 后程序退出影响不大；Android app 返回游戏列表后进程继续，`DebugLogEnabled` 保持 `true`，下次进入游戏时 `IntoFunction`/`ReturnF`/`ClearFunctionList`/`GetValue` 等高频调用持续写日志，导致 debug_log.log 快速增长
  - 修复：在所有异常处理结束路径（`ClearFunctionList` 之后）和 THROW 的所有 `break` 路径前重置 `DebugLogEnabled = false`

***

## [4.1.1] — 音频 API Bug 修复（A33-A34 回流）

### Fixed — 内核 Bug 修复（A类，从 feature/xamarin 回流）

- **Creator.Method.cs** — ISPLAYINGSOUND 死代码 + 无限循环（A33）
  - `arguments[0] == null` 永假：在调用 `arguments[0].GetIntValue(exm)` 之后，`arguments[0]` 不可能为 null
  - for 循环条件 `channelId < GlobalStatic.Sound.Length` 应为 `i < GlobalStatic.Sound.Length`，使用 `channelId` 导致死循环
  - 修复：简化为直接检查指定通道是否正在播放

- **Creator.Method.cs** — SOUNDCONTROL 注释错误（A34）
  - 注释 `2=变速` 与实际 switch 逻辑不一致（action=2 是停止，action=3 才是变速）
  - 修正注释为 `0=暂停, 1=恢复, 2=停止, 3=变速`

***

## [4.1.0] — 跨平台音频架构重构 + 回流修复

### Changed — 音频架构重构（跨平台基础建设）

- **Sound.cs** — 新增音频播放基类
  - 提取虚方法：play/stop/pause/resume/close/isPlaying/setVolume/getVolume/setSpeed/getSpeed/GetTotalTime/GetCurrentTime/SetPreservePitch
  - 新增 `Sound.Factory` 静态工厂属性，宿主程序设置平台实现（WinForms→NAudioSound，Android→AndroidSound）
  - 内核代码通过 Factory 创建实例，消除平台直接依赖

- **Sound.NAudio.cs** — NAudioSound 重构为 Sound 子类
  - 原 `Sound` 类重命名为 `NAudioSound : Sound, ISampleProvider`
  - 所有方法加 `override`，移除重复 `Playing` 字段（使用基类）
  - `SoundMixer.PlaySound/StopSound` 参数类型改为 `NAudioSound`

- **GlobalStatic.cs** — 去除 NAudioSound 直接依赖
  - `Sound[]` 和 `Bgm` 初始化使用基类 `Sound`
  - `Reset()` 中 stop+close 后用 `Sound.Factory()` 重建实例

- **Program.cs** — WinForms 入口设置 Factory
  - `Sound.Factory = () => new NAudioSound()` + `GlobalStatic.Bgm = Sound.Factory()`

- **Creator.Method.cs / Instraction.Child.cs** — 所有 `new Sound()` 改为 `Sound.Factory()`
  - 确保运行时创建平台正确的实例

### Fixed — 内核 Bug 修复

- **EraStreamReader.cs** — try 块缺少 catch 导致 CS1524
  - 添加 `catch { return false; }`

## [4.0.0] — A类修复回流（第三轮）+ BINPUT 计数修复

### Fixed — 内核 Bug 修复（A类，从 EmueraFL 闭源引擎回溯）

> 以下修复源自 EmueraFL（Kom1 闭源引擎）commit 历史中的引擎 bug 描述，我们在源码中验证并修复。

- **GraphicsImage.cs** — GSetFont Dispose 缓存共享 SKFont（A27）
  - `FontFactory.GetFont` 返回缓存共享的 SKFont 对象，`GSetFont` 中 `font.Dispose()` 会使缓存中的引用失效
  - 多次 GSETFONT 且参数组合有重复时（如先 Regular 再 Bold 再切回 Regular），缓存返回已被 Dispose 的 SKFont → 崩溃
  - 修复：移除 `GSetFont` 中的 `font.Dispose()` 调用，FontFactory 统一管理 SKFont 生命周期
  - 对应 EmueraFL commit `1ee5d509`

- **GraphicsImage.cs** — 属性无 null 保护（A28）
  - `Fontname`/`Fontsize`/`Fnt`/`Pen`/`Brush` 属性直接访问字段，无 null 检查
  - GCREATE 后未 GSETFONT/GSETPEN/GSETBRUSH 就调用 GGETFONT/GGETPEN/GGETBRUSH 会 NRE
  - 修复：`Fontname` 返回 `font?.Typeface?.FamilyName ?? ""`，`Fontsize` 返回 `font != null ? (int)font.Size : 0`
  - 修复：新增 `PenColorArgb`/`PenWidth`/`BrushColorArgb` null-safe 属性，`GGETPEN`/`GGETPENWIDTH`/`GGETBRUSH` 改用这些属性，未设置时返回 0 而非 NRE
  - `Fnt`/`Pen`/`Brush` 保持返回原始字段（null 语义由内部调用方处理）
  - 对应 EmueraFL commit `4ce390b0`

### Fixed — BINPUT 按钮计数修复

- **Instraction.Child.cs** — BINPUT/BINPUTS/ONEBINPUT/ONEBINPUTS EscapedParts 计数早期退出
  - EscapedParts 计数循环中，找到第一个 div 按钮后 `goto loopend` 跳出，其余 div 按钮未计入 count
  - 修复：移除 `goto loopend`，遍历所有 div 的所有按钮，确保计数准确
  - 影响：非功能 Bug（count>0 即进 WaitInput 流程，匹配阶段无此问题），仅计数值更精确

### Changed — 版本号

- Skia 变体版本号从 3.x 升级到 **4.0.0**（A类修复累积 + BINPUT 修复）
- `InformationalVersion` 从 `Skia3` 更新为 `Skia4`

## [3.9.1] — A类修复回流（第二轮）+ 上游对齐 + EmueraFL bug 修复

### Fixed — 内核 Bug 修复（A类，从 feature/xamarin 回流）

> 以下修复源自 erafl-CHS 在 Xamarin 端运行时发现的跨平台 Bug，WinForms 版同样受益。

- **Creator.Method.cs** — ENUMFILES 路径修复（A23）
  - `EnumFilesMethod` 返回绝对路径，`LOADTEXT` 的 `GetValidPath` 拒绝绝对路径导致 `XML_GET` 解析失败
  - 使用 `Path.GetRelativePath(Program.ExeDir, files[i])` 将绝对路径转为相对路径
  - 对齐上游 ee+em a4d3665 + 1c495b5（上游先用了 `Path.GetRelativePath(dir, ...)` 后修正为 `Program.ExeDir`）

- **EmueraConsole.Print.cs** — PRINTFORMC 编码崩溃修复（A24）
  - 硬编码 `Shift-JIS` 无法编码中文等非日文字符，且无 `ReplacementFallback` 导致崩溃
  - 用 `Config.Encode.CodePage` + `EncoderFallback.ReplacementFallback` 替换，尊重 `DEFAULT ANSI ENCODING` 配置

- **HtmlManager.cs** — HTML 自闭合 div 标签支持（A25）
  - `<div ... />` 自闭合标签语法未被解析器支持，`/` 被当作 `OperatorCode.Div` 导致属性解析失败
  - 在 div 属性解析循环中检测 `</>` 并创建空 `ConsoleDivPart`
  - 根因：`XML_GET` 序列化空 div 元素按 XML 规范输出 `<div ... />`，但 HTML 解析器不支持（同一引擎的输出和输入不兼容）

- **Utils.cs** — `GetValidPath` 文档注释同步
  - 同步上游 ee+em a4d3665 的注释：标注 `GetValidPath` 返回绝对路径，调用方需自行 `GetRelativePath`

### Fixed — EmueraFL bug 修复（从闭源引擎 commit 提取）

> 以下修复源自 EmueraFL（Kom1 闭源引擎）commit 历史中的引擎 bug 描述，我们在源码中验证并修复。

- **Creator.Method.cs** — GSETFONT FontStyle 丢失（A26）
  - `GSETFONT` 从 `Pfc.Families` 找到字体时，`new SKFont(SKTypeface.FromFamilyName(...), fontsize)` 未传入 `fs` 参数
  - 导致 Bold/Italic/Underline/Strikeout 样式被忽略，始终为 Regular
  - 修复：改为 `FontFactory.GetFont(ff.Name, fs, fontsize)` 统一处理
  - 对应 EmueraFL commit `ae958303`

***

## [3.9.0] — A类修复回流 + 编译警告修复

### Fixed — 内核 Bug 修复（A类，从 feature/xamarin 回流）

> 以下修复源自 Xamarin 移植过程中发现的跨平台 Bug，WinForms 版同样受益。

- **Process.State.cs** — 4 项修复
  - `ShiftNextLine` null guard：`currentLine==null` 时直接 return，防止空栈 NRE（A1）
  - `ReturnF` 空栈 guard：`functionList.Count==0` 时安全退出（A2）
  - `CurrentLabel` null 检查：DebugMode 追踪日志中 `called.CurrentLabel` 可能为 null，添加 guard（A3）
  - BEFORE_ERROR/BEFORE_THROW 逻辑简化：合并两个 BEFORE_THROW 分支为单一条件判断，移除冗余 `GameProcProcess.DebugLog` 调用，逻辑等价（A4）

- **Process.LazyLoading.cs** — 2 项修复
  - `LoadLazyLoadingFolders`：同时替换 `\` 和 `/` 为 `Path.DirectorySeparatorChar`，不再依赖 `RuntimeInformation.IsOSPlatform`（A5）
  - `ErbPath`：从 `string.Create("ERB/" + a)` 简化为 `Program.ErbDir + a`，使用已有的跨平台路径属性（A6）

- **CharacterData.cs** — 数组 null 检查
  - `SaveToStreamBinary`：1D/2D 数组 `dataIntegerArray`/`dataStringArray`/`dataFloatArray` 在 `ToArray()`/`WriteWithKey` 前检查 null，防止空角色数据保存崩溃（A7）

- **VariableEvaluator.cs** — StainDefault null 兜底
  - `setDefaultStain`：`Config.StainDefault ?? new List<long>(new long[] { 0, 0, 2, 1, 8 })`，防止配置缺失时 NRE（A8）

- **Instraction.Child.cs** — 2 项修复
  - CALLPLUGIN null guard：`arg.CallFunc == null` 时设置 RESULT=0 并 return，防止插件未加载时 NRE（A11）
  - TIMES 溢出：`null` → `default(ScriptPosition)`，修复 `PrintWarning` 参数类型（A12）

- **Creator.Method.cs** — GetCurrentProcess try-catch
  - `GetMemoryUseMethod`/`ClearMemoryMethod`：`Process.GetCurrentProcess()` 包裹 try-catch，Android 上可能抛异常，返回 0L 降级（A16）

- **Config.cs** — 属性可见性改 internal
  - 17 个配置属性从 `private set` 改为 `internal set`，允许 Xamarin 项目覆盖配置值（A21）

- **OperatorMethod.cs** — 溢出警告参数修正
  - `null` → `default(ScriptPosition)`：4 处整数溢出警告的第2参数从 null 改为 default(ScriptPosition)，修复 PrintWarning 参数类型

### Fixed — 编译警告修复（1035→240，减少 77%）

- **CA2200** — `Instraction.Child.cs`：`throw e` → `throw`，保留原始异常堆栈
- **SYSLIB0014** — `Instraction.Child.cs`：`WebClient` → `HttpClient`，移除过时 API
- **CS4014** — `MainWindow.cs`：未 await 的 `ReloadPartialErb` 调用添加 `_ =` 丢弃标记
- **CA1069** — `VariableCode.cs`：枚举添加 `[Flags]` 属性和 `SuppressMessage`，`__COUNT_*__` 值重复是设计意图
- **CA1806** — `WinmmTimer.cs`：检查 `timeBeginPeriod`/`timeEndPeriod` 返回值
- **CA1825** — `VariableData.cs`/`CharacterData.cs`/`Creator.Method.cs`：零长度数组 `new T[0]` → `Array.Empty<T>()`
- **CA1834** — 7 个文件：单字符 `Append("x")` → `Append('x')`，使用 char 重载
- **CA1854** — 9 个文件：`ContainsKey` + 索引器 → `TryGetValue`，消除双重字典查找

***

## [3.8.3] — 上游对齐（BREAKBUTTON + 调试模式不关闭窗口）

### Added

- **BREAKBUTTON 指令**（源自 ee+em/master）
  - `BREAKBUTTON` — 强制中断当前按钮等待，立即刷新显示
  - 调用 `EmueraConsole.forceUpdateGeneration()` 实现
  - 可选参数，用于需要强制刷新 UI 的场景
- **调试模式下错误时不关闭窗口**（源自 ee+em/master，CRER 氏补丁）
  - `ConsoleState.Error` 状态下若 `Program.DebugMode` 为 true 则直接返回，不执行关闭流程

### Changed

- **.gitignore 合并优化**：融合 ee+em/master 的组织方式与 feature/xamarin 的通配符覆盖（`**/bin/*`、`**/obj/*`、`**/artifacts/**`、`*.user`、`*.suo`）

## [3.8.2-xamarin.1] — 2026-05-15

### Added

- **Emuera.Xamarin 项目**（feature/xamarin 分支）
  - 在 LazyLoading 仓库内新建 Xamarin.Forms + SkiaSharp 跨平台前端
  - csproj 直接编译 LazyLoading 内核（Runtime/ + UI/），不复制文件
  - 从 XEmuera-1 复制 Skia 平台层到 Platform/ 目录
  - 排除不兼容内核文件（Sound/PluginSystem/JSON/Shape/Lang/Utils/Clipboard）
  - 添加 IsExternalInit NuGet 包（解决 `init` 属性在 netstandard2.1 的问题）
  - 批量替换平台层命名空间（26 个文件）
  - 首次编译：129 个错误（主要分类：UI 层引用 ~80、netstandard2.1 不兼容 ~30、NuGet 缺失 ~10）

***

## [3.8.2](https://gitgud.io/minus010001/emuera_lazyloading_selfmodified_version) — 2026-05-15

### Fixed

- **OperatorCode.opDictionary 丢失 `/`, `%`, `==` 三个条目**（上游缺陷修复）
  - 上游 emuera.em 的 `opDictionary` 集合初始化器中遗漏了 `Div`(`/`)、`Mod`(`%`)、`Equal`(`==`) 三个运算符的反向映射
  - 导致 `ToOperatorString()` 对这三个运算符返回空字符串，影响错误消息的可读性（如"无法将运算符 应用于整数类型"）
  - 运算符本身的计算不受影响（`/`、`%`、`==` 在表达式中正常工作），仅影响错误提示
  - XEmuera-1 使用 `.Add()` 静态构造函数初始化，条目完整，不受此缺陷影响

***

## [3.8.1](https://gitgud.io/minus010001/emuera_lazyloading_selfmodified_version) — 2026-05-11

### Fixed

- **SELECTCASE 跳转表现在支持可折叠常量的表达式函数**
  - `TryBuild()` 中当 `LeftTerm.IsConst` 为 `false` 时尝试调用 `Restructure(null)` 折叠表达式
  - 纯函数（如 `ABS(3)`、`SIN(0)`、`TOINT("123")` 等 `CanRestructure=true` 的函数）可被折叠为 `SingleTerm`，参与跳转表 O(1) 查找
  - 有副作用或依赖运行时状态的函数（如 `RAND()`、`RESULT`、`GETTIME` 等 `CanRestructure=false`）不受影响，自动回退到线性扫描
  - try-catch 包裹 `Restructure` 调用，折叠失败时安全回退到线性扫描
- **SELECTCASE 跳转表重复值处理策略（FIFO）**
  - 重复的 CASE 值保留第一个出现的分枝，后续重复项触发 warning 后跳过
  - 此行为与线性扫描的 fallthrough 语义一致，且不影响跳转表的确定性
- **TOSTRF 第二参数现在可省略**
  - 修复了 `argumentTypeArray` 导致强制校验参数个数、第二参数无法省略的问题
  - 改用 `argumentTypeArrayEx` + `OmitStart = 1`，允许 `TOSTRF(value)` 单参数调用
  - 扩展 `ArgType` 枚举增加 `Float` 类型，完善浮点参数类型支持

## [3.8.0](https://gitgud.io/minus010001/emuera_lazyloading_selfmodified_version) — 2026-05-10

### Added

- **BEFORE_THROW / BEFORE_ERROR 事件函数**
  - `BEFORE_THROW`：在 `THROW` 指令抛出异常前调用，允许脚本拦截和处理异常
  - `BEFORE_ERROR`：在任何错误第一次发生时调用，提供错误处理的钩子
  - 若事件函数存在，异常会被延迟抛出，允许脚本进行清理或恢复操作
- **TEXT_BGC_ON / TEXT_BGC_OFF 文本背景色控制**（SK 专属）
  - `TEXT_BGC_ON R, G, B, Alpha%`：为后续所有行设置整行背景色（Alpha 为 0～100 不透明度）
  - `TEXT_BGC_OFF`：清除背景色，恢复透明
  - 背景以行全宽 × 行高的矩形绘制，仅当行内存在实际文本时才绘制
- **STRICT_FONT_FALLBACK 严格字体回退模式**（SK 专属）
  - `STRICT_FONT_FALLBACK value`：设为 1 启用严格模式，不存在字形的字符显示为 □（tofu）而非回退字体
  - 设为 0 恢复默认回退行为
  
## [3.7.0](https://gitgud.io/minus010001/emuera_lazyloading_selfmodified_version) — 2026-05-10

### Added

- **SQL_CONNECTION_OPEN 便利函数**（源自 DotNet）
  - `SQL_CONNECTION_OPEN(string name)` — 自动在 `sav/sql/` 目录下创建/打开 SQLite 数据库连接
  - 若同名连接已存在则自动关闭重建

### Fixed

- **SQL_CONNECTION_OPEN 数据库崩溃损坏风险**：`PRAGMA journal_mode=OFF; synchronous=OFF` 改为 `WAL; NORMAL`，兼顾写入性能与崩溃安全
- **SQL_CONNECTION_OPEN 路径穿越漏洞**：`name` 参数增加非法字符和 `..` 校验，阻止 ERB 脚本穿透 `sav/sql/` 目录
- **SQL_CONNECTION_OPEN 连接句柄泄露**：`conn.Open()` 后 PRAGMA 执行失败时未 Dispose，已加 try-catch 防护
- **SQL_CONNECTION_OPEN 路径拼接不规范**：`$"{dir}{name}.db"` 改为 `Path.Combine(dir, $"{name}.db")`

### Changed

- **SQL_CONNECTION_OPEN PRAGMA 策略调整**：DotNet 上游使用 `journal_mode=OFF; synchronous=OFF` 追求极致写入速度但存在崩溃损坏风险；Skia 变体改为 `WAL; NORMAL`，写入性能接近 OFF 模式且崩溃时数据库不会损坏
- **SQL 泛型重构**
  - `SqlManager` 内部 `ExecuteScalarLong`/`ExecuteScalarString`/`ExecuteScalarFloat` 合并为泛型 `ExecuteScalar<T>`
  - 保留所有 ERB 层 API 不变（`SQL_EXECUTE_SCALAR_LONG`/`STRING`/`FLOAT`）
  
## [3.6.0](https://gitgud.io/minus010001/emuera_lazyloading_selfmodified_version) — 2026-05-10

### Added

- **G_POLYGON 多边形指令集**（源自 DotNet）
  - `G_POLYGON_DRAW(int ID)` — 用当前画笔描边多边形
  - `G_POLYGON_FILL(int ID)` — 用当前画刷填充多边形
  - `G_POLYGON_POINT_ADD(int ID, int x, int y)` — 添加多边形顶点
  - `G_POLYGON_POINT_CLEAR(int ID)` — 清除所有顶点
  - `GraphicsImage` 新增 `_points` 字段及 `GDrawPolygon`/`GFillPolygon`/`GDrawPolygonAddPoint`/`GDrawPolygonClearPoint` 方法
  - 仅 Skia 模式可用（GDI 模式抛出 CodeEE）
- **文字装饰线渲染**（源自 DotNet）
  - `StringStyle` 新增 `HasUnderline` / `HasStrikeout` 属性，在 `FontStyle` setter 中自动同步
  - `ConsoleStyledString.DrawTo` 在 Skia 路径中绘制下划线和删除线
  - 使用 `SKFont.Metrics.UnderlinePosition` / `StrikeoutPosition` 定位装饰线
  - 支持 `FONTSTYLE` 指令的 Underline(8) / Strikeout(4) 位掩码


***

## [3.5.0](https://gitgud.io/minus010001/emuera_lazyloading_selfmodified_version) — 2026-05-09

### Added

- **Stopwatch 高精度计时重构**（源自 DotNet）
  - `SpriteAnime` / `SpriteAnimated` 动画帧计时从 `DateTime.Now` 迁移到 `Stopwatch.GetTimestamp()` + `Stopwatch.GetElapsedTime()`
  - 消除 `DateTime.Now` 的系统时钟精度限制（~15ms），提升动画帧率稳定性
  - 暂停/恢复逻辑同步迁移：`PauseAnimation()` / `ResumeAnimation()` 使用 timestamp 差值计算
- **图像翻转逻辑**（源自 DotNet）
  - `ASpriteSingle.GraphicsDraw` 支持 `destRect.Width`/`Height` 为负值时自动翻转
  - 使用 `canvas.Scale(sx, sy)` 实现水平/垂直翻转，同时支持带 `SKColorFilter` 的翻转渲染
- **HTML DisplayMode 属性移植**（源自 DotNet）
  - `<img>` 标签新增 `display` 属性：`relative`（默认）/ `absolute-lefttop` / `absolute-leftbottom`
  - `<img>` 标签新增 `xpos` 属性：绝对定位时的 X 坐标
  - `<div>` 标签 `display` 属性扩展支持 `absolute-lefttop` / `absolute-leftbottom`
  - `ConsoleImagePart.DrawTo` 实现三种定位模式：相对定位、左上角绝对定位、左下角绝对定位
  - `ConsoleDivPart.DrawTo` 同步支持三种 DisplayMode

***

## [3.4.0](https://gitgud.io/minus010001/emuera_lazyloading_selfmodified_version) — 2026-05-09

### Added

- **GETCSVNOBY* 名字反查**（源自 DotNet）
  - `GETCSVNOBYNAME(str)` / `GETCSVNOBYNICKNAME(str)` / `GETCSVNOBYCALLNAME(str)` / `GETCSVNOBYMASTERNAME(str)`
  - 通过 NAME/NICKNAME/CALLNAME/MASTERNAME 反查角色模板编号，O(1) 查找
  - 未找到返回 -1
- **MATCHALL / MATCHALLEX 全量搜索**（源自 DotNet，重新设计）
  - `MATCHALL(var, value[, beg, end[, outArr]])` — 变量引用形式
  - `MATCHALLEX("varName", value[, beg, end[, outArr]])` — 字符串变量名形式
  - 返回匹配计数，第五参数输出索引数组（从 0 开始）
  - 比 DotNet 指令形式更灵活：不污染 RESULT，可在表达式中使用
- **Preload 字节级优化**（源自 DotNet）
  - 启动时将 ERB/CSV 文件一次性预加载到内存
  - `EraStreamReader.OpenOnCache()` 从内存读取而非磁盘 IO
  - `ConstantData.cs` 中 CSV 加载改用 `OpenOnCache()` 充分利用缓存
  - .NET 8 内存流方案，解决编码兼容性与 BOM 剥离问题

### Fixed

- **METHOD_Instruction Float 分支缺失**：Float 表达式函数（TOFLOAT 等 8 个）用作命令时抛出异常，已添加 `EraType.Float` 分支写入 `RESULTF`
- **TOINT 非法输入崩溃**：增加 try-catch 拦截非法字符串转换，无法解析时返回 0（源自 DotNet）
- **MainWindow console null 崩溃**：ShowConfigDialog 和剪贴板处理器加 null 检查（源自 DotNet）
- **PrintStringBuffer 空数组越界**：Flush() 中 ButtonsToDisplayLines 返回空数组时跳过 ret[^1] 访问（源自 DotNet）
- **SKPaint 资源泄漏**：Creator.Method.cs 补全 `using var`，防止非托管内存泄漏（源自 DotNet）

### Changed

- **教程交叉引用**：`line-types.zh.md`、`index.zh.md`、`CALLF.zh.md` 添加命令/表达式语法交叉引用

***

## [3.3.0](https://gitgud.io/minus010001/emuera_lazyloading_selfmodified_version) — 2026-05-07

### Added

- **SELECTCASE 编译期跳转表优化**（Phase 4.2+4.6）
  - `SelectCaseJumpTable` 核心类：编译期构建 `Dictionary<long/string/double, InstructionLine>` 跳转表
  - `TryBuild()` 编译期构建：遍历 IfCaseList，检查每个 CaseExpression 是否 `CaseType == Normal && LeftTerm.IsConst`
  - 不可优化时（含 TO/IS/非常量/重复键）自动返回 null，fallback 到线性扫描
  - `Lookup()` 运行时 O(1) 查找，未命中时返回 CASEELSE 或 ENDSELECT 行
  - `AExpression.IsConst` 属性：`SingleTerm` override 为 true，复合表达式和变量引用默认 false
  - `InstructionLine.SelectCaseJumpTable` 字段存储编译期跳转表
  - `SELECTCASE_Instruction` 快速路径：有跳转表时直接 Lookup + JumpTo，跳过线性扫描

***

## [3.2.0](https://gitgud.io/minus010001/emuera_lazyloading_selfmodified_version) — 2026-05-07

### Added

- **SETIMAGELAYER 图层渲染指令集**（Phase 5.11）
  - `SETIMAGELAYER spriteName, depth, x, y, width, height, opacity, CM_ARRAY, followScroll` — 在独立图层上渲染 Sprite
  - `EXISTSIMAGELAYER(depth)` — 检测指定深度图层是否存在
  - `CLEARIMAGELAYER depth` — 清除指定深度图层
  - `CLEARIMAGELAYER_ALL` — 清除所有图层
  - `ImageLayerManager` 核心类：按 depth 排序的 Dictionary 存储，每帧直接绘制
  - `ColorMatrixHelper` 工具类：DRY 重构颜色矩阵解析（5×5 二维/三维整数数组 → SkiaSharp float\[]）
  - 视口裁剪：离窗图层跳过绘制，节省 GPU 资源
  - 动图离窗暂停：`IsOffScreen` 标记触发 `PauseAnimation()`/`ResumeAnimation()`
  - 跟随滚动：`FollowScroll` + `InitialScrollY` 存储滚动增量
  - 左下原点坐标系：与 CBGSETSPRITE 一致
- **CBGSETSPRITE 升级**：从 4 参数升级为 8 参数 `(imgName, x, y, zdepth, width, height, opacity, CM)`，第 2 个参数起全部可省略

### Fixed

- **ColorMatrix 解析代码重复**：提取到 `ColorMatrixHelper`，ConsoleImagePart 和 Instraction.Child 共用
- **ArgumentBuilder 手动 LexicalAnalyzer 解析导致参数丢失**：改用 popTerms 标准方法
- **SETBGIMAGE 参数解析方式修复**：`FORM_STR_ANY` → `SP_SETBGIMAGE`，修复变量参数被当作字面量字符串的 bug（depth/opacity 自上游已有）
- **ClientBackGroundImage 缺少 width/height**：添加字段，OnPaint 中使用缩放尺寸
- **FollowScroll 使用绝对 scrollY 导致图片在视口外**：存储 initialScrollY，改用滚动增量
- **SETIMAGELAYER 坐标系与 CBGSETSPRITE 不一致**：改为左下原点坐标系

***

## [3.1.0](https://gitgud.io/minus010001/emuera_lazyloading_selfmodified_version) — 2026-05-06

### Added

- **可变参数函数（Variadic Arguments）**（Phase 2.1）
  - `VARIADIC ARG/ARGS/ARGF` 关键字声明可变参数
  - `ARGLEN()` 内置函数返回可变参数数量
  - `VariadicArgTerm` 表达式类封装剩余实参
  - 支持 Int/String/Float 三种可变参数类型，Int→Float 隐式转换
  - 私有变量做固定参数时，ARG 数组仅包含可变参数
- **元素级引用（Element-Level Reference）**（Phase 2.2）
  - `ElementRefInfo` 结构体传输"目标变量 + 固定索引"引用
  - `ReferenceToken` 子类（Scalar/1D/2D/3D）使用 `ElementRefInfo` 代理读写
  - `ScopeIn`/`ScopeOut` 保存/恢复引用状态（`_scopeState` 列表）
  - `SetTransporter` 三路分发：数组引用 / 元素级引用 / NullRef
- **`#REF`** **/** **`#REFS`** **元素引用关键字**（Phase 2.3）
  - `#REF X` 声明整数元素引用（Dimension=0）
  - `#REFS S` 声明字符串元素引用（Dimension=0）
  - 与 `#DIM REF` 数组引用彻底分离，消除语义歧义
  - `ConvertArg` 三维分支匹配（Dimension=0 / Dimension>0 / OUT）
- **OUT 参数（Optional Output Parameters）**（Phase 2.4）
  - `#DIM OUT X` / `#DIMS OUT X` 声明可省略输出参数
  - `NullRefTerm` 黑洞变量：省略时所有读写被静默忽略
  - OUT 与 `#REF` 同构（元素引用 Dimension=0），与 `#DIM REF` 不同构
  - `refDestDimension` 字段区分元素引用和数组引用传递方式
  - 支持 OUT + 可变参数组合、嵌套调用、CALLFORM/TRYCALL

### Fixed

- **ARGLEN() 编译期常量折叠为 0**：`CanRestructure = false` 阻止优化器误折叠
- **ElementRefInfo 上下文依赖导致引用写回失败**：创建时捕获实际数组快照
- **ReferenceToken ScopeIn/ScopeOut 未保存引用状态**：引入 `_scopeState` 列表保存/恢复
- **SetTransporter 数组 REF 参数传递错误**：`refDestDimension` 区分元素引用 vs 数组引用
- **CreatePrivateVariable 缺少 IsOut=true 设置**：OUT 参数省略时无法识别
- **MatchType 缺少 allowElementRef 参数**：OUT 参数引用匹配失败
- **MatchType 阻止 OUT 参数链式传递**：增加 `!rother.IsOut` 豁免
- **OUT 参数被错误创建为一维数组引用**：强制 `Dimension=0` 和 `Lengths=[1]`
- **IntoFunction Float 可变参数类型截断**：`(long)arg.GetFloatValue()` → `arg.GetFloatValue()`

***

## [3.0.0](https://gitgud.io/minus010001/emuera_lazyloading_selfmodified_version) — 2026-05-05

### Added

- **Float 类型系统全量重构完成**（B.3 全部子任务）
  - `#DIMF` 浮点变量声明、`LOCALF`/`ARGF`/`RESULTF` 内置浮点变量
  - `#FUNCTIONF` 浮点返回函数、Float 四则/比较/一元/三元运算
  - 同名重载数学函数（SIN/COS/SQRT 等）、数组函数 Float 分支
  - `TOFLOAT`/`TOSTRF` 类型转换、存档双精度支持、DT/SQL Float 操作
- **三角函数与端数处理函数**：SIN/COS/TAN/ASIN/ACOS/ATAN/FLOOR/CEIL/ROUND（Int+Float 同名重载）
- **角色浮点变量支持**：CharacterData 中 dataFloat/dataFloatArray/dataFloatArray2D
- **RenderingBackend 渲染后端配置**：Auto/OpenGL/CPU 三模式，运行时无缝降级
- **TEXT\_BGC\_ON / TEXT\_BGC\_OFF**：文本背景色开关指令
- **上游同步**：CurrentCulture→InvariantCulture、TIMES 文化依赖修复、VARS2D 修复、FORCE\_QUIT 修复、ServerGC 启用

### Fixed

- **OpenGL 上下文丢失崩溃**：双显卡/虚拟机环境自动降级到 CPU 渲染
- **ColorMatrix GDI+→SkiaSharp 迁移修复**：列优先→行优先布局、平移分量 \*255f、GDrawG GDI+ 残留清理
- **合并冲突黑屏**：修复 mr-6 分支合并后 PaintSurface 事件隐藏
- **Float 存档数据丢失**：LoadVariableBinary Float 段被错误截断为 long

### Changed

- `typeof(long)`/`typeof(string)` 硬编码 → `EraType` 枚举 + `VariableDescriptor` 查询（746 处替换）

***

## [2.0.0](https://gitgud.io/minus010001/emuera_lazyloading_selfmodified_version) — 2026-05-04

### Added

- **ExecutionContext 栈式函数上下文**：修复 LOCAL/ARG 同函数递归覆写污染
- **SparseArray\<T> 稀疏数组存储**：大幅节省大下标数组内存
- **SafeArithmetic 安全运算**：溢出保护，不再静默溢出
- **EraType 枚举 + VariableDescriptor**：类型系统基础设施
- **#DIMF 语法解析**：浮点变量声明 + 浮点字面量
- **存档 Float 段**：EraSaveDataType Float/FloatArray/FloatArray2D/FloatArray3D

### Fixed

- **ConvertArg() 多余参数静默丢弃**：移除 TooManyFuncArgs 报错，增加 TRY 安全网
- **TIMES\_Instruction 溢出保护**
- **INITRAND/DUMPRAND 与新随机数算法解耦**

***

## [1.3.0](https://gitgud.io/minus010001/emuera_lazyloading_selfmodified_version) — 2026-05-02

### Fixed

- **工具栏返回标题后精灵索引悬置**：导致立绘透明

***

## [1.2.0](https://gitgud.io/minus010001/emuera_lazyloading_selfmodified_version) — 2026-04-28

### Fixed

- **SpriteG 快照模式**：导致合成精灵渲染为空白

***

## [1.1.0](https://gitgud.io/minus010001/emuera_lazyloading_selfmodified_version) — 2026-04-26 \~ 2026-04-27

### Added

- **HTML\_PRINTC / HTML\_PRINTLC**：基于像素的 HTML 制表命令，非等宽字体精确对齐
- **HTML\_PRINT font 标签 size 属性**
- **SQL 参数化查询**：`SQL_ESCAPE`、`SQL_P_EXECUTE_*` 系列，`@0,@1...` 占位符防注入
- **MAP 全套方法 API**：MAP\_VALUES/MAP\_MERGE/MAP\_REMOVEIF/MAP\_FINDKEY/MAP\_TOSTRING/MAP\_FROMSTRING

### Fixed

- **彩色文字渲染偏细**：修复 SETCOLOR 后文字偏细偏虚
- **SKIA 下 GDRAW 管线实现方式**
- **HTML 新增标签关闭行为**

***

## [1.0.0](https://gitgud.io/minus010001/emuera_lazyloading_selfmodified_version) — 2026-04-22 \~ 2026-04-25

### Added

- **SkiaSharp 渲染引擎**：全面替代 GDI+，支持 GPU 加速
- **OpenGL 硬件加速**：自动检测 + 运行时降级
- **SRGB 颜色空间修复**：修复 SkiaSharp 默认颜色空间导致画面偏暗
- **GDI 字体回退**：MS Gothic 等光栅字体保留 GDI 渲染路径
- **智能字体回退**：衬线/无衬线分类回退，CJK 全覆盖
- **渲染控制 API**：SET\_TEXT\_DRAWING\_MODE / GET\_TEXT\_DRAWING\_MODE / SET\_SKIA\_QUALITY / GET\_SKIA\_QUALITY
- **HTML\_PRINT font 渲染属性扩展**：render/edging/hinting
- **全屏功能 (F11)**：覆盖开始菜单，鼠标移到顶部自动显示工具栏
- **SPRITECREATEFROMFILE**：从图像文件直接创建 Sprite，无需 GCREATE 中转
- **BitArray 功能**
- **DIV 渲染性能优化**：命中测试 O(1) 定位 + Y轴预剔除
- **ToolTip 防遮挡**：屏幕边缘自动翻转
- **图像资源管理重构**：SharedBitmapCache 全局位图池 + ConstImage 轻量外壳
- **STRICT\_FONT\_FALLBACK**：严格字体回退模式
- **SETANIMETIMER**：动画帧间隔控制
- **BITMAP\_CACHE\_ENABLE**：位图缓存开关

### Fixed

- **FontFactory 字体缓存内存泄漏**：游戏重置时正确释放
- **RasterFont 检查方法**
- **MS Gothic 字体与过去产生差异**
- **SkiaSharp 文本渲染排版不对称**
- **SpriteAnime 动画渲染卡顿**：同一文件重复解码导致内存爆炸
- **Word Wrap 导致富文本字体回退丢失**
- **DIV 内图片按钮点击失效**（高度超过一行）
- **MoveMouse 剪枝错误**
- **符号渲染空格与重叠**
- **字体回收 Bug**
- **Skia 渲染质量用户设置不生效**
- **PrintPlainwithSingleLine 在 Plugin API 不执行渲染**
- **数组扩大到一千万位**

***

## [0.3.0](https://gitgud.io/minus010001/emuera_lazyloading_selfmodified_version) — 2026-04-13 \~ 2026-04-18

### Added

- **EVAL / EVALS**：运行时动态求值表达式
- **CALLSTR / JUMPSTR / TRYCALLSTR / TRYJUMPSTR / TRYCCALLSTR / TRYCJUMPSTR**：动态函数调度
- **SQL 数据库操作全套**：SQL\_CONNECT/DISCONNECT/EXECUTE\_NONQUERY/EXECUTE\_READER/READER\_*/EXECUTE\_SCALAR\_*/IMPORT\_MAP\_XML/IMPORT\_DT\_XML/EXPORT\_MAP\_XML/EXPORT\_DT\_XML/IMPORT\_XML\_CUSTOM
- **资源管理系统 (ResourceManager)**：RM\_RESOURCECHECK\_LOAD / RM\_RELEASE\_ALL / RM\_RESOURCE\_EXIST，LRU 缓存淘汰
- **SqlManager 流式 XML 解析**：XmlReader/XmlWriter，支持 GB 级数据导入导出

### Fixed

- **XML\_ADDNODE 多节点匹配**：修复只插入最后一次的 Bug
- **字符串比较逻辑**：GreaterEqualStrStr / LessEqualStrStr 错误逻辑
- **SqlManager.CloseAll() 未集成到全局重置**：GlobalStatic.Reset() 中添加调用
- **遗漏的本地化条目**

***

## [0.2.0](https://gitgud.io/minus010001/emuera_lazyloading_selfmodified_version) — 2026-03-09

### Fixed

- **BGMControl 功能异常**：参数重载失效
- **变调标志存在即变调**：逻辑修正

***

## [0.1.0](https://gitgud.io/minus010001/emuera_lazyloading_selfmodified_version) — 2026-02-16 \~ 2026-02-19

### Added

- **SoundTouch 音频变速库**：支持变速不变调/变速变调
- **音频控制指令**：GETSOUNDORBGMINFO / ISPLAYINGSOUND / SOUNDCONTROL / ISPLAYINGBGM / BGMCONTROL
- **SOUNDCONTROL 停止通道音频**
- **EXISTVAR 扩展**：支持存储单元存在性检查（第二参数）

### Fixed

- **EXISTFUNCTION**：支持 Lazyloading 包含且未运行的函数检测
- **.als 文件**：序号 10 后的字符串指针读取修复
- **SPRITECREATE**：支持 8/10 参数写法，与 CSV 能力对齐
- **GCREATEFROMFILE isRelative 参数**：第三参数非 0 时从当前工作目录解析相对路径

***

