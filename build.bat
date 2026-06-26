@echo off
chcp 65001 >nul
echo ========================================
echo   WordReminder 构建脚本
echo   将构建安装版和绿色版
echo ========================================
echo.

echo [1/4] 清理旧的构建文件...
if exist "publish" rmdir /s /q "publish"
if not exist "release" mkdir "release"

echo.
echo [2/4] 发布应用程序...
cd WordReminder
call dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "../publish" -p:DebugType=None -p:DebugSymbols=false

if %errorlevel% neq 0 (
    echo.
    echo 发布失败！请检查错误信息。
    pause
    exit /b 1
)

cd ..

echo.
echo [3/4] 制作绿色版...
for /f "tokens=2 delims==" %%a in ('findstr "Version" WordReminder\WordReminder.csproj ^| findstr "<Version>"') do set "version=%%~a"
set version=%version:<Version>=%
set version=%version:</Version>=%
set version=%version: =%

copy /Y "publish\WordReminder.exe" "release\WordReminder-portable-%version%.exe" >nul

if %errorlevel% equ 0 (
    echo   绿色版: release\WordReminder-portable-%version%.exe
) else (
    echo   绿色版制作失败！
)

echo.
echo [4/4] 编译安装版...

if exist "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" (
    echo   检测到 Inno Setup，开始编译安装包...
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" WordReminder\installer.iss
    if %errorlevel% equ 0 (
        echo   安装版: installer\WordReminder-Setup-%version%.exe
    ) else (
        echo   安装版编译失败！
    )
) else (
    echo   未检测到 Inno Setup Compiler。
    echo.
    echo   请先安装 Inno Setup:
    echo     下载地址: https://jrsoftware.org/isdl.php
)

echo.
echo ========================================
echo   构建完成！
echo   版本: %version%
echo.
if exist "release\WordReminder-portable-%version%.exe" (
    echo   绿色版: release\WordReminder-portable-%version%.exe
)
if exist "installer\WordReminder-Setup-%version%.exe" (
    echo   安装版: installer\WordReminder-Setup-%version%.exe
)
echo ========================================
echo.
pause
