# Changelog

## 0.1.0 (2026-11-21)


### Features

* **api:** add rate limits per account and IP with counters in Redis ([#308](https://github.com/shoraLBRT/kitabber/issues/308)) ([4a428e0](https://github.com/shoraLBRT/kitabber/commit/4a428e0e843cbf007a442b671698590d55e230fe))
* **api:** add REST conventions with versioning, problem details, cursor pagination and OpenAPI ([#283](https://github.com/shoraLBRT/kitabber/issues/283)) ([20c4984](https://github.com/shoraLBRT/kitabber/commit/20c4984254625e68060766d473e05592eca422d6))
* **blogs:** add the blog page with its header and the posts and comments tabs ([#327](https://github.com/shoraLBRT/kitabber/issues/327)) ([dd9e7ee](https://github.com/shoraLBRT/kitabber/commit/dd9e7ee05341a4a384be871b33bc3e1837c5be13))
* **blogs:** add the Subsite model, a personal blog for every account and the profile API ([#325](https://github.com/shoraLBRT/kitabber/issues/325)) ([b2b8033](https://github.com/shoraLBRT/kitabber/commit/b2b8033fa808669d1536e475f5ba61230c5c6266))
* **editor:** add heading and subheading blocks with stable anchors ([#332](https://github.com/shoraLBRT/kitabber/issues/332)) ([f0c2938](https://github.com/shoraLBRT/kitabber/commit/f0c29386574d80394a70f9eb4074b17c4b9f16e8))
* **editor:** add the block editor core with undo and redo across blocks ([#326](https://github.com/shoraLBRT/kitabber/issues/326)) ([8a6f5b7](https://github.com/shoraLBRT/kitabber/commit/8a6f5b7d2210d832d180ab299c180f5c36bddcfa))
* **editor:** add the text block with a floating format toolbar, checked links and paste cleaning ([#330](https://github.com/shoraLBRT/kitabber/issues/330)) ([322f27f](https://github.com/shoraLBRT/kitabber/commit/322f27fd402fb24005d97b95ff4564f6cea4d753))
* **editor:** validate post blocks by versioned schemas and clean inline HTML ([#324](https://github.com/shoraLBRT/kitabber/issues/324)) ([906f0e6](https://github.com/shoraLBRT/kitabber/commit/906f0e61fce1e001bc59d198e128a31d1059f5e6))
* **identity:** add the Identity module with a global account id and the credential boundary ([#310](https://github.com/shoraLBRT/kitabber/issues/310)) ([16f43b2](https://github.com/shoraLBRT/kitabber/commit/16f43b279e6a837fe40eb31ec3d9ade46caf40a4))
* **identity:** normalise and confirm the account email and require it to publish ([#314](https://github.com/shoraLBRT/kitabber/issues/314)) ([2561d71](https://github.com/shoraLBRT/kitabber/commit/2561d716161dfe8892bd1172237026b268bef5f7))
* **identity:** refresh tokens silently and sign out with revocation ([#319](https://github.com/shoraLBRT/kitabber/issues/319)) ([8e60b90](https://github.com/shoraLBRT/kitabber/commit/8e60b90de4f840196e6c00578db2b16c7342a632))
* **identity:** register by email and password with a code from the letter ([#315](https://github.com/shoraLBRT/kitabber/issues/315)) ([fa18e84](https://github.com/shoraLBRT/kitabber/commit/fa18e8460bb7da265356d3908f8c929fb5b80809))
* **identity:** sign in by email with the password or a one-time code ([#317](https://github.com/shoraLBRT/kitabber/issues/317)) ([b1e5158](https://github.com/shoraLBRT/kitabber/commit/b1e5158a1d471b5d397d1927fb267ab73a1d6858))
* **identity:** sign in through an OpenID Connect provider with PKCE ([#311](https://github.com/shoraLBRT/kitabber/issues/311)) ([51faa5d](https://github.com/shoraLBRT/kitabber/commit/51faa5dd8910d380a1ded9010e3e439a0f1678ba))
* **infra:** add EF Core migrations with a schema per module ([#279](https://github.com/shoraLBRT/kitabber/issues/279)) ([f9d50c4](https://github.com/shoraLBRT/kitabber/commit/f9d50c465f83b38b86180c759434d8ac9c0eb0b1))
* **infra:** add environment configuration validation and gitleaks secret scan ([#284](https://github.com/shoraLBRT/kitabber/issues/284)) ([9cf1927](https://github.com/shoraLBRT/kitabber/commit/9cf1927daafd926b0e96225f540774aa7fc0a1a3))
* **infra:** add OpenTelemetry tracing, health probes and Prometheus metrics ([#301](https://github.com/shoraLBRT/kitabber/issues/301)) ([82584d4](https://github.com/shoraLBRT/kitabber/commit/82584d4b1792a4f80335ff464178282f18235460))
* **infra:** add RabbitMQ messaging with outbox, Redis cache and workers host ([#282](https://github.com/shoraLBRT/kitabber/issues/282)) ([12bf5b5](https://github.com/shoraLBRT/kitabber/commit/12bf5b5cfc02fd45d5345fa824c675c4811e2c49))
* **mail:** add mail templates and queued SMTP delivery with Mailpit tests ([#309](https://github.com/shoraLBRT/kitabber/issues/309)) ([d26a93f](https://github.com/shoraLBRT/kitabber/commit/d26a93f41183b33f1db47a297437f246822a7fd2))
* **media:** add the upload service with type checks by content, size limits and S3 storage ([#328](https://github.com/shoraLBRT/kitabber/issues/328)) ([89cb9a2](https://github.com/shoraLBRT/kitabber/commit/89cb9a24e3658e98e0cb7beda7a04ba43a2c850c))
* **posts:** add the post model with origin and soft deletion and its CRUD API ([#329](https://github.com/shoraLBRT/kitabber/issues/329)) ([c4a091e](https://github.com/shoraLBRT/kitabber/commit/c4a091e2b694e1c03f24a3cd2d19c4b948292ddd))
* **realtime:** add the SignalR hub with tickets for the site and a Redis backplane ([#313](https://github.com/shoraLBRT/kitabber/issues/313)) ([f213134](https://github.com/shoraLBRT/kitabber/commit/f213134e9cd1f166579b8a31ec7342ce8bba629b))
* **web:** add base UI components with keyboard support and a showcase page ([#293](https://github.com/shoraLBRT/kitabber/issues/293)) ([88c856a](https://github.com/shoraLBRT/kitabber/commit/88c856ac0bc8cf6431385627df695685a0ecb546))
* **web:** add design tokens for both palettes with a style linter and showcase ([#291](https://github.com/shoraLBRT/kitabber/issues/291)) ([7ff642b](https://github.com/shoraLBRT/kitabber/commit/7ff642b8720486abdce9ad1e61c6ca5fce386dc0))
* **web:** add desktop shell with header, left menu, feed column and sticky right column ([#294](https://github.com/shoraLBRT/kitabber/issues/294)) ([8fd23f4](https://github.com/shoraLBRT/kitabber/commit/8fd23f41c523bfe113d78c5a668d905bad6cc361))
* **web:** add phone shell with bottom bar, menu screen and floating write button ([#295](https://github.com/shoraLBRT/kitabber/issues/295)) ([dd5cb8d](https://github.com/shoraLBRT/kitabber/commit/dd5cb8d8649d303462dd49a99a9cdaa1005b54cc))
* **web:** add temporary kitabber brand with single-source name, wordmark and Lucide icons ([#290](https://github.com/shoraLBRT/kitabber/issues/290)) ([9e691a6](https://github.com/shoraLBRT/kitabber/commit/9e691a642bb03f983e17dd86f5409c2d650025e3))


### Bug Fixes

* **web:** give the /brand showcase a first-level heading ([#304](https://github.com/shoraLBRT/kitabber/issues/304)) ([884eb5c](https://github.com/shoraLBRT/kitabber/commit/884eb5c9fe4572c734c0f3c241b285731c81eae0))
