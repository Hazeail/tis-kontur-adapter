# Карта БД и SQL-границ реконструкции

## Назначение

Документ фиксирует таблицы, SQL-скрипты и границы хранения, которые участвуют в адаптере Контур ЭТрН.

Цель:

- видеть, какие структуры уже являются интеграционным контрактом;
- понимать, какие таблицы относятся к `Perdoc`, а какие живут в legacy-контуре TIS;
- не потерять статические данные и настройки при реконструкции.

## SQL-пакет интеграции

Исходный пакет в реконструкции:

`C:\Users\gl\source\repos\Kontur\7. Реконструкция\Исходники\Sql\KonturIntegration`

### Список скриптов

| Скрипт | Назначение |
|---|---|
| `001_Perdoc_TEpdOperatorRef.sql` | Таблица внешних идентификаторов и refs оператора |
| `002_Perdoc_TEpdOperatorRawLog.sql` | Таблица raw-log по API-запросам |
| `003_Perdoc_TEpdOperatorSettings.sql` | Таблица настроек оператора |
| `004_Perdoc_TEpdOperatorSettings_Seed_Kontur.sql` | Базовый seed настроек Контур |
| `005_Perdoc_TEpdTitleArtifact.sql` | Таблица XML/SGN артефактов |
| `006_Perdoc_TEpdOperatorRoleAccess.sql` | Таблица соответствия ролей и boxId |
| `007_Perdoc_TEpdOperatorRoleAccess_Seed_TestRoles.sql` | Seed тестовых ролей доступа |
| `008_Perdoc_TEpdOperatorSettings_Seed_Oidc_Kontur.sql` | Seed OIDC-настроек Контур |
| `009_Perdoc_TEpdStageSignerSelection.sql` | Таблица выбора подписанта этапа |
| `010_Perdoc_TEpdKonturTestMode.sql` | Таблица флага `Kontur-only` по `TimelineId` |
| `011_Perdoc_TEpdKonturStageState.sql` | Таблица явного состояния этапа Контур ЭТрН |
| `012_Perdoc_TEpdKonturStageCompletionEvidence.sql` | Таблица evidence-снимков проверки бизнес-завершения этапа |

## Интеграционные таблицы `Perdoc`

### 1. `dbo.TEpdOperatorRef`

Назначение:

- хранение внешних идентификаторов, которые возвращает оператор.

#### Поля

| Поле | Типовой смысл |
|---|---|
| `Id` | PK |
| `TimelineId` | Внутренний timeline |
| `OperatorCode` | Код оператора, для Контур — `Kontur` |
| `RefType` | Тип внешнего ref |
| `RefValue` | Значение ref |
| `SourceEventId` | Идентификатор исходного события |
| `CreatedAt` | Дата записи |

#### Типовые `RefType`

| `RefType` | Смысл |
|---|---|
| `transportationId` | Идентификатор перевозочного документа у Контур |
| `titleId` | Идентификатор титула |
| `draftId` | Идентификатор черновика |
| `uid_zak` | Внутренне используемая связка для продолжения T2/T3/T4 |

### 2. `dbo.TEpdOperatorRawLog`

Назначение:

- хранение sanitized-логов интеграционных вызовов.

#### Поля

| Поле | Типовой смысл |
|---|---|
| `Id` | PK |
| `TimelineId` | Внутренний timeline |
| `OperatorCode` | Код оператора |
| `Direction` | Направление: request/response |
| `Endpoint` | Вызванный endpoint |
| `HttpStatus` | HTTP-код |
| `SanitizedPayload` | Очищенный payload |
| `CreatedAt` | Дата записи |

### 3. `dbo.TEpdOperatorSettings`

Назначение:

- хранение настроек оператора Контур.

#### Поля

| Поле | Типовой смысл |
|---|---|
| `Id` | PK |
| `OperatorCode` | Код оператора |
| `SettingKey` | Ключ настройки |
| `SettingValue` | Значение |
| `IsSecret` | Секретная настройка или нет |
| `UpdatedAt` | Дата обновления |

#### Ключевые `SettingKey`

| Ключ | Назначение |
|---|---|
| `ApiKey` | Общий API-key, если используется |
| `OidcAccessToken` | Общий access token |
| `OidcRefreshToken` | Общий refresh token |
| `OidcClientId` | Общий client id |
| `OidcClientSecret` | Общий client secret |
| `OidcTokenEndpoint` | Token endpoint |
| `OidcTokenExpiresAtUtc` | Время истечения access token |
| `SenderBoxId_T1` | boxId для T1 |
| `DanaflexSenderBoxId` | Дополнительный boxId из раннего контура |
| `XSolutionInfo` | Заголовок `X-Solution-Info` |

#### Ролевые ключи, которые уже встречались в контуре

| Ключ | Назначение |
|---|---|
| `ApiKey_Consignor` | Настройка для ГО |
| `ApiKey_Carrier` | Настройка для ТК |
| `ApiKey_Consignee` | Настройка для ГП |
| `OidcAccessToken_Consignor` | Токен ГО |
| `OidcAccessToken_Carrier` | Токен ТК |
| `OidcAccessToken_Consignee` | Токен ГП |
| `OidcClientId_Consignor` | ClientId ГО |
| `OidcClientId_Carrier` | ClientId ТК |
| `OidcClientId_Consignee` | ClientId ГП |

### 4. `dbo.TEpdTitleArtifact`

Назначение:

- хранение XML и SGN артефактов по этапам.

#### Поля

| Поле | Типовой смысл |
|---|---|
| `Id` | PK |
| `TimelineId` | Внутренний timeline |
| `TitleCode` | Код титула: `T1`, `T2`, `T3`, `T4` |
| `VersionNo` | Версия артефакта |
| `XmlFileName` | Имя XML-файла |
| `TitleXml` | Содержимое XML |
| `SignatureFileName` | Имя файла подписи |
| `TitleSgn` | Содержимое подписи |
| `Thumbprint` | Отпечаток сертификата |
| `SignerRole` | Роль подписанта |
| `SignedAt` | Время подписи |
| `CreatedAt` | Дата создания записи |

### 5. `dbo.TEpdOperatorRoleAccess`

Назначение:

- маршрутизация доступа к Контур по роли этапа.

#### Поля

| Поле | Типовой смысл |
|---|---|
| `Id` | PK |
| `OperatorCode` | Код оператора |
| `TitleCode` | Код титула |
| `SenderRole` | Роль отправителя в API |
| `Inn` | ИНН организации |
| `Kpp` | КПП организации |
| `DiadocBoxId` | boxId / mailbox для маршрутизации |
| `ApiKey` | API-key, если используется |
| `Priority` | Приоритет правила |
| `IsActive` | Активно правило или нет |
| `CreatedAt` | Дата создания |
| `UpdatedAt` | Дата обновления |

### 6. `dbo.TEpdStageSignerSelection`

Назначение:

- хранение выбранного подписанта по этапу и timeline.

#### Поля

| Поле | Типовой смысл |
|---|---|
| `Id` | PK |
| `TimelineId` | Внутренний timeline |
| `StageCode` | Код этапа |
| `SignerFizLicoId` | `TFizLico.id` выбранного подписанта |
| `UpdatedByUserId` | Кто обновил |
| `UpdatedAt` | Когда обновил |

#### Ограничение

Эта таблица хорошо работает для штатного режима, но не покрывает виртуальных тестовых подписантов `Kontur-only`.

Это нужно учитывать при реконструкции.

### 7. `dbo.TEpdKonturTestMode`

Назначение:

- флаг включения `Kontur-only` по `TimelineId`.

#### Поля

| Поле | Типовой смысл |
|---|---|
| `Id` | PK |
| `TimelineId` | Внутренний timeline |
| `IsEnabled` | Включен ли `Kontur-only` |
| `UpdatedByUserId` | Кто обновил |
| `UpdatedAt` | Когда обновил |

### 8. `dbo.TEpdKonturStageState`

Назначение:

- хранение явного состояния этапа Контур ЭТрН;
- отделение процесса этапа от XML/SGN-артефактов, raw-log и внешних refs;
- подготовка read-model для будущего `KonturStageScreenService` без изменения текущего runtime-пути UI.

#### Поля

| Поле | Типовой смысл |
|---|---|
| `Id` | PK |
| `TimelineId` | Внутренний timeline |
| `StageCode` | Код этапа UI или сценария: `T1_INITIAL`, `T2`, `T3`, `T4` |
| `TitleCode` | Код титула: `T1`, `T2`, `T3`, `T4` |
| `XmlBuilt` | XML этапа сформирован и зафиксирован |
| `SignatureImported` | Подпись этапа импортирована или подготовлена |
| `Sent` | Этап отправлен оператору |
| `Completed` | Этап бизнес-завершен |
| `NextStageAllowed` | Следующий этап разрешен |
| `TransportationId` | Последний известный идентификатор перевозки по этапу |
| `TitleId` | Последний известный идентификатор титула по этапу |
| `LastOperatorStatus` | Последний известный статус оператора |
| `LastErrorCode` | Последний технический код ошибки |
| `LastErrorMessage` | Последнее диагностическое сообщение |
| `UpdatedAt` | Когда обновлено состояние |

#### Ограничение

Для одной пары `TimelineId` + `StageCode` хранится одна актуальная запись состояния.

`TransportationId` и `TitleId` в этой таблице используются как синхронизированные compatibility-refs для use case и screen-layer.
Они не заменяют evidence завершения T1 и не должны автоматически открывать T2.

### 9. `dbo.TEpdKonturStageCompletionEvidence`

Назначение:

- хранение evidence-снимков проверки бизнес-завершения этапа;
- отделение внешних диагностических признаков от состояния `Sent/Completed/NextStageAllowed`;
- фиксация того, почему T1 можно или нельзя считать завершенным перед открытием T2.

#### Поля

| Поле | Типовой смысл |
|---|---|
| `Id` | PK |
| `TimelineId` | Внутренний timeline |
| `StageCode` | Код этапа UI или сценария |
| `TitleCode` | Код титула |
| `TransportationId` | Идентификатор перевозки в Контур |
| `TitleId` | Идентификатор титула в Контур |
| `ExternalDocumentStatus` | Внешний статус документа или перевозки, если он получен |
| `ExternalTitleStatus` | Внешний статус титула, если он получен |
| `ExternalActionCode` | Внешний код действия, например будущий признак разрешения T2 |
| `IsDraft` | Признак, что внешний объект все еще похож на черновик |
| `HasActiveError` | Признак активной ошибки по этапу |
| `HttpStatus` | Последний HTTP-статус evidence-источника |
| `RawEvidenceSummary` | Краткое диагностическое описание evidence |
| `CompletionSource` | Источник evidence: `StoredDiagnostic`, `OperatorApiStatus`, `ManualOperator` |
| `CheckedAt` | Когда выполнена проверка |
| `CreatedAt` | Когда сохранена запись |

#### Ограничение

Таблица хранит историю проверок, а не единственное актуальное состояние.

`TransportationId` и `TitleId` в этой таблице не являются разрешением T2 сами по себе.
Для открытия следующего этапа нужен внешний признак готовности T2 и отдельное подтверждение состояния процесса.

## Legacy-таблицы, влияющие на Контур ЭТрН

### `Perdoc.dbo.epd_timeline`

Назначение:

- основной timeline документооборота.

Ключевые поля, которые использовались в анализе:

| Поле | Назначение |
|---|---|
| `id` | `TimelineId` |
| `idzak` | Идентификатор заявки в TIS |
| `doc_type` | Тип документа |
| `doc_number` | Номер документа |
| `tis_entity_type` | Тип TIS-сущности |
| `tis_entity_id` | Идентификатор TIS-сущности |
| `ezz_state` | Техническое состояние |
| `last_status` | Последний статус |
| `del` | Флаг удаления |
| `created_local_at` | Дата создания |

### `Perdoc.dbo.epd_doc_store`

Назначение:

- legacy-хранилище текущих XML/подписей и служебных данных по этапам.

Важно:

- структура в разных базах может отличаться;
- в проекте уже были ошибки из-за ожидания полей, которых нет в конкретной БД;
- при реконструкции нельзя полагаться на неподтвержденные колонки без проверки.

### `transinfoservice.dbo.TZayavka`

Назначение:

- заявка TIS.

Ключевые поля:

| Поле | Назначение |
|---|---|
| `id` | `IdZak` |
| `number` | Номер заявки |
| `idZakazchik` | Контрагент-заказчик |

### `transinfoservice.dbo.TZRekviz`

Назначение:

- реквизитный контур заявки, включая ГО/ГП и часть транспортных данных.

Ключевые поля:

| Поле | Назначение |
|---|---|
| `idn` | Связь с `TZayavka.id` |
| `idOtpravitel` | Контрагент-грузоотправитель |
| `idPoluchatel` | Контрагент-грузополучатель |
| `otpravitel` | Текстовое имя ГО |
| `poluchatel` | Текстовое имя ГП |
| `gruzUslPerevozki` | Текстовое поле по перевозке |

### `transinfoservice.dbo.TKontragent`

Назначение:

- карточка контрагента.

Ключевые поля, важные для реконструкции:

| Поле | Назначение |
|---|---|
| `id` | Идентификатор контрагента |
| `name` | Краткое название |
| `bigname` | Полное название |
| `docname` | Документное название |
| `inn` | ИНН |
| `kpp` | КПП |
| `urlico` | Признак юрлица |

### `transinfoservice.dbo.TRukAndUL`

Назначение:

- руководители и уполномоченные лица.

Используется как один из источников подписантов.

### `transinfoservice.dbo.TMchdK`

Назначение:

- МЧД и уполномоченные лица контрагента.

Используется как источник подписантов и оснований подписи.

## Подтвержденные статические данные

### BoxId

| Назначение | Значение |
|---|---|
| ТРАНСИНФОСЕРВИС (Заказчик/ГО/ГП) | `3d63a359-42a5-4a19-97af-c63f139b9423` |
| ТРАНСИНФОСЕРВИС (Перевозчик) | `ef561ca3-122f-4fe3-8689-848ee3845acc` |
| ТРАНСИНФОСЕРВИС (ГП/ГО) | `843b9531-a716-46a7-b4af-3b89f261f693` |

### Прочие настройки

| Ключ | Значение |
|---|---|
| `XSolutionInfo` | `TIS_165015198138` |
| `OidcTokenEndpoint` | `https://identity.kontur.ru/connect/token` |

### Тестовые сертификаты

| Этап | Подписант | ИНН |
|---|---|---|
| T1 | Соколов Лука Тимофеевич | `635552018474` |
| T2 | Захаров Петр Русланович | `081206022988` |

### Контрольные кейсы

| `TimelineId` | `IdZak` | Назначение |
|---|---|---|
| `10212` | `2100441` | Основной текущий тестовый кейс реконструкции |
| `10207` | `2110060` | Предыдущий тестовый кейс |
| `10206` | `2110070` | Ранний тестовый кейс |

## Главные SQL-ограничения реконструкции

1. Нельзя считать, что структура `epd_doc_store` одинакова на всех стендах.
2. Нельзя встраивать новую бизнес-логику в ad-hoc SQL внутри code-behind.
3. Все новые интеграционные состояния лучше выносить в отдельные таблицы `Perdoc`, а не перегружать legacy-таблицы.
4. Для любой новой таблицы реконструкции нужен отдельный SQL-скрипт и понятный storage boundary.
