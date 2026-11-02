$ErrorActionPreference = 'Stop'
Push-Location (Join-Path $PSScriptRoot '..')
function Check-Exit { if ($LASTEXITCODE -ne 0) { throw 'Packaging command failed.' } }
try {
    New-Item -ItemType Directory -Force artifacts/demo/web, artifacts/demo/database | Out-Null
    dotnet tool restore; Check-Exit
    dotnet publish src/backend/GameCoTuong.Api -c Release --no-restore -o artifacts/demo/api -m:1 -p:UseSharedCompilation=false; Check-Exit
    dotnet ef migrations script --idempotent --no-build --configuration Release --project src/backend/GameCoTuong.Infrastructure --startup-project src/backend/GameCoTuong.Api --output artifacts/demo/database/migrations.sql; Check-Exit
    npm --prefix src/frontend run build; Check-Exit
    Copy-Item src/frontend/dist/* artifacts/demo/web -Recurse -Force
    Copy-Item src/backend/GameCoTuong.Api/appsettings.Local.example.json artifacts/demo/backend-config.example.json
    Copy-Item src/frontend/.env.example artifacts/demo/frontend-config.example.txt
    Copy-Item docs/release/runbook.md artifacts/demo/README.md
    git rev-parse HEAD | Set-Content artifacts/demo/SOURCE_COMMIT
    if (git status --porcelain) { 'dirty - rebuild after commit' | Set-Content artifacts/demo/WORKTREE_STATE }
    else { 'clean' | Set-Content artifacts/demo/WORKTREE_STATE }
    if (Test-Path artifacts/demo/api/appsettings.Local.json) { throw 'Local backend configuration must not be packaged.' }
    node scripts/release-manifest.mjs; Check-Exit
}
finally { Pop-Location }
