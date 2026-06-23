# LLMGateway

Сервис для работы с LLM-моделями через единый HTTP API.

Вызывающий проект отправляет ключ модели и сообщения, не учитывая ее фактическое
размещение и способ подключения. Одна модель может быть доступна одновременно
локально и через удаленный API.

## Основные понятия

- **Модель** - логическая модель с постоянным ключом, например `mistral:7b`.
  По этому ключу к ней обращаются вызывающие проекты.
- **Реализация модели** (`deployment`) - конкретный способ выполнить запрос к
  модели: локальный Ollama-сервер или удаленный OpenAI-совместимый API.
- **Приоритет** - порядок выбора реализации. Чем меньше значение `priority`,
  тем раньше она будет выбрана.

При обработке запроса сервис выбирает первую доступную реализацию модели по
приоритету. Поэтому ключ `mistral:7b` может сначала выполняться локально, а при
недоступности локального сервера - через удаленный API. Для вызывающей стороны
характеристики модели и ключ остаются одинаковыми.

## API

Получить ключи доступных моделей:

```http
GET /api/models
```

```json
[
  "gpt-5.4-mini",
  "mistral-large-latest",
  "mistral:7b"
]
```

Создать логическую модель:

```http
POST /api/models
Content-Type: application/json

{
  "key": "mistral:7b",
  "displayName": "Mistral 7B",
  "description": "Mistral 7B с локальным и удаленным deployment-ами"
}
```

Добавить локальную реализацию модели через Ollama:

```http
POST /api/models/mistral:7b/deployments
Content-Type: application/json

{
  "providerType": "Ollama",
  "endpoint": "http://localhost:11434",
  "providerModelId": "mistral:7b",
  "priority": 0,
  "maxConcurrentRequests": 1
}
```

Добавить ограничение количества запросов для реализации модели:

```http
POST /api/models/deployments/1/rate-limits
Content-Type: application/json

{
  "windowSeconds": 60,
  "maxRequests": 20
}
```

Изменить реализацию модели:

```http
PUT /api/models/deployments/1
Content-Type: application/json

{
  "providerType": "Ollama",
  "endpoint": "http://localhost:11434",
  "providerModelId": "mistral:7b",
  "isEnabled": true,
  "priority": 0,
  "maxConcurrentRequests": 1
}
```

Удалить правило ограничения:

```http
DELETE /api/models/rate-limits/1
```

Отправить чат-запрос:

```http
POST /api/chat
Content-Type: application/json

{
  "model": "mistral:7b",
  "temperature": 0.7,
  "messages": [
    { "role": "system", "content": "Отвечай кратко." },
    { "role": "user", "content": "Кто ты?" }
  ]
}
```
