@echo off
PowerShell.exe -ExecutionPolicy ByPass -Command "& { %~dp0build.ps1 -Script %~dp0build.cake -Target Build-And-Merge -Configuration Release; exit $LASTEXITCODE }"
pause