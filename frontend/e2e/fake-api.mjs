// A fake of the platform API for the e2e tests (docs/TESTING.md: external services only as fakes), as far as the
// realtime channel needs it (E01-11, ADR-0019): POST /api/v1/realtime/tickets trades a bearer token for a hub ticket,
// and /hubs/realtime speaks the SignalR JSON protocol over long polling (negotiate, poll, send, close) to holders of
// a ticket it issued. Control for the tests: GET /__realtime (tickets and connections), POST /__realtime/drop (breaks
// every open connection, as a restart of the API would), POST /__realtime/push (sends an event to every connection).
// Tests run in parallel, so each marks its browser with an `x-e2e-client` header and reads only its own connections
// (`?client=`).
import { randomBytes } from 'node:crypto'
import { createServer } from 'node:http'

const port = Number(process.env.FAKE_API_PORT ?? 3102)
const hub = '/hubs/realtime'
const separator = '\u001e'
const tickets = new Map() // ticket -> bearer token it was bought with
const connections = new Map() // connection token -> connection

const cors = {
  'access-control-allow-origin': '*',
  'access-control-allow-headers':
    'authorization, content-type, x-requested-with, x-signalr-user-agent, x-e2e-client',
  'access-control-allow-methods': 'GET, POST, DELETE, OPTIONS',
}

function send(response, status, body, type = 'application/json') {
  response.writeHead(status, { ...cors, 'content-type': type, 'cache-control': 'no-store' })
  response.end(body === undefined ? undefined : typeof body === 'string' ? body : JSON.stringify(body))
}

function bearer(request) {
  const header = request.headers.authorization ?? ''
  return header.startsWith('Bearer ') ? header.slice(7) : ''
}

function readBody(request) {
  return new Promise((resolve) => {
    let body = ''
    request.on('data', (chunk) => (body += chunk))
    request.on('end', () => resolve(body))
  })
}

function enqueue(connection, message) {
  if (!connection.open) return
  connection.queue.push(JSON.stringify(message) + separator)
  flush(connection)
}

function flush(connection) {
  if (!connection.poll) return
  if (!connection.open) {
    send(connection.poll, 204)
    connection.poll = null
    return
  }
  if (connection.queue.length === 0) return
  send(connection.poll, 200, connection.queue.join(''), 'text/plain')
  connection.queue = []
  connection.poll = null
}

function close(connection) {
  connection.open = false
  clearInterval(connection.ping)
  flush(connection)
}

function handleMessages(connection, body) {
  for (const raw of body.split(separator).filter(Boolean)) {
    const message = JSON.parse(raw)
    if (!connection.handshake) {
      connection.handshake = true
      enqueue(connection, {})
    } else if (message.type === 1) {
      if (message.target === 'Subscribe') connection.topics.add(message.arguments[0])
      if (message.target === 'Unsubscribe') connection.topics.delete(message.arguments[0])
      if (message.invocationId)
        enqueue(connection, { type: 3, invocationId: message.invocationId, result: null })
    } else if (message.type === 7) {
      close(connection)
    }
  }
}

function state(client) {
  return {
    connections: [...connections.values()]
      .filter((connection) => !client || connection.client === client)
      .map(({ ticket, open, handshake, topics }) => ({
        ticket,
        open,
        handshake,
        topics: [...topics],
      })),
  }
}

createServer(async (request, response) => {
  const url = new URL(request.url ?? '/', `http://127.0.0.1:${port}`)
  if (request.method === 'OPTIONS') return send(response, 204)

  if (url.pathname === '/') return send(response, 200, { ok: true })
  const client = url.searchParams.get('client')
  if (url.pathname === '/__realtime' && request.method === 'GET') return send(response, 200, state(client))
  if (url.pathname === '/__realtime/drop' && request.method === 'POST') {
    for (const connection of connections.values())
      if (connection.open && (!client || connection.client === client)) close(connection)
    return send(response, 200, state(client))
  }
  if (url.pathname === '/__realtime/push' && request.method === 'POST') {
    const event = JSON.parse(await readBody(request))
    for (const connection of connections.values())
      if (!client || connection.client === client)
        enqueue(connection, { type: 1, target: 'Receive', arguments: [event] })
    return send(response, 200, state(client))
  }

  if (url.pathname === '/api/v1/realtime/tickets' && request.method === 'POST') {
    const token = bearer(request)
    if (!token) return send(response, 401, { status: 401, code: 'unauthorized' })
    const ticket = randomBytes(32).toString('base64url')
    tickets.set(ticket, token)
    return send(response, 200, { ticket, expiresAt: new Date(Date.now() + 60_000).toISOString() })
  }

  if (url.pathname === `${hub}/negotiate` && request.method === 'POST') {
    const ticket = bearer(request) || url.searchParams.get('access_token')
    if (!tickets.has(ticket)) return send(response, 401)
    const token = randomBytes(16).toString('base64url')
    connections.set(token, {
      client: request.headers['x-e2e-client'] ?? null,
      ticket,
      open: true,
      handshake: false,
      topics: new Set(),
      queue: [],
      poll: null,
    })
    return send(response, 200, {
      negotiateVersion: 1,
      connectionId: randomBytes(16).toString('base64url'),
      connectionToken: token,
      availableTransports: [{ transport: 'LongPolling', transferFormats: ['Text', 'Binary'] }],
    })
  }

  if (url.pathname === hub) {
    const connection = connections.get(url.searchParams.get('id'))
    if (!connection) return send(response, 404)
    if (!tickets.has(bearer(request) || url.searchParams.get('access_token'))) return send(response, 401)
    if (request.method === 'GET') {
      if (!connection.open) return send(response, 204)
      if (!connection.ping) {
        // The first poll answers at once; then the server pings well inside the client's 30 s timeout.
        connection.ping = setInterval(() => enqueue(connection, { type: 6 }), 5_000)
        return send(response, 200, '', 'text/plain')
      }
      connection.poll = response
      return flush(connection)
    }
    if (request.method === 'POST') {
      handleMessages(connection, await readBody(request))
      return send(response, 200, '', 'text/plain')
    }
    if (request.method === 'DELETE') {
      close(connection)
      return send(response, 202, '', 'text/plain')
    }
  }

  send(response, 404, { status: 404, code: 'not_found' })
}).listen(port, '127.0.0.1')
