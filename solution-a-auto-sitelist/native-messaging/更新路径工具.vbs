Set fso = CreateObject("Scripting.FileSystemObject")
root = fso.GetAbsolutePathName("../../")
exePath = root & "\solution-a-auto-sitelist\host-program\IETabHelper.exe"
jsonPath = fso.GetAbsolutePathName("host-manifest.json")
regPath = fso.GetAbsolutePathName("install.reg")

'替换json内exe路径
Set jsonFile = fso.CreateTextFile(jsonPath,True)
jsonContent = fso.OpenTextFile(jsonPath,1).ReadAll
jsonContent = Replace(jsonContent,"E:\\IETabHelper\\IETabHelper.exe", Replace(exePath,"\","\\"))
jsonFile.Write jsonContent
jsonFile.Close

'替换注册表文件路径
Set regFile = fso.CreateTextFile(regPath,True)
regContent = fso.OpenTextFile(regPath,1).ReadAll
regContent = Replace(regContent,"E:\\IETabHelper\\manifest.json", Replace(jsonPath,"\","\\"))
regFile.Write regContent
regFile.Close

MsgBox "路径自动更新完成！"