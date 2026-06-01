<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="KonturProbe.aspx.cs" Inherits="tis.Account.EpdSrc.KonturProbe" ValidateRequest="false" MaintainScrollPositionOnPostBack="true" %>
<!DOCTYPE html>
<html>
<head runat="server">
    <title>Контур ЭТрН</title>
    <meta http-equiv="Content-Type" content="text/html; charset=utf-8" />
    <style>
        body {
            margin: 0;
            background: #f3f5f8;
            font-family: Segoe UI, Arial, sans-serif;
            color: #243447;
        }

        .page {
            width: 1360px;
            margin: 16px auto;
        }

        .page-title {
            margin: 0 0 6px;
            color: #3f5f86;
            font-size: 26px;
            font-weight: 600;
        }

        .page-subtitle {
            margin: 0 0 14px;
            color: #6d7f97;
            font-size: 14px;
        }

        .workspace {
            display: flex;
            gap: 16px;
            align-items: flex-start;
        }

        .sidebar {
            width: 250px;
            border: 1px solid #d7deea;
            background: #ffffff;
            padding: 14px;
            box-sizing: border-box;
        }

        .sidebar-title,
        .panel-title {
            margin: 0 0 12px;
            color: #345274;
            font-size: 18px;
            font-weight: 600;
        }

        .route-list,
        .route-list ul {
            margin: 0;
            padding: 0;
            list-style: none;
        }

        .route-item {
            position: relative;
            margin-bottom: 14px;
            padding-left: 22px;
            color: #50637d;
            font-size: 15px;
            line-height: 1.35;
        }

        .route-item:before,
        .route-subitem:before {
            content: "";
            position: absolute;
            border-radius: 50%;
            background: #ffffff;
        }

        .route-item:before {
            left: 0;
            top: 6px;
            width: 9px;
            height: 9px;
            border: 1px solid #97a7bb;
        }

        .route-item ul {
            margin-top: 10px;
            padding-left: 18px;
        }

        .route-subitem {
            position: relative;
            margin-bottom: 10px;
            padding-left: 18px;
            color: #567195;
            font-size: 14px;
            line-height: 1.35;
        }

        .route-subitem:before {
            left: 0;
            top: 5px;
            width: 7px;
            height: 7px;
            border: 1px solid #a8b4c5;
        }

        .route-subitem.active {
            color: #174986;
            font-weight: 700;
        }

        .route-subitem.active:before {
            border-color: #174986;
            background: #174986;
        }

        .main {
            flex: 1;
            min-width: 0;
        }

        .panel {
            margin-bottom: 14px;
            border: 1px solid #d7deea;
            background: #ffffff;
        }

        .panel-body {
            padding: 14px 16px;
        }

        .top-grid {
            display: grid;
            grid-template-columns: 90px 170px 44px 1fr 44px 1fr 150px;
            gap: 10px 12px;
            align-items: center;
        }

        .top-grid label,
        .signer-label {
            color: #51647d;
            font-size: 14px;
        }

        input[type=text],
        select {
            width: 100%;
            height: 34px;
            border: 1px solid #bcc7d6;
            background: #ffffff;
            padding: 5px 8px;
            box-sizing: border-box;
            font-size: 14px;
            color: #243447;
        }

        .btn,
        .top-grid input[type=submit] {
            height: 34px;
            border: 1px solid #8295ad;
            background: #f6f8fb;
            color: #213548;
            padding: 0 12px;
            box-sizing: border-box;
            font-size: 14px;
            cursor: pointer;
        }

        .btn-primary {
            border-color: #486a97;
            background: #eaf1fb;
            color: #174986;
            font-weight: 600;
        }

        .btn[disabled] {
            opacity: 0.55;
            cursor: default;
        }

        .signer-grid {
            display: grid;
            grid-template-columns: 1fr 330px;
            gap: 10px 16px;
            align-items: center;
        }

        .signer-context {
            color: #324863;
            font-size: 14px;
            line-height: 1.45;
        }

        .signer-picker {
            display: grid;
            grid-template-columns: 82px 1fr;
            gap: 8px 10px;
            align-items: center;
        }

        .signer-status-wrap {
            grid-column: 1 / -1;
        }

        .signer-status,
        .step-status {
            display: inline-block;
            padding: 4px 8px;
            border-radius: 3px;
            font-size: 12px;
            line-height: 1.45;
        }

        .signer-status-ok,
        .step-state-ready {
            background: #eef8f0;
            color: #0f6b27;
        }

        .signer-status-warn,
        .step-state-warn {
            background: #fff8e8;
            color: #7a5a08;
        }

        .signer-status-err,
        .step-state-error {
            background: #fff0f1;
            color: #9b1f2a;
        }

        .stage-switcher {
            display: flex;
            gap: 10px;
            margin-bottom: 14px;
        }

        .stage-switcher button {
            min-width: 84px;
            height: 38px;
            border: 1px solid #91a2b7;
            background: #f8fafc;
            color: #3d526d;
            font-size: 15px;
            font-weight: 600;
            cursor: pointer;
        }

        .stage-switcher button.active {
            border-color: #486a97;
            background: #eaf1fb;
            color: #174986;
        }

        .stage-summary {
            margin-bottom: 12px;
            color: #324863;
            font-size: 15px;
            line-height: 1.55;
        }

        .stage-summary-code {
            display: inline-block;
            min-width: 36px;
            margin-right: 12px;
            color: #174986;
            font-size: 32px;
            font-weight: 700;
            vertical-align: top;
        }

        .stage-summary-text {
            display: inline-block;
            width: calc(100% - 60px);
            vertical-align: top;
        }

        .flow-table {
            width: 100%;
            border-collapse: collapse;
            table-layout: fixed;
        }

        .flow-table th,
        .flow-table td {
            border: 1px solid #d8e0ea;
            padding: 12px;
            vertical-align: top;
        }

        .flow-table th {
            background: #f7f9fc;
            color: #4f6480;
            font-size: 14px;
            font-weight: 600;
            text-align: left;
        }

        .flow-table td {
            background: #ffffff;
        }

        .flow-stage-code {
            color: #174986;
            font-size: 44px;
            font-weight: 700;
            line-height: 1;
            margin-bottom: 10px;
        }

        .flow-stage-role,        .flow-note,
        .result-summary {
            color: #667a93;
            font-size: 13px;
            line-height: 1.6;
        }

        .stage-extra {
            margin-top: 14px;
            padding: 14px;
            border: 1px solid #d8e0ea;
            background: #fbfcfe;
        }

        .stage-extra-title {
            margin: 0 0 8px;
            color: #345274;
            font-size: 15px;
            font-weight: 600;
        }

        .flow-purpose {
            color: #324863;
            font-size: 15px;
            line-height: 1.6;
        }

        .action-stack {
            display: flex;
            flex-direction: column;
            gap: 10px;
        }

        .action-stack .btn {
            width: 100%;
            text-align: left;
        }

        .result-box {
            min-height: 240px;
            border: 1px solid #d4dbe6;
            background: #fbfcfe;
            padding: 12px;
            overflow: auto;
            box-sizing: border-box;
        }

        .ok,
        .err {
            white-space: pre-wrap;
            font-size: 14px;
        }

        .ok {
            color: #0b6a24;
        }

        .err {
            color: #a01f26;
        }
    </style>
    <script type="text/javascript">
        var stageMeta = {
            'T1': {
                ddlValue: 'T1_INITIAL',
                routeNode: 'route-t1',
                role: 'Грузоотправитель',
                purpose: 'Первичная отправка накладной грузоотправителем. Для дальнейшей цепочки использовать нормализованный t1_override.',
                hint: 'Если подпись делается вручную, сначала сформировать T1, затем подписать именно актуальный t1_override_*.xml и импортировать .sgn.'
            },
            'T2': {
                ddlValue: 'T2',
                routeNode: 'route-t2',
                role: 'Перевозчик',
                purpose: 'Ответный титул перевозчика по приемке. XML должен собираться от актуального T1 и под выбранного подписанта перевозчика.',
                hint: 'Для T2 важны связность с T1, корректный подписант перевозчика и использование свежего t2_timeline...xml вместе с его собственной подписью.'
            },
            'T3': {
                ddlValue: 'T3',
                routeNode: 'route-t3',
                role: 'Грузополучатель',
                purpose: 'Ответный титул грузополучателя по приемке. Этап продолжает цепочку после успешного прохождения T2.',
                hint: 'На этом шаге критичен переход на подписанта грузополучателя и согласованность его подписи с выбранным этапом.'
            },
            'T4': {
                ddlValue: 'T4',
                routeNode: 'route-t4',
                role: 'Перевозчик',
                purpose: 'Завершающий титул перевозчика. Финализирует контур после прохождения T3.',
                hint: 'Этап возвращает процесс к перевозчику и должен использовать согласованного подписанта этой организации.'
            }
        };

        function clickServerButton(clientId) {
            var btn = document.getElementById(clientId);
            if (btn) {
                btn.click();
            }
        }

        function setSelectedStage(stageCode) {
            var ddl = document.getElementById('<%= ddlStage.ClientID %>');
            var meta = stageMeta[stageCode];
            if (ddl && meta) {
                ddl.value = meta.ddlValue;
            }
        }

        function getCurrentStageCode() {
            var selected = document.querySelector('.stage-switcher button.active');
            return selected ? selected.getAttribute('data-stage-code') : '<%= GetCurrentStageTabCode() %>';
        }

        function hasSelectedSigner() {
            var ddl = document.getElementById('<%= ddlStageSigner.ClientID %>');
            return ddl && !ddl.disabled && (ddl.value || '').trim() !== '';
        }

        function updateActionAvailability() {
            var enabled = hasSelectedSigner();
            var ids = ['btnBuildStageXml', 'btnOpenSignStage', 'btnImportStageSgn', 'btnCheckStageAccess', 'btnSendStage', 'btnConfirmStage'];
            for (var i = 0; i < ids.length; i++) {
                var node = document.getElementById(ids[i]);
                if (node) {
                    node.disabled = !enabled;
                }
            }
        }

        function updateRouteHighlight(stageCode) {
            var nodes = document.querySelectorAll('[data-route-stage]');
            for (var i = 0; i < nodes.length; i++) {
                nodes[i].classList.remove('active');
            }

            var meta = stageMeta[stageCode];
            if (meta) {
                var node = document.getElementById(meta.routeNode);
                if (node) {
                    node.classList.add('active');
                }
            }
        }

        function updateStageView(stageCode) {
            var buttons = document.querySelectorAll('.stage-switcher button');
            for (var i = 0; i < buttons.length; i++) {
                buttons[i].classList.remove('active');
            }

            var activeButton = document.getElementById('stage-tab-' + stageCode);
            if (activeButton) {
                activeButton.classList.add('active');
            }

            var meta = stageMeta[stageCode];
            if (!meta) {
                return;
            }

            var code = document.getElementById('currentStageCode');
            var flowCode = document.getElementById('flowStageCode');
            var role = document.getElementById('currentStageRole');
            var purpose = document.getElementById('currentStagePurpose');
            var flowPurpose = document.getElementById('flowStagePurpose');
            var hint = document.getElementById('currentStageHint');

            if (code) code.innerHTML = stageCode;
            if (flowCode) flowCode.innerHTML = stageCode;
            if (role) role.innerHTML = meta.role;
            if (purpose) purpose.innerHTML = meta.purpose;
            if (flowPurpose) flowPurpose.innerHTML = meta.purpose;
            if (hint) hint.innerHTML = meta.hint;

            setSelectedStage(stageCode);
            updateRouteHighlight(stageCode);
            updateActionAvailability();
        }

        function onStageTabClick(stageCode) {
            updateStageView(stageCode);
            clickServerButton('<%= btnApplyStage.ClientID %>');
        }

        function buildStageXml() {
            if (!hasSelectedSigner()) {
                alert('Сначала выберите подписанта этапа.');
                return;
            }

            var stageCode = getCurrentStageCode();
            setSelectedStage(stageCode);

            if (stageCode === 'T1') {
                clickServerButton('<%= btnBuildT1Xml.ClientID %>');
            } else if (stageCode === 'T2') {
                clickServerButton('<%= btnBuildT2Xml.ClientID %>');
            } else if (stageCode === 'T3') {
                clickServerButton('<%= btnBuildT3Xml.ClientID %>');
            } else if (stageCode === 'T4') {
                clickServerButton('<%= btnBuildT4Xml.ClientID %>');
            }
        }

        function openSignEpdForCurrentTimeline() {
            if (!hasSelectedSigner()) {
                alert('Сначала выберите подписанта этапа.');
                return;
            }

            var tb = document.getElementById('<%= tbTimelineId.ClientID %>');
            var signer = document.getElementById('<%= ddlStageSigner.ClientID %>');
            if (!tb || !signer) {
                return;
            }

            var timelineId = (tb.value || '').trim();
            var signerId = (signer.value || '').trim();
            if (!timelineId) {
                alert('Укажите TimelineId перед открытием подписи.');
                return;
            }

            var stageCode = getCurrentStageCode();
            var xmlSelector = document.getElementById('<%= ddlServerXmlFile.ClientID %>');
            var xmlFile = xmlSelector ? (xmlSelector.value || '').trim() : '';
            var url = '/Account/EpdSrc/SignEpdKontur.aspx?timelineId=' + encodeURIComponent(timelineId)
                + '&idPodpisant=' + encodeURIComponent(signerId)
                + '&stageCode=' + encodeURIComponent(stageCode)
                + '&xmlFile=' + encodeURIComponent(xmlFile);
            window.open(url, 'signEpdWindow', 'width=1160,height=780,resizable=yes,scrollbars=yes');
        }

        function importSignature() {
            if (!hasSelectedSigner()) {
                alert('Сначала выберите подписанта этапа.');
                return;
            }

            setSelectedStage(getCurrentStageCode());
            clickServerButton('<%= btnImportServerSignature.ClientID %>');
        }

        function runStageAccessCheck() {
            if (!hasSelectedSigner()) {
                alert('Сначала выберите подписанта этапа.');
                return;
            }

            setSelectedStage(getCurrentStageCode());
            clickServerButton('<%= btnCheckAccess.ClientID %>');
        }

        function runStageSend() {
            if (!hasSelectedSigner()) {
                alert('Сначала выберите подписанта этапа.');
                return;
            }

            setSelectedStage(getCurrentStageCode());
            clickServerButton('<%= btnRunStage.ClientID %>');
        }

        function confirmStageCompletion() {
            if (!hasSelectedSigner()) {
                alert('Сначала выберите подписанта этапа.');
                return;
            }

            setSelectedStage(getCurrentStageCode());
            clickServerButton('<%= btnConfirmStage.ClientID %>');
        }

        document.addEventListener('DOMContentLoaded', function () {
            updateStageView('<%= GetCurrentStageTabCode() %>');
        });
    </script>
</head>
<body>
    <form id="form1" runat="server">
        <div class="page">
            <h1 class="page-title">Контур ЭТрН</h1>
            <div class="page-subtitle">Сформировать, подписать, отправить и сразу проверить результат этапа.</div>

            <div class="workspace">
                <div class="sidebar">
                    <div class="sidebar-title">Маршрут этапов</div>
                    <ul class="route-list">
                        <li class="route-item">Документ создан</li>
                        <li class="route-item">Погрузка</li>
                        <li class="route-item">
                            <ul>
                                <li class="route-subitem" id="route-t1" data-route-stage="T1">Сдан отправителем (T1)</li>
                                <li class="route-subitem" id="route-t2" data-route-stage="T2">Принят перевозчиком (T2)</li>
                            </ul>
                        </li>
                        <li class="route-item">Разгрузка</li>
                        <li class="route-item">
                            <ul>
                                <li class="route-subitem" id="route-t3" data-route-stage="T3">Принят получателем (T3)</li>
                                <li class="route-subitem" id="route-t4" data-route-stage="T4">Сдан перевозчиком (T4)</li>
                            </ul>
                        </li>
                        <li class="route-item">Документооборот завершен</li>
                    </ul>
                </div>

                <div class="main">
                    <div class="panel">
                        <div class="panel-body">
                            <div class="top-grid">
                                <label for="tbTimelineId">TimelineId</label>
                                <asp:TextBox ID="tbTimelineId" runat="server" Text="1" AutoPostBack="true" OnTextChanged="tbTimelineId_TextChanged"></asp:TextBox>

                                <label for="ddlServerXmlFile">XML</label>
                                <asp:DropDownList ID="ddlServerXmlFile" runat="server"></asp:DropDownList>

                                <label for="ddlServerSgnFile">.sgn</label>
                                <asp:DropDownList ID="ddlServerSgnFile" runat="server"></asp:DropDownList>

                                <asp:Button ID="btnReloadServerFiles" runat="server" Text="Обновить" OnClick="btnReloadServerFiles_Click" />
                            </div>
                        </div>
                    </div>

                    <div class="panel">
                        <div class="panel-body">
                            <div class="panel-title">Подписант этапа</div>
                            <div class="signer-grid">
                                <div class="signer-context">
                                    <asp:Literal ID="litStageSignerContext" runat="server"></asp:Literal>
                                </div>
                                <div class="signer-picker">
                                    <span class="signer-label">Подписант</span>
                                    <asp:DropDownList ID="ddlStageSigner" runat="server" AutoPostBack="true" OnSelectedIndexChanged="ddlStageSigner_SelectedIndexChanged"></asp:DropDownList>
                                </div>
                                <div class="signer-status-wrap">
                                    <asp:Literal ID="litStageSignerWarning" runat="server"></asp:Literal>
                                </div>
                            </div>
                        </div>
                    </div>

                    <div class="panel">
                        <div class="panel-body">
                            <div class="panel-title">Прогон ЭТрН по этапам</div>

                            <div class="stage-switcher">
                                <button type="button" id="stage-tab-T1" data-stage-code="T1" onclick="onStageTabClick('T1');">T1</button>
                                <button type="button" id="stage-tab-T2" data-stage-code="T2" onclick="onStageTabClick('T2');">T2</button>
                                <button type="button" id="stage-tab-T3" data-stage-code="T3" onclick="onStageTabClick('T3');">T3</button>
                                <button type="button" id="stage-tab-T4" data-stage-code="T4" onclick="onStageTabClick('T4');">T4</button>
                            </div>

                            <div class="stage-summary">
                                <span class="stage-summary-code" id="currentStageCode">T1</span>
                                <span class="stage-summary-text">
                                    <span id="currentStagePurpose">Первичная отправка накладной грузоотправителем. Для дальнейшей цепочки использовать нормализованный t1_override.</span>
                                </span>
                            </div>

                            <table class="flow-table">
                                <colgroup>
                                    <col style="width: 120px;" />
                                    <col style="width: 280px;" />
                                    <col style="width: 220px;" />
                                    <col style="width: 190px;" />
                                    <col style="width: 190px;" />
                                </colgroup>
                                <thead>
                                    <tr>
                                        <th>Этап</th>
                                        <th>Назначение</th>
                                        <th>Сформировать XML</th>
                                        <th>Подписать</th>
                                        <th>Отправить</th>
                                        
                                    </tr>
                                </thead>
                                <tbody>
                                    <tr>
                                        <td>
                                            <div class="flow-stage-code" id="flowStageCode">T1</div>
                                            <div class="flow-stage-role" id="currentStageRole">Грузоотправитель</div>
                                        </td>
                                        <td>
                                            <div class="flow-purpose" id="flowStagePurpose">Первичная отправка накладной грузоотправителем. Для дальнейшей цепочки использовать нормализованный t1_override.</div>
                                        </td>
                                        <td>
                                            <div class="action-stack">
                                                <span class="<%= GetCurrentXmlStepCssClass() %>"><%= Server.HtmlEncode(GetCurrentXmlStepText()) %></span>
                                                <button type="button" class="btn btn-primary" id="btnBuildStageXml" onclick="buildStageXml();">Сформировать XML</button>
                                            </div>
                                        </td>
                                        <td>
                                            <div class="action-stack">
                                                <span class="<%= GetCurrentSignatureStepCssClass() %>"><%= Server.HtmlEncode(GetCurrentSignatureStepText()) %></span>
                                                <button type="button" class="btn" id="btnOpenSignStage" onclick="openSignEpdForCurrentTimeline();">Открыть SignEpd</button>
                                                <button type="button" class="btn" id="btnImportStageSgn" onclick="importSignature();">Импорт .sgn в БД</button>
                                            </div>
                                        </td>
                                        <td>
                                            <div class="action-stack">
                                                <span class="<%= GetCurrentSendStepCssClass() %>"><%= Server.HtmlEncode(GetCurrentSendStepText()) %></span>
                                                <button type="button" class="btn" id="btnCheckStageAccess" onclick="runStageAccessCheck();">Проверить доступ</button>
                                                <button type="button" class="btn btn-primary" id="btnSendStage" onclick="runStageSend();">Отправить этап</button>
                                                <button type="button" class="btn" id="btnConfirmStage" onclick="confirmStageCompletion();">Подтвердить этап</button>
                                            </div>
                                        </td>
                                        </tr>
                                </tbody>
                            </table>

                            <div class="stage-extra">
                                <div class="stage-extra-title">Подсказка и краткий результат этапа</div>
                                <div class="flow-note" id="currentStageHint">Если подпись делается вручную, сначала сформировать T1, затем подписать именно актуальный t1_override_*.xml и импортировать .sgn.</div>
                                <div class="result-summary" style="margin-top: 10px;">
                                    <span class="<%= GetCurrentResultStepCssClass() %>"><%= Server.HtmlEncode(GetCurrentResultStepText()) %></span>
                                    <div style="margin-top: 8px;"><%= Server.HtmlEncode(GetCurrentResultSummaryText()) %></div>
                                    <div style="margin-top: 6px; color: #6d7f97; font-size: 12px;"><%= Server.HtmlEncode(GetCurrentStateSourceText()) %></div>
                                </div>
                            </div>

                            <div style="display:none;">
                                <asp:DropDownList ID="ddlStage" runat="server">
                                    <asp:ListItem Value="T1_INITIAL" Text="T1 (первичная отправка)"></asp:ListItem>
                                    <asp:ListItem Value="T1_DRAFT" Text="T1 (черновик)"></asp:ListItem>
                                    <asp:ListItem Value="T2" Text="T2 (ответный титул перевозчика)"></asp:ListItem>
                                    <asp:ListItem Value="T3" Text="T3 (ответный титул грузополучателя)"></asp:ListItem>
                                    <asp:ListItem Value="T4" Text="T4 (ответный титул перевозчика, выдача)"></asp:ListItem>
                                </asp:DropDownList>

                                <asp:Button ID="btnBuildT1Xml" runat="server" Text="Сформировать T1 XML" OnClick="btnBuildT1Xml_Click" />
                                <asp:Button ID="btnBuildT2Xml" runat="server" Text="Сформировать T2 XML" OnClick="btnBuildT2Xml_Click" />
                                <asp:Button ID="btnBuildT3Xml" runat="server" Text="Сформировать T3 XML" OnClick="btnBuildT3Xml_Click" />
                                <asp:Button ID="btnBuildT4Xml" runat="server" Text="Сформировать T4 XML" OnClick="btnBuildT4Xml_Click" />
                                <asp:Button ID="btnImportServerSignature" runat="server" Text="Импорт .sgn в БД" OnClick="btnImportServerSignature_Click" />
                                <asp:Button ID="btnCheckAccess" runat="server" Text="Проверить доступ этапа" OnClick="btnCheckAccess_Click" />
                                <asp:Button ID="btnRunStage" runat="server" Text="Запустить выбранный этап" OnClick="btnRunStage_Click" />
                                <asp:Button ID="btnConfirmStage" runat="server" Text="Подтвердить этап" OnClick="btnConfirmStage_Click" />
                                <asp:Button ID="btnApplyStage" runat="server" Text="Применить этап" OnClick="btnApplyStage_Click" />
                            </div>
                        </div>
                    </div>

                    <div class="panel">
                        <div class="panel-body">
                            <div class="panel-title">Результат и диагностика</div>
                            <div class="result-box">
                                <asp:Literal ID="litLog" runat="server"></asp:Literal>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    </form>
</body>
</html>





