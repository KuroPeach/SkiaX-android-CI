p = '/sdcard/emuera/项目档案.md'
t = open(p, encoding='utf-8').read()

old = """**✅ 最终方案：返回游戏列表 = 重启整个 App**
- `RestartApp()`：取自身 launchIntent（`NewTask | ClearTask`）拉起新实例 → `Handler.PostDelayed(500ms)` 后 `Finish()` + `JavaSystem.Exit(0)`
- 不用 `Process.KillProcess`（前台被杀会被系统当成崩溃弹「应用已停止」）
- 引擎执行中仍弹确认框，避免丢存档"""

new = """**✅ 最终方案：返回游戏列表 = 重启整个 App**

⚠️ **第一版实现错了**：先用 StartActivity 拉起新实例、再 Exit(0) —— 新 Activity 在**同一进程**里，被一并杀掉，症状是「游戏列表闪一下、整个 App 消失退回桌面」。

正确做法（已修）：
- 用 AlarmManager 排定延迟拉起（AlarmManagerService 是独立进程，本进程死后定时器依然有效）
- launchIntent 加 flags（NEW_TASK | CLEAR_TASK）+ PendingIntent.GetActivity
- alarm.Set(AlarmType.ElapsedRealtime, now + 600ms, pending) → FinishAffinity() + JavaSystem.Exit(0)
- **排定失败就绝不杀进程**，退回 StopGame + Finish 的软清理兜底
- 不用 Process.KillProcess（前台被杀会被系统当成崩溃弹「应用已停止」）
- 引擎执行中仍弹确认框，避免丢存档

**教训**：在 Android 上「重启自己」必须让**进程先死**、由外部（系统服务）拉起；先拉起再自杀一定会把新实例一起杀掉。"""

assert old in t, 'anchor missing'
open(p, 'w', encoding='utf-8').write(t.replace(old, new))
print('档案已更新')
