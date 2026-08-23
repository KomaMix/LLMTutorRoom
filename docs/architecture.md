# Архитектура системы

Этот документ даёт общую карту проекта. Детали конкретного сервиса вынесены в
отдельные документы:

- [AuthService](auth-service.md)
- [TeachingService](teaching-service.md)
- [AttemptService](attempt-service.md)
- [LLMTutorRoom](llm-tutor-room.md)
- [ReviewService](review-service.md)
- [LLMGateway](llm-gateway.md)

## Границы ответственности

Система разделена не по экранам, а по владельцам данных и бизнес-правил.

| Сервис | Чем владеет | Чего не делает |
| --- | --- | --- |
| `AuthService` | Пользователи, роли, пароли и выдача JWT | Не хранит учебные данные |
| `TeachingService` | Тесты, задания, версии тестов и опубликованные правила проверки | Не создаёт проверки и не хранит ответы учеников |
| `AttemptService` | Попытки учеников, ответы, серверный таймер и факт явной отправки | Не хранит каталог тестов и не проверяет ответы |
| `LLMTutorRoom` | React-приложение, role-aware web-фасад и сборка overview | Не владеет бизнесовыми данными и собственной БД |
| `ReviewService` | Проверки, результаты заданий, ручная проверка, LLM-очередь, доступы и квоты преподавателей | Не хранит редактируемый каталог тестов и не выполняет LLM-запрос напрямую к provider-у |
| `LLMGateway` | Каталог моделей, deployment-ы, лимиты и вызов OpenAI-compatible provider-а | Не знает о тестах, учениках, попытках и баллах |

У каждого сервиса своя база данных. Сейчас они работают в одном экземпляре
PostgreSQL, но используют разные базы и не читают таблицы друг друга.

```mermaid
flowchart LR
    Browser["Браузер"] --> Nginx["nginx :8080"]
    Nginx --> Auth["AuthService"]
    Nginx --> Teaching["TeachingService"]
    Nginx --> Attempt["AttemptService"]
    Nginx --> Tutor["LLMTutorRoom + React"]
    Nginx -- "JWT: ручная проверка и доступы" --> Review["ReviewService"]
    Nginx --> Gateway["LLMGateway"]

    Tutor -- "внутренний HTTP: тесты" --> Teaching
    Tutor -- "внутренний HTTP: попытки для overview" --> Attempt
    Tutor -- "внутренний HTTP: overview и история" --> Review
    Attempt -- "внутренний HTTP: опубликованная версия" --> Teaching
    Review -- "HTTP: каталог и chat" --> Gateway

    Teaching -- "policy published" --> Rabbit[(RabbitMQ)]
    Attempt -- "attempt submitted" --> Rabbit
    Rabbit --> Review

    Auth --> AuthDb[(auth DB)]
    Teaching --> TeachingDb[(teaching DB)]
    Attempt --> AttemptDb[(attempt DB)]
    Review --> ReviewDb[(review DB)]
    Gateway --> GatewayDb[(gateway DB)]
```

## Два независимых события

Для создания проверки `ReviewService` нужны два факта:

1. `TestReviewPolicyPublishedV1` — неизменяемые правила конкретной версии теста.
2. `AttemptSubmittedV1` — ответы конкретной отправленной попытки.

Суффикс `V1` обозначает версию схемы сообщения, а не номер версии теста. Если
wire-контракт когда-нибудь изменится несовместимо, для него нужно создать новый
тип и routing key, например `...V2`; поле `Revision` продолжит обозначать версию
конкретного теста.

Они приходят от разных сервисов и могут быть доставлены в любом порядке.
`TeachingService` не запускает проверку: он сообщает только о публикации правил.
Бизнес-триггером является сдача попытки учеником, а жизненным циклом проверки
полностью управляет `ReviewService`.

```mermaid
sequenceDiagram
    actor Teacher as Преподаватель
    actor Student as Ученик
    participant Teaching as TeachingService
    participant Attempt as AttemptService
    participant Rabbit as RabbitMQ
    participant Review as ReviewService
    participant Gateway as LLMGateway

    Teacher->>Teaching: Публикует версию v4
    Teaching->>Teaching: Сохраняет version + policy + outbox
    Teaching-->>Rabbit: TestReviewPolicyPublishedV1
    Rabbit-->>Review: Снимок правил v4

    Student->>Attempt: Явно отправляет попытку v4
    Attempt->>Attempt: Сохраняет Submitted + outbox
    Attempt-->>Rabbit: AttemptSubmittedV1
    Rabbit-->>Review: Ответы ученика
    Review->>Review: Создаёт Review и выбирает Auto / Manual / Llm
    opt Есть LLM-задания
        Review->>Gateway: POST /api/chat/{modelKey}
        Gateway->>Gateway: Выбирает deployment и применяет лимиты
        Gateway-->>Review: Ответ модели
    end
```

Если попытка пришла раньше policy, она сохраняется в `PendingSubmissions`.
После получения точной версии policy проверка создаётся автоматически. Новая
версия теста никогда не подменяет правила уже начатой или отправленной попытки.

## Синхронное и асинхронное взаимодействие

Синхронный HTTP используется, когда ответ нужен пользователю прямо сейчас:

- вход и получение текущего пользователя;
- чтение и редактирование тестов;
- старт и сохранение попытки;
- чтение результатов и истории;
- выставление ручного балла напрямую в `ReviewService`;
- управление доступами к моделям напрямую в `ReviewService`.

RabbitMQ используется только для фактов, которые должны пережить временную
недоступность другого сервиса:

- опубликована новая версия правил проверки;
- ученик окончательно отправил попытку;
- внутри `ReviewService` нужно выполнить или повторить LLM-проверку.

`TeachingService` и `AttemptService` записывают интеграционное событие в outbox в
той же транзакции, что и бизнесовое изменение. Фоновый процесс доставляет outbox
в RabbitMQ. `ReviewService` ведёт inbox по `EventId`, поэтому повторная доставка
не создаёт вторую проверку. Это модель доставки **at least once**: дубль допустим
на транспорте, но не в бизнесовых данных.

Внутренняя очередь `review.processing` устроена иначе: сообщение публикуется
после создания `Review`, а источником истины остаётся база `ReviewService`. Если
публикация или worker падают, maintenance-процесс повторно ставит due-проверку в
очередь по её сохранённому состоянию.

## Версии теста и попытки

Логический тест имеет последовательные версии. Одновременно могут существовать
одна опубликованная версия и один редактируемый черновик. Публикация делает
черновик текущей версией, а предыдущую опубликованную версию — `Superseded`.

Попытка сохраняет номер версии и список допустимых заданий на момент старта.
Поэтому:

- правки будущего черновика не меняют уже начатую работу;
- публикация v5 не отменяет попытку или проверку v4;
- старые незавершённые проверки остаются видимыми;
- завершённая история загружается отдельно и постранично.

Фоновое наступление дедлайна переводит попытку в `Expired`, но не считается
отправкой и не создаёт проверку. `AttemptSubmittedV1` возникает только после
явного submit ученика, принятого до дедлайна.

## Сеть и доверие

В полном Docker Compose рекомендуемой точкой входа в приложение является nginx
на порту `8080`. Он маршрутизирует только публичные `/api/*` и интерфейс React.
Любой путь `/internal/*` получает `404`.

Development Compose дополнительно публикует на хост прямые порты `AuthService`
и `LLMGateway` для отладки. `TeachingService`, `AttemptService`, `ReviewService`
и `LLMTutorRoom` доступны пользователю только через разрешённые nginx-маршруты.
Это не production security boundary: особенно важно помнить, что API
`LLMGateway` сейчас не имеет собственной аутентификации. Во внешнем окружении
прямые host-порты нужно закрыть и оставить контролируемый ingress.

Публичные команды `AttemptService` и `ReviewService` самостоятельно проверяют
JWT и роль. `AttemptService` принимает команды ученика, а `ReviewService` —
ручную проверку преподавателя и управление доступами администратора. Внутренние
API обоих сервисов не имеют отдельного API key: они доступны в межсервисном
Docker-контуре, а nginx возвращает `404` для `/internal/*`. `LLMTutorRoom`
обращается к ним только для сборки overview и истории. При другом способе
развёртывания эту границу нужно сохранить сетевой политикой или добавить
межсервисную аутентификацию.

## Где искать код

- Регистрация зависимостей и запуск каждого сервиса находятся в его `Program.cs`.
- HTTP-граница — в `Controllers/`.
- Бизнесовые сценарии — в `Services/`.
- EF Core-модель и владение данными — в `Data/*DbContext.cs` и `Models/`.
- Общие wire-контракты событий и internal API — в
  [`AttemptService.Contracts`](../AttemptService.Contracts),
  [`TeachingService.Contracts`](../TeachingService.Contracts) и
  [`ReviewService.Contracts`](../ReviewService.Contracts).
- Docker-топология — в [`docker-compose.yml`](../docker-compose.yml), публичная
  маршрутизация — в [`infra/nginx`](../infra/nginx).

Для знакомства с кодом удобно сначала прочитать этот документ, затем документ
нужного сервиса и только после этого переходить к указанным в нём точкам входа.
