#!/bin/bash
set -e

export NVM_DIR="/home/shenxianovo/.nvm"
source "$NVM_DIR/nvm.sh"
nvm use 20

# ==== 配置 ====
APP_NAME="heartbeat"
APP_DIR="/srv/heartbeat"
DOTNET_PROJECT="server/Heartbeat.Server/Heartbeat.Server.csproj"
PUBLISH_DIR="$APP_DIR/publish"
DOTNET_ENV="Production"
VUE_PROJECT="frontend"
LOG_FILE="$APP_DIR/$APP_NAME.log"
PID_FILE="$APP_DIR/$APP_NAME.pid"

cd "$APP_DIR"

echo "Pulling latest code..."
git fetch origin main
OLD_HEAD=$(git rev-parse HEAD)
git reset --hard origin/main

# ==== 检查变更 ====
SERVER_CHANGED=$(git diff --name-only $OLD_HEAD HEAD | grep -E '^(server|shared)/' || true)
FRONTEND_CHANGED=$(git diff --name-only $OLD_HEAD HEAD | grep '^frontend/' || true)
DEPLOY_CHANGED=$(git diff --name-only $OLD_HEAD HEAD | grep '^deploy/' || true)

# ==== 如果 deploy 变更 → 强制全量 ====
if [ -n "$DEPLOY_CHANGED" ]; then
    echo "Deploy scripts changed → full rebuild & restart"
    SERVER_CHANGED=1
    FRONTEND_CHANGED=1
fi

# ==== 前端 ====
if [ -n "$FRONTEND_CHANGED" ]; then
    echo "Building frontend..."
    npm ci --prefix "$VUE_PROJECT"
    npm run build --prefix "$VUE_PROJECT"
fi

# ==== 后端构建 ====
if [ -n "$SERVER_CHANGED" ]; then
    echo "Publishing backend..."
    rm -rf "$PUBLISH_DIR"/*
    dotnet publish "$DOTNET_PROJECT" -c Release -o "$PUBLISH_DIR"
fi

# ==== 优雅停止（仅后端变更时） ====
if [ -n "$SERVER_CHANGED" ] && [ -f "$PID_FILE" ]; then
    PID=$(cat "$PID_FILE")

    if ps -p $PID > /dev/null; then
        echo "Stopping service (PID $PID)..."

        kill -15 $PID  # SIGTERM

        # 等最多 30 秒优雅退出
        for i in {1..30}; do
            if ! ps -p $PID > /dev/null; then
                echo "Stopped gracefully"
                break
            fi
            sleep 1
        done

        # 还没退出 → 强杀
        if ps -p $PID > /dev/null; then
            echo "Force killing..."
            kill -9 $PID
        fi
    fi

    rm -f "$PID_FILE"
fi

# ==== 启动 ====
if [ -n "$SERVER_CHANGED" ]; then
    echo "Starting backend..."

    nohup dotnet "$PUBLISH_DIR/Heartbeat.Server.dll" \
        --environment $DOTNET_ENV \
        > "$LOG_FILE" 2>&1 &

    echo $! > "$PID_FILE"
    echo "Started (PID $(cat $PID_FILE))"
else
    echo "No backend changes"
fi