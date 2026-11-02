import { spawn } from 'node:child_process'
import { randomBytes, randomUUID } from 'node:crypto'
import { existsSync, mkdtempSync, mkdirSync, writeFileSync, rmSync } from 'node:fs'
import { createServer } from 'node:net'
import { tmpdir } from 'node:os'
import { resolve, join, dirname } from 'node:path'
import { setTimeout as delay } from 'node:timers/promises'
const api = resolve('artifacts/demo/api'), dll = join(api, 'GameCoTuong.Api.dll')
if (!existsSync(dll)) throw new Error('Run scripts/package-demo.* first.')
const reserve = createServer(); await new Promise(r => reserve.listen(0, '127.0.0.1', r))
const port = reserve.address().port; await new Promise(r => reserve.close(r))
const temp = mkdtempSync(join(tmpdir(), 'gct-smoke-')), base = `http://127.0.0.1:${port}`
let startupError
const child = spawn(process.env.GCT_DOTNET || 'dotnet', [dll, '--urls', base], {
  cwd: api, stdio: 'ignore', env: { ...process.env, ASPNETCORE_ENVIRONMENT: 'Testing',
    Database__Provider: 'Sqlite', ConnectionStrings__GameDatabase: `Data Source=${join(temp, 'smoke.db')}`,
    Jwt__SigningKey: randomBytes(48).toString('base64') }
})
child.on('error', error => { startupError = error })
const checks = []
async function request(path, body, token) {
  const response = await fetch(base + path, { method: body === undefined ? 'GET' : 'POST',
    headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) },
    ...(body === undefined ? {} : { body: JSON.stringify(body) }), signal: AbortSignal.timeout(10000) })
  if (!response.ok) throw new Error(`${path}: HTTP ${response.status}`)
  const json = await response.json(); return json.data ?? json
}
try {
  let ready = false
  for (let attempt = 0; attempt < 40; attempt++) {
    if (startupError) throw startupError
    try { await request('/health'); ready = true; break } catch { await delay(250) }
  }
  if (!ready) throw new Error('Published API did not start.')
  checks.push('published API health')
  async function user() {
    const username = 'smoke' + randomUUID().replaceAll('-', '').slice(0, 12), password = 'Demo!123' + randomBytes(8).toString('hex')
    await request('/api/v1/auth/register', { requestId: randomUUID(), username, displayName: 'Smoke', password, email: `${username}@example.com` })
    return (await request('/api/v1/auth/login', { requestId: randomUUID(), username, password })).accessToken
  }
  const red = await user(), black = await user(); checks.push('two account registration/login')
  const room = await request('/api/v1/rooms', { requestId: randomUUID(), name: 'Release smoke' }, red)
  await request(`/api/v1/rooms/join/${room.code}`, {}, black)
  await request(`/api/v1/rooms/${room.id}/ready`, { ready: true }, red)
  await request(`/api/v1/rooms/${room.id}/ready`, { ready: true }, black)
  const started = await request(`/api/v1/rooms/${room.id}/start`, { requestId: randomUUID() }, red), id = started.matchId
  checks.push('room/join/ready/start')
  const move = { requestId: randomUUID(), expectedVersion: 1, pieceId: 'red-soldier-1', toCol: 0, toRow: 5 }
  await request(`/api/v1/matches/${id}/moves`, move, red)
  await request(`/api/v1/matches/${id}/moves`, move, red); checks.push('move and idempotent retry')
  await request(`/api/v1/matches/${id}/surrender`, { confirmed: true }, black)
  await request(`/api/v1/rooms/${room.id}/close`, {}, red)
  const history = await request(`/api/v1/matches/${id}/history`, undefined, red)
  if (history.moveNumber !== 1 || history.moves.length !== 1 || history.currentBoardJson !== history.moves[0].boardSnapshotJson || history.winner !== 'red')
    throw new Error('Published API history is inconsistent.')
  checks.push('surrender/close/persistent replay history')
  const report = { passed: true, scope: 'published API; loopback HTTP; temporary SQLite; no browser/real SQL Server/TURN', checks }
  const target = process.argv[2] || 'artifacts/qa/smoke.json'
  mkdirSync(dirname(target), { recursive: true }); writeFileSync(target, JSON.stringify(report, null, 2) + '\n')
  console.log(JSON.stringify(report, null, 2))
} finally {
  if (child.exitCode === null && !startupError) {
    const closed = new Promise(r => child.once('close', r)); child.kill(); await closed
  }
  rmSync(temp, { recursive: true, force: true })
}
