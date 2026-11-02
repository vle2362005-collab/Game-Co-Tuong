#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
mkdir -p artifacts/demo/web artifacts/demo/database
dotnet tool restore
dotnet publish src/backend/GameCoTuong.Api -c Release --no-restore -o artifacts/demo/api -m:1 -p:UseSharedCompilation=false
dotnet ef migrations script --idempotent --no-build --configuration Release --project src/backend/GameCoTuong.Infrastructure --startup-project src/backend/GameCoTuong.Api --output artifacts/demo/database/migrations.sql
npm --prefix src/frontend run build
cp -R src/frontend/dist/. artifacts/demo/web/
cp src/backend/GameCoTuong.Api/appsettings.Local.example.json artifacts/demo/backend-config.example.json
cp src/frontend/.env.example artifacts/demo/frontend-config.example.txt
cp docs/release/runbook.md artifacts/demo/README.md
git rev-parse HEAD > artifacts/demo/SOURCE_COMMIT
git diff --quiet && git diff --cached --quiet && printf 'clean\n' > artifacts/demo/WORKTREE_STATE || printf 'dirty - rebuild after commit\n' > artifacts/demo/WORKTREE_STATE
test ! -f artifacts/demo/api/appsettings.Local.json
node scripts/release-manifest.mjs
