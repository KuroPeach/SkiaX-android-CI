@echo off
chcp 65001 >nul 2>&1
echo === XEmuera Android APK Publish ===
echo.

echo [1/3] Restoring NuGet packages...
dotnet restore Emuera.Xamarin.Android\Emuera.Xamarin.Android.csproj
if %ERRORLEVEL% neq 0 (
    echo.
    echo [FAILED] NuGet restore failed.
    pause
    exit /b 1
)

echo.
echo [2/3] Publishing Android APK...
dotnet publish Emuera.Xamarin.Android\Emuera.Xamarin.Android.csproj -c Release -f net8.0-android -p:AndroidPackageFormat=apk -p:AndroidKeyStore=false -p:RunAOTCompilation=false

if %ERRORLEVEL% neq 0 (
    echo.
    echo [FAILED] Android publish failed.
    pause
    exit /b 1
)

echo.
echo [3/3] Locating output APK...
set APK_DIR=Emuera.Xamarin.Android\bin\Release\net8.0-android
if exist "%APK_DIR%\com.xemuera.app-Signed.apk" (
    echo [OK] Signed APK: %APK_DIR%\com.xemuera.app-Signed.apk
) else if exist "%APK_DIR%\com.xemuera.app.apk" (
    echo [OK] APK: %APK_DIR%\com.xemuera.app.apk
) else (
    echo [WARN] APK not found in %APK_DIR%
    dir /b "%APK_DIR%\*.apk" 2>nul
)

echo.
echo === Publish complete ===
pause
