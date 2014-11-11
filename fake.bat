:: This bat script bootstraps the fake.exe executable to not depend on the package version.
@echo off
setlocal

for /D %%C IN (packages\FAKE*) do set FAKE=%%C\tools\fake.exe
IF "%FAKE%" == "" (
   echo Cannot find fake.exe
   EXIT /b 1
) 

call %FAKE% %*