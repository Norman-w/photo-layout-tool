#!/usr/bin/env bash
set -euo pipefail

# 本地开发：后端 API :5066 + 前端 Vite :5173（代理 /api）

ROOT_DIR=$(cd "$(dirname "$0")" && pwd)
cd "$ROOT_DIR"

if [ ! -d frontend/node_modules ]; then
  echo "[dev] 安装前端依赖..."
  (cd frontend && npm install)
fi

echo "[dev] 启动后端 PhotoLayout.Api at :5066"
(
  cd backend/PhotoLayout.Api
  dotnet run --urls "http://127.0.0.1:5066"
) &
API_PID=$!

cleanup() {
  kill "$API_PID" 2>/dev/null || true
}
trap cleanup EXIT INT TERM

sleep 2

echo "[dev] 启动前端 at :5173 (base=/tools/photo-layout/)"
cd frontend && VITE_BASE_PATH=/tools/photo-layout/ npm run dev
