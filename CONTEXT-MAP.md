# Context Map

Heartbeat 是一个 Windows PC 应用使用时长监控系统。系统分为三个领域上下文和一个共享内核。

## Contexts

| Context | Directory | Responsibility |
|---------|-----------|----------------|
| Collection | `desktop/` | 监听前台窗口切换，生成使用记录，上传至服务端 |
| Analytics | `server/` | 接收使用数据，合并碎片记录，聚合报表 |
| Dashboard | `frontend/` | 可视化使用数据 |

## Shared Kernel

`shared/Heartbeat.Core` — 跨上下文共享的 DTO 和核心工具（如 UsageMerger）。

## Relationships

```
Collection ──uploads──▶ Analytics ──serves──▶ Dashboard
     │                      │
     └──── Shared Kernel ───┘
```

- Collection → Analytics: 上游/下游（Upstream/Downstream），Collection 生产数据，Analytics 消费并持久化
- Analytics → Dashboard: 上游/下游，Analytics 提供 API，Dashboard 只读消费
