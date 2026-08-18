// Первоначальная настройка Logto для локальной разработки (ADR 0036).
//
// Занимает место импорта realm: после пересоздания контейнера приложение SPA,
// ресурс API, коннектор почты и тестовые пользователи восстанавливаются сами.
//
// Как получается доступ к Management API. Команда `logto db seed` заводит
// служебное приложение с постоянным идентификатором `m-default`, которому уже
// разрешён Management API рабочего пространства. Свой секрет Logto наружу не
// отдаёт, поэтому единственное обращение к его базе - чтение этой строки.
// Дальше всё идёт документированными вызовами Management API.
//
// Все шаги идемпотентны: сначала ищем по естественному ключу, создаём только
// если не нашли. Повторный `yarn dev` ничего не дублирует.
import { Client } from "pg";

/** Служебное приложение, которому сев разрешил Management API рабочего пространства. */
const MANAGEMENT_APP_ID = "m-default";

/** Индикатор ресурса Management API у рабочего пространства. Значение задано самим Logto. */
const MANAGEMENT_API_INDICATOR = "https://default.logto.app/api";

/** Идентификатор фабрики коннекторов SMTP в каталоге Logto. */
const SMTP_CONNECTOR_FACTORY_ID = "simple-mail-transfer-protocol";

/**
 * Шаблоны писем. Схема коннектора требует их обязательно, а `usageType` покрывает
 * четыре случая: вход, регистрацию, восстановление пароля и всё остальное.
 * Место для кода подстановки - `{{code}}`.
 */
const SMTP_TEMPLATES = [
  {
    usageType: "SignIn",
    contentType: "text/plain",
    subject: "DotnetVue3TemplateRu: код для входа",
    content: "Код для входа: {{code}}. Он действует 10 минут.",
  },
  {
    usageType: "Register",
    contentType: "text/plain",
    subject: "DotnetVue3TemplateRu: код для регистрации",
    content: "Код для подтверждения адреса: {{code}}. Он действует 10 минут.",
  },
  {
    usageType: "ForgotPassword",
    contentType: "text/plain",
    subject: "DotnetVue3TemplateRu: восстановление пароля",
    content: "Код для смены пароля: {{code}}. Он действует 10 минут.",
  },
  {
    usageType: "Generic",
    contentType: "text/plain",
    subject: "DotnetVue3TemplateRu: код подтверждения",
    content: "Код подтверждения: {{code}}. Он действует 10 минут.",
  },
];

export interface LogtoUserSeed {
  username: string;
  password: string;
  email: string;
  name: string;
}

export interface LogtoProvisioningOptions {
  /** Адрес OIDC и Management API, например http://localhost:3441. */
  endpoint: string;
  /** Адрес консоли администратора: на нём же лежит выдача токена служебному приложению. */
  adminEndpoint: string;
  /** Строка подключения к базе Logto в виде URI - нужна только чтобы прочитать секрет `m-default`. */
  databaseUrl: string;
  /** Индикатор ресурса API. Он же приезжает в `aud` токена и в `Jwt:Audience`. */
  apiIndicator: string;
  /** Адреса возврата после входа. */
  redirectUris: string[];
  /** Адреса возврата после выхода. */
  postLogoutRedirectUris: string[];
  /** Разрешённые источники запросов из браузера. */
  corsAllowedOrigins: string[];
  smtp: { host: string; port: number; from: string };
  users: LogtoUserSeed[];
  /** Куда писать ход настройки. По умолчанию в консоль. */
  log?: (message: string) => void;
}

export interface LogtoProvisioningResult {
  /** Идентификатор клиента SPA. Logto выдаёт его сам, поэтому он не закоммичен. */
  clientId: string;
}

/**
 * Доводит Logto до состояния, в котором SPA может войти, а API - проверить токен.
 * Возвращает идентификатор клиента SPA.
 */
export async function provisionLogto(
  options: LogtoProvisioningOptions,
): Promise<LogtoProvisioningResult> {
  const log =
    options.log ?? ((message: string) => console.log(`[logto] ${message}`));

  await waitForLogto(options.endpoint, log);

  const secret = await readManagementAppSecret(options.databaseUrl);
  const token = await requestManagementToken(options.adminEndpoint, secret);
  const api = new ManagementApi(options.endpoint, token);

  await ensureApiResource(api, options.apiIndicator, log);
  const clientId = await ensureSpaApplication(api, options, log);
  await ensureSmtpConnector(api, options.smtp, log);
  await ensureUsers(api, options.users, log);

  log(`настройка завершена, идентификатор клиента SPA: ${clientId}`);
  return { clientId };
}

/**
 * Ждёт, пока Logto начнёт отвечать документом конфигурации OIDC.
 * На чистой базе контейнер тратит на сев и старт около пятнадцати секунд.
 */
async function waitForLogto(
  endpoint: string,
  log: (message: string) => void,
): Promise<void> {
  const discoveryUrl = `${endpoint}/oidc/.well-known/openid-configuration`;
  const deadline = Date.now() + 180_000;
  let reported = false;

  for (;;) {
    try {
      const response = await fetch(discoveryUrl);
      if (response.ok) {
        return;
      }
    } catch {
      // Контейнер ещё поднимается: отказ соединения здесь - ожидаемое состояние, а не ошибка.
    }

    if (!reported) {
      log("жду готовности Logto");
      reported = true;
    }
    if (Date.now() > deadline) {
      throw new Error(`Logto не ответил на ${discoveryUrl} за 180 секунд`);
    }
    await delay(1000);
  }
}

/** Читает секрет служебного приложения. Единственное место, где нужен доступ к базе Logto. */
async function readManagementAppSecret(databaseUrl: string): Promise<string> {
  const client = new Client({ connectionString: databaseUrl });
  await client.connect();
  try {
    const result = await client.query<{ secret: string }>(
      "select secret from applications where id = $1",
      [MANAGEMENT_APP_ID],
    );
    const secret = result.rows[0]?.secret;
    if (!secret) {
      throw new Error(
        `в базе Logto нет приложения ${MANAGEMENT_APP_ID}: сев базы не отработал или образ несовместим`,
      );
    }
    return secret;
  } finally {
    await client.end();
  }
}

/** Обменивает секрет служебного приложения на токен Management API. */
async function requestManagementToken(
  adminEndpoint: string,
  secret: string,
): Promise<string> {
  const credentials = Buffer.from(`${MANAGEMENT_APP_ID}:${secret}`).toString(
    "base64",
  );
  const response = await fetch(`${adminEndpoint}/oidc/token`, {
    method: "POST",
    headers: {
      authorization: `Basic ${credentials}`,
      "content-type": "application/x-www-form-urlencoded",
    },
    body: new URLSearchParams({
      grant_type: "client_credentials",
      resource: MANAGEMENT_API_INDICATOR,
      scope: "all",
    }),
  });

  if (!response.ok) {
    throw new Error(
      `Logto не выдал токен Management API: ${response.status} ${await response.text()}`,
    );
  }

  const payload = (await response.json()) as { access_token?: string };
  if (!payload.access_token) {
    throw new Error("ответ Logto на запрос токена не содержит access_token");
  }
  return payload.access_token;
}

/** Тонкая обёртка над Management API: только то, что нужно этому скрипту. */
class ManagementApi {
  constructor(
    private readonly endpoint: string,
    private readonly token: string,
  ) {}

  async get<T>(path: string): Promise<T> {
    return this.send<T>("GET", path);
  }

  async post<T>(path: string, body: unknown): Promise<T> {
    return this.send<T>("POST", path, body);
  }

  private async send<T>(
    method: string,
    path: string,
    body?: unknown,
  ): Promise<T> {
    const response = await fetch(`${this.endpoint}/api${path}`, {
      method,
      headers: {
        authorization: `Bearer ${this.token}`,
        ...(body === undefined ? {} : { "content-type": "application/json" }),
      },
      body: body === undefined ? undefined : JSON.stringify(body),
    });

    if (!response.ok) {
      throw new Error(
        `${method} ${path} вернул ${response.status}: ${await response.text()}`,
      );
    }
    return (await response.json()) as T;
  }
}

interface LogtoResource {
  id: string;
  indicator: string;
}

async function ensureApiResource(
  api: ManagementApi,
  indicator: string,
  log: (message: string) => void,
): Promise<void> {
  const existing = await api.get<LogtoResource[]>("/resources");
  if (existing.some((resource) => resource.indicator === indicator)) {
    log(`ресурс API ${indicator} уже есть`);
    return;
  }

  await api.post("/resources", { name: "DotnetVue3TemplateRu API", indicator });
  log(`ресурс API ${indicator} создан`);
}

interface LogtoApplication {
  id: string;
  name: string;
  type: string;
}

async function ensureSpaApplication(
  api: ManagementApi,
  options: LogtoProvisioningOptions,
  log: (message: string) => void,
): Promise<string> {
  const applicationName = "DotnetVue3TemplateRu SPA";
  const existing = await api.get<LogtoApplication[]>("/applications");
  const found = existing.find(
    (application) => application.name === applicationName,
  );
  if (found) {
    log(`приложение SPA уже есть: ${found.id}`);
    return found.id;
  }

  const created = await api.post<LogtoApplication>("/applications", {
    name: applicationName,
    type: "SPA",
    oidcClientMetadata: {
      redirectUris: options.redirectUris,
      postLogoutRedirectUris: options.postLogoutRedirectUris,
    },
    customClientMetadata: {
      corsAllowedOrigins: options.corsAllowedOrigins,
    },
  });
  log(`приложение SPA создано: ${created.id}`);
  return created.id;
}

interface LogtoConnector {
  id: string;
  connectorId: string;
}

/**
 * Коннектор почты. Без него Logto не отправит письмо с подтверждением адреса
 * и со ссылкой на восстановление пароля. Локально письма забирает Mailpit.
 */
async function ensureSmtpConnector(
  api: ManagementApi,
  smtp: LogtoProvisioningOptions["smtp"],
  log: (message: string) => void,
): Promise<void> {
  const existing = await api.get<LogtoConnector[]>("/connectors");
  if (
    existing.some(
      (connector) => connector.connectorId === SMTP_CONNECTOR_FACTORY_ID,
    )
  ) {
    log("коннектор SMTP уже есть");
    return;
  }

  await api.post("/connectors", {
    connectorId: SMTP_CONNECTOR_FACTORY_ID,
    config: {
      host: smtp.host,
      port: smtp.port,
      fromEmail: smtp.from,
      // Поле обязательно по схеме коннектора, хотя Mailpit проверку не делает:
      // он запущен с MP_SMTP_AUTH_ACCEPT_ANY и принимает любую пару.
      auth: { type: "login", user: "logto", pass: "logto" },
      templates: SMTP_TEMPLATES,
    },
  });
  log("коннектор SMTP создан");
}

interface LogtoUser {
  id: string;
  username: string | null;
}

async function ensureUsers(
  api: ManagementApi,
  users: LogtoUserSeed[],
  log: (message: string) => void,
): Promise<void> {
  const existing = await api.get<LogtoUser[]>("/users");

  for (const user of users) {
    if (existing.some((candidate) => candidate.username === user.username)) {
      log(`пользователь ${user.username} уже есть`);
      continue;
    }

    await api.post("/users", {
      username: user.username,
      password: user.password,
      primaryEmail: user.email,
      name: user.name,
    });
    log(`пользователь ${user.username} создан`);
  }
}

function delay(milliseconds: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, milliseconds));
}
