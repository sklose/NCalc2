@ECHO OFF
dotnet restore
dotnet test test\NCalc.Tests -appveyor
dotnet pack src\NCalc --configuration Release
