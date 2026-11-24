Set shell = CreateObject("WScript.Shell")
Set files = CreateObject("Scripting.FileSystemObject")
runner = shell.ExpandEnvironmentStrings("%USERPROFILE%") & "\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe"
script = files.BuildPath(files.GetParentFolderName(WScript.ScriptFullName), "2026-10-24-01.py")
shell.Run Chr(34) & runner & Chr(34) & " " & Chr(34) & script & Chr(34), 0, False
