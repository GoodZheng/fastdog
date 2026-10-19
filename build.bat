@echo off
setlocal
echo ========================================
echo   FastDog 构建脚本
echo   将构建安装版和绿色版
echo ========================================
echo.

echo [1/4] 清理旧的构建文件...
if exist "publish" rmdir /s /q "publish"
if not exist "release" mkdir "release"
if not exist "installer" mkdir "installer"

echo.
echo [2/4] 发布应用程序（单文件自包含）...
dotnet publish src\FastDog\FastDog.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "publish" -p:DebugType=None -p:DebugSymbols=false

if %errorlevel% neq 0 (
    echo.
    echo 发布失败！请检查错误信息。
    pause
    exit /b 1
)

echo.
echo [3/4] 制作绿色版（publish 目录打包为 zip，含 FastDog.exe + tools\rg.exe）...
for /f "tokens=3 delims=<>" %%a in ('findstr "<Version>" src\FastDog\FastDog.csproj') do set "version=%%a"

powershell -NoProfile -Command "Compress-Archive -Path 'publish\FastDog.exe','publish\tools' -DestinationPath 'release\FastDog-portable-%version%.zip' -Force"

if %errorlevel% neq 0 (
    echo.
    echo 绿色版制作失败！
    pause
    exit /b 1
)
echo   绿色版: release\FastDog-portable-%version%.zip

echo.
echo [4/4] 编译安装版...
if not exist "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" (
    echo   未检测到 Inno Setup Compiler，跳过安装版。
    echo   下载地址: https://jrsoftware.org/isdl.php
    goto :summary
)
echo   检测到 Inno Setup，开始编译安装包...
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer.iss
if %errorlevel% neq 0 (
    echo.
    echo 安装版编译失败！
    pause
    exit /b 1
)
echo   安装版: installer\FastDog-Setup-%version%.exe

:summary
echo.
echo ========================================
echo   构建完成！版本: %version%
echo.
if exist "release\FastDog-portable-%version%.zip" (
    echo   绿色版: release\FastDog-portable-%version%.zip
)
if exist "installer\FastDog-Setup-%version%.exe" (
    echo   安装版: installer\FastDog-Setup-%version%.exe
)
echo ========================================
echo.
pause
