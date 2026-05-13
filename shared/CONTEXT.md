# Shared Kernel — CONTEXT

## Conventions

- **时间存储**：所有时间字段在数据库中以 UTC+0 存储。"今天"/"本周"的边界由前端根据用户浏览器时区确定，通过 DateTimeOffset 参数传给服务端。
- **认证架构**：依赖外部自建 Auth 平台（支持邮箱/Google/GitHub 登录）。Collection（Agent）使用 Auth 平台签发的 ApiKey 上传数据；Dashboard（前端）计划通过 Auth 平台登录获取 JWT 访问报表 API（尚未实现）。
- **数据隔离**：多用户模式下，User 拥有多个 Device，AppUsage 通过 Device 间接关联到 User，用户只能看到自己 Device 的数据。

## Glossary

| Term | Definition |
|------|-----------|
| Device | 一台唯一的物理机器。运行 Agent 采集数据。属于某个 User（通过 Auth 平台的 subject ID 关联）。 |
| App | 一个应用程序，由进程可执行文件名（不含路径）唯一标识。同一 exe 无论开几个窗口都算同一个 App。 |
| AppUsage | 一段某个 App 处于前台的时间记录（StartTime → EndTime）。系统忠实记录所有前台窗口，包括 explorer.exe（桌面）和 LockApp.exe（锁屏），不做活跃/非活跃过滤。 |
| AppIcon | App 对应的图标二进制数据，由 Agent 上传，供 Dashboard 展示。 |
| ApiKey | Collection 上传数据到 Analytics 的凭证，类似 LLM API Key。 |
| UsageMerger | 合并因上传分片截断而产生的同一 App 碎片记录。规则：同 AppName + 时间间隔 ≤1s 则合并。客户端和服务端双层执行。不做跨切换的聚合——用户切走再切回产生的两段记录是独立的。 |
