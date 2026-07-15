@echo off
chcp 65001 >nul
echo 正在编译 IETabHelper.exe
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /out:IETabHelper.exe IETabHelper.cs /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll /r:System.Xml.dll
if exist IETabHelper.exe (
    echo 编译成功！输出文件：IETabHelper.exe
) else (
    echo 编译失败，请检查系统.NET框架
)
pause