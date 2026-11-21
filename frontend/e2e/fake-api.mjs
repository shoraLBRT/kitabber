// A fake of the platform API for the e2e tests (docs/TESTING.md: external services only as fakes), as far as the
// realtime channel needs it (E01-11, ADR-0019): POST /api/v1/realtime/tickets trades a bearer token for a hub ticket,
// and /hubs/realtime speaks the SignalR JSON protocol over long polling (negotiate, poll, send, close) to holders of
// a ticket it issued. Control for the tests: GET /__realtime (tickets and connections), POST /__realtime/drop (breaks
// every open connection, as a restart of the API would), POST /__realtime/push (sends an event to every connection).
// Tests run in parallel, so each marks its browser with an `x-e2e-client` header and reads only its own connections
// (`?client=`).
// Registration (E03-01): POST /api/v1/registration, /{id}/code and /{id}/confirmation with the API's answers; the
// confirmation sets the provider's session cookie. The "letters" with the codes are read with GET /__letters?to=
// (the backend tests read the real ones from Mailpit). The resend timer is FAKE_RESEND_SECONDS (default 60), short
// in the e2e run; the address taken@example.com is already taken.
// Sign-in (E03-02): POST /api/v1/sign-in/password, /code and /code/{id}/confirmation; see signIn below.
import { randomBytes, randomUUID } from 'node:crypto'
import { createServer } from 'node:http'

const port = Number(process.env.FAKE_API_PORT ?? 3102)
const hub = '/hubs/realtime'
const separator = '\u001e'
const tickets = new Map() // ticket -> bearer token it was bought with
const connections = new Map() // connection token -> connection
const registrations = new Map() // registration id -> { email, code, sentAt, confirmed }
const letters = [] // { to, code }
const resendSeconds = Number(process.env.FAKE_RESEND_SECONDS ?? 60)

const cors = {
  'access-control-allow-headers':
    'authorization, content-type, x-requested-with, x-signalr-user-agent, x-e2e-client, x-forwarded-for, traceparent',
  'access-control-allow-methods': 'GET, POST, DELETE, OPTIONS',
  'access-control-allow-credentials': 'true',
}

function newCode() {
  return String(Math.floor(Math.random() * 1_000_000)).padStart(6, '0')
}

function sendCode(id, registration) {
  registration.code = newCode()
  registration.sentAt = Date.now()
  letters.push({ to: registration.email, code: registration.code })
  return { registrationId: id, resendAfterSeconds: resendSeconds }
}

function resendLeft(registration) {
  return Math.ceil((registration.sentAt + resendSeconds * 1000 - Date.now()) / 1000)
}

function tooSoon(response, seconds) {
  response.setHeader('retry-after', String(seconds))
  response.setHeader('access-control-expose-headers', 'retry-after')
  return send(response, 429, { status: 429, code: 'code_too_soon' })
}

async function registration(request, response, url) {
  if (url.pathname === '/api/v1/registration') {
    const { email = '', password = '' } = JSON.parse((await readBody(request)) || '{}')
    const errors = {}
    if (!/^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(email)) errors.email = ['Not an email address.']
    if (password.trim().length < 6) errors.password = ['From 6 to 128 characters.']
    if (Object.keys(errors).length > 0)
      return send(response, 400, { status: 400, code: 'validation_failed', errors })
    if (email.toLowerCase() === 'taken@example.com')
      return send(response, 409, { status: 409, code: 'email_taken' })
    const id = randomBytes(16)
      .toString('hex')
      .replace(/(.{8})(.{4})(.{4})(.{4})(.{12})/, '$1-$2-$3-$4-$5')
    const entry = { email, password, confirmed: false }
    registrations.set(id, entry)
    return send(response, 202, sendCode(id, entry))
  }
  const [, id, action] = url.pathname.match(/^\/api\/v1\/registration\/([\w-]+)\/(code|confirmation)$/) ?? []
  const entry = registrations.get(id)
  if (action === 'code') {
    if (!entry || entry.confirmed) return send(response, 404, { status: 404, code: 'registration_not_found' })
    const left = resendLeft(entry)
    return left > 0 ? tooSoon(response, left) : send(response, 202, sendCode(id, entry))
  }
  if (action === 'confirmation') {
    const { code = '' } = JSON.parse((await readBody(request)) || '{}')
    if (!entry || entry.confirmed || code !== entry.code)
      return send(response, 400, { status: 400, code: 'confirmation_invalid' })
    entry.confirmed = true
    passwords.set(entry.email.toLowerCase(), entry.password)
    response.setHeader('set-cookie', 'identity.session=fake-session; Path=/; HttpOnly; SameSite=Lax')
    return send(response, 204)
  }
  return send(response, 404, { status: 404, code: 'not_found' })
}

// Sign-in (E03-02): the password of a confirmed registration, or of reader@example.com ("secret-1"); a code to any
// address, as the API sends one to every address it is asked for. Wrong answers are alike for every address.
const passwords = new Map([['reader@example.com', 'secret-1']]) // email -> password of a confirmed account
const signInCodes = new Map() // email -> { id, code, sentAt }

async function signIn(request, response, url) {
  const body = JSON.parse((await readBody(request)) || '{}')
  if (url.pathname === '/api/v1/sign-in/password') {
    const email = String(body.email ?? '').toLowerCase()
    if (!passwords.has(email) || passwords.get(email) !== body.password)
      return send(response, 400, { status: 400, code: 'invalid_credentials' })
    response.setHeader('set-cookie', 'identity.session=fake-session; Path=/; HttpOnly; SameSite=Lax')
    return send(response, 204)
  }
  if (url.pathname === '/api/v1/sign-in/code') {
    const email = String(body.email ?? '')
    if (!/^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(email))
      return send(response, 400, {
        status: 400,
        code: 'validation_failed',
        errors: { email: ['Not an email address.'] },
      })
    const last = signInCodes.get(email.toLowerCase())
    if (last && resendLeft(last) > 0) return tooSoon(response, resendLeft(last))
    const entry = {
      id: randomBytes(16)
        .toString('hex')
        .replace(/(.{8})(.{4})(.{4})(.{4})(.{12})/, '$1-$2-$3-$4-$5'),
      code: newCode(),
      sentAt: Date.now(),
    }
    signInCodes.set(email.toLowerCase(), entry)
    letters.push({ to: email, code: entry.code })
    return send(response, 202, { challengeId: entry.id, resendAfterSeconds: resendSeconds })
  }
  const [, id] = url.pathname.match(/^\/api\/v1\/sign-in\/code\/([\w-]+)\/confirmation$/) ?? []
  const found = [...signInCodes.entries()].find(([, entry]) => entry.id === id)
  if (!found || found[1].code !== body.code)
    return send(response, 400, { status: 400, code: 'confirmation_invalid' })
  signInCodes.delete(found[0])
  response.setHeader('set-cookie', 'identity.session=fake-session; Path=/; HttpOnly; SameSite=Lax')
  return send(response, 204)
}

function send(response, status, body, type = 'application/json') {
  response.writeHead(status, { ...cors, 'content-type': type, 'cache-control': 'no-store' })
  response.end(body === undefined ? undefined : typeof body === 'string' ? body : JSON.stringify(body))
}

function expired(jwt) {
  try {
    const { exp } = JSON.parse(Buffer.from(jwt.split('.')[1] ?? '', 'base64url').toString())
    return typeof exp === 'number' && exp * 1000 <= Date.now()
  } catch {
    return false
  }
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

// Blogs (E04-02, ADR-0025): GET /api/v1/profiles/{id}, /by-nickname/{nickname} and /me. `anna` belongs to the account
// of the fake provider (FAKE_ACCOUNT there), so a signed-in test is her owner and sees `registeredAt`; `boris` is
// someone else's; one blog has no nickname yet; the nickname `broken` fails as an unavailable API would.
const owner = '0195f3a0-0000-7000-8000-000000000001'
const blogs = [
  {
    id: '0195f3a0-0000-7000-8000-0000000000b1',
    owner,
    type: 'personal',
    name: 'Анна',
    nickname: 'anna',
    description: 'Пишу о книгах и городах.',
    registeredAt: '2026-03-14T10:00:00Z',
  },
  {
    id: '0195f3a0-0000-7000-8000-0000000000b2',
    owner: '0195f3a0-0000-7000-8000-000000000002',
    type: 'personal',
    name: 'Борис',
    nickname: 'boris',
    description: null,
    registeredAt: '2026-05-01T08:30:00Z',
  },
  {
    id: '0195f3a0-0000-7000-8000-0000000000b3',
    owner: '0195f3a0-0000-7000-8000-000000000003',
    type: 'personal',
    name: null,
    nickname: null,
    description: null,
    registeredAt: '2026-06-01T08:30:00Z',
  },
]

/** The `sub` of the bearer token (the fake provider's JWT; the fake does not check the signature). */
function subject(request) {
  try {
    return JSON.parse(Buffer.from(bearer(request).split('.')[1], 'base64url').toString()).sub
  } catch {
    return null
  }
}

// Editing the own profile (E04-03): PUT /api/v1/profiles/me and POST /api/v1/media. Tests run in parallel against
// this one fake, so an edit is kept per client address (`x-forwarded-for`, which the site passes on to the API) and
// only requests from that address see it; the stored blogs never change. The name `сбой` fails as an unavailable API
// would. GET /__uploads?client= lists the files a client uploaded, with whether each one is a JPEG.
const edits = new Map() // client address -> { name, description, avatarId }
const uploads = [] // { client, id, jpeg, size }

function clientOf(request) {
  return String(request.headers['x-forwarded-for'] ?? '')
}

function edited(blog, request) {
  const edit = clientOf(request) && edits.get(clientOf(request))
  return edit && blog.owner === owner ? { ...blog, ...edit } : { avatarId: null, ...blog }
}

function readBytes(request) {
  return new Promise((resolve) => {
    const chunks = []
    request.on('data', (chunk) => chunks.push(chunk))
    request.on('end', () => resolve(Buffer.concat(chunks)))
  })
}

const graphemes = new Intl.Segmenter('ru', { granularity: 'grapheme' })
const length = (text) => [...graphemes.segment((text ?? '').trim())].length
const clean = (text) => (text ?? '').trim() || null

async function editProfile(request, response) {
  const reader = subject(request)
  const own = blogs.find((blog) => reader && blog.owner === reader)
  if (!own) return send(response, 401, { status: 401, code: 'unauthorized' })
  const input = JSON.parse((await readBody(request)) || '{}')
  if (input.name?.trim() === 'сбой') return send(response, 500, { status: 500, code: 'internal' })
  const errors = {}
  if (length(input.name) > 25) errors.name = ['Название — не длиннее 25 символов.']
  if (length(input.description) > 160) errors.description = ['Описание — не длиннее 160 символов.']
  if (input.avatarId && !uploads.some((file) => file.id === input.avatarId))
    errors.avatarId = ['Такого загруженного файла нет.']
  if (Object.keys(errors).length > 0)
    return send(response, 400, { status: 400, code: 'validation_failed', errors })
  edits.set(clientOf(request), {
    name: clean(input.name),
    description: clean(input.description),
    avatarId: input.avatarId ?? null,
  })
  // The owner's own view: `owner` is the fake's, not the API's (undefined drops it from the JSON).
  return send(response, 200, { ...edited(own, request), owner: undefined })
}

async function uploadMedia(request, response) {
  if (!subject(request)) return send(response, 401, { status: 401, code: 'unauthorized' })
  const body = await readBytes(request)
  // The part's content starts after the blank line that ends its headers.
  const start = body.indexOf('\r\n\r\n') + 4
  const jpeg = body[start] === 0xff && body[start + 1] === 0xd8 && body[start + 2] === 0xff
  if (!jpeg) return send(response, 415, { status: 415, code: 'media_type_unsupported' })
  const file = { client: clientOf(request), id: randomUUID(), jpeg, size: body.length }
  uploads.push(file)
  return send(response, 201, {
    id: file.id,
    kind: 'image',
    contentType: 'image/jpeg',
    size: file.size,
    fileName: 'avatar.jpg',
    createdAt: new Date().toISOString(),
  })
}

function profile(request, response, url) {
  const reader = subject(request)
  const view = (blog) => {
    const { owner: blogOwner, registeredAt, ...rest } = edited(blog, request)
    return { ...rest, registeredAt: blogOwner === reader ? registeredAt : null }
  }
  const [, kind, value] = url.pathname.match(/^\/api\/v1\/profiles\/(?:(by-nickname)\/)?([^/]+)$/) ?? []
  if (value === 'me' && !kind) {
    const own = blogs.find((blog) => reader && blog.owner === reader)
    return own ? send(response, 200, view(own)) : send(response, 401, { status: 401, code: 'unauthorized' })
  }
  const nickname = decodeURIComponent(value ?? '')
    .replace(/^@/, '')
    .toLowerCase()
  if (kind && nickname === 'broken') return send(response, 500, { status: 500, code: 'internal' })
  const found = blogs.find((blog) => (kind ? blog.nickname === nickname : blog.id === value))
  return found
    ? send(response, 200, view(found))
    : send(response, 404, { status: 404, code: 'profile_not_found' })
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
  // Credentialed requests (the registration's cookie) need the caller's origin, not '*'.
  response.setHeader('access-control-allow-origin', request.headers.origin ?? '*')
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

  if (url.pathname === '/__letters' && request.method === 'GET') {
    const to = url.searchParams.get('to')
    return send(
      response,
      200,
      letters.filter((letter) => letter.to === to),
    )
  }
  if (url.pathname.startsWith('/api/v1/sign-in/') && request.method === 'POST')
    return signIn(request, response, url)
  if (url.pathname.startsWith('/api/v1/registration') && request.method === 'POST')
    return registration(request, response, url)

  if (url.pathname === '/api/v1/profiles/me' && request.method === 'PUT')
    return editProfile(request, response)
  if (url.pathname.startsWith('/api/v1/profiles/') && request.method === 'GET')
    return profile(request, response, url)
  if (url.pathname === '/api/v1/media' && request.method === 'POST') return uploadMedia(request, response)
  if (url.pathname === '/__uploads' && request.method === 'GET')
    return send(
      response,
      200,
      uploads.filter((file) => file.client === url.searchParams.get('client')),
    )

  if (url.pathname === '/api/v1/realtime/tickets' && request.method === 'POST') {
    const token = bearer(request)
    // An expired access token is refused, as the real API does: the site must renew it first (E03-03).
    if (!token || expired(token)) return send(response, 401, { status: 401, code: 'unauthorized' })
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
