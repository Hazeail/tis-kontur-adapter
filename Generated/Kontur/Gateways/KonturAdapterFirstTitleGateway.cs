/*
  ФАЙЛ: KonturAdapterFirstTitleGateway.cs
  НАЗНАЧЕНИЕ: Adapter-backed реализация порта отправки первого титула T1.
  Оборачивает текущий KonturAdapter без переписывания существующего API-кода.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  29.05.2026 - Первичное создание реализации gateway-порта первого титула.
*/

using System;
using Tis.KonturIntegration.Models;
using KonturApiAdapter = Tis.KonturIntegration.KonturAdapter.KonturAdapter;

namespace Tis.KonturIntegration.Integration
{
    /// <summary>
    /// Реализует отправку первого титула через существующий KonturAdapter.
    /// </summary>
    public class KonturAdapterFirstTitleGateway : IKonturFirstTitleGateway
    {
        /// <summary>
        /// Инициализирует gateway текущим адаптером Контур.
        /// </summary>
        /// <param name="adapter">Существующий адаптер операторного слоя.</param>
        /// <remarks>Gateway не создает адаптер сам, чтобы не смешивать настройку доступа и выполнение сценария.</remarks>
        public KonturAdapterFirstTitleGateway(KonturApiAdapter adapter)
        {
            if (adapter == null)
            {
                throw new ArgumentNullException("adapter");
            }

            Adapter = adapter;
        }

        /// <summary>
        /// Получает существующий адаптер Контур, используемый как внешний adapter-слой.
        /// </summary>
        public KonturApiAdapter Adapter { get; private set; }

        /// <summary>
        /// Отправляет первичный титул T1 как старт нового документооборота.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа в ТИС.</param>
        /// <param name="xmlPath">Путь к XML-файлу титула T1.</param>
        /// <param name="senderBoxId">DiadocBoxId отправителя, выбранный для этапа.</param>
        /// <returns>Результат отправки первого титула.</returns>
        /// <remarks>Метод делегирует вызов в текущий KonturAdapter и не меняет существующий runtime-контур.</remarks>
        public KonturT1ExecutionResult SendInitial(long timelineId, string xmlPath, string senderBoxId)
        {
            return Adapter.StartDanaflexT1Initial(timelineId, xmlPath, senderBoxId);
        }

        /// <summary>
        /// Отправляет титул T1 через draft-сценарий Контур.
        /// </summary>
        /// <param name="timelineId">Идентификатор timeline документа в ТИС.</param>
        /// <param name="xmlPath">Путь к XML-файлу титула T1.</param>
        /// <param name="senderBoxId">DiadocBoxId отправителя, выбранный для этапа.</param>
        /// <returns>Результат отправки первого титула через draft-сценарий.</returns>
        /// <remarks>Draft-вызов оставлен отдельным, чтобы будущий use case явно выбирал режим отправки T1.</remarks>
        public KonturT1ExecutionResult SendDraft(long timelineId, string xmlPath, string senderBoxId)
        {
            return Adapter.StartDanaflexT1Draft(timelineId, xmlPath, senderBoxId);
        }
    }
}
