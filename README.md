# LLMGateway

Репозиторий состоит из двух связанных проектов:

- **LLMGateway** - единый HTTP API для обращения к LLM-моделям через локальные
  или удаленные deployment-ы.
- **LLMTutorRoom** - учебное веб-приложение на ASP.NET Core и React, где
  преподаватели создают тесты, а ученики проходят их с серверным учетом времени.

## LLMGateway

`LLMGateway` скрывает детали подключения к конкретной модели. Внешний проект
обращается к логическому ключу модели, например `gemma3:12b`, а gateway сам
выбирает подходящий deployment.

Сейчас поддерживается:

- логическая модель с постоянным `key`;
- несколько deployment-ов для одной модели;
- выбор deployment-а по `priority`;
- Ollama и OpenAI-совместимые API;
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
  "description": "Gemma 3 12B через Ollama или совместимый API"
}
```

Пример добавления deployment-а:

```http
POST /api/models/gemma3:12b/deployments
Content-Type: application/json

{
  "providerType": "Ollama",
  "endpoint": "http://192.168.1.14:11434",
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
построен на контроллерах ASP.NET Core, React-клиенте и PostgreSQL.

Основной функционал:

- JWT-авторизация;
- роли `Admin`, `Teacher`, `Student`;
- пользователи из `appsettings.json` синхронизируются с базой при запуске;
- администратор может добавлять преподавателей;
- преподаватель может создавать и редактировать тесты;
- у теста есть статус, дедлайн, время выполнения и описание;
- задания бывают трех типов: один ответ, несколько ответов, свободный ответ;
- для заданий можно задать баллы, варианты ответов и штраф за неправильный
  вариант при множественном выборе;
- задания можно редактировать, скрывать и удалять;
- ученик может начать тест, сохранить ответы и отправить попытку;
- таймер попытки хранится на backend-е, поэтому после перезагрузки страницы
  выполнение можно продолжить;
- после истечения времени ответы нельзя изменить;
- автоматическая проверка ответов через LLM пока вынесена в отдельный будущий
  этап.

Функционал тестов отделен от будущей LLM-проверки: создание тестов, прохождение
и хранение попыток не завязаны на выбор модели.

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
- Ollama или другой OpenAI-совместимый provider, если нужен реальный LLM-вызов.

Сборка решения:

```bash
dotnet restore LLMGateway.sln
dotnet build LLMGateway.sln
```

Запуск gateway:

```bash
dotnet run --project LLMGateway/LLMGateway.csproj
```

Запуск учебного приложения:

```bash
dotnet run --project LLMTutorRoom/LLMTutorRoom.csproj
```

React-клиент `LLMTutorRoom` собирается автоматически при сборке проекта и
попадает в `LLMTutorRoom/wwwroot`. Миграции базы данных применяются при запуске
приложения.

Для локальной разработки используются настройки из `appsettings.json` и
`appsettings.Development.json`: строка подключения к PostgreSQL, JWT-настройки и
начальные пользователи.
