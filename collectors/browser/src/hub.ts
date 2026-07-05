// loopback hub 上报客户端（ADR-017 §1）：POST http://127.0.0.1:{port}/v1/segments。
// 采集器不持凭证、不知服务端地址——离线缓存、ApiKey、重试全在 Agent 侧复用。

import type { SegmentSnapshot } from './fold'

export type PostResult = 'ok' | 'rejected' | 'unreachable'

/**
 * - ok：hub 收下，队列可清。
 * - rejected（4xx）：hub 明确拒绝（校验失败/毒批次；将来 403 = 被停用，issue 04）——重传无意义，丢弃。
 * - unreachable（网络错误/5xx）：Agent 未运行或暂时故障——保留队列，下个周期重试。
 */
export async function postSegments(
  port: number,
  segments: SegmentSnapshot[],
): Promise<PostResult> {
  try {
    const res = await fetch(`http://127.0.0.1:${port}/v1/segments`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ segments }),
    })
    if (res.ok) return 'ok'
    return res.status >= 400 && res.status < 500 ? 'rejected' : 'unreachable'
  } catch {
    return 'unreachable'
  }
}
