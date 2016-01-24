@ECHO OFF
dnu restore --quiet
cd test\NCalc.Test

dnvm use 1.0.0-rc1-update1 -r clr
dnx test

dnvm use 1.0.0-rc1-update1 -r coreclr
dnx test

cd ../..