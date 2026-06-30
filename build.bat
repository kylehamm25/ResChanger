@echo off
setlocal

set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe

if not exist "%CSC%" (
    echo Could not find csc.exe. Your Windows install may not have .NET Framework 4.0+ registered.
    echo You can alternatively install the .NET SDK from https://dotnet.microsoft.com and run:
    echo     dotnet build
    pause
    exit /b 1
)

"%CSC%" /target:winexe /out:ResToggle.exe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Xml.dll *.cs

if %ERRORLEVEL% EQU 0 (
    echo.
    echo Build succeeded: ResToggle.exe
) else (
    echo.
    echo Build failed
)

pause