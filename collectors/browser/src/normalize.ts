// IdentityKey 规范化：origin + pathname，掐掉 query/fragment（utm、时间戳、锚点是假碎片主源）。
// 完整原始 URL 始终随段存入 Attributes——判据可有损，原始数据无损（ADR-012 原则）。
// per-domain 覆写表（"query 即身份"的站点，如 youtube.com/watch 需保留 v）见 issue 02，本片不含。

/** 规范化 URL 为续接判据。非法 URL 原样返回（判据退化但不丢数据）。 */
export function identityKeyOf(rawUrl: string): string {
  let u: URL
  try {
    u = new URL(rawUrl)
  } catch {
    return rawUrl
  }

  // 自定义 scheme（edge://、about: 等）origin 为 "null"：退化为掐 query/fragment 的原串。
  if (u.origin === 'null') {
    return u.href.split('#')[0].split('?')[0]
  }

  // 尾斜杠归一：/docs/ 与 /docs 同一活动；根路径 "/" 保留。
  const path =
    u.pathname !== '/' && u.pathname.endsWith('/')
      ? u.pathname.slice(0, -1)
      : u.pathname

  // URL.origin 已做 host 小写化与默认端口剔除。
  return u.origin + path
}

/** 提取 hostname 供 Attributes.domain（回放按域名聚合用）。 */
export function domainOf(rawUrl: string): string {
  try {
    return new URL(rawUrl).hostname
  } catch {
    return ''
  }
}
