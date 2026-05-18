#!/usr/bin/env bash
set -euo pipefail

# 发布到 145 服务器（与 norman-cloud-todo 同机）
# 公网入口：在 16.2 Nginx 配置 deploy/nginx.norman.wang.snippet.conf
#           → https://norman.wang/tools/photo-layout/

SERVER_USER=${SERVER_USER:-root}
SERVER_HOST=${SERVER_HOST:-10.10.10.100}
DEPLOY_PATH=${DEPLOY_PATH:-/opt/photo-layout}
HTTP_PORT=${HTTP_PORT:-5088}
SERVICE_NAME=${SERVICE_NAME:-photolayout}
VITE_BASE_PATH=${VITE_BASE_PATH:-/tools/photo-layout/}

ROOT_DIR=$(cd "$(dirname "$0")" && pwd)
cd "$ROOT_DIR"

echo "[deploy] 发布 PhotoLayout → ${SERVER_USER}@${SERVER_HOST}:${DEPLOY_PATH}"

if [ ! -d frontend/node_modules ]; then
  echo "[deploy] 安装前端依赖..."
  (cd frontend && npm install)
fi

echo "[deploy] 构建前端 base=${VITE_BASE_PATH}"
(
  cd frontend
  VITE_BASE_PATH="${VITE_BASE_PATH}" npm run build
)

echo "[deploy] 发布后端 (linux-x64 self-contained)..."
rm -rf ./publish
dotnet publish backend/PhotoLayout.Api/PhotoLayout.Api.csproj \
  -c Release -r linux-x64 --self-contained true -o ./publish

if [ ! -f ./publish/PhotoLayout.Api ]; then
  echo "[deploy] 错误: 未找到 publish/PhotoLayout.Api" >&2
  exit 1
fi

echo "[deploy] 打包前端静态到 publish/www"
rm -rf ./publish/www
mkdir -p ./publish/www
cp -R frontend/dist/. ./publish/www/

cp deploy/photolayout.service ./publish/

cat > ./publish/appsettings.Production.json << EOF
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
EOF

echo "[deploy] 同步到服务器..."
ssh "${SERVER_USER}@${SERVER_HOST}" "mkdir -p '${DEPLOY_PATH}'"
rsync -avz --delete --progress ./publish/ "${SERVER_USER}@${SERVER_HOST}:${DEPLOY_PATH}/"

echo "[deploy] 配置 systemd..."
ssh "${SERVER_USER}@${SERVER_HOST}" bash -s <<REMOTE
set -euo pipefail
chmod +x ${DEPLOY_PATH}/PhotoLayout.Api
sed "s|{{DEPLOY_PATH}}|${DEPLOY_PATH}|g; s|{{HTTP_PORT}}|${HTTP_PORT}|g" \
  ${DEPLOY_PATH}/photolayout.service > /etc/systemd/system/${SERVICE_NAME}.service
ln -sfn "${DEPLOY_PATH}/www" "${DEPLOY_PATH}/wwwroot"
systemctl daemon-reload
systemctl enable ${SERVICE_NAME}
systemctl restart ${SERVICE_NAME}
sleep 2
systemctl is-active --quiet ${SERVICE_NAME}
curl -sf "http://127.0.0.1:${HTTP_PORT}/" >/dev/null && echo "HTTP 检查 OK"
REMOTE

echo "[deploy] 完成"
echo "  内网: http://${SERVER_HOST}:${HTTP_PORT}/"
echo "  公网: https://norman.wang/tools/photo-layout/ （需 16.2 Nginx 已配置 snippet）"
