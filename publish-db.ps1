dotnet msbuild ".\DataCollector.Server.DataBase\DataCollector.Server.DataBase.sqlproj" /t:Build /p:Configuration=Debug
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

sqlpackage /Action:Publish `
  /SourceFile:".\DataCollector.Server.DataBase\bin\Debug\DataCollector.Server.DataBase.dacpac" `
  /TargetServerName:"localhost" `
  /TargetDatabaseName:"DataCollector_Dev" `
  /TargetTrustServerCertificate:True
