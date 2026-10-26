$ErrorActionPreference = 'Stop'
Push-Location (Join-Path $PSScriptRoot '..')
try {
    dotnet restore GameCoTuong.sln --locked-mode -m:1
    if ($LASTEXITCODE -ne 0) { throw 'Backend restore failed.' }
    dotnet build GameCoTuong.sln -c Release --no-restore -m:1 -p:UseSharedCompilation=false
    if ($LASTEXITCODE -ne 0) { throw 'Backend build failed.' }
    dotnet test GameCoTuong.sln -c Release --no-build --no-restore -m:1
    if ($LASTEXITCODE -ne 0) { throw 'Backend tests failed.' }
    npm --prefix src/frontend ci
    if ($LASTEXITCODE -ne 0) { throw 'Frontend restore failed.' }
    npm --prefix src/frontend run build
    if ($LASTEXITCODE -ne 0) { throw 'Frontend build failed.' }
    npm --prefix src/frontend test
    if ($LASTEXITCODE -ne 0) { throw 'Frontend tests failed.' }
}
finally {
    Pop-Location
}
