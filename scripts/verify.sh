#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
dotnet restore GameCoTuong.sln --locked-mode -m:1
dotnet build GameCoTuong.sln -c Release --no-restore -m:1 -p:UseSharedCompilation=false
dotnet test GameCoTuong.sln -c Release --no-build --no-restore -m:1
npm --prefix src/frontend ci
npm --prefix src/frontend run build
npm --prefix src/frontend test
