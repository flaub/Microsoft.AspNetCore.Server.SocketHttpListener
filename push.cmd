@ECHO OFF

dotnet pack -c Release Microsoft.AspNetCore.Server.SocketHttpListener
nuget push -Source https://www.nuget.org/api/v2/package Microsoft.AspNetCore.Server.SocketHttpListener\bin\Release\*.nupkg
