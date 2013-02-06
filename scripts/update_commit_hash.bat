@echo off
echo|set /p="<appSettings><add key='commit_hash' value='"
for /f "tokens=*" %%a in ('git rev-parse HEAD') do (set myvar=%%a)
echo|set /p=%myvar%
echo|set /p="' /></appSettings>"
@echo on