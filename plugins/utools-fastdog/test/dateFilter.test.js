const test = require('node:test');
const assert = require('node:assert');
const { passDateFilter } = require('../lib/searchService');

test('passDateFilter: 无日期限制时通过', () => {
  const r = { lastModified: new Date('2026-01-15') };
  assert.strictEqual(passDateFilter(r, { dateFilterEnabled: false }), true);
});

test('passDateFilter: 早于 from 被过滤', () => {
  const r = { lastModified: new Date('2026-01-14') };
  const q = { dateFilterEnabled: true, dateFrom: new Date('2026-01-15'), dateTo: null };
  assert.strictEqual(passDateFilter(r, q), false);
});

test('passDateFilter: 晚于 to+1天 被过滤（to 含当天全天，半开区间 [from, to+1)）', () => {
  // to=01-15 → 边界为 01-16T00:00:00（次日0点）；严格 > 才过滤
  // r=01-16T00:00:00 恰好等于边界，> 为 false，应保留
  const r1 = { lastModified: new Date('2026-01-16T00:00:00') };
  const q = { dateFilterEnabled: true, dateFrom: null, dateTo: new Date('2026-01-15') };
  assert.strictEqual(passDateFilter(r1, q), true);
  // r=01-16T00:00:01 超过边界，应过滤
  const r2 = { lastModified: new Date('2026-01-16T00:00:01') };
  assert.strictEqual(passDateFilter(r2, q), false);
});

test('passDateFilter: 在范围内通过（含 to 当天全天）', () => {
  const r = { lastModified: new Date('2026-01-15T23:59:59') };
  const q = { dateFilterEnabled: true, dateFrom: new Date('2026-01-15'), dateTo: new Date('2026-01-15') };
  assert.strictEqual(passDateFilter(r, q), true);
});
