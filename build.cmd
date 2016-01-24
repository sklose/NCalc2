@ECHO OFF
dnu restore --quiet

dnvm use 1.0.0-rc1-update1 -r clr
dnx -p test\NCalc.Tests test -appveyor

dnvm use 1.0.0-rc1-update1 -r coreclr
dnx -p test\NCalc.Tests test -appveyor
