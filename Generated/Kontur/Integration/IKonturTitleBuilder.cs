/*
  ФАЙЛ: IKonturTitleBuilder.cs
  НАЗНАЧЕНИЕ: Порт сборки XML титула ЭТрН для конвейера Контур.
  Отделяет оркестратор этапа от конкретных builder-реализаций и legacy-источников данных ТИС.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  13.05.2026 - Первичное создание порта сборки титула.
*/

using Tis.KonturIntegration.Models;

namespace Tis.KonturIntegration.Integration
{
    /// <summary>
    /// Описывает контракт построения XML титула ЭТрН из данных ТИС или чтения уже сохраненного артефакта.
    /// Используется stage-runner, чтобы бизнес-сценарий не зависел от конкретных классов XML-маппинга.
    /// </summary>
    public interface IKonturTitleBuilder
    {
        /// <summary>
        /// Собирает или получает XML указанного титула.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа в ТИС.</param>
        /// <param name="titleCode">Код титула: T1, T2, T3 или T4.</param>
        /// <param name="tisEntityId">Идентификатор сущности ТИС, обычно id заявки или рейса.</param>
        /// <returns>Результат сборки с артефактом или диагностикой ошибки.</returns>
        /// <remarks>Реализация не выполняет подпись и отправку оператору.</remarks>
        KonturTitleBuildResult Build(long timelineId, string titleCode, string tisEntityId);
    }
}
