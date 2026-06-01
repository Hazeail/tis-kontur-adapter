/*
  ФАЙЛ: KonturEventMapper.cs
  НАЗНАЧЕНИЕ: Маппинг событий и статусов Kontur Logistics API в внутренние статусы ТИС.
  Используется для единообразного обновления timeline без привязки UI к внешним кодам.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  05.05.2026 - Первичное создание каркаса маппера статусов Контур.
*/

namespace Tis.KonturIntegration.KonturEventMapper
{
    /// <summary>
    /// Маппер событий Kontur в внутренние статусы EPD-layer ТИС.
    /// </summary>
    public class KonturEventMapper
    {
        /// <summary>
        /// Преобразует внешний статус Kontur в внутренний код статуса ТИС.
        /// </summary>
        /// <param name="konturStatus">Статус из события Kontur, например WaybillDeliveryWaitCarrierSignature.</param>
        /// <returns>Внутренний нормализованный статус ТИС.</returns>
        /// <remarks>Метод использует минимальную карту переходов и возвращает UnknownStatus для неучтенных значений.</remarks>
        public string MapStatus(string konturStatus)
        {
            if (konturStatus == "Completed")
            {
                return "Completed";
            }

            if (konturStatus == "OnTheWay")
            {
                return "InTransit";
            }

            if (konturStatus == "WaybillReceptionWaitDriverConfirmation")
            {
                return "WaitDriverConfirmation";
            }

            return "UnknownStatus";
        }
    }
}
