/*
  ФАЙЛ: KonturTitleArtifact.cs
  НАЗНАЧЕНИЕ: Модель внутреннего артефакта титула ЭТрН для сценариев Контур.
  Хранит XML, открепленную подпись и технические признаки подписанта без зависимости от пользовательских файлов.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  13.05.2026 - Первичное создание модели артефакта титула для внутреннего stage-runner.
*/

using System;

namespace Tis.KonturIntegration.Models
{
    /// <summary>
    /// Описывает сохраненный XML титула ЭТрН и связанную с ним открепленную подпись в контуре Контур.
    /// Используется как внутренняя модель между сборщиком титула, сервисом подписи, хранилищем и адаптером оператора.
    /// </summary>
    public class KonturTitleArtifact
    {
        /// <summary>
        /// Получает или задает внутренний идентификатор записи артефакта.
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Получает или задает идентификатор timeline документа в ТИС.
        /// </summary>
        public long TimelineId { get; set; }

        /// <summary>
        /// Получает или задает код титула ЭТрН: T1, T2, T3 или T4.
        /// </summary>
        public string TitleCode { get; set; }

        /// <summary>
        /// Получает или задает имя XML-файла для multipart-отправки оператору.
        /// </summary>
        public string XmlFileName { get; set; }

        /// <summary>
        /// Получает или задает XML титула в исходных байтах.
        /// </summary>
        public byte[] TitleXml { get; set; }

        /// <summary>
        /// Получает или задает имя файла открепленной подписи для multipart-отправки оператору.
        /// </summary>
        public string SignatureFileName { get; set; }

        /// <summary>
        /// Получает или задает открепленную CMS-подпись титула.
        /// </summary>
        public byte[] TitleSgn { get; set; }

        /// <summary>
        /// Получает или задает отпечаток сертификата подписанта, если он известен на этапе подписи.
        /// </summary>
        public string Thumbprint { get; set; }

        /// <summary>
        /// Получает или задает роль подписанта внутри процесса ЭТрН.
        /// </summary>
        public string SignerRole { get; set; }

        /// <summary>
        /// Получает или задает дату подписи, если подпись уже получена.
        /// </summary>
        public DateTime? SignedAt { get; set; }

        /// <summary>
        /// Получает признак наличия XML титула.
        /// </summary>
        public bool HasXml
        {
            get { return TitleXml != null && TitleXml.Length > 0; }
        }

        /// <summary>
        /// Получает признак наличия открепленной подписи.
        /// </summary>
        public bool HasSignature
        {
            get { return TitleSgn != null && TitleSgn.Length > 0; }
        }
    }
}
