# Heartbeat 部署指南

> 本文档对应部署需求：Ubuntu 24.04 服务器 + 自定义域名 awaqwq233.com/heartbeat

---

## 一、服务器部署

### 1. 前置条件

服务器已安装：

- Docker
- Docker Compose（通常随 Docker 一起安装）
- Nginx（作为反向代理，与 Docker 容器配合）

检查：

```bash
docker --version
docker compose version
```

### 2. 克隆项目 & 配置

```bash
ssh awaqwq233@57.158.99.134
# 输入密码: 417618Li.awaqwq233

# 创建部署目录
sudo mkdir -p /srv/heartbeat
sudo chown awaqwq233:awaqwq233 /srv/heartbeat
cd /srv/heartbeat

# 克隆项目（如果你的 GitHub 仓库是私有的，需要先配置 SSH key 或 token）
git clone https://github.com/awaqwq233/Heartbeat.git .
# 或者用 SSH: git clone git@github.com:awaqwq233/Heartbeat.git .
```

### 3. 配置环境变量

```bash
cp .env.example .env
```

编辑 `.env` 文件，**必须修改** `DB_PASSWORD` 为一个强密码：

```
DB_PASSWORD=你的强密码（如 Heartbeat@2026!）
```

> 注：鉴权功能已暂时禁用，无需配置 AUTH_* 相关变量。

### 4. 构建并启动

```bash
docker compose up -d
```

查看运行状态：

```bash
docker compose ps
docker compose logs -f
```

首次启动会自动：
- 拉取 PostgreSQL 18 镜像
- 从 ghcr.io 拉取 backend 和 frontend 镜像
- 自动运行数据库迁移（创建表结构）

如果一切正常，backend 和 frontend 应该都在运行中。

### 5. Nginx 反向代理配置

在 `/etc/nginx/sites-available/heartbeat` 创建以下配置：

```nginx
server {
    listen 80;
    server_name awaqwq233.com;

    # 根路径指向 Heartbeat
    location /heartbeat/ {
        proxy_pass http://127.0.0.1:8081/;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

启用并测试：

```bash
sudo ln -s /etc/nginx/sites-available/heartbeat /etc/nginx/sites-enabled/
sudo nginx -t
sudo systemctl reload nginx
```

> 注意：以上配置假设你希望 Heartbeat 通过 `https://awaqwq233.com/heartbeat/` 访问。
> 如果你已经有 Nginx 配置，请将上述 server block 合并到现有配置中。
> 如果还没有配置 SSL 证书，建议使用 Let's Encrypt / Certbot 配置 HTTPS。

### 6. 验证部署

在浏览器访问：https://awaqwq233.com/heartbeat/

如果看到页面正常加载（虽然还没有数据），说明部署成功。

### 7. 更新部署

以后项目更新时，在服务器上执行：

```bash
cd /srv/heartbeat
git pull
docker compose pull
docker compose up -d
```

或者配置 GitHub Actions（见 `.github/workflows/`）自动部署。

---

## 二、被监视电脑设置（客户端）

### 1. 构建 Agent

在被监视的 Windows 电脑上：

```powershell
# 克隆项目
git clone https://github.com/awaqwq233/Heartbeat.git
cd Heartbeat

# 发布 Agent
dotnet publish desktop/Heartbeat.Agent.Runner -c Release -o C:\HeartbeatAgent
```

### 2. 创建配置文件

在 `C:\HeartbeatAgent` 目录下创建 `config.json`：

```json
{
  "ApiBaseUrl": "https://awaqwq233.com/heartbeat/api",
  "DeviceName": "PC-笔记本",
  "UploadIntervalMinutes": 1,
  "AwayProcessNames": ["LockApp"]
}
```

> **设备名称说明：**
> - 笔记本 → `"DeviceName": "PC-笔记本"`
> - 台式机 → `"DeviceName": "PC台式机"`
>
> 这些名称会显示在 Web 面板的设备选择下拉框中。

### 3. 启动 Agent

```powershell
# 前台运行（测试用）
C:\HeartbeatAgent\Heartbeat.Agent.Runner.exe

# 或设为开机启动（推荐）
# 在任务计划程序中创建任务，触发器设为"登录时"，操作指向该 exe
```

启动后，Agent 会自动：
- 监听前台窗口切换
- 每分钟上传一次使用数据
- 在 Web 面板中注册设备

### 4. 验证客户端

浏览器访问你的面板，应该能看到设备出现在设备选择器中，
并显示在线状态。

---

## 三、自定义背景图片

### 当前背景

项目默认使用 CSS 渐变作为背景，渐变定义在 `frontend/src/styles/tokens.css` 文件中。
渐变分为浅色模式和深色模式两套。

### 替换为自定义图片

编辑 `frontend/src/styles/tokens.css`：

**浅色模式**（`--bg-gradient`）——将渐变替换为图片：

```css
--bg-gradient: url('/your-image.jpg');
/* 或者用多个背景叠加 */
--bg-gradient: url('/your-image.jpg'), radial-gradient(
    circle at 20% 20%,
    oklch(1 0 0) 0%,
    oklch(0.985 0.008 235) 35%,
    oklch(0.95 0.03 232) 100%
  );
```

**深色模式**（`.dark` 类中的 `--bg-gradient`）：

```css
--bg-gradient: url('/your-image-dark.jpg');
```

### 放置图片文件

将你的背景图片放入 `frontend/public/` 目录，例如 `frontend/public/my-bg.jpg`。

> `frontend/public/` 下的文件会直接复制到网站根目录，可以通过 `/my-bg.jpg` 路径访问。

### body 背景样式

背景样式在 `frontend/src/style.css` 的 `body` 选择器中设置：

```css
body {
    background-image: var(--bg-gradient);
    background-attachment: fixed;
    background-size: cover;    /* 如果需要图片铺满，加上这行 */
    background-position: center; /* 图片居中 */
}
```

`background-attachment: fixed` 让背景在滚动时保持固定，营造视差效果。
如果你希望背景随页面滚动，可以改为 `background-attachment: scroll`。

### 构建后生效

修改完前端代码后，需要重新构建并部署：

```bash
# 本地构建
cd frontend
npm run build

# 重新部署（服务器上）
cd /srv/heartbeat
docker compose up -d --build frontend
```

---

## 四、常见问题

### Q: 数据库密码忘了怎么办？

```bash
docker compose down
# 编辑 .env 修改密码
# 然后删除旧数据库卷重新创建
docker volume rm heartbeat_db-data
docker compose up -d
```

注意：删除卷会丢失所有数据。

### Q: Agent 无法连接服务器？

检查：
1. Agent 电脑能否访问 `https://awaqwq233.com`
2. 服务器防火墙是否放行了 443 和 80 端口
3. Nginx 配置是否正确

### Q: 页面显示但没数据？

1. 确认 Agent 已启动
2. 检查浏览器控制台是否有 404/500 错误
3. 查看服务器日志：`docker compose logs -f backend`
