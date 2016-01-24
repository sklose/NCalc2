@ECHO OFF
call dnu.cmd restore --quiet
call dnvm.cmd use 1.0.0-rc1-update1 -r clr
call dnx -p test\NCalc.Tests test -appveyor
call dnvm.cmd use 1.0.0-rc1-update1 -r coreclr
call dnx -p test\NCalc.Tests test -appveyor
call dnu pack src\NCalc --quiet --configuration Release
