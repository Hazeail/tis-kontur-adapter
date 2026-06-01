/*
  ФАЙЛ: IKonturOperatorRefRepository.cs
  НАЗНАЧЕНИЕ: Порт чтения и записи legacy refs оператора Контур из TEpdOperatorRef.
  Изолирует use case-слой реконструкции от прямой SQL-реализации таблицы операторных ссылок.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  29.05.2026 - Первичное создание порта чтения и записи operator refs для синхронизации состояния этапа.
*/

namespace Tis.KonturIntegration.Storage
{
    /// <summary>
    /// Определяет контракт доступа к внешним идентификаторам оператора Контур.
    /// </summary>
    public interface IKonturOperatorRefRepository
    {
        /// <summary>
        /// Сохраняет одну запись внешнего идентификатора оператора.
        /// </summary>
        /// <param name="timelineId">Идентификатор документа ТИС в timeline.</param>
        /// <param name="operatorCode">Код оператора, например Kontur.</param>
        /// <param name="refType">Тип внешнего идентификатора, например TransportationId.</param>
        /// <param name="refValue">Значение внешнего идентификатора.</param>
        /// <param name="sourceEventId">Идентификатор источника события, если он известен.</param>
        /// <returns>Количество затронутых строк SQL.</returns>
        /// <remarks>Метод нужен adapter-слою и не должен дублироваться в use case через ручной SQL.</remarks>
        int InsertRef(long timelineId, string operatorCode, string refType, string refValue, string sourceEventId);

        /// <summary>
        /// Возвращает последнее значение внешнего идентификатора по типу.
        /// </summary>
        /// <param name="timelineId">Идентификатор документа ТИС в timeline.</param>
        /// <param name="operatorCode">Код оператора, например Kontur.</param>
        /// <param name="refType">Тип идентификатора, например TransportationId.</param>
        /// <returns>Значение идентификатора или пустую строку, если запись отсутствует.</returns>
        /// <remarks>Чтение refs используется как compatibility-источник для safe-bridge между legacy runtime и новым состоянием этапа.</remarks>
        string GetLatestRefValue(long timelineId, string operatorCode, string refType);
    }
}
