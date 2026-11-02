#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
dotnet restore GameCoTuong.sln --locked-mode -m:1
dotnet build GameCoTuong.sln -c Release --no-restore -m:1 -p:UseSharedCompilation=false
dotnet test GameCoTuong.sln -c Release --no-build --no-restore -m:1 --logger 'trx;LogFileName=backend.trx' --results-directory "$PWD/artifacts/qa"
node scripts/extract-performance.mjs
npm --prefix src/frontend ci
npm --prefix src/frontend run lint
npm --prefix src/frontend run build
npm --prefix src/frontend test
