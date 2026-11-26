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
// Sign-in (E03-02): POST /api/v1/sign-in/password, /code and /code/{id}/confirmation; see signIn below. The captcha
// (E03-12): GET /api/v1/sign-in/captcha.
// Password (E03-05): POST /api/v1/password-reset and /{id}/confirmation, PUT /api/v1/account/password.
// Email (E03-06): GET/PUT /api/v1/account/email and POST /api/v1/account/email/confirmation; see accountEmailRoute.
// Devices (E03-09): GET /api/v1/account/sessions, DELETE /{id}, POST /sign-out-others; see accountSessionsRoute.
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
  'access-control-allow-methods': 'GET, POST, PATCH, DELETE, OPTIONS',
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
// The captcha (E03-12): after three failed passwords to an address every attempt needs the fake provider's solution,
// until one succeeds. The real thresholds per address and per network are tested by the backend (SignInTests).
const failedPasswords = new Map() // email -> failures
const captchaSolution = 'fake-captcha-solved'

async function signIn(request, response, url) {
  const body = JSON.parse((await readBody(request)) || '{}')
  if (url.pathname === '/api/v1/sign-in/password') {
    const email = String(body.email ?? '').toLowerCase()
    const failures = failedPasswords.get(email) ?? 0
    if (failures >= 3 && body.captcha !== captchaSolution)
      return send(response, 400, { status: 400, code: 'captcha_required' })
    if (!passwords.has(email) || passwords.get(email) !== body.password) {
      failedPasswords.set(email, failures + 1)
      return send(response, 400, { status: 400, code: 'invalid_credentials' })
    }
    failedPasswords.delete(email)
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

// Password reset (E03-05): a letter only to an address with a password (see `passwords` above), the same answer for
// every address; the new password replaces the old one and the answer sets the provider's session cookie.
const passwordResets = new Map() // email -> { id, code, sentAt }

async function passwordReset(request, response, url) {
  const body = JSON.parse((await readBody(request)) || '{}')
  if (url.pathname === '/api/v1/password-reset') {
    const email = String(body.email ?? '').toLowerCase()
    if (!/^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(email))
      return send(response, 400, {
        status: 400,
        code: 'validation_failed',
        errors: { email: ['Not an email address.'] },
      })
    const last = passwordResets.get(email)
    if (last && resendLeft(last) > 0) return tooSoon(response, resendLeft(last))
    const entry = {
      id: randomBytes(16)
        .toString('hex')
        .replace(/(.{8})(.{4})(.{4})(.{4})(.{12})/, '$1-$2-$3-$4-$5'),
      code: newCode(),
      sentAt: Date.now(),
    }
    passwordResets.set(email, entry)
    if (passwords.has(email)) letters.push({ to: email, code: entry.code })
    return send(response, 202, { resetId: entry.id, resendAfterSeconds: resendSeconds })
  }
  const [, id] = url.pathname.match(/^\/api\/v1\/password-reset\/([\w-]+)\/confirmation$/) ?? []
  if (String(body.password ?? '').trim().length < 6)
    return send(response, 400, {
      status: 400,
      code: 'validation_failed',
      errors: { password: ['From 6 to 128 characters.'] },
    })
  const found = [...passwordResets.entries()].find(([, entry]) => entry.id === id)
  if (!found || !passwords.has(found[0]) || found[1].code !== body.code)
    return send(response, 400, { status: 400, code: 'confirmation_invalid' })
  passwordResets.delete(found[0])
  passwords.set(found[0], body.password)
  response.setHeader('set-cookie', 'identity.session=fake-session; Path=/; HttpOnly; SameSite=Lax')
  return send(response, 204)
}

// Changing the password (E03-05) of the fake provider's account: "wrong-password" is never its current one.
async function changePassword(request, response) {
  if (!subject(request)) return send(response, 401, { status: 401, code: 'unauthorized' })
  const body = JSON.parse((await readBody(request)) || '{}')
  if (String(body.newPassword ?? '').trim().length < 6)
    return send(response, 400, {
      status: 400,
      code: 'validation_failed',
      errors: { newPassword: ['From 6 to 128 characters.'] },
    })
  if (!body.currentPassword || body.currentPassword === 'wrong-password')
    return send(response, 400, { status: 400, code: 'current_password_invalid' })
  return send(response, 204)
}

// The account's email (E03-06): GET/PUT /api/v1/account/email and POST /api/v1/account/email/confirmation with the
// API's answers. Kept per client address (`x-forwarded-for`), as the profile edits are; the fake provider's account
// starts with the confirmed reader@example.com. taken@example.com is another account's, the code goes to the new
// address and, once it is entered, a notice (code null) to the old one.
const accountEmails = new Map() // client address -> { email, confirmedAt, pendingEmail, code }

function accountEmail(request) {
  const client = clientOf(request)
  if (!accountEmails.has(client))
    accountEmails.set(client, {
      email: 'reader@example.com',
      confirmedAt: '2026-11-01T09:00:00Z',
      pendingEmail: null,
      code: null,
    })
  return accountEmails.get(client)
}

function emailView({ email, confirmedAt, pendingEmail }) {
  return { email, confirmedAt, pendingEmail }
}

async function accountEmailRoute(request, response, url) {
  if (!subject(request)) return send(response, 401, { status: 401, code: 'unauthorized' })
  const entry = accountEmail(request)
  if (request.method === 'GET') return send(response, 200, emailView(entry))
  const body = JSON.parse((await readBody(request)) || '{}')
  if (url.pathname.endsWith('/confirmation')) {
    if (!entry.pendingEmail || !entry.code || body.code !== entry.code)
      return send(response, 400, { status: 400, code: 'confirmation_invalid' })
    letters.push({ to: entry.email, code: null })
    Object.assign(entry, {
      email: entry.pendingEmail,
      confirmedAt: new Date().toISOString(),
      pendingEmail: null,
      code: null,
    })
    return send(response, 204)
  }
  const email = String(body.email ?? '').trim()
  if (!/^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(email))
    return send(response, 400, {
      status: 400,
      code: 'validation_failed',
      errors: { email: ['Not an email address.'] },
    })
  if (email.toLowerCase() === entry.email.toLowerCase()) return send(response, 204)
  if (email.toLowerCase() === 'taken@example.com')
    return send(response, 409, { status: 409, code: 'email_taken', title: 'Another account has this email.' })
  entry.pendingEmail = email
  entry.code = String(Math.floor(100000 + Math.random() * 900000))
  letters.push({ to: email, code: entry.code })
  return send(response, 202)
}

// The devices of the account (E03-09): GET /api/v1/account/sessions, DELETE /api/v1/account/sessions/{id} and
// POST /api/v1/account/sessions/sign-out-others with the API's answers. Kept per client address (`x-forwarded-for`),
// as the email is: the fake provider's account starts signed in on this device and on two others.
const accountSessions = new Map() // client address -> sessions, the current one first

function sessionsOf(request) {
  const client = clientOf(request)
  if (!accountSessions.has(client))
    accountSessions.set(client, [
      {
        id: randomUUID(),
        device: 'Chrome, Windows',
        createdAt: '2026-11-20T10:00:00Z',
        lastActiveAt: new Date().toISOString(),
        current: true,
      },
      {
        id: randomUUID(),
        device: 'Safari, iOS',
        createdAt: '2026-11-18T08:00:00Z',
        lastActiveAt: '2026-11-25T21:15:00Z',
        current: false,
      },
      {
        id: randomUUID(),
        device: 'Firefox, Linux',
        createdAt: '2026-11-02T12:00:00Z',
        lastActiveAt: '2026-11-10T07:40:00Z',
        current: false,
      },
    ])
  return accountSessions.get(client)
}

function accountSessionsRoute(request, response, url) {
  if (!subject(request)) return send(response, 401, { status: 401, code: 'unauthorized' })
  const client = clientOf(request)
  const sessions = sessionsOf(request)
  if (request.method === 'GET') return send(response, 200, sessions)
  if (url.pathname.endsWith('/sign-out-others')) {
    accountSessions.set(
      client,
      sessions.filter((s) => s.current),
    )
    return send(response, 200, { ended: sessions.length - 1 })
  }
  const id = decodeURIComponent(url.pathname.split('/').at(-1))
  if (!sessions.some((s) => s.id === id && !s.current))
    return send(response, 404, { status: 404, code: 'session_not_found' })
  accountSessions.set(
    client,
    sessions.filter((s) => s.id !== id),
  )
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
  {
    id: '0195f3a0-0000-7000-8000-0000000000b4',
    owner: '0195f3a0-0000-7000-8000-000000000002',
    type: 'topic',
    name: 'Книги',
    nickname: 'books',
    description: 'Что читать и как читать.',
    registeredAt: null,
  },
  // Topics of the topic page (E11-01, ADR-0082): «Наука» has no posts yet, and the feed of «Сбой» fails.
  {
    id: '0195f3a0-0000-7000-8000-0000000000b5',
    owner: '0195f3a0-0000-7000-8000-000000000001',
    type: 'topic',
    name: 'Наука',
    nickname: 'science',
    description: null,
    registeredAt: null,
  },
  {
    id: '0195f3a0-0000-7000-8000-0000000000b6',
    owner: '0195f3a0-0000-7000-8000-000000000001',
    type: 'topic',
    name: 'Сбой',
    nickname: 'outage',
    description: null,
    registeredAt: null,
  },
]

// The first post carries every block type of schema version 1 and a block of an unknown type (E07-03, ADR-0045).
const extraBlocks = {
  '0195f3a0-0000-7000-8000-0000000000c1': [
    { id: 'b2', type: 'header', data: { text: 'Глава первая', level: 2 } },
    {
      id: 'b3',
      type: 'paragraph',
      data: {
        text: 'Текст с <b>жирным</b> и <a href="https://example.org" rel="nofollow ugc noopener noreferrer">ссылкой</a>.',
      },
    },
    { id: 'b4', type: 'header', data: { text: 'Раздел', level: 3 } },
    {
      id: 'b5',
      type: 'list',
      data: { style: 'ordered', items: ['Раз', 'Вложенный', 'Два'], levels: [0, 1, 0] },
    },
    { id: 'b6', type: 'quote', data: { text: 'Краткость — сестра таланта.', caption: 'А. П. Чехов' } },
    { id: 'b7', type: 'delimiter', data: {} },
    { id: 'b8', type: 'gallery', data: {} },
    // A longread's length, so the action panel has a page to stay fixed over (E07-04).
    ...Array.from({ length: 30 }, (_, n) => ({
      id: `p${n}`,
      type: 'paragraph',
      data: {
        text: `Абзац ${n + 1} длинного поста: текст, который читают, прокручивая страницу вниз. ${'Ещё одна фраза абзаца. '.repeat(4)}`,
      },
    })),
  ],
}

// Posts (E07-01, E07-02): GET /api/v1/posts/{id}. The slug is what the API gives (PostSlug); a post without a title has
// none. Two posts of anna (with a title and a short one) and one by boris in the topic `books` (E07-04: its author is
// not the blog).
const posts = [
  ['0195f3a0-0000-7000-8000-0000000000c1', 0, 'Привет, мир', 'privet-mir', 'Первый абзац поста.'],
  ['0195f3a0-0000-7000-8000-0000000000c2', 0, null, null, 'Короткий пост без заголовка.'],
  ['0195f3a0-0000-7000-8000-0000000000c3', 3, 'Книги осени', 'knigi-oseni', 'Что почитать в ноябре.'],
].map(([id, blog, title, slug, text]) => ({
  id,
  blogId: blogs[blog].id,
  authorAccountId: blogs[blog].owner ?? blogs[1].owner,
  title,
  slug,
  body: { version: 1, blocks: [{ id: 'b1', type: 'paragraph', data: { text } }, ...(extraBlocks[id] ?? [])] },
  status: 'published',
  origin: 'native',
  source: null,
  createdAt: '2026-11-20T10:00:00Z',
  updatedAt: '2026-11-20T10:00:00Z',
  publishedAt: '2026-11-20T10:00:00Z',
  images: {},
  imagePreviews: {},
  viewCount: id.endsWith('c1') ? 1234 : 0,
}))

// A post of Arabic quotes (E02-14, ADR-0068): an Arabic block with harakat, an Arabic block with a Russian word, a
// Russian block with an Arabic quote and digits after it, an Arabic quote and list. It stays out of the feed: it is one
// of the `written` posts below (found by its id, no slug), so the seeded feed and blog tests do not see it.
const arabicPost = {
  id: '0195f3a0-0000-7000-8000-0000000000c9',
  blogId: blogs[0].id,
  authorAccountId: blogs[0].owner,
  title: 'Цитаты: «الحمد لله» 2 раза',
  slug: null,
  body: {
    version: 1,
    blocks: [
      { id: 'a1', type: 'paragraph', data: { text: 'بِسْمِ اللَّهِ الرَّحْمَٰنِ الرَّحِيمِ' } },
      { id: 'a2', type: 'paragraph', data: { text: 'قال: «Привет»، ثم ذهب.' } },
      { id: 'a3', type: 'paragraph', data: { text: 'Скажи «<b>سبحان</b> الله» 33 раза, да.' } },
      { id: 'a4', type: 'quote', data: { text: 'العِلْمُ نُورٌ', caption: 'Пословица' } },
      { id: 'a5', type: 'list', data: { style: 'ordered', items: ['واحد', 'اثنان'], levels: [0, 0] } },
    ],
  },
  status: 'published',
  origin: 'native',
  source: null,
  createdAt: '2026-11-20T10:00:00Z',
  updatedAt: '2026-11-20T10:00:00Z',
  publishedAt: '2026-11-20T10:00:00Z',
  images: {},
  viewCount: 0,
}

function post(response, url) {
  const [, id] = url.pathname.match(/^\/api\/v1\/posts\/([^/]+)$/) ?? []
  const found = posts.find((item) => item.id === id) ?? written.get(id)
  return found
    ? send(response, 200, found)
    : send(response, 404, { status: 404, code: 'posts.post_not_found' })
}

// Comments (E09-02, ADR-0048, ADR-0054): GET /api/v1/posts/{postId}/comments and /api/v1/comments/{id}/branch with
// `?levels=`, in the order of the tree, each comment with the number of replies under it at any depth. The titled post
// has two threads, one seven levels deep, and a guest comment; the short one none; the post in the topic a thousand
// (250 threads of four levels, for the test of a big tree). The comments are long past the 15 minutes their author
// may change them in (E09-05): anna's take only «Удалить»; «Вторая ветка» was changed («изменено»).
const editMinutes = 15
const commentPost = (id, postId, parent, n, text, author) => {
  const createdAt = new Date(Date.UTC(2026, 10, 20, 12, 0, n))
  return {
    id,
    postId,
    parentId: parent?.id ?? null,
    depth: (parent?.depth ?? 0) + 1,
    kind: author ? 'native' : 'guest',
    authorAccountId: author ?? null,
    guest: author ? null : { name: 'Гость из канала', url: 'https://t.me/example/1' },
    text,
    createdAt: createdAt.toISOString(),
    editedAt: null,
    editableUntil: author ? new Date(createdAt.getTime() + editMinutes * 60_000).toISOString() : null,
    deleted: false,
  }
}
const commentId = (n) => `0195f3a0-0000-7000-8000-${n.toString(16).padStart(12, '0')}`
const comments = (() => {
  const list = []
  const add = (postId, parent, text, author) => {
    const comment = commentPost(commentId(0xd00000 + list.length), postId, parent, list.length, text, author)
    list.push(comment)
    return comment
  }
  const titledPost = posts[0].id
  const anna = blogs[0].owner
  const boris = blogs[1].owner
  let deep = add(titledPost, null, 'Первый уровень', anna)
  const levelNames = ['Второй', 'Третий', 'Четвёртый', 'Пятый', 'Шестой', 'Седьмой']
  for (const [n, name] of levelNames.entries()) {
    deep = add(titledPost, deep, `${name} уровень`, n % 2 ? anna : boris)
  }
  const second = add(titledPost, null, 'Вторая ветка', boris)
  second.editedAt = new Date(Date.parse(second.createdAt) + 5 * 60_000).toISOString()
  add(titledPost, second, 'Ответ во второй ветке', anna)
  add(titledPost, second, 'Ещё ответ во второй ветке', anna)
  add(titledPost, null, 'Комментарий из обсуждения канала', undefined)
  const bigPost = posts[2].id
  for (let n = 0; n < 250; n++) {
    let parent = null
    for (let level = 0; level < 4; level++) {
      parent = add(bigPost, parent, `Комментарий ${n + 1}.${level + 1}`, level % 2 ? anna : boris)
    }
  }
  // The Arabic post (E02-14): a Russian comment with an Arabic quote and digits after it.
  add(arabicPost.id, null, 'Согласен: «جزاك الله خيرا» 100 раз.', boris)
  return list
})()

// What a read shows, as CommentTree.Shown does: `levels` levels below the start, with every reply counted.
function shownComments(ordered, start, levels) {
  return ordered
    .map((comment, at) => {
      let replies = 0
      while (ordered[at + 1 + replies]?.depth > comment.depth) replies++
      return { ...comment, replies }
    })
    .filter((comment) => !levels || comment.depth <= start + levels)
}

// The threads in the order `sort` asks for, as CommentTree.Sorted does (E09-04, ADR-0058, ADR-0081): «popular» — the
// highest rated first, then the most replies, then the older; «date» — the newest first. Replies keep the order of the
// tree. The rating is the others' likes, a like weighing 1 (ReactionWeights): the order is the same for every client.
function sortedThreads(shown, sort) {
  const threads = []
  for (const comment of shown) {
    if (comment.depth === 1 || threads.length === 0) threads.push([])
    threads.at(-1).push(comment)
  }
  const byDate = (a, b) => a[0].createdAt.localeCompare(b[0].createdAt)
  const rating = (thread) => othersLikes.get(`comment:${thread[0].id}`) ?? 0
  threads.sort(
    sort === 'date'
      ? (a, b) => byDate(b, a)
      : (a, b) => rating(b) - rating(a) || b[0].replies - a[0].replies || byDate(a, b),
  )
  return threads.flat()
}

function commentTree(response, url) {
  const levels = Number(url.searchParams.get('levels')) || null
  const sort = url.searchParams.get('sort') ?? 'popular'
  const [, postId] = url.pathname.match(/^\/api\/v1\/posts\/([^/]+)\/comments$/) ?? []
  if (postId) {
    if (!['popular', 'date'].includes(sort))
      return send(response, 400, {
        status: 400,
        errors: { sort: ['Порядок комментариев — popular или date.'] },
      })
    if (!posts.some((item) => item.id === postId) && !written.has(postId))
      return send(response, 404, { status: 404, code: 'comments.post_not_found' })
    const ordered = comments.filter((comment) => comment.postId === postId)
    const shown = sortedThreads(shownComments(ordered, 0, levels), sort)
    return send(response, 200, { postId, maxDepth: 8, comments: shown })
  }
  const [, id] = url.pathname.match(/^\/api\/v1\/comments\/([^/]+)\/branch$/) ?? []
  const at = comments.findIndex((comment) => comment.id === id)
  if (at < 0) return send(response, 404, { status: 404, code: 'comments.comment_not_found' })
  const root = comments[at]
  let end = at + 1
  while (comments[end]?.depth > root.depth) end++
  const branch = shownComments(comments.slice(at, end), root.depth, levels)
  return send(response, 200, { postId: root.postId, maxDepth: 8, comments: branch })
}

// Writing a comment (E09-03): POST /api/v1/posts/{postId}/comments with the checks of the server (a signed-in account,
// a text, a parent under the post that takes replies). The comment is answered, not kept: tests run in parallel against
// one fake, and the trees the other tests read stay as they are. A text «сбой» fails as the server would.
async function writeComment(request, response, postId) {
  const account = subject(request)
  if (!account) return send(response, 401, { status: 401, code: 'unauthorized' })
  if (!posts.some((item) => item.id === postId))
    return send(response, 404, { status: 404, code: 'comments.post_not_found' })
  const input = JSON.parse((await readBody(request)) || '{}')
  const text = typeof input.text === 'string' ? input.text.trim() : ''
  if (text === 'сбой') return send(response, 500, { status: 500, code: 'internal' })
  const parent = input.parentId ? comments.find((comment) => comment.id === input.parentId) : null
  const errors = {}
  if (!text) errors.text = ['Напишите текст комментария.']
  if (input.parentId && (!parent || parent.postId !== postId))
    errors.parentId = ['Под этим постом нет такого комментария.']
  else if (parent?.kind === 'guest') errors.parentId = ['На гостевой комментарий ответить нельзя.']
  if (Object.keys(errors).length > 0) return send(response, 400, { status: 400, code: 'validation', errors })
  return send(response, 201, {
    id: randomUUID(),
    postId,
    parentId: parent?.id ?? null,
    depth: (parent?.depth ?? 0) + 1,
    kind: 'native',
    authorAccountId: account,
    guest: null,
    text,
    createdAt: new Date().toISOString(),
    editedAt: null,
    editableUntil: new Date(Date.now() + editMinutes * 60_000).toISOString(),
    deleted: false,
    replies: 0,
  })
}

// The own comments, changing and deleting them (E09-05, ADR-0059): GET /api/v1/posts/{postId}/comments/mine lists the
// signed-in account's comments under the post; PATCH and DELETE /api/v1/comments/{id} check the author and the time to
// change the text, as the server does. Like a comment written, a change is answered, not kept. A comment the fake does
// not know is one the test has just written (it was not kept either): it is the caller's, still in its time. A text
// «сбой» fails as the server would.
function ownComments(request, response, postId) {
  const account = subject(request)
  if (!account) return send(response, 401, { status: 401, code: 'unauthorized' })
  if (!posts.some((item) => item.id === postId))
    return send(response, 404, { status: 404, code: 'comments.post_not_found' })
  const ids = comments.filter((c) => c.postId === postId && c.authorAccountId === account).map((c) => c.id)
  return send(response, 200, { postId, ids })
}

async function changeComment(request, response, id) {
  const account = subject(request)
  if (!account) return send(response, 401, { status: 401, code: 'unauthorized' })
  const known = comments.find((comment) => comment.id === id)
  if (known && known.authorAccountId !== account)
    return send(response, 403, { status: 403, code: 'comments.not_author' })
  if (request.method === 'DELETE') return send(response, 204)
  if (known) return send(response, 403, { status: 403, code: 'comments.edit_closed' })
  const input = JSON.parse((await readBody(request)) || '{}')
  const text = typeof input.text === 'string' ? input.text.trim() : ''
  if (text === 'сбой') return send(response, 500, { status: 500, code: 'internal' })
  if (!text)
    return send(response, 400, {
      status: 400,
      code: 'validation',
      errors: { text: ['Напишите текст комментария.'] },
    })
  const now = new Date()
  return send(response, 200, {
    id,
    postId: '',
    parentId: null,
    depth: 1,
    kind: 'native',
    authorAccountId: account,
    guest: null,
    text,
    createdAt: now.toISOString(),
    editedAt: now.toISOString(),
    editableUntil: new Date(now.getTime() + editMinutes * 60_000).toISOString(),
    deleted: false,
    replies: 0,
  })
}

// Publishing (E05-09, ADR-0046): GET /api/v1/topics, POST /api/v1/posts (a draft), PUT /api/v1/posts/{id} and
// POST /api/v1/posts/{id}/publish with the checks of PostChecks: a post with no text is refused, as is a title longer
// than 120 characters. A post whose text is «сбой» fails as an unavailable API would. Written posts stay out of the
// feed (tests run in parallel against this one fake), are found by GET /api/v1/posts/{id} and have no slug.
const written = new Map() // id -> post
written.set(arabicPost.id, arabicPost)
const titleMaxLength = 120
const hasText = (body) =>
  (body?.blocks ?? []).some(
    (block) =>
      block?.type === 'image' ||
      String(block?.data?.text ?? block?.data?.code ?? '')
        .replace(/<[^>]*>|&nbsp;/g, '')
        .trim(),
  )
// The sources of the pictures of image blocks (E05-06, PostView.images): the widths of PostImages.Variants.
const imagesOf = (body) =>
  Object.fromEntries(
    (body?.blocks ?? [])
      .filter((block) => block?.type === 'image' && block.data?.media)
      .map(({ data: { media } }) => [
        media,
        [320, 640, 960, 1280, 1920].map((width) => ({
          url: `http://127.0.0.1:${port}/__images/${media}-${width}.webp`,
          width,
        })),
      ]),
  )

async function writePost(request, response, url) {
  const account = subject(request)
  if (!account) return send(response, 401, { status: 401, code: 'unauthorized' })
  const [, id, action] = url.pathname.match(/^\/api\/v1\/posts(?:\/([^/]+))?(?:\/(publish))?$/) ?? []
  const existing = id ? written.get(id) : undefined
  if (id && (!existing || existing.authorAccountId !== account))
    return send(response, 404, { status: 404, code: 'posts.post_not_found' })

  if (action === 'publish') {
    const errors = {}
    if ((existing.title ?? '').length > titleMaxLength)
      errors.title = [`Заголовок длиннее ${titleMaxLength} символов.`]
    if (!hasText(existing.body)) errors.body = ['Пустой пост опубликовать нельзя.']
    if (Object.keys(errors).length > 0)
      return send(response, 400, { status: 400, code: 'validation', errors })
    if (existing.status !== 'published')
      Object.assign(existing, { status: 'published', publishedAt: new Date().toISOString() })
    return send(response, 200, existing)
  }

  const input = JSON.parse((await readBody(request)) || '{}')
  if ((input.body?.blocks ?? []).some((block) => block?.data?.text === 'сбой'))
    return send(response, 500, { status: 500, code: 'internal' })
  const own = blogs.find((blog) => blog.owner === account)
  const now = new Date().toISOString()
  const fields = {
    blogId: input.blogId ?? existing?.blogId ?? own?.id ?? blogs[0].id,
    title: (input.title ?? '').trim() || null,
    slug: null,
    body: input.body ?? { version: 1, blocks: [] },
    images: imagesOf(input.body),
    updatedAt: now,
  }
  const saved = existing
    ? Object.assign(existing, fields)
    : {
        id: randomUUID(),
        authorAccountId: account,
        status: 'draft',
        origin: 'native',
        source: null,
        createdAt: now,
        publishedAt: null,
        viewCount: 0,
        ...fields,
      }
  written.set(saved.id, saved)
  return send(response, existing ? 200 : 201, saved)
}

// Deleting (E07-05): DELETE /api/v1/posts/{id} of a written post by its author; the post is then not found. A seeded
// post of another author is refused as by the API (posts.not_author), and the seeded posts stay for the other tests.
function deletePost(request, response, url) {
  const account = subject(request)
  if (!account) return send(response, 401, { status: 401, code: 'unauthorized' })
  const [, id] = url.pathname.match(/^\/api\/v1\/posts\/([^/]+)$/) ?? []
  if (posts.some((item) => item.id === id))
    return send(response, 403, { status: 403, code: 'posts.not_author' })
  const existing = written.get(id)
  if (!existing || existing.authorAccountId !== account)
    return send(response, 404, { status: 404, code: 'posts.post_not_found' })
  written.delete(id)
  return send(response, 204)
}

const topics = () =>
  blogs.filter((blog) => blog.type === 'topic').map(({ id, name, nickname }) => ({ id, name, nickname }))

// Creating a topic (E11-11, ADR-0084): POST /api/v1/topics with the API's answers. A created topic is found by its
// address and has an empty feed, but stays out of GET /api/v1/topics (tests run in parallel against this one fake);
// the limit counts the topics of one client address (`x-forwarded-for`). The name of a stored topic is taken, the
// address `settings` is reserved, the name `сбой` fails as an unavailable API would, and the address `unconfirmed`
// answers as for an account without a confirmed email. The real rules are the backend's (TopicCreateApiTests).
const createdTopics = [] // { client, ...blog }
const topicLimit = 2
const addressForm = /^[a-z][a-z0-9_]{2,31}$/

async function createTopic(request, response) {
  if (!subject(request)) return send(response, 401, { status: 401, code: 'unauthorized' })
  const input = JSON.parse((await readBody(request)) || '{}')
  const name = clean(input.name)
  const nickname = String(input.nickname ?? '')
    .trim()
    .toLowerCase()
  if (name === 'сбой') return send(response, 500, { status: 500, code: 'internal' })
  if (nickname === 'unconfirmed') return send(response, 403, { status: 403, code: 'email_not_confirmed' })
  const client = clientOf(request)
  if (createdTopics.filter((topic) => topic.client === client).length >= topicLimit)
    return send(response, 409, {
      status: 409,
      code: 'blogs.topic_limit_reached',
      detail: `Одному аккаунту можно создать не больше ${topicLimit} тем.`,
    })
  const all = [...blogs, ...createdTopics]
  const errors = {}
  if (length(name) < 2 || length(name) > 25) errors.name = ['Название — от 2 до 25 символов.']
  else if (all.some((blog) => blog.type === 'topic' && blog.name?.toLowerCase() === name.toLowerCase()))
    errors.name = ['Тема с таким названием уже есть.']
  if (!addressForm.test(nickname))
    errors.nickname = ['Адрес — от 3 до 32 латинских букв, цифр и _, начинается с буквы.']
  else if (nickname === 'settings') errors.nickname = ['Такой адрес выбрать нельзя.']
  else if (all.some((blog) => blog.nickname === nickname))
    errors.nickname = ['Этот адрес уже занят блогом или темой.']
  if (Object.keys(errors).length > 0)
    return send(response, 400, { status: 400, code: 'validation_failed', errors })
  const topic = {
    client,
    id: randomUUID(),
    owner,
    type: 'topic',
    name,
    nickname,
    description: clean(input.description),
    registeredAt: null,
  }
  createdTopics.push(topic)
  return send(response, 201, { id: topic.id, name, nickname })
}
// The topic catalogue and the menu's topics (E11-02, ADR-0088): GET /api/v1/topics/catalog, the stored topics with
// their subscribers (as the profile counts them), the most followed first and then the newest, searched by name or
// address, a page of `limit` (20 by default) with an opaque cursor; the search `сбой поиска` fails as an unavailable
// API would. GET /api/v1/topics/menu gives a person's subscribed topics by name, or the popular ones. The real rules
// are the backend's (TopicCatalogApiTests).
const menuSize = 10

function topicCards(request) {
  return blogs
    .filter((blog) => blog.type === 'topic')
    .map(({ id, name, nickname, description }) => ({
      id,
      name,
      nickname,
      description,
      subscriberCount: subscriptionFields({ id, nickname, type: 'topic' }, request).subscriberCount,
    }))
    .sort((a, b) => b.subscriberCount - a.subscriberCount || b.id.localeCompare(a.id))
}

function topicCatalog(request, response, url) {
  const q = (url.searchParams.get('q') ?? '').trim().replace(/\s+/g, ' ').toLowerCase()
  if (q.length > 50)
    return send(response, 400, {
      status: 400,
      code: 'validation_failed',
      errors: { q: ['Поиск — не длиннее 50 символов.'] },
    })
  if (q === 'сбой поиска') return send(response, 500, { status: 500, code: 'internal' })
  const found = topicCards(request).filter(
    (topic) => !q || topic.name.toLowerCase().includes(q) || topic.nickname.includes(q),
  )
  const limit = Number(url.searchParams.get('limit') ?? 20)
  const cursor = url.searchParams.get('cursor')
  const start = cursor ? Number(Buffer.from(cursor, 'base64url').toString()) : 0
  const items = found.slice(start, start + limit)
  const next = start + limit < found.length ? Buffer.from(String(start + limit)).toString('base64url') : null
  return send(response, 200, { items, nextCursor: next })
}

function topicMenu(request, response) {
  const own = subject(request) ? subscribedBy(request) : new Set()
  const subscribed = blogs
    .filter((blog) => blog.type === 'topic' && own.has(blog.id))
    .sort((a, b) => a.name.localeCompare(b.name, 'ru'))
    .slice(0, menuSize)
    .map(({ id, name, nickname }) => ({ id, name, nickname }))
  if (subscribed.length > 0) return send(response, 200, { source: 'subscriptions', topics: subscribed })
  const popular = topicCards(request)
    .slice(0, menuSize)
    .map(({ id, name, nickname }) => ({ id, name, nickname }))
  return send(response, 200, { source: 'popular', topics: popular })
}

// The fresh feed (E08-01): GET /api/v1/feeds/fresh, the posts above newest first (by publishedAt, then id), two per
// page so the e2e test reaches a second page. The cursor is opaque to the client, as the API's (ADR-0006).
const feedPageSize = 2

// The feed of one blog (E11-01): GET /api/v1/feeds/blogs/{blogId}, the same pages for the posts of that blog only.
function blogFeed(response, url) {
  const [, blogId] = url.pathname.match(/^\/api\/v1\/feeds\/blogs\/([^/]+)$/) ?? []
  const blog = [...blogs, ...createdTopics].find((item) => item.id === blogId)
  if (!blog) return send(response, 404, { status: 404, code: 'posts.blog_not_found' })
  if (blog.nickname === 'outage') return send(response, 500, { status: 500, code: 'internal' })
  return freshFeed(response, url, (post) => post.blogId === blogId)
}

// «Моя лента» (E08-04, ADR-0089): GET /api/v1/feeds/my, the same pages for the posts of the blogs and topics the client
// is subscribed to (kept per client address, see the subscriptions below) and of the people whose blogs it follows.
function myFeed(request, response, url) {
  if (!subject(request)) return send(response, 401, { status: 401, code: 'unauthorized' })
  const own = subscribedBy(request)
  const followed = new Set(
    blogs.filter((blog) => blog.type === 'personal' && own.has(blog.id)).map((blog) => blog.owner),
  )
  return freshFeed(response, url, (post) => own.has(post.blogId) || followed.has(post.authorAccountId))
}

function freshFeed(response, url, only = () => true) {
  const sorted = [...posts]
    .filter(only)
    .sort((a, b) => b.publishedAt.localeCompare(a.publishedAt) || b.id.localeCompare(a.id))
  const cursor = url.searchParams.get('cursor')
  const start = cursor ? Number(Buffer.from(cursor, 'base64url').toString()) : 0
  if (!Number.isInteger(start) || start < 0)
    return send(response, 400, { status: 400, code: 'invalid_cursor' })
  const end = start + feedPageSize
  const nextCursor = end < sorted.length ? Buffer.from(String(end)).toString('base64url') : null
  return send(response, 200, { items: sorted.slice(start, end).map(feedItem), nextCursor })
}

// A feed item carries the author's personal blog and the post's topic (E08-02, ADR-0052).
const feedBlog = ({ id, type, name, nickname }) => ({ id, type, name, nickname, avatarSources: [] })

function feedItem(post) {
  const author = blogs.find((blog) => blog.type === 'personal' && blog.owner === post.authorAccountId)
  const topic = blogs.find((blog) => blog.type === 'topic' && blog.id === post.blogId)
  return {
    post,
    author: author ? feedBlog(author) : null,
    topic: topic ? feedBlog(topic) : null,
    bestComment: bestComment(post.id),
  }
}

// The best comment of a card (E08-15, ADR-0062), as CommentDirectory.BestShownAsync chooses it: the most liked shown
// comment, the earlier among equals; its text cut to 300 characters with «…».
const excerptLength = 300

function bestComment(postId) {
  const likesOf = (comment) => othersLikes.get(`comment:${comment.id}`) ?? 0
  const [best] = comments
    .filter((comment) => comment.postId === postId && !comment.deleted)
    .sort((a, b) => likesOf(b) - likesOf(a) || a.createdAt.localeCompare(b.createdAt))
  if (!best) return null
  const author = best.authorAccountId
    ? blogs.find((blog) => blog.type === 'personal' && blog.owner === best.authorAccountId)
    : null
  const text =
    best.text.length > excerptLength ? `${best.text.slice(0, excerptLength).trimEnd()}…` : best.text
  return {
    id: best.id,
    author: author ? feedBlog(author) : null,
    guestName: best.guest?.name ?? null,
    text,
  }
}

// Likes (E10-01, ADR-0061): GET /api/v1/posts/{postId}/likes, PUT and DELETE /api/v1/posts/{postId}/like and
// /api/v1/comments/{id}/like, with the server's checks (a signed-in account, not one's own content, not a guest
// comment). Others' likes are fixed: three on the post in the topic, two on «Вторая ветка». The signed-in person's
// likes are kept per client address (`x-forwarded-for`), as the profile edits are, so parallel tests never see each
// other's. A comment the fake does not know is one the test has just written: it is the caller's own.
const othersLikes = new Map([
  [`post:${posts[2].id}`, 3],
  [`comment:${comments.find((c) => c.text === 'Вторая ветка').id}`, 2],
])
const ownLikes = new Map() // client address -> Set of `post:<id>` and `comment:<id>`

function likeState(request, key, account) {
  const liked = Boolean(account) && (ownLikes.get(clientOf(request))?.has(key) ?? false)
  return { count: (othersLikes.get(key) ?? 0) + (liked ? 1 : 0), liked }
}

function postLikes(request, response, postId) {
  // A post a test has just published (`written`) has no likes yet.
  if (!posts.some((item) => item.id === postId) && !written.has(postId))
    return send(response, 404, { status: 404, code: 'reactions.post_not_found' })
  const account = subject(request)
  const keys = new Set([...othersLikes.keys(), ...(ownLikes.get(clientOf(request)) ?? [])])
  const under = comments.filter((c) => c.postId === postId && keys.has(`comment:${c.id}`))
  return send(response, 200, {
    postId,
    post: likeState(request, `post:${postId}`, account),
    comments: under.map((c) => ({ commentId: c.id, ...likeState(request, `comment:${c.id}`, account) })),
  })
}

function changeLike(request, response, target, id) {
  const account = subject(request)
  if (!account) return send(response, 401, { status: 401, code: 'unauthorized' })
  const item =
    target === 'post'
      ? (posts.find((p) => p.id === id) ?? written.get(id))
      : comments.find((c) => c.id === id)
  if (!item && target === 'post')
    return send(response, 404, { status: 404, code: 'reactions.post_not_found' })
  const key = `${target}:${id}`
  if (request.method === 'PUT') {
    if (item?.kind === 'guest')
      return send(response, 403, {
        status: 403,
        code: 'reactions.guest_comment',
        detail: 'Комментарий гостя оценить нельзя: у его автора нет аккаунта.',
      })
    if (!item || item.authorAccountId === account)
      return send(response, 403, {
        status: 403,
        code: 'reactions.own_content',
        detail: 'Свой пост или комментарий оценить нельзя.',
      })
    const mine = ownLikes.get(clientOf(request)) ?? new Set()
    ownLikes.set(clientOf(request), mine.add(key))
  } else {
    ownLikes.get(clientOf(request))?.delete(key)
  }
  return send(response, 200, likeState(request, key, account))
}

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
const edits = new Map() // client address -> { name, description, avatarId, avatarUrl, avatarSources }
const uploads = [] // { client, id, jpeg, size }
const pixel = Buffer.from('UklGRhoAAABXRUJQVlA4TA0AAAAvAAAAEAcQERGIiP4HAA==', 'base64')

function clientOf(request) {
  return String(request.headers['x-forwarded-for'] ?? '')
}

function edited(blog, request) {
  const edit = clientOf(request) && edits.get(clientOf(request))
  const empty = { avatarId: null, avatarUrl: null, avatarSources: [], cover: null }
  const shown = { ...empty, ...blog, ...subscriptionFields(blog, request) }
  return edit && blog.owner === owner ? { ...shown, ...edit } : shown
}

// Subscriptions (E11-03, ADR-0085): PUT and DELETE /api/v1/blogs/{id}/subscription. Like the edits, they are kept per
// client address, so only that client sees them. «Книги» has 41 subscribers of others, the other blogs none; anna
// follows no one of her own. Her own blog is refused, and the subscription to «Сбой» fails as an unavailable API would.
const othersSubscribers = { books: 41 }
const subscriptions = new Map() // client address -> Set of blog ids

function subscribedBy(request) {
  const client = clientOf(request)
  if (client && !subscriptions.has(client)) subscriptions.set(client, new Set())
  return subscriptions.get(client) ?? new Set()
}

function subscriptionFields(blog, request) {
  const reader = subject(request)
  const own = reader ? subscribedBy(request) : new Set()
  const subscribed = own.has(blog.id)
  return {
    subscriberCount: (othersSubscribers[blog.nickname] ?? 0) + (subscribed ? 1 : 0),
    subscriptionCount: blog.type === 'personal' ? (reader && blog.owner === reader ? own.size : 0) : null,
    subscribed,
  }
}

function changeSubscription(request, response, blogId) {
  const reader = subject(request)
  if (!reader) return send(response, 401, { status: 401, code: 'unauthorized' })
  const blog = blogs.find((item) => item.id === blogId)
  if (!blog) return send(response, 404, { status: 404, code: 'blogs.blog_not_found' })
  if (blog.nickname === 'outage') return send(response, 500, { status: 500, code: 'internal' })
  if (blog.type === 'personal' && blog.owner === reader)
    return send(response, 403, {
      status: 403,
      code: 'blogs.own_blog',
      detail: 'На свой блог подписаться нельзя.',
    })
  const own = subscribedBy(request)
  if (request.method === 'PUT') own.add(blog.id)
  else own.delete(blog.id)
  const { subscriberCount, subscribed } = subscriptionFields(blog, request)
  return send(response, 200, { subscriberCount, subscribed })
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
    ...edits.get(clientOf(request)),
    name: clean(input.name),
    description: clean(input.description),
    avatarId: input.avatarId ?? null,
    // The delivery link (ADR-0037): here the fake itself stands in for imgproxy.
    avatarUrl: input.avatarId ? `http://127.0.0.1:${port}/__images/${input.avatarId}-128.webp` : null,
    // One square per screen density (ADR-0040), as the API gives them.
    avatarSources: input.avatarId
      ? [64, 128, 192].map((width) => ({
          url: `http://127.0.0.1:${port}/__images/${input.avatarId}-${width}.webp`,
          width,
        }))
      : [],
  })
  // The owner's own view: `owner` is the fake's, not the API's (undefined drops it from the JSON).
  return send(response, 200, { ...edited(own, request), owner: undefined })
}

// The nickname (E04-05, ADR-0090): PUT /api/v1/profiles/me/nickname, kept per client address like the edits. A
// nickname of another blog or topic and `settings` are refused, `outage` fails as an unavailable API would; the second
// change of one client is too soon (30 days). The old nickname keeps answering with the blog, so the site redirects.
async function changeNickname(request, response) {
  const reader = subject(request)
  const own = blogs.find((blog) => reader && blog.owner === reader)
  if (!own) return send(response, 401, { status: 401, code: 'unauthorized' })
  const nickname = String(JSON.parse((await readBody(request)) || '{}').nickname ?? '')
    .replace(/^@/, '')
    .toLowerCase()
  if (nickname === 'outage') return send(response, 500, { status: 500, code: 'internal' })
  const invalid = (message) =>
    send(response, 400, { status: 400, code: 'validation_failed', errors: { nickname: [message] } })
  if (nickname === 'settings') return invalid('Такой никнейм выбрать нельзя.')
  if ([...blogs, ...createdTopics].some((blog) => blog.nickname === nickname && blog.id !== own.id))
    return invalid('Этот никнейм уже занят блогом или темой.')
  const edit = edits.get(clientOf(request)) ?? {}
  if (edit.nicknameChangeableFrom)
    return send(response, 409, {
      status: 409,
      code: 'blogs.nickname_change_too_soon',
      detail: 'Никнейм можно менять раз в 30 дней; следующая смена — с 2027-01-01.',
    })
  edits.set(clientOf(request), { ...edit, nickname, nicknameChangeableFrom: '2099-01-01T00:00:00Z' })
  return send(response, 200, { ...edited(own, request), owner: undefined })
}

// The cover (E04-04, ADR-0079): PUT and DELETE /api/v1/profiles/me/cover, kept per client address like the edits.
// The sources are the fake's square pixel, so the picture is taller than the header and can be moved.
async function setCover(request, response) {
  const reader = subject(request)
  const own = blogs.find((blog) => reader && blog.owner === reader)
  if (!own) return send(response, 401, { status: 401, code: 'unauthorized' })
  const input = request.method === 'PUT' ? JSON.parse((await readBody(request)) || '{}') : null
  if (input) {
    const errors = {}
    if (!uploads.some((file) => file.id === input.mediaId))
      errors.mediaId = ['Такого загруженного файла нет.']
    const position = input.position ?? 50
    if (!Number.isInteger(position) || position < 0 || position > 100)
      errors.position = ['Положение обложки — от 0 до 100.']
    if (Object.keys(errors).length > 0)
      return send(response, 400, { status: 400, code: 'validation_failed', errors })
  }
  const cover = input
    ? {
        id: input.mediaId,
        position: input.position ?? 50,
        sources: [400, 640, 800, 1280, 1920].map((width) => ({
          url: `http://127.0.0.1:${port}/__images/${input.mediaId}-cover-${width}.webp`,
          width,
        })),
        preview: { color: '#336699', image: `data:image/webp;base64,${pixel.toString('base64')}` },
      }
    : null
  edits.set(clientOf(request), { ...edits.get(clientOf(request)), cover })
  return send(response, 200, { ...edited(own, request), owner: undefined })
}

// The account's settings (E04-08, ADR-0070): GET /api/v1/preferences/me and PUT /api/v1/preferences/me/general.
// Kept per client address, as the profile edits are: two browsers of one test sending the same `x-forwarded-for` are
// two devices of the same person.
const preferences = new Map() // client address -> { theme }
const themes = ['system', 'light', 'dark']

function ownPreferences(request, response) {
  if (!subject(request)) return send(response, 401, { status: 401, code: 'unauthorized' })
  return send(response, 200, { general: preferences.get(clientOf(request)) ?? { theme: 'system' } })
}

async function saveGeneral(request, response) {
  if (!subject(request)) return send(response, 401, { status: 401, code: 'unauthorized' })
  const input = JSON.parse((await readBody(request)) || '{}')
  if (!themes.includes(input.theme))
    return send(response, 400, {
      status: 400,
      code: 'validation_failed',
      errors: { theme: ['Тема — system, light или dark.'] },
    })
  preferences.set(clientOf(request), { theme: input.theme })
  return send(response, 200, { general: { theme: input.theme } })
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
    preview: null,
  })
}

function profile(request, response, url) {
  const reader = subject(request)
  const view = (blog) => {
    const { owner: blogOwner, registeredAt, ...rest } = edited(blog, request)
    // A topic names its owner's personal blog (ADR-0082).
    const ownersBlog =
      blog.type === 'topic' && blogs.find((item) => item.type === 'personal' && item.owner === blogOwner)
    return {
      ...rest,
      registeredAt: blogOwner === reader ? registeredAt : null,
      owner: ownersBlog ? { id: ownersBlog.id, name: ownersBlog.name, nickname: ownersBlog.nickname } : null,
    }
  }
  const [, kind, value] =
    url.pathname.match(/^\/api\/v1\/profiles\/(?:(by-nickname|by-account)\/)?([^/]+)$/) ?? []
  if (kind === 'by-account') {
    const personal = blogs.find((blog) => blog.type === 'personal' && blog.owner === value)
    return personal
      ? send(response, 200, view(personal))
      : send(response, 404, { status: 404, code: 'profile_not_found' })
  }
  if (value === 'me' && !kind) {
    const own = blogs.find((blog) => reader && blog.owner === reader)
    return own ? send(response, 200, view(own)) : send(response, 401, { status: 401, code: 'unauthorized' })
  }
  const nickname = decodeURIComponent(value ?? '')
    .replace(/^@/, '')
    .toLowerCase()
  if (kind && nickname === 'broken') return send(response, 500, { status: 500, code: 'internal' })
  // A client's new nickname (changeNickname) names the own blog for that client only.
  const renamed =
    kind && blogs.find((blog) => blog.owner === owner && edits.get(clientOf(request))?.nickname === nickname)
  const found =
    renamed ||
    [...blogs, ...createdTopics].find((blog) => (kind ? blog.nickname === nickname : blog.id === value))
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
  if (url.pathname === '/api/v1/sign-in/captcha' && request.method === 'GET')
    return send(response, 200, {
      provider: 'fake',
      algorithm: null,
      challenge: null,
      salt: null,
      signature: null,
      maxNumber: null,
    })
  if (url.pathname.startsWith('/api/v1/sign-in/') && request.method === 'POST')
    return signIn(request, response, url)
  if (url.pathname.startsWith('/api/v1/registration') && request.method === 'POST')
    return registration(request, response, url)
  if (url.pathname.startsWith('/api/v1/password-reset') && request.method === 'POST')
    return passwordReset(request, response, url)
  if (url.pathname === '/api/v1/account/password' && request.method === 'PUT')
    return changePassword(request, response)
  if (
    (url.pathname === '/api/v1/account/email' && (request.method === 'GET' || request.method === 'PUT')) ||
    (url.pathname === '/api/v1/account/email/confirmation' && request.method === 'POST')
  )
    return accountEmailRoute(request, response, url)
  if (
    (url.pathname === '/api/v1/account/sessions' && request.method === 'GET') ||
    (url.pathname === '/api/v1/account/sessions/sign-out-others' && request.method === 'POST') ||
    (url.pathname.startsWith('/api/v1/account/sessions/') && request.method === 'DELETE')
  )
    return accountSessionsRoute(request, response, url)
  if (url.pathname === '/api/v1/preferences/me' && request.method === 'GET')
    return ownPreferences(request, response)
  if (url.pathname === '/api/v1/preferences/me/general' && request.method === 'PUT')
    return saveGeneral(request, response)
  if (url.pathname === '/api/v1/profiles/me' && request.method === 'PUT')
    return editProfile(request, response)
  if (url.pathname === '/api/v1/profiles/me/nickname' && request.method === 'PUT')
    return changeNickname(request, response)
  if (url.pathname === '/api/v1/profiles/me/cover' && ['PUT', 'DELETE'].includes(request.method))
    return setCover(request, response)
  if (url.pathname.startsWith('/api/v1/profiles/') && request.method === 'GET')
    return profile(request, response, url)
  const [, subscriptionBlog] = url.pathname.match(/^\/api\/v1\/blogs\/([^/]+)\/subscription$/) ?? []
  if (subscriptionBlog && ['PUT', 'DELETE'].includes(request.method))
    return changeSubscription(request, response, subscriptionBlog)
  const [, likedPost] = url.pathname.match(/^\/api\/v1\/posts\/([^/]+)\/likes$/) ?? []
  if (likedPost && request.method === 'GET') return postLikes(request, response, likedPost)
  const [, likeTarget, likeId] = url.pathname.match(/^\/api\/v1\/(posts|comments)\/([^/]+)\/like$/) ?? []
  if (likeTarget && ['PUT', 'DELETE'].includes(request.method))
    return changeLike(request, response, likeTarget === 'posts' ? 'post' : 'comment', likeId)
  if (
    /^\/api\/v1\/(posts\/[^/]+\/comments|comments\/[^/]+\/branch)$/.test(url.pathname) &&
    request.method === 'GET'
  )
    return commentTree(response, url)
  const [, commentedPost] = url.pathname.match(/^\/api\/v1\/posts\/([^/]+)\/comments$/) ?? []
  if (commentedPost && request.method === 'POST') return writeComment(request, response, commentedPost)
  const [, ownPost] = url.pathname.match(/^\/api\/v1\/posts\/([^/]+)\/comments\/mine$/) ?? []
  if (ownPost && request.method === 'GET') return ownComments(request, response, ownPost)
  const [, changed] = url.pathname.match(/^\/api\/v1\/comments\/([^/]+)$/) ?? []
  if (changed && ['PATCH', 'DELETE'].includes(request.method))
    return changeComment(request, response, changed)
  // The view counter (E07-06, ADR-0071): reports are taken and not counted; the counts are the seeded viewCount.
  // e2e/views.spec.ts checks what the browser reports to the site's server.
  if (
    /^\/api\/v1\/posts\/([^/]+\/(open|reading)|impressions)$/.test(url.pathname) &&
    request.method === 'POST'
  )
    return send(response, 204)
  if (url.pathname.startsWith('/api/v1/posts/') && request.method === 'GET') return post(response, url)
  if (url.pathname.startsWith('/api/v1/posts') && ['POST', 'PUT'].includes(request.method))
    return writePost(request, response, url)
  if (url.pathname.startsWith('/api/v1/posts/') && request.method === 'DELETE')
    return deletePost(request, response, url)
  if (url.pathname === '/api/v1/topics' && request.method === 'GET') return send(response, 200, topics())
  if (url.pathname === '/api/v1/topics' && request.method === 'POST') return createTopic(request, response)
  if (url.pathname === '/api/v1/topics/catalog' && request.method === 'GET')
    return topicCatalog(request, response, url)
  if (url.pathname === '/api/v1/topics/menu' && request.method === 'GET') return topicMenu(request, response)
  if (url.pathname === '/api/v1/feeds/fresh' && request.method === 'GET') return freshFeed(response, url)
  if (url.pathname === '/api/v1/feeds/my' && request.method === 'GET') return myFeed(request, response, url)
  if (url.pathname.startsWith('/api/v1/feeds/blogs/') && request.method === 'GET')
    return blogFeed(response, url)
  if (url.pathname === '/api/v1/media' && request.method === 'POST') return uploadMedia(request, response)
  // imgproxy stands in: any delivered image is the same 1×1 WebP.
  if (url.pathname.startsWith('/__images/') && request.method === 'GET') {
    response.writeHead(200, { 'content-type': 'image/webp', 'cache-control': 'public, max-age=31536000' })
    return response.end(pixel)
  }
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
