---
description: 修改 Emuera.Xamarin 或 Emuera.Xamarin.Android 下的代码，或涉及 ADB/Android 测试时生效
---

# Xamarin ADB 测试规则

ADB 不在系统 PATH 中，每次使用前必须先定义：
```powershell
$adb = "D:\Program Files (x86)\Microsoft Visual Studio\Shared\Android\android-sdk\platform-tools\adb.exe"
```

## APK 构建

```powershell
dotnet publish "D:\emuera\emuera_lazyloading_selfmodified_version\Emuera.Xamarin.Android\Emuera.Xamarin.Android.csproj" -f net8.0-android -c Debug 2>&1
```

## 测试流程

1. 连接模拟器：`& $adb connect 127.0.0.1:16384`（MuMu）或 `127.0.0.1:5555`（华为）
2. 安装 APK：`& $adb -s $device install -r "APK路径" 2>&1`
3. 清除 logcat + 启动：`& $adb -s $device logcat -c` + `am start`
4. 导航到引擎：等待 8 秒 → `input tap 540 300`
5. 等待加载：eratw-chs 约 23 秒
6. 拉取诊断日志：`& $adb -s $device logcat -d | Select-String "EmueraRender"`

## 诊断日志

```powershell
& $adb -s $device logcat -d | Select-String "EmueraRender"  # 渲染诊断
& $adb -s $device logcat -d | Select-String "EmueraHost"    # 引擎初始化
```

完整流程见 [adb-testing.md](file:///d:/emuera/shared-trae/knowledge/xemuera/adb-testing.md)
