@echo off
echo Compiling Autocleaner mod...

set RIMWORLD_PATH=C:\Program Files (x86)\Steam\steamapps\common\RimWorld
set CSC_PATH=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe

"%CSC_PATH%" /target:library ^
/out:..\1.6\Assemblies\Autocleaner.dll ^
/reference:"%RIMWORLD_PATH%\RimWorldWin64_Data\Managed\Assembly-CSharp.dll" ^
/reference:"%RIMWORLD_PATH%\RimWorldWin64_Data\Managed\UnityEngine.dll" ^
/reference:"%RIMWORLD_PATH%\RimWorldWin64_Data\Managed\UnityEngine.CoreModule.dll" ^
/reference:"%RIMWORLD_PATH%\RimWorldWin64_Data\Managed\UnityEngine.IMGUIModule.dll" ^
/reference:"%RIMWORLD_PATH%\RimWorldWin64_Data\Managed\UnityEngine.TextRenderingModule.dll" ^
/reference:"%RIMWORLD_PATH%\RimWorldWin64_Data\Managed\Unity.Mathematics.dll" ^
/reference:"%RIMWORLD_PATH%\RimWorldWin64_Data\Managed\netstandard.dll" ^
/reference:..\packages\Lib.Harmony.2.0.2\lib\net472\0Harmony.dll ^
*.cs

if %ERRORLEVEL% EQU 0 (
    echo Compilation successful!
) else (
    echo Compilation failed!
)

pause 