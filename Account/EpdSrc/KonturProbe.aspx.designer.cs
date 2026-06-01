/*
  ФАЙЛ: KonturProbe.aspx.designer.cs
  НАЗНАЧЕНИЕ: Дизайнер-часть WebForms-страницы продуктового запуска этапов Контур ЭТрН.
  Содержит объявления контролов для code-behind страницы KonturProbe.

  ЖУРНАЛ ИЗМЕНЕНИЙ:
  07.05.2026 - Первичное создание дизайнер-части страницы.
  12.05.2026 - Синхронизированы контролы под продуктовый сценарий запуска этапов T1/T2 и проверки доступов.
  14.05.2026 - Поля путей XML/SGN переведены на выбор серверных файлов из App_Data\Temp\KonturEtrn.
  15.05.2026 - Добавлено поле кнопки генерации T1 XML для синхронизации с KonturProbe.aspx.
  15.05.2026 - Добавлено поле кнопки генерации T2 XML для внутреннего контура этапа перевозчика.
  18.05.2026 - Добавлены кнопки генерации T3/T4 XML.
  21.05.2026 - Добавлена кнопка ручного импорта .sgn в epd_doc_store для локального сценария подписи T1/T2.
  22.05.2026 - Добавлены контролы выбора подписанта этапа и синхронизации stage-переключателя.
*/

namespace tis.Account.EpdSrc
{
    /// <summary>
    /// Дизайнер-класс страницы продуктового запуска этапов Контур ЭТрН.
    /// </summary>
    public partial class KonturProbe
    {
        /// <summary>
        /// HTML-форма страницы.
        /// </summary>
        protected global::System.Web.UI.HtmlControls.HtmlForm form1;

        /// <summary>
        /// Поле ввода идентификатора timeline.
        /// </summary>
        protected global::System.Web.UI.WebControls.TextBox tbTimelineId;

        /// <summary>
        /// Список выбора запускаемого этапа ЭТрН.
        /// </summary>
        protected global::System.Web.UI.WebControls.DropDownList ddlStage;

        /// <summary>
        /// Список выбора XML-файла титула из серверной папки.
        /// </summary>
        protected global::System.Web.UI.WebControls.DropDownList ddlServerXmlFile;

        /// <summary>
        /// Список выбора файла подписи титула (.sgn) из серверной папки.
        /// </summary>
        protected global::System.Web.UI.WebControls.DropDownList ddlServerSgnFile;

        /// <summary>
        /// Поле вывода сведений о роли этапа и организации подписи.
        /// </summary>
        protected global::System.Web.UI.WebControls.Literal litStageSignerContext;

        /// <summary>
        /// Список выбора подписанта текущего этапа.
        /// </summary>
        protected global::System.Web.UI.WebControls.DropDownList ddlStageSigner;

        /// <summary>
        /// Поле вывода статуса подбора или сохранения подписанта.
        /// </summary>
        protected global::System.Web.UI.WebControls.Literal litStageSignerWarning;

        /// <summary>
        /// Кнопка формирования T1 XML в серверной папке.
        /// </summary>
        protected global::System.Web.UI.WebControls.Button btnBuildT1Xml;

        /// <summary>
        /// Кнопка формирования T2 XML в серверной папке.
        /// </summary>
        protected global::System.Web.UI.WebControls.Button btnBuildT2Xml;

        /// <summary>
        /// Кнопка формирования T3 XML в серверной папке.
        /// </summary>
        protected global::System.Web.UI.WebControls.Button btnBuildT3Xml;

        /// <summary>
        /// Кнопка формирования T4 XML в серверной папке.
        /// </summary>
        protected global::System.Web.UI.WebControls.Button btnBuildT4Xml;

        /// <summary>
        /// Кнопка ручного импорта выбранного серверного файла .sgn в epd_doc_store.
        /// </summary>
        protected global::System.Web.UI.WebControls.Button btnImportServerSignature;

        /// <summary>
        /// Кнопка запуска выбранного этапа.
        /// </summary>
        protected global::System.Web.UI.WebControls.Button btnRunStage;

        /// <summary>
        /// Скрытая кнопка синхронизации выбранного этапа после переключения вкладки.
        /// </summary>
        protected global::System.Web.UI.WebControls.Button btnApplyStage;

        /// <summary>
        /// Кнопка обновления списка серверных файлов.
        /// </summary>
        protected global::System.Web.UI.WebControls.Button btnReloadServerFiles;

        /// <summary>
        /// Кнопка проверки ролевых доступов без отправки.
        /// </summary>
        protected global::System.Web.UI.WebControls.Button btnCheckAccess;

        /// <summary>
        /// Кнопка подтверждения завершения текущего этапа.
        /// </summary>
        protected global::System.Web.UI.WebControls.Button btnConfirmStage;

        /// <summary>
        /// Область вывода диагностического лога.
        /// </summary>
        protected global::System.Web.UI.WebControls.Literal litLog;
    }
}
