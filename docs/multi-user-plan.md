# Heartbeat 多用户转换方案

## 1. 现状总结

后端数据隔离已基本完成：
- Device.OwnerId + ICurrentUserService WHERE 过滤
- 所有 Controller 标记 [Authorize]，JWT via OIDC discovery
- 前端 auth store 已处理 token/refresh/userId
- 部署同源（nginx 反代），无 CORS 问题

**核心结论：后端对"两个用户各看各的数据"已能工作。缺失在 App 全局共享、无用户身份展示、无登出、无路由。**

## 2. 缺失部分

### P0 — 阻塞多用户

| 问题 | 位置 |
|------|------|
| App 表全局无过滤 | AppController.GetAll() |
| AppIcon 无权限校验 | AppController.UploadIcon() |
| 前端无登出按钮 | header |
| 前端无 vue-router | 架构 |

### P1 — 体验缺失

- 无用户身份展示（无 /me 端点）
- 无用户设置（时区等）
- 新用户无 ApiKey 获取流程

### P2 — 安全/运维

- 无 rate limiting
- 无 token 吊销
- 无 .env.example

## 3. 实施方案

### Phase 1：最小可用（MVP）

#### 1.2 前端路由 + 登出

- 安装 vue-router
- 添加路由配置（/, /callback）
- App.vue 改为 router-view
- 添加 LoginCallback.vue 处理 auth 回调
- Dashboard header 添加登出按钮

#### 1.1 App 查询过滤

- App 保持全局目录，GetAll 改为只返回当前用户有 Usage 的 App
- AppIcon 上传加 first-write-wins 校验

### Phase 2：体验优化

- User 表 + /api/v1/users/me
- 用户设置页
- ApiKey 自助管理

### Phase 3：高级功能（按需）

- 团队/组织
- RBAC
- Rate limiting
- Token 吊销

## 4. 技术决策

- App 保持全局目录，查询时按用户 Usage 过滤
- 用户信息通过后端 User 表 + OIDC userinfo 获取
- AppIcon first-write-wins
- vue-router history mode
- User 表延到 Phase 2
