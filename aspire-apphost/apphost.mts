// Aspire TypeScript AppHost (Aspire CLI 13). Запуск: `aspire run` (из этой папки)
// или `yarn dev` из корня. Пути - относительно aspire-apphost/.
//
// ServiceDefaults (C#) подмешивает в .NET-сервис Serilog, OpenTelemetry,
// health checks и service discovery; здесь - только оркестрация ресурсов.
import { createBuilder } from "./.aspire/modules/aspire.mjs";
import { provisionLogto } from "./logto/bootstrap.mjs";

const builder = await createBuilder();

// Порты, зафиксированные в конфигурации нескольких сторон сразу. Менять их
// поодиночке нельзя: см. комментарии у соответствующих ресурсов.
const LOGTO_PORT = 3481;
const LOGTO_ADMIN_PORT = 3482;
const MAILPIT_SMTP_PORT = 1425;
const MAILPIT_UI_PORT = 8425;
const WEB_PORT = 5173;

// Пароль локального PostgreSQL задан явно, а не сгенерирован Aspire. Причина -
// шаг настройки Logto: он идёт в базу за секретом служебного приложения из
// процесса AppHost, а значение сгенерированного параметра TS-обвязка прочитать
// не даёт. Контейнер слушает только 127.0.0.1 и живёт одну сессию разработки.
const POSTGRES_USER = "postgres";
const POSTGRES_PASSWORD = "postgres-dev";

// Адрес SPA знают сразу трое: CORS-политика API, список адресов возврата внутри
// Logto и сам Vite. Расхождение проявляется отказом входа, а не ошибкой сборки.
const WEB_ORIGIN = `http://localhost:${WEB_PORT}`;

// Индикатор ресурса API. Приезжает в `aud` выпущенного токена, поэтому обязан
// совпадать с `Jwt:Audience` на стороне API. Это идентификатор, а не адрес:
// по нему никто не ходит, и существовать такой узел не обязан.
const API_RESOURCE_INDICATOR = "https://api.dotnet-vue3-template-ru.local";

// PostgreSQL - один сервер на весь локальный стек: база приложения и база Logto
// стоят на нём соседями. Данные лежат в именованном volume, поэтому переживают
// перезапуск: пересоздавать их после каждого `yarn dev` дороже, чем держать один
// том на диске.
const postgresPassword = await builder.addParameter("postgres-password", {
  value: POSTGRES_PASSWORD,
  secret: true,
});
const postgres = await builder
  .addPostgres("postgres", { password: postgresPassword })
  .withDataVolume({ name: "dotnet-vue3-template-ru-pgdata" });
const db = await postgres.addDatabase("dotnet-vue3-template-ru-db");
const logtoDb = await postgres.addDatabase("logto-db");

// Mailpit - перехватчик почты. Logto отправляет через него письма с кодом
// подтверждения адреса и со ссылкой на смену пароля, наружу ничего не уходит.
// MP_SMTP_AUTH_ACCEPT_ANY нужен потому, что схема коннектора Logto требует пару
// логин-пароль обязательно, а проверять её тут нечем.
const mailpit = await builder
  .addContainer("mailpit", "axllent/mailpit")
  .withImageTag("v1.28")
  .withEnvironment("MP_SMTP_AUTH_ACCEPT_ANY", "1")
  .withEnvironment("MP_SMTP_AUTH_ALLOW_INSECURE", "1")
  .withEndpoint({
    targetPort: 1025,
    port: MAILPIT_SMTP_PORT,
    name: "smtp",
    isProxied: false,
  })
  .withHttpEndpoint({
    targetPort: 8025,
    port: MAILPIT_UI_PORT,
    name: "http",
    isProxied: false,
  });

// Logto - провайдер идентичности (ADR 0036). Точка входа делает сев базы и
// запускает сервис; флаг --swe означает "skip when exists" и делает повторный
// старт безопасным, --dapc убирает обращение к api.pwnedpasswords.com, из-за
// которого первый запуск без интернета висел бы на проверке пароля.
//
// isProxied: false - порт публикуется на хост напрямую, без прокси DCP. Обычно
// прокси удобен, здесь он мешает: он выдаёт случайный порт, а адрес провайдера
// обязан быть постоянным сразу в трёх местах - в списке адресов возврата внутри
// Logto, в issuer выпущенного токена и в Jwt:Authority, по которому API этот
// issuer сверяет. Случайный порт рассогласовал бы их.
const logto = await builder
  .addContainer("logto", "svhd/logto")
  .withImageTag("1.42")
  // Строку подключения Logto ждёт в виде URI. Aspire умеет отдать её в этом
  // формате готовой: uriExpression резолвится под потребителя, поэтому контейнер
  // получает адрес сервера в сети Aspire, а не адрес на хосте.
  .withEnvironment("DB_URL", await logtoDb.uriExpression())
  .withEnvironment("ENDPOINT", `http://localhost:${LOGTO_PORT}`)
  .withEnvironment("ADMIN_ENDPOINT", `http://localhost:${LOGTO_ADMIN_PORT}`)
  .withEnvironment("TRUST_PROXY_HEADER", "1")
  .withEntrypoint("sh")
  .withArgs(["-c", "npm run cli db seed -- --swe --dapc && npm start"])
  .withEndpoint({
    targetPort: 3001,
    port: LOGTO_PORT,
    name: "http",
    scheme: "http",
    isProxied: false,
  })
  .withEndpoint({
    targetPort: 3002,
    port: LOGTO_ADMIN_PORT,
    name: "admin",
    scheme: "http",
    isProxied: false,
  })
  .waitFor(logtoDb);

// Адрес выдачи токенов. Суффикс /oidc обязателен: именно он приезжает в issuer,
// и по нему же лежит discovery-документ.
const logtoAuthority = `http://localhost:${LOGTO_PORT}/oidc`;

// Backend (.NET). Строку подключения кладёт withReference в штатную секцию
// ConnectionStrings - приложение читает её через GetConnectionString.
const api = await builder
  .addProject(
    "api",
    "../apps/backend/DotnetVue3TemplateRu.Api/DotnetVue3TemplateRu.Api.csproj",
  )
  .withReference(db)
  .withEnvironment("Jwt__Authority", logtoAuthority)
  .withEnvironment("Jwt__Audience", API_RESOURCE_INDICATOR)
  .waitFor(db)
  .waitFor(logto);

// SPA (Vite). Порт фиксированный: его знают CORS-политика API
// (Cors:AllowedOrigins в appsettings.Development.json) и список адресов возврата
// в Logto. Случайный порт молча ломает либо запросы из браузера, либо вход.
//
// Адрес API и адрес провайдера приезжают переменными сборки Vite, а не через
// service discovery: их читает браузерный код, а не процесс Node.
//
// Идентификатор клиента приходит из настройки Logto, а не из константы: Logto
// выдаёт его сам и своего значения при создании приложения не принимает.
// Настройка идёт здесь, в колбэке переменных окружения, потому что к этому
// моменту Logto уже поднят, а Vite ещё не стартовал.
await builder
  .addViteApp("web", "../apps/frontend/web")
  .withYarn()
  .withHttpEndpoint({ name: "http", port: WEB_PORT })
  .withEnvironment("VITE_API_BASE_URL", api.getEndpoint("http"))
  .withEnvironment("VITE_OIDC_AUTHORITY", logtoAuthority)
  .withEnvironment("VITE_OIDC_RESOURCE", API_RESOURCE_INDICATOR)
  .withEnvironmentCallback(async (context) => {
    // Адрес базы с точки зрения хоста: порт Aspire выдаёт сам, поэтому его
    // спрашиваем у эндпоинта, а не закрепляем константой.
    const postgresEndpoint = await postgres.primaryEndpoint();
    const postgresHost = await postgresEndpoint.host();
    const postgresPort = await postgresEndpoint.port();

    const { clientId } = await provisionLogto({
      endpoint: `http://localhost:${LOGTO_PORT}`,
      adminEndpoint: `http://localhost:${LOGTO_ADMIN_PORT}`,
      databaseUrl: `postgres://${POSTGRES_USER}:${POSTGRES_PASSWORD}@${postgresHost}:${postgresPort}/logto-db`,
      apiIndicator: API_RESOURCE_INDICATOR,
      redirectUris: [
        `${WEB_ORIGIN}/auth/callback`,
        `${WEB_ORIGIN}/auth/silent`,
      ],
      postLogoutRedirectUris: [WEB_ORIGIN],
      corsAllowedOrigins: [WEB_ORIGIN],
      smtp: {
        host: "mailpit",
        port: 1025,
        from: "DotnetVue3TemplateRu <no-reply@dotnet-vue3-template-ru.local>",
      },
      users: [
        {
          username: "alice",
          password: "Passw0rd!2026",
          email: "alice@example.com",
          name: "Alice",
        },
        {
          username: "bob",
          password: "Passw0rd!2026",
          email: "bob@example.com",
          name: "Bob",
        },
      ],
    });
    await context.environment().set("VITE_OIDC_CLIENT_ID", clientId);
  })
  .withReference(api)
  .waitFor(api)
  .waitFor(mailpit);

await builder.build().run();
