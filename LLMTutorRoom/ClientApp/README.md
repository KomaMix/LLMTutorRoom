# LLMTutorRoom ClientApp

React-клиент учебного кабинета. Исходники находятся в `src`, а production-сборка
генерируется в `../wwwroot`. Файлы из `wwwroot` вручную менять не нужно.

Требуется Node.js `20.19.x`, `22.13+` или `24+`. Рекомендуемая версия для nvm
указана в `.nvmrc`.

## Команды

```bash
npm ci
npm run dev
npm run lint
npm run build
```

`npm run build` сначала запускает линтер, затем собирает приложение Vite.

Для `npm run dev` должны быть запущены .NET-сервисы на стандартных локальных
портах. Vite направляет запросы по тем же границам, что и nginx:

- `/api/auth` и `/api/users` → `AuthService` (`5210`);
- `/api/teaching` → `TeachingService` (`5212`);
- `/api/attempts` → `AttemptService` (`5216`);
- `/api/classroom` → `LLMTutorRoom` (`5206`);
- `/api/llm` → `LLMGateway` (`5200`).

## Структура

```text
src/
  main.jsx          запуск React и общие providers
  app/              маршрутизация, layout и загрузка overview
  auth/             сессия и экран входа
  api/              HTTP-клиент и API отдельных микросервисов
  features/
    admin/           преподаватели и доступ к моделям
    teacher/         тесты, задания, проверки и модели
    student/         попытка, автосохранение и результаты
  shared/            общие UI-компоненты, статусы и форматирование
```

Компонент страницы не должен самостоятельно собирать заголовки авторизации или
разбирать HTTP-ответ. Для этого используются функции из `api/`. Общее состояние
сессии хранится в `AuthProvider`, серверное состояние кабинета — в
`useOverview`, а состояние форм остается внутри соответствующей feature.

## Маршруты

- `/admin/teachers`, `/admin/model-access`;
- `/teacher/dashboard`, `/teacher/tests`, `/teacher/tests/:testId`, `/teacher/reviews`,
  `/teacher/models`;
- `/student/tests`, `/student/tests/:testId`, `/student/results`.

ASP.NET Core настроен на fallback к `index.html`, поэтому прямое открытие этих
URL работает и в production.

## Важные правила

- Сервер является источником истины для времени попытки и статусов проверки.
- Все сохранения ответов ученика сериализуются: более старый запрос не может
  затереть новую ревизию ответа.
- Дедлайн из поля `date` преобразуется в конец выбранного дня в локальном
  часовом поясе пользователя.
- При добавлении нового экрана размещайте его в соответствующей `features/`, а
  повторно используемый элемент — в `shared/ui`.
