@echo off
chcp 65001 >nul
%1 mshta vbscript:CreateObject("Shell.Application").ShellExecute("cmd.exe","/c %~s0 ::","","runas",1)(window.close)&&exit
cd /d "%~dp0"

echo 导入IE模式右键注册表配置
reg import "启用IE模式右键菜单.reg"

echo 关闭所有Edge进程
taskkill /f /im msedge.exe >nul 2>&1

echo 重启Edge浏览器
start msedge.exe
echo 操作完成！右键网页即可看到IE模式选项
pause