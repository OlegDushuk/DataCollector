dotnet msbuild ".\DataCollector.DataBase\DataCollector.DataBase.sqlproj" /t:Build /p:Configuration=Debug
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

sqlpackage /Action:Publish `
  /SourceFile:".\DataCollector.DataBase\bin\Debug\DataCollector.DataBase.dacpac" `
  /TargetServerName:"localhost" `
  /TargetDatabaseName:"DataCollector_Dev" `
  /TargetTrustServerCertificate:True
