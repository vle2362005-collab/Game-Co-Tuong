import { ref, shallowRef } from 'vue'
export interface VideoSignal { roomId: string; senderId: string; recipientId: string; callId: string; type: 'offer' | 'answer' | 'ice' | 'hangup' | 'reject'; data: string }
export interface VideoDependencies {
  send: (signal: Omit<VideoSignal, 'senderId' | 'roomId'>) => Promise<void>
  media?: () => Promise<MediaStream>
  peer?: () => RTCPeerConnection
}
const DEFAULT_ICE_SERVERS: RTCIceServer[] = [{ urls: 'stun:stun.l.google.com:19302' }]

export function resolveIceConfiguration(
  serversValue: string | undefined = import.meta.env.VITE_ICE_SERVERS,
  policyValue: string | undefined = import.meta.env.VITE_ICE_TRANSPORT_POLICY
): Pick<RTCConfiguration, 'iceServers' | 'iceTransportPolicy'> {
  let iceServers = DEFAULT_ICE_SERVERS
  if (serversValue?.trim()) {
    let parsed: unknown
    try {
      parsed = JSON.parse(serversValue)
    } catch {
      throw new Error('VITE_ICE_SERVERS must be a valid JSON array.')
    }
    if (!Array.isArray(parsed) || parsed.length === 0 || !parsed.every(server =>
      typeof server === 'object' && server !== null &&
      (typeof server.urls === 'string' && server.urls.length > 0 ||
        Array.isArray(server.urls) && server.urls.length > 0 && server.urls.every((url: unknown) => typeof url === 'string' && url.length > 0)) &&
      (server.username === undefined || typeof server.username === 'string') &&
      (server.credential === undefined || typeof server.credential === 'string')
    )) {
      throw new Error('VITE_ICE_SERVERS must be a non-empty array of valid RTCIceServer entries.')
    }
    iceServers = parsed as RTCIceServer[]
  }

  const iceTransportPolicy = policyValue?.trim() || 'all'
  if (iceTransportPolicy !== 'all' && iceTransportPolicy !== 'relay') {
    throw new Error('VITE_ICE_TRANSPORT_POLICY must be "all" or "relay".')
  }
  return { iceServers, iceTransportPolicy }
}

export function createVideoCall(deps: VideoDependencies) {
  const status = ref('idle'), error = ref(''), incoming = shallowRef<VideoSignal | null>(null)
  const local = shallowRef<MediaStream | null>(null), remote = shallowRef<MediaStream | null>(null)
  const mic = ref(true), camera = ref(true)
  let pc: RTCPeerConnection | null = null, callId: string | null = null, peerId: string | null = null
  let candidates: RTCIceCandidateInit[] = [], outgoing: RTCIceCandidateInit[] = [], signaled = false, generation = 0
  let timer: ReturnType<typeof setTimeout> | null = null
  function clear() {
    generation++; if (timer) clearTimeout(timer); timer = null
    if (pc) { pc.onicecandidate = null; pc.ontrack = null; pc.onconnectionstatechange = null; pc.close() }
    pc = null; local.value?.getTracks().forEach(t => t.stop()); local.value = null; remote.value = null
    callId = null; peerId = null; incoming.value = null; candidates = []; outgoing = []; signaled = false
    mic.value = true; camera.value = true; status.value = 'idle'
  }
  async function send(type: VideoSignal['type'], data: unknown = {}) {
    if (callId && peerId) await deps.send({ callId, recipientId: peerId, type, data: JSON.stringify(data) })
  }
  async function hangup(notify = true) {
    const activeId = callId, target = peerId
    clear()
    if (notify && activeId && target) await deps.send({ callId: activeId, recipientId: target, type: 'hangup', data: '{}' }).catch(() => {})
  }
  function fail(e: unknown) {
    const name = e instanceof Error ? e.name : ''
    error.value = name === 'NotAllowedError' ? 'Bạn chưa cấp quyền camera/micro.' :
      name === 'NotFoundError' ? 'Không tìm thấy camera hoặc micro.' :
      name === 'NotReadableError' ? 'Camera hoặc micro đang bận.' : e instanceof Error ? e.message : String(e)
  }
  function timeout() { timer = setTimeout(() => { error.value = 'Cuộc gọi chưa kết nối. Hãy thử lại.'; void hangup() }, 45000) }
  async function prepare(expectedGeneration: number) {
    if (!deps.media && !navigator.mediaDevices?.getUserMedia) throw new Error('Camera cần HTTPS hoặc localhost.')
    const stream = await (deps.media?.() ?? navigator.mediaDevices.getUserMedia({ audio: true, video: { width: 640, height: 360 } }))
    if (generation !== expectedGeneration) { stream.getTracks().forEach(t => t.stop()); return null }
    local.value = stream
    const configuration = resolveIceConfiguration()
    const connection = deps.peer?.() ?? new RTCPeerConnection(configuration)
    pc = connection
    stream.getTracks().forEach(t => connection.addTrack(t, stream))
    connection.ontrack = e => { remote.value = e.streams[0] || new MediaStream([e.track]) }
    connection.onicecandidate = e => {
      if (!e.candidate) return
      const candidate = e.candidate.toJSON()
      if (!signaled) outgoing.push(candidate)
      else void send('ice', candidate).catch(fail)
    }
    connection.onconnectionstatechange = () => {
      if (connection.connectionState === 'connected') { status.value = 'connected'; if (timer) clearTimeout(timer) }
      else if (connection.connectionState === 'failed') { error.value = 'Không kết nối được video. Kiểm tra cấu hình mạng/TURN.'; void hangup() }
    }
    return connection
  }
  async function flush() {
    signaled = true
    for (const candidate of outgoing.splice(0)) await send('ice', candidate)
    if (pc?.remoteDescription) for (const candidate of candidates.splice(0)) await pc.addIceCandidate(candidate)
  }
  async function start(opponentId: string) {
    if (status.value !== 'idle') return
    error.value = ''; callId = crypto.randomUUID(); peerId = opponentId; status.value = 'preparing'
    const expected = generation
    try {
      const connection = await prepare(expected); if (!connection) return
      await connection.setLocalDescription(await connection.createOffer())
      if (generation !== expected) return
      await send('offer', connection.localDescription)
      if (generation !== expected) return
      status.value = 'calling'; timeout(); await flush()
    } catch (e) { if (generation === expected) { fail(e); await hangup() } }
  }
  async function accept() {
    const offer = incoming.value
    if (!offer || status.value !== 'incoming') return
    status.value = 'preparing'; error.value = ''; const expected = generation
    try {
      const connection = await prepare(expected); if (!connection) return
      await connection.setRemoteDescription(JSON.parse(offer.data))
      await connection.setLocalDescription(await connection.createAnswer())
      if (generation !== expected) return
      await send('answer', connection.localDescription)
      if (generation !== expected) return
      incoming.value = null; status.value = connection.connectionState === 'connected' ? 'connected' : 'connecting'
      if (status.value !== 'connected') timeout()
      await flush()
    } catch (e) { if (generation === expected) { fail(e); await hangup() } }
  }
  async function decline() { await send('reject').catch(() => {}); clear() }
  async function receive(signal: VideoSignal) {
    if (signal.type === 'offer') {
      if (status.value !== 'idle') return
      callId = signal.callId; peerId = signal.senderId; incoming.value = signal; status.value = 'incoming'; timeout(); return
    }
    if (signal.callId !== callId || signal.senderId !== peerId) return
    try {
      if (signal.type === 'hangup' || signal.type === 'reject') { clear(); return }
      if (signal.type === 'ice') {
        const candidate = JSON.parse(signal.data) as RTCIceCandidateInit
        if (pc?.remoteDescription) await pc.addIceCandidate(candidate)
        else if (candidates.length < 100) candidates.push(candidate)
      } else if (signal.type === 'answer' && pc && status.value === 'calling') {
        await pc.setRemoteDescription(JSON.parse(signal.data)); status.value = pc.connectionState === 'connected' ? 'connected' : 'connecting'; await flush()
      }
    } catch (e) { fail(e); await hangup() }
  }
  function toggle(kind: 'audio' | 'video') {
    const target = kind === 'audio' ? mic : camera
    target.value = !target.value
    local.value?.getTracks().filter(t => t.kind === kind).forEach(t => { t.enabled = target.value })
  }
  return { status, error, incoming, local, remote, mic, camera, start, accept, decline, receive, hangup, toggle }
}
