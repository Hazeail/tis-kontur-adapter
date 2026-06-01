/*
  ФАЙЛ: KonturTransportContextGateway.cs
  НАЗНАЧЕНИЕ: Реализация порта чтения транспортного контекста Контур ЭТрН.
  Читает TransportationId из operator refs и last_status из legacy timeline через существующие repository.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  29.05.2026 - Первичное создание gateway транспортного контекста.
*/

using System;
using Tis.KonturIntegration.Storage;

namespace Tis.KonturIntegration.Integration
{
    /// <summary>
    /// Реализует чтение транспортного контекста для ответных титулов Контур ЭТрН.
    /// </summary>
    public class KonturTransportContextGateway : IKonturTransportContextGateway
    {
        /// <summary>
        /// Инициализирует gateway существующими repository операторных refs и timeline.
        /// </summary>
        /// <param name="operatorRefRepository">Repository внешних идентификаторов оператора.</param>
        /// <param name="timelineRepository">Repository чтения legacy timeline.</param>
        /// <remarks>Gateway не содержит SQL напрямую и не восстанавливает контекст из raw-log.</remarks>
        public KonturTransportContextGateway(KonturOperatorRefRepository operatorRefRepository, KonturTimelineRepository timelineRepository)
        {
            if (operatorRefRepository == null)
            {
                throw new ArgumentNullException("operatorRefRepository");
            }

            if (timelineRepository == null)
            {
                throw new ArgumentNullException("timelineRepository");
            }

            OperatorRefRepository = operatorRefRepository;
            TimelineRepository = timelineRepository;
        }

        /// <summary>
        /// Получает repository внешних идентификаторов оператора.
        /// </summary>
        public KonturOperatorRefRepository OperatorRefRepository { get; private set; }

        /// <summary>
        /// Получает repository чтения legacy timeline.
        /// </summary>
        public KonturTimelineRepository TimelineRepository { get; private set; }

        /// <summary>
        /// Возвращает последний известный TransportationId для документа.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа в ТИС.</param>
        /// <returns>TransportationId или пустую строку, если связь еще не сохранена.</returns>
        /// <remarks>Источник значения явно ограничен таблицей refs оператора.</remarks>
        public string GetTransportationId(long timelineId)
        {
            return OperatorRefRepository.GetLatestRefValue(timelineId, "Kontur", "TransportationId");
        }

        /// <summary>
        /// Возвращает последний статус документа из legacy timeline.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа в ТИС.</param>
        /// <returns>Значение last_status или пустую строку, если документ не найден.</returns>
        /// <remarks>Этот метод оставляет зависимость от epd_timeline явной и временно изолированной.</remarks>
        public string GetLastTimelineStatus(long timelineId)
        {
            return TimelineRepository.GetLastStatus(timelineId);
        }
    }
}
