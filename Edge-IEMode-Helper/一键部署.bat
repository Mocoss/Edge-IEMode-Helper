@echo off
chcp 65001 >nul
%1 mshta vbscript:CreateObject("Shell.Application").ShellExecute("cmd.exe","/c %~s0 ::","","runas",1)(window.close)&&exit
cd /d "%~dp0"

echo ==============================================
echo Edge IE模式工具 一键部署脚本（管理员）
echo ==============================================
echo 1. 自动编译IETabHelper.exe
echo 2. 自动替换注册表内本地绝对路径
echo 3. 自动导入注册表原生消息配置
echo ==============================================
pause

:: 1. 编译C#程序
cd solution-a-auto-sitelist\host-program
call 编译.bat
cd ../..

:: 2. 自动获取当前项目根路径
set "ROOT=%cd%"
set "EXE_PATH=%ROOT%\solution-a-auto-sitelist\host-program\IETabHelper.exe"
set "HOST_JSON=%ROOT%\solution-a-auto-sitelist\native-messaging\host-manifest.json"
set "REG_FILE=%ROOT%\solution-a-auto-sitelist\native-messaging\install.reg"

echo.
echo 当前项目根目录：%ROOT%
echo 程序路径：%EXE_PATH%
echo.

:: 3. 自动修改host-manifest.json内exe路径
powershell -Command "(Get-Content '%HOST_JSON%' -Raw) -replace 'E:\\\\IETabHelper\\\\IETabHelper.exe', '%EXE_PATH:\=\\\\%' | Set-Content '%HOST_JSON%'"
powershell -Command "(Get-Content '%REG_FILE%' -Raw) -replace 'E:\\\\IETabHelper\\\\manifest.json', '%HOST_JSON:\=\\\\%' | Set-Content '%REG_FILE%'"

echo 路径自动替换完成！
echo.

:: 4. 导入注册表配置
reg import "%REG_FILE%"
echo 原生消息主机注册表注册成功！
echo.

echo ==============================================
echo 部署完成！后续操作：
echo 1. 打开Edge → 扩展 → 开发者模式 → 加载解压缩扩展
echo 2. 选择文件夹：%ROOT%\solution-a-auto-sitelist\edge-extension
echo 3. 查看扩展ID，复制替换host-manifest.json中的扩展ID
echo ==============================================
pause
start msedge.exe edge://extensions/
exit