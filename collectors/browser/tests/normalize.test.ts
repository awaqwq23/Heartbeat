import { describe, expect, it } from 'vitest'
import { identityKeyOf, domainOf } from '../src/normalize'

describe('identityKeyOf', () => {
  it('掐掉 query 与 fragment', () => {
    expect(identityKeyOf('https://github.com/foo/bar?tab=readme#install')).toBe(
      'https://github.com/foo/bar',
    )
  })

  it('utm 等追踪参数不产生新身份', () => {
    const a = identityKeyOf('https://example.com/post/1?utm_source=x&utm_medium=y')
    const b = identityKeyOf('https://example.com/post/1')
    expect(a).toBe(b)
  })

  it('host 小写化、默认端口剔除（URL.origin 行为），path 大小写保留', () => {
    expect(identityKeyOf('HTTPS://GitHub.COM:443/Foo')).toBe('https://github.com/Foo')
  })

  it('非默认端口保留', () => {
    expect(identityKeyOf('http://localhost:5173/app')).toBe('http://localhost:5173/app')
  })

  it('尾斜杠归一，根路径保留', () => {
    expect(identityKeyOf('https://a.com/docs/')).toBe('https://a.com/docs')
    expect(identityKeyOf('https://a.com/')).toBe('https://a.com/')
  })

  it('本片已知限制：youtube watch 的 v 参数被掐掉（覆写表见 issue 02）', () => {
    const a = identityKeyOf('https://www.youtube.com/watch?v=aaa')
    const b = identityKeyOf('https://www.youtube.com/watch?v=bbb')
    expect(a).toBe(b) // issue 02 落地后此断言应反转
  })

  it('自定义 scheme（origin 为 null）退化为掐 query/fragment 的原串', () => {
    expect(identityKeyOf('edge://newtab/?param=1')).toBe('edge://newtab/')
  })

  it('非法 URL 原样返回', () => {
    expect(identityKeyOf('not a url')).toBe('not a url')
  })
})

describe('domainOf', () => {
  it('提取 hostname', () => {
    expect(domainOf('https://www.youtube.com/watch?v=x')).toBe('www.youtube.com')
  })

  it('非法 URL 返回空串', () => {
    expect(domainOf('nope')).toBe('')
  })
})
