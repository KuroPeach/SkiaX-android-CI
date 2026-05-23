# Emuera.Xamarin 音频系统移植 TODO

> 版本对应：Phase 1 = v0.53.0，Phase 1.5 = v0.53.1，Phase 2-4 = v0.54.0
> 知识库参考：[audio-system.md](file:///d:/emuera/shared-trae/knowledge/xemuera/audio-system.md)
> 回流审计：[kernel-audit.md](file:///d:/emuera/shared-trae/knowledge/lazyloading/kernel-audit.md)

## 已完成

### Phase 1：AndroidSound 基础实现 ✅ v0.53.0

- [x] Sound 虚方法继承方案 + Factory 工厂模式
- [x] NAudioSound : Sound（桌面端）
- [x] AndroidSound : Sound（Android 端，MediaPlayer）
- [x] csproj 条件编译（Sound.NAudio.cs 仅 windows，AndroidSound.cs 仅 android）
- [x] DesktopHost / AndroidHost 初始化 Factory
- [x] 7 个基础指令：PLAYSOUND/PLAYBGM/STOPSOUND/STOPBGM/SETSOUNDVOLUME/SETBGMVOLUME/EXISTSOUND

### Phase 1.5：审查修复 + 内核解耦 ✅ v0.53.1

- [x] GlobalStatic.cs 去除 NAudioSound 直接依赖
- [x] WinForms Program.cs 设置 Sound.Factory
- [x] AndroidSound OnCompletion repeat>1 修复
- [x] AndroidSound play() 改 PrepareAsync
- [x] GraphicsImage 补 PenColorArgb/PenWidth/BrushColorArgb
- [x] GlobalStatic.Reset() 音量重置（stop+close+Factory 重建）
- [x] AndroidSound.play 签名修正（long→int 匹配基类）
- [x] AndroidSound.cs 重复 play 方法删除

## 待回流 develop-skiasharp

- [ ] **RF1** PlatformInterop.cs（委托模式）— 内核 29 处引用
- [ ] **RF2** Program.MusicDir / FileExists — 6 处音频路径引用
- [ ] **RF3** SKColor→Color 转换 — Creator.Method.cs ToArgb()

> 回流在 develop-skiasharp 分支完成，Xamarin 合并即可。

## 待实施

### Phase 2：PLAYSOUND repeat 参数升级

- [ ] **2A** 升级 PLAYSOUND ArgumentBuilder：STR_EXPRESSION → SP_HTML_PRINT
- [ ] **2B** AndroidSound repeat 完善（repeat=N Completion 计数）

### Phase 3：扩展音频 API

| 函数 | 返回 | 参数 | 说明 |
|------|------|------|------|
| GETSOUNDORBGMINFO | Int | channelId, [infoType] | 总长度/当前时间/状态/音量/速度 |
| ISPLAYINGSOUND | Int | channelId | 检查通道播放状态 |
| SOUNDCONTROL | Int | channelId, action, [speed, preservePitch] | 暂停/恢复/停止/变速 |
| ISPLAYINGBGM | Int | 无 | 检查 BGM 播放状态 |
| BGMCONTROL | Int | action, [speed, preservePitch] | BGM 暂停/恢复/停止/变速 |

- [ ] **3A** 移植 GETSOUNDORBGMINFO
- [ ] **3B** 移植 ISPLAYINGSOUND / ISPLAYINGBGM
- [ ] **3C** 移植 SOUNDCONTROL / BGMCONTROL
- [ ] **3D** 变速支持调研（SoundTouch/Oboe / PlaybackParams 可靠性）

### Phase 4：APK 构建验证

- [ ] **4A** 完整 APK 构建 + 真机测试
- [ ] **4B** 音频功能端到端测试

## 风险

| # | 风险 | 缓解 |
|---|------|------|
| 1 | Android MediaPlayer 变速不可靠 | PlaybackParams + 设备测试 |
| 2 | PLAYSOUND SP_HTML_PRINT 参数构建器复杂 | 参考 LazyLoading 已有实现 |
