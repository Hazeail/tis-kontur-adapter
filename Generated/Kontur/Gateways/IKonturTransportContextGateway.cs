/*
  ФАЙЛ: IKonturTransportContextGateway.cs
  НАЗНАЧЕНИЕ: Порт чтения транспортного контекста Контур ЭТрН.
  Отделяет use case ответных титулов от чтения operator refs и legacy timeline.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  29.05.2026 - Первичное создание порта чтения транспортного контекста.
*/

namespace Tis.KonturIntegration.Integration
{
    /// <summary>
    /// Описывает контракт чтения транспортного контекста для ответных титулов Контур ЭТрН.
    /// Используется use case-слоем для получения внешних идентификаторов и legacy-статуса отдельно от SQL-реализации.
    /// </summary>
    public interface IKonturTransportContextGateway
    {
        /// <summary>
        /// Возвращает последний известный TransportationId для документа.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа в ТИС.</param>
        /// <returns>TransportationId или пустую строку, если связь еще не сохранена.</returns>
        /// <remarks>Метод нужен только для транспортного контекста и не разрешает запуск этапа сам по себе.</remarks>
        string GetTransportationId(long timelineId);

        /// <summary>
        /// Возвращает последний статус документа из legacy timeline.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа в ТИС.</param>
        /// <returns>Значение last_status или пустую строку, если документ не найден.</returns>
        /// <remarks>Метод сохраняет временную границу между новой моделью состояния и legacy timeline.</remarks>
        string GetLastTimelineStatus(long timelineId);
    }
}
