# LLMGateway

Репозиторий состоит из нескольких связанных сервисов:

- **LLMGateway** - единый HTTP API для обращения к LLM-моделям через локальные
  или удаленные deployment-ы.
- **AuthService** - пользователи, роли и выдача JWT.
- **TeachingService** - преподавательский каталог тестов и заданий.
- **LLMTutorRoom** - учебное веб-приложение на ASP.NET Core и React, где
  ученики проходят тесты с серверным учетом времени, а результаты отправляются
  на проверку.
- **Shared.Auth** и **TeachingService.Contracts** - общие библиотеки для JWT и
  контрактов между сервисами.

## LLMGateway

`LLMGateway` скрывает детали подключения к конкретной модели. Внешний проект
обращается к логическому ключу модели, например `gemma3:12b`, а gateway сам
выбирает подходящий deployment.

Сейчас поддерживается:

- логическая модель с постоянным `key`;
- несколько deployment-ов для одной модели;
- выбор deployment-а по `priority`;
- OpenAI-compatible API;
- проверка доступности provider-а перед выполнением chat-запроса;
- ограничение одновременных запросов;
- rate limit через скользящее окно;
- корректные HTTP-статусы для недоступной модели, provider-а, лимитов и ошибок.

Пример добавления модели:

```http
POST /api/models
Content-Type: application/json

{
  "key": "gemma3:12b",
  "displayName": "Gemma 3 12B",
  "description": "Gemma 3 12B через OpenAI-compatible API"
}
```

Пример добавления deployment-а:

```http
POST /api/models/gemma3:12b/deployments
Content-Type: application/json

{
  "endpoint": "http://192.168.1.14:11434/v1",
  "providerModelId": "gemma3:12b",
  "isEnabled": true,
  "priority": 0,
  "maxConcurrentRequests": 1
}
```

Пример chat-запроса:

```http
POST /api/chat/gemma3:12b
Content-Type: application/json

{
  "temperature": 0.7,
  "messages": [
    { "role": "user", "content": "Ответь одним предложением." }
  ]
}
```

Полезные endpoint-ы:

- `GET /api/models` - список ключей моделей;
- `POST /api/models` - создать модель;
- `DELETE /api/models/{modelKey}` - удалить модель;
- `POST /api/models/{modelKey}/deployments` - добавить deployment;
- `PUT /api/models/deployments/{deploymentId}` - изменить deployment;
- `DELETE /api/models/deployments/{deploymentId}` - удалить deployment;
- `POST /api/models/deployments/{deploymentId}/rate-limits` - добавить правило
  скользящего окна;
- `DELETE /api/models/rate-limits/{ruleId}` - удалить правило.

## LLMTutorRoom

`LLMTutorRoom` - прототип учебного сервиса для преподавателей и учеников. Проект
построен на контроллерах ASP.NET Core, React-клиенте, PostgreSQL и RabbitMQ.

Основной функционал:

- JWT-авторизация;
- роли `Admin`, `Teacher`, `Student`;
- пользователи из `AuthService/appsettings.json` синхронизируются с auth-базой
  при запуске `AuthService`;
- администратор может добавлять преподавателей;
- преподаватель может создавать и редактировать тесты через `TeachingService`;
- у теста есть статус, дедлайн, время выполнения и описание;
- задания бывают трех типов: один ответ, несколько ответов, свободный ответ;
- для заданий можно задать баллы, варианты ответов и штраф за неправильный
  вариант при множественном выборе;
- задания можно редактировать, скрывать и удалять;
- ученик может начать тест, сохранить ответы и отправить попытку;
- таймер попытки хранится на backend-е, поэтому после перезагрузки страницы
  выполнение можно продолжить;
- после истечения времени ответы нельзя изменить;
- проверка свободных ответов идет асинхронно через RabbitMQ и `LLMGateway`.

Каталог тестов отделен от ученических попыток: `TeachingService` владеет тестами
и заданиями, а `LLMTutorRoom` хранит попытки, ответы и результаты проверки.

## Скриншоты

Рабочее место преподавателя:

![Рабочее место преподавателя](docs/images/llmtutorroom-teacher-tests.png)

Кабинет ученика:

![Кабинет ученика](docs/images/llmtutorroom-student-attempt.png)

## Запуск

Требования:

- .NET 9 SDK;
- PostgreSQL;
- Node.js и npm для сборки React-клиента;
- RabbitMQ для очереди проверки;
- Docker и Docker Compose для контейнерного запуска;
- Ollama или другой OpenAI-compatible provider, если нужен реальный LLM-вызов.

Сборка решения:

```bash
dotnet restore LLMGateway.sln
dotnet build LLMGateway.sln
```

Запуск сервисов:

```bash
dotnet run --project AuthService/AuthService.csproj
dotnet run --project TeachingService/TeachingService.csproj
dotnet run --project LLMGateway/LLMGateway.csproj
dotnet run --project LLMTutorRoom/LLMTutorRoom.csproj
```

Локальные порты по умолчанию:

- AuthService: `http://localhost:5210`
- TeachingService: `http://localhost:5212`
- LLMGateway: `http://localhost:5200`
- LLMTutorRoom: `http://localhost:5206`
- nginx: `http://localhost:8080`

React-клиент `LLMTutorRoom` собирается автоматически при сборке проекта и
попадает в `LLMTutorRoom/wwwroot`. Миграции базы данных применяются при запуске
соответствующего сервиса.

Структура фронтенда, клиентские маршруты и команды локальной разработки описаны
в [`LLMTutorRoom/ClientApp/README.md`](LLMTutorRoom/ClientApp/README.md).

Для локальной разработки используются настройки из `appsettings.json` и
`appsettings.Development.json`: строки подключения к PostgreSQL, JWT-настройки,
RabbitMQ-настройки и начальные пользователи.

### Docker Compose

Полный запуск всего стека в Docker:

```bash
docker compose up --build
```

После запуска:

- приложение через nginx: `http://localhost:8080`
- AuthService: `http://localhost:5210`
- TeachingService: `http://localhost:5212`
- LLMGateway: `http://localhost:5200`
- LLMTutorRoom: `http://localhost:5206`
- RabbitMQ management: `http://localhost:15672` (`llm` / `llm-dev`)
- PostgreSQL доступен с хоста на `localhost:5433`

Остановить стек:

```bash
docker compose down
```

Удалить данные PostgreSQL и RabbitMQ:

```bash
docker compose down -v
```

Для режима, где в Docker работают только nginx и RabbitMQ, а .NET-сервисы
запускаются на хосте через `dotnet run` или Rider:

```bash
docker compose -f docker-compose.infra.yml up
```

В этом режиме nginx проксирует в локальные порты `5210`, `5212`, `5200` и
`5206`, а RabbitMQ доступен на `localhost:5672`.

Контейнеры полного стека собираются как publish-образы. Изменения C# или
React-клиента не появляются внутри них автоматически: после правок нужен
повторный `docker compose up --build`. В infra-only режиме код выполняется на
хосте, поэтому обновления зависят от способа запуска: Rider перезапускает
проект, а `dotnet watch run` подхватывает изменения автоматически.
