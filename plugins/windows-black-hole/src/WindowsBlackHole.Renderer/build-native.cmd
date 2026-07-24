@echo off
setlocal

set "CONFIGURATION=%~1"
if "%CONFIGURATION%"=="" set "CONFIGURATION=Release"

set "VCVARS=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Auxiliary\Build\vcvars64.bat"
set "CMAKE_EXE=C:\Program Files\CMake\bin\cmake.exe"
set "SOURCE_DIR=%~dp0"
set "SOURCE_DIR=%SOURCE_DIR:~0,-1%"
set "BUILD_DIR=%~dp0build\%CONFIGURATION%"

if not exist "%VCVARS%" (
    echo Visual Studio C++ Build Tools were not found.
    exit /b 1
)
if not exist "%CMAKE_EXE%" (
    echo CMake was not found.
    exit /b 1
)

call "%VCVARS%"
if errorlevel 1 exit /b %errorlevel%

"%CMAKE_EXE%" -S "%SOURCE_DIR%" -B "%BUILD_DIR%" -G "NMake Makefiles" -DCMAKE_BUILD_TYPE="%CONFIGURATION%" "-DCMAKE_MAKE_PROGRAM:FILEPATH=%VCToolsInstallDir%bin\Hostx64\x64\nmake.exe"
if errorlevel 1 exit /b %errorlevel%

"%CMAKE_EXE%" --build "%BUILD_DIR%"
if errorlevel 1 exit /b %errorlevel%

"%CMAKE_EXE%" --build "%BUILD_DIR%" --target test
exit /b %errorlevel%
