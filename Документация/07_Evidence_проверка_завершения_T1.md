# Evidence-проверка завершения T1

## Назначение

Документ фиксирует первый безопасный шаг этапа 6:

- не считать успешную отправку T1 бизнес-завершением T1;
- отделить `Sent` от `Completed`;
- собрать внешние признаки завершения T1 в отдельную evidence-модель;
- не открывать T2 только по факту `TransportationId`, `TitleId` или HTTP 2xx.

## Что добавлено

### Модели

| Файл | Назначение |
|---|---|
| `KonturStageCompletionEvidence.cs` | Evidence-снимок внешних признаков завершения этапа |
| `KonturStageCompletionCheckResult.cs` | Результат проверки evidence без автоматического изменения состояния |

### Gateway-порт

| Файл | Назначение |
|---|---|
| `IKonturStageCompletionGateway.cs` | Порт получения evidence-признаков из внешнего или диагностического источника |
| `KonturStoredStageCompletionGateway.cs` | Безопасная реализация по уже сохраненным refs, raw-log и legacy timeline |

### Storage

| Файл | Назначение |
|---|---|
| `IKonturStageCompletionEvidenceRepository.cs` | Порт хранения evidence-снимков |
| `KonturStageCompletionEvidenceRepository.cs` | SQL repository для `TEpdKonturStageCompletionEvidence` |
| `012_Perdoc_TEpdKonturStageCompletionEvidence.sql` | SQL-граница истории проверок evidence |

### Use case

| Файл | Назначение |
|---|---|
| `CheckT1CompletionUseCase.cs` | Проверяет, достаточно ли evidence для подтверждения T1, но не выставляет `Completed` сам |
| `SyncStageStateRefsUseCase.cs` | Синхронизирует `TransportationId` и `TitleId` из `TEpdOperatorRef` в `KonturStageState` перед completion-check |

## Принцип работы

`CheckT1CompletionUseCase` выполняет проверку в таком порядке:

1. проверяет, что этап относится к T1;
2. синхронизирует `TransportationId` и `TitleId` из `TEpdOperatorRef` в `KonturStageState`;
3. читает `KonturStageState`;
4. требует `Sent = true`;
5. получает evidence через `IKonturStageCompletionGateway`;
6. сохраняет evidence в `TEpdKonturStageCompletionEvidence`;
7. возвращает решение `CanConfirmCompletion`.

Use case не делает:

- не выставляет `Completed = true`;
- не выставляет `NextStageAllowed = true`;
- не запускает T2;
- не меняет UI-runtime путь.

## Консервативный критерий T1

T1 нельзя считать завершенным, если есть только:

- `Sent = true`;
- `TransportationId`;
- `TitleId`;
- HTTP 2xx.

Эти признаки означают только техническую отправку или регистрацию результата вызова.

Для `CanConfirmCompletion = true` дополнительно нужен внешний признак, что Контур перешел в состояние, где допустим ответный титул T2.

На первом шаге это выражено через allow-list признаков:

- `T2_ALLOWED`;
- `T2_AVAILABLE`;
- `RECIPIENT_TITLE_ALLOWED`;
- `NEXT_TITLE_ALLOWED`;
- `WAITING_RECIPIENT_TITLE`;
- `WAITING_T2`.

Этот список является временным техническим allow-list.
После реального прогона его нужно заменить точными кодами или статусами Контур.

## Почему добавлен `KonturStoredStageCompletionGateway`

На текущем шаге нельзя безопасно добавлять новый сетевой polling без подтвержденного API endpoint и формата ответа.

Поэтому первая реализация gateway читает только уже сохраненные источники:

- `TEpdOperatorRef`;
- `TEpdOperatorRawLog`;
- `epd_timeline.last_status`.

Это дает возможность на контрольном прогоне увидеть:

- что уже сохраняется после T1;
- каких признаков не хватает;
- какой внешний статус или action нужно добавить в будущий API-backed gateway.

## Следующий практический шаг

На контрольном `TimelineId = 10212` нужно выполнить прогон:

1. пересобрать T1 XML;
2. заново подписать актуальный XML;
3. отправить T1;
4. выполнить `CheckT1CompletionUseCase`;
5. посмотреть строку в `TEpdKonturStageCompletionEvidence`;
6. зафиксировать, есть ли внешний признак готовности T2.

Если evidence не содержит такого признака, T1 остается в состоянии:

```text
Sent = true
Completed = false
NextStageAllowed = false
```

И это считается правильным поведением.

## Что закрыто этим шагом

Добавленная синхронизация refs закрывает практический разрыв сегодняшнего прогона:

- `LegacyRuntime` по-прежнему пишет `TEpdOperatorRef`;
- `KonturStageState` теперь получает `TransportationId` и `TitleId` без ручного SQL;
- evidence-проверка T1 работает по полному состоянию этапа, а не по урезанному снимку без внешних идентификаторов.
