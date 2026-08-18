# LLMGateway

Репозиторий состоит из нескольких связанных сервисов:

- **LLMGateway** - единый HTTP API для обращения к LLM-моделям через локальные
  или удаленные deployment-ы.
- **AuthService** - пользователи, роли и выдача JWT.
- **TeachingService** - преподавательский каталог тестов и заданий, включая
  текущий выбор модели и неизменяемые ревизии правил проверки.
- **LLMTutorRoom** - учебное веб-приложение на ASP.NET Core и React, где
  ученики проходят тесты с серверным учетом времени, а отправленные попытки
  фиксируются и передаются на проверку.
- **ReviewService** - внутренний сервис, который сам создает и выполняет
  автоматические, ручные и LLM-проверки.
- **Shared.Auth**, **TeachingService.Contracts** и **ReviewService.Contracts** -
  общие библиотеки для JWT и контрактов между сервисами.

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
- отправка попытки фиксируется вместе с событием в transactional outbox;
- результаты и настройки доступных преподавателю моделей запрашиваются через
  внутренний API `ReviewService`.

Каталог тестов отделен от ученических попыток: `TeachingService` владеет тестами
и заданиями, а `LLMTutorRoom` хранит попытки и ответы. Результатами проверки
владеет `ReviewService`.

### Версии тестов

Опубликованный тест неизменяем. Преподаватель создаёт из него один отдельный
черновик следующей версии (например, v4 из v3), может сколько угодно менять
настройки и задания, а затем явно нажимает «Опубликовать». Обычные сохранения
черновика не создают событий для `ReviewService`.

При публикации новая версия становится `Published`, предыдущая —
`Superseded`, и `TeachingService` создаёт ровно одну новую политику проверки и
одно outbox-событие. Номера версий монотонны и после удаления черновика не
переиспользуются. Начатые и уже отправленные попытки остаются привязаны к своей
точной версии; публикация новой версии не даёт дополнительную попытку и не
отменяет старую проверку. Завершённые проверки прошлых версий скрыты в интерфейсе
по умолчанию, но доступны через историю.

Незавершённые проверки всех версий всегда остаются в основном обзоре. Завершённые
результаты загружаются страницами по 25 записей и кнопкой «Загрузить ещё»;
курсор строится по паре `SubmittedAt + Id`, поэтому одинаковое время отправки не
приводит к пропускам. У преподавателя основной список показывает результаты
текущих опубликованных версий, а режим «Все версии» лениво читает полную историю.

Доставленные сообщения producer outbox удаляются через 30 дней (настраивается в
`IntegrationEvents` и `AttemptSubmissionOutbox`), а обработанный inbox
`ReviewService` — через 90 дней (`ReviewStorage`). Поэтому технические копии
политик и ученических ответов не растут бесконечно. Сами опубликованные версии,
попытки, политики и результаты сохраняются как учебная история. Ответы, которые
ждут опоздавшую policy, автоматически не удаляются: после 24 часов сервис пишет
ошибку в журнал, чтобы оператор проверил integration DLQ.

## ReviewService

`ReviewService` управляет полным жизненным циклом проверки. Другие сервисы не
запускают конкретный способ проверки и не вызывают LLM от его имени:

1. `TeachingService` хранит текущий выбор модели для теста. При публикации он
   создает неизменяемую ревизию правил и через outbox публикует
   `TestReviewPolicyPublishedV1`.
2. `LLMTutorRoom` хранит попытку и при окончательной отправке через свой outbox
   публикует только факт `AttemptSubmittedV1`.
3. `ReviewService` сопоставляет попытку с ревизией правил и сам решает, какие
   задания проверить сразу (`Auto`), передать преподавателю (`Manual`) или
   поставить в очередь LLM-проверки (`Llm`). Порядок прихода двух событий не
   важен: попытка ожидает нужную ревизию политики.

`ReviewService` хранит результаты по заданиям, доступы преподавателей к моделям,
расход квот и состояние повторных попыток. В созданной проверке
фиксируется `ModelKeySnapshot` из опубликованной ревизии. Поле
`ActualModelKey` намеренно не хранится.

Интеграционные события передаются через RabbitMQ exchange
`review.integration`. Outbox-ы производителей и inbox `ReviewService` защищают
поток от потери и повторной обработки событий. Ошибочные сообщения направляются
в отдельные DLQ, а LLM-задания выполняются собственной очередью `ReviewService`.
Ошибки LLM-проверки не переводят задание в ручной режим: работа остаётся в
`RetryScheduled` и повторяется без ограничения числа попыток. После прохождения
настроенного списка задержек для следующих попыток используется его последнее
значение. `Manual` применяется только к заданиям, для которых преподаватель
изначально выбрал ручной режим проверки.

HTTP API сервиса предназначен только для межсервисного взаимодействия. Маршруты
`/internal/*` не имеют прикладной аутентификации и доступны только из защищённой
внутренней сети. У сервиса нет маршрута в nginx, и его нельзя публиковать в
интернет.

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
dotnet run --project ReviewService/ReviewService.csproj
dotnet run --project LLMTutorRoom/LLMTutorRoom.csproj
```

Локальные порты по умолчанию при запуске .NET-сервисов через `dotnet run` или
Rider:

- AuthService: `http://localhost:5210`
- TeachingService: `http://localhost:5212`
- LLMGateway: `http://localhost:5200`
- ReviewService: `http://127.0.0.1:5214` (только внутренний loopback API)
- LLMTutorRoom: `http://localhost:5206`
- nginx: `http://localhost:8080`

Проверки состояния `ReviewService` не требуют внутреннего ключа:

- `GET http://127.0.0.1:5214/health/live` - процесс запущен;
- `GET http://127.0.0.1:5214/health/ready` - доступны PostgreSQL и RabbitMQ.

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
- публичный API TeachingService: `http://localhost:8080/api/teaching/*`
- LLMGateway: `http://localhost:5200`
- LLMTutorRoom: `http://localhost:5206`
- TeachingService доступен контейнерам nginx и backend-сети как
  `http://teaching-service:8080`, но не публикует порт `5212` на хосте;
  `/internal/*` через nginx не маршрутизируется
- ReviewService доступен только другим контейнерам как
  `http://review-service:8080` и не публикует порт на хосте
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
`5206`, а RabbitMQ доступен на `localhost:5672`. `ReviewService` также нужно
запустить на хосте на loopback-порту `5214`; nginx его не проксирует.
Таким образом, `localhost:5212` относится только к локальному запуску
TeachingService (`dotnet run`/Rider) и infra-only compose, а не к полному
Docker Compose.

Контейнеры полного стека собираются как publish-образы. Изменения C# или
React-клиента не появляются внутри них автоматически: после правок нужен
повторный `docker compose up --build`. В infra-only режиме код выполняется на
хосте, поэтому обновления зависят от способа запуска: Rider перезапускает
проект, а `dotnet watch run` подхватывает изменения автоматически.
