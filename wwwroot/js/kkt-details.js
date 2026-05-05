const urlParams = new URLSearchParams(window.location.search);
const kktId = urlParams.get('id');

if (!kktId) {
    window.location.href = '/kktmonitor/';
}

let kktData = null;
let fullReportData = null;
let pollingInProgress = false;
let autoRefreshInterval = null;

document.addEventListener('DOMContentLoaded', () => {
    loadDetailsFromDB();
    
    autoRefreshInterval = setInterval(() => {
        refreshDetailsFromDB();
    }, 30000);
});

window.addEventListener('beforeunload', () => {
    if (autoRefreshInterval) {
        clearInterval(autoRefreshInterval);
    }
});

async function loadDetailsFromDB() {
    const loading = document.getElementById('loading');
    const errorDiv = document.getElementById('error');
    const detailsContainer = document.getElementById('details-container');
    
    loading.style.display = 'block';
    errorDiv.style.display = 'none';
    detailsContainer.style.display = 'none';
    
    try {
        const response = await fetch('/api/kkt/' + kktId);
        if (!response.ok) throw new Error('Ошибка загрузки данных ККМ');
        
        kktData = await response.json();
        console.log('Данные из БД:', kktData);
        
        updatePollingButtons(kktData.isPollingStopped);
        renderDetails();
        
        loading.style.display = 'none';
        detailsContainer.style.display = 'block';
    } catch (error) {
        console.error('Ошибка:', error);
        loading.style.display = 'none';
        errorDiv.style.display = 'block';
        errorDiv.textContent = 'Ошибка: ' + error.message;
    }
}

async function refreshDetailsFromDB() {
    if (pollingInProgress) return;
    
    try {
        const response = await fetch('/api/kkt/' + kktId);
        if (!response.ok) throw new Error('Ошибка загрузки данных ККМ');
        
        kktData = await response.json();
        console.log('Данные из БД (автообновление):', kktData);
        
        updatePollingButtons(kktData.isPollingStopped);
        renderDetails();
    } catch (error) {
        console.error('Ошибка автообновления:', error);
    }
}

function updatePollingButtons(isPollingStopped) {
    console.log('[DEBUG] updatePollingButtons called, isPollingStopped: ' + isPollingStopped);
    
    const stopBtn = document.getElementById('stopPollingBtn');
    const startBtn = document.getElementById('startPollingBtn');
    const pollBtn = document.getElementById('pollBtn');
    
    if (isPollingStopped) {
        console.log('[DEBUG] Polling is stopped - showing start button');
        if (stopBtn) stopBtn.style.display = 'none';
        if (startBtn) startBtn.style.display = 'inline-block';
        if (pollBtn) {
            pollBtn.disabled = true;
            pollBtn.style.opacity = '0.5';
            pollBtn.title = 'Опрос остановлен. Запустите опрос для выполнения ручного опроса.';
        }
    } else {
        console.log('[DEBUG] Polling is active - showing stop button');
        if (stopBtn) stopBtn.style.display = 'inline-block';
        if (startBtn) startBtn.style.display = 'none';
        if (pollBtn) {
            pollBtn.disabled = false;
            pollBtn.style.opacity = '1';
            pollBtn.title = 'Выполнить ручной опрос ККМ';
        }
    }
}

async function loadFullReport(ip) {
    if (!ip) return null;
    
    try {
        const response = await fetch('/api/kkt/fullReport/' + ip);
        if (response.ok) {
            fullReportData = await response.json();
            console.log('Полный отчет получен:', fullReportData);
            return fullReportData;
        }
    } catch (error) {
        console.error('Ошибка загрузки полного отчета:', error);
    }
    return null;
}

async function manualPoll() {
    if (pollingInProgress) {
        alert('Опрос уже выполняется, подождите...');
        return;
    }
    
    if (kktData?.isPollingStopped) {
        alert('Опрос этой ККМ остановлен. Запустите опрос перед выполнением ручного опроса.');
        return;
    }
    
    const ip = kktData?.ip;
    if (!ip) {
        alert('IP адрес не найден');
        return;
    }
    
    if (!confirm('Запустить принудительный опрос ККМ ' + ip + '? Это может занять до 30 секунд.')) {
        return;
    }
    
    pollingInProgress = true;
    const pollBtn = document.getElementById('pollBtn');
    const stopPollingBtn = document.getElementById('stopPollingBtn');
    const startPollingBtn = document.getElementById('startPollingBtn');
    const indicator = document.getElementById('pollingIndicator');
    
    if (pollBtn) {
        pollBtn.disabled = true;
        pollBtn.textContent = '⏳ Опрос...';
    }
    if (stopPollingBtn) stopPollingBtn.disabled = true;
    if (startPollingBtn) startPollingBtn.disabled = true;
    if (indicator) indicator.style.display = 'block';
    
    try {
        const pollResponse = await fetch('/api/kkt/poll/' + kktId, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' }
        });
        
        if (!pollResponse.ok) {
            const error = await pollResponse.json();
            throw new Error(error.error || 'Ошибка опроса');
        }
        
        const pollResult = await pollResponse.json();
        console.log('Результат опроса:', pollResult);
        
        const dbResponse = await fetch('/api/kkt/' + kktId);
        if (dbResponse.ok) {
            kktData = await dbResponse.json();
        }
        
        await loadFullReport(ip);
        renderDetails();
        
        let message = 'Опрос ККМ ' + ip + ' завершён!\n';
        message += 'Состояние: ' + (pollResult.state || kktData.State) + '\n';
        message += 'ЗН: ' + (pollResult.serialNumber || kktData.SerialNumber || 'не получен') + '\n';
        message += 'ИНН: ' + (pollResult.inn || kktData.INN || 'не получен') + '\n';
        message += 'Тип ФФД: ' + (pollResult.ffdVersion || kktData.FfdVersion || 'не получен') + '\n';
        message += 'Номер последнего чека: ' + (pollResult.lastReceiptNumber || kktData.LastReceiptNumber || 'не получен');
        
        alert(message);
        
    } catch (error) {
        console.error('Ошибка:', error);
        alert('Ошибка при опросе: ' + error.message);
    } finally {
        pollingInProgress = false;
        if (pollBtn) {
            pollBtn.disabled = false;
            pollBtn.textContent = '🔄 Опрос ККМ';
        }
        if (stopPollingBtn) stopPollingBtn.disabled = false;
        if (startPollingBtn) startPollingBtn.disabled = false;
        if (indicator) indicator.style.display = 'none';
    }
}

async function stopPolling() {
    console.log('[DEBUG] stopPolling called for id: ' + kktId);
    
    if (!confirm('Остановить опрос этой ККМ? Данные не будут обновляться до повторного запуска.')) return;
    
    const stopBtn = document.getElementById('stopPollingBtn');
    const originalText = stopBtn ? stopBtn.textContent : '⏹️ Остановить опрос';
    if (stopBtn) {
        stopBtn.disabled = true;
        stopBtn.textContent = '⏳ Остановка...';
    }
    
    try {
        const response = await fetch('/api/kkt/stopPolling/' + kktId, { method: 'POST' });
        console.log('[DEBUG] stopPolling response status: ' + response.status);
        
        if (response.ok) {
            const result = await response.json();
            console.log('[DEBUG] stopPolling result:', result);
            
            alert('Опрос ККМ остановлен');
            
            if (kktData) {
                kktData.isPollingStopped = true;
                console.log('[DEBUG] Updated local kktData.isPollingStopped: ' + kktData.isPollingStopped);
            }
            
            updatePollingButtons(true);
            await refreshDetailsFromDB();
        } else {
            const error = await response.json();
            console.error('[DEBUG] stopPolling error:', error);
            alert('Ошибка при остановке опроса: ' + (error.message || error.error || 'Неизвестная ошибка'));
        }
    } catch (error) {
        console.error('[DEBUG] stopPolling exception:', error);
        alert('Ошибка: ' + error.message);
    } finally {
        if (stopBtn) {
            stopBtn.disabled = false;
            stopBtn.textContent = originalText;
        }
    }
}

async function startPolling() {
    console.log('[DEBUG] startPolling called for id: ' + kktId);
    
    if (!confirm('Запустить опрос этой ККМ?')) return;
    
    const startBtn = document.getElementById('startPollingBtn');
    const originalText = startBtn ? startBtn.textContent : '▶️ Запустить опрос';
    if (startBtn) {
        startBtn.disabled = true;
        startBtn.textContent = '⏳ Запуск...';
    }
    
    try {
        const response = await fetch('/api/kkt/startPolling/' + kktId, { method: 'POST' });
        console.log('[DEBUG] startPolling response status: ' + response.status);
        
        if (response.ok) {
            const result = await response.json();
            console.log('[DEBUG] startPolling result:', result);
            
            alert('Опрос ККМ запущен');
            
            if (kktData) {
                kktData.isPollingStopped = false;
                console.log('[DEBUG] Updated local kktData.isPollingStopped: ' + kktData.isPollingStopped);
            }
            
            updatePollingButtons(false);
            await refreshDetailsFromDB();
        } else {
            const error = await response.json();
            console.error('[DEBUG] startPolling error:', error);
            alert('Ошибка при запуске опроса: ' + (error.message || error.error || 'Неизвестная ошибка'));
        }
    } catch (error) {
        console.error('[DEBUG] startPolling exception:', error);
        alert('Ошибка: ' + error.message);
    } finally {
        if (startBtn) {
            startBtn.disabled = false;
            startBtn.textContent = originalText;
        }
    }
}

async function refreshDetails() {
    await refreshDetailsFromDB();
}

function formatDateTime(dateString) {
    if (!dateString) return '—';
    try {
        const date = new Date(dateString);
        if (isNaN(date.getTime())) return dateString;
        return date.toLocaleString('ru-RU', {
            day: '2-digit',
            month: '2-digit',
            year: 'numeric',
            hour: '2-digit',
            minute: '2-digit'
        });
    } catch (e) {
        return dateString;
    }
}

function formatDate(dateString) {
    if (!dateString) return '—';
    try {
        const date = new Date(dateString);
        if (isNaN(date.getTime())) return dateString;
        return date.toLocaleDateString('ru-RU');
    } catch (e) {
        return dateString;
    }
}

function showModal(title, content) {
    const modal = document.getElementById('infoModal');
    const modalTitle = document.getElementById('modalTitle');
    const modalPre = document.getElementById('modalPre');
    
    modalTitle.textContent = title;
    modalPre.textContent = content;
    modal.style.display = 'block';
}

function closeModal() {
    document.getElementById('infoModal').style.display = 'none';
}

function showSDCardDetails() {
    if (!kktData) {
        showModal('SD карта - подробная информация', 'Данные не загружены');
        return;
    }
    
    const content = 'Размер кластера: ' + (kktData.SdCardClusterSize || '—') + '\n' +
        'Всего секторов: ' + (kktData.SdCardTotalSectors || '—') + '\n' +
        'Свободных секторов: ' + (kktData.SdCardFreeSectors || '—') + '\n' +
        'Ошибок I/O: ' + (kktData.SdCardIoErrors || '—') + '\n' +
        'Повторных записей: ' + (kktData.SdCardRetryCount || '—');
    
    showModal('SD карта - подробная информация', content);
}

function showFNExpiryDetails() {
    if (!kktData) {
        showModal('ФН - подробная информация', 'Данные не загружены');
        return;
    }
    
    let expiryDateStr = '—';
    if (kktData.FnExpiryDate) {
        try {
            const expiryDate = new Date(kktData.FnExpiryDate);
            if (!isNaN(expiryDate.getTime()) && expiryDate.getFullYear() > 1970) {
                expiryDateStr = expiryDate.toLocaleDateString('ru-RU');
            }
        } catch (e) {
            expiryDateStr = kktData.FnExpiryDate;
        }
    }
    
    const content = 'Срок действия ФН: ' + expiryDateStr + '\n' +
        'Кол-во оставшихся отчетов о перерегистрации: ' + (kktData.FreeRegistration || '—') + '\n' +
        'Выполнено отчетов о перерегистрации: ' + (kktData.RegistrationNumber || '—');
    
    showModal('ФН - подробная информация', content);
}

function showTransferData() {
    if (!kktData) {
        showModal('Передаваемые данные', 'Данные не загружены');
        return;
    }
    
    const content = 'ЗН: ' + (kktData.SerialNumber || '—') + '\n' +
        'ИНН: ' + (kktData.INN || '—') + '\n' +
        'РНМ: ' + (kktData.Rnm || '—') + '\n' +
        'ФН: ' + (kktData.FnNumber || '—') + '\n' +
        'ТИП Налогообложения: ' + (kktData.TaxSystem || '—') + '\n' +
        'Организация: ' + (kktData.UserName || '—') + '\n' +
        'Имя оператора: ' + (kktData.OperatorName || '—') + '\n' +
        'Адрес расчетов: ' + (kktData.Address || '—') + '\n' +
        'Оператор ОФД: ' + (kktData.OfdName || '—') + '\n' +
        'URL ОФД: ' + (kktData.OfdUrl || '—') + '\n' +
        'ИНН ОФД: ' + (kktData.OfdInn || '—') + '\n' +
        'Сайт ИФНС: ' + (kktData.TaxOfficeUrl || '—') + '\n' +
        'Место расчета: ' + (kktData.PlaceOfSettlement || '—') + '\n' +
        'Почта ОФД: ' + (kktData.SenderEmail || '—');
    
    showModal('Передаваемые данные', content);
}

function showKktStatusDetails() {
    if (!kktData) {
        showModal('Статус ККМ - подробная информация', 'Данные не загружены');
        return;
    }
    
    let content = '';
    const state = kktData.State;
    
    if (state === 'OK') {
        content = '✅ Нет ошибок.\nККМ работает в штатном режиме.';
    } else if (state === 'WARNING') {
        content = '⚠️ ПРЕДУПРЕЖДЕНИЕ\n\n';
        let hasIssues = false;
        
        if (kktData.BatteryVoltage && kktData.BatteryVoltage < 3) {
            content += '• Напряжение батареи: ' + kktData.BatteryVoltage + ' В (норма: ≥ 3 В)\n';
            hasIssues = true;
        }
        if (kktData.PowerSourceVoltage && kktData.PowerSourceVoltage < 24) {
            content += '• Напряжение источника: ' + kktData.PowerSourceVoltage + ' В (норма: ≥ 24 В)\n';
            hasIssues = true;
        }
        if (kktData.LastReceiptDate) {
            const lastReceipt = new Date(kktData.LastReceiptDate);
            const now = new Date();
            const daysDiff = Math.floor((now - lastReceipt) / (1000 * 60 * 60 * 24));
            if (daysDiff > 14) {
                content += '• Давно не было чеков: ' + daysDiff + ' дней\n';
                hasIssues = true;
            }
        }
        if (kktData.FnExpiryDate) {
            const expiryDate = new Date(kktData.FnExpiryDate);
            const now = new Date();
            const daysLeft = Math.floor((expiryDate - now) / (1000 * 60 * 60 * 24));
            if (daysLeft > 10 && daysLeft <= 30) {
                content += '• Срок действия ФН истекает через ' + daysLeft + ' дней\n';
                hasIssues = true;
            }
        }
        if (kktData.FnDocsLeft && kktData.FnDocsLeft < 100000 && kktData.FnDocsLeft >= 50000) {
            content += '• Остаточная ёмкость ФН: ' + kktData.FnDocsLeft.toLocaleString() + ' (норма: ≥ 100 000)\n';
            hasIssues = true;
        }
        if (!hasIssues) {
            content += 'Причина не определена.';
        }
    } else if (state === 'DANGER') {
        content = '🔴 КРИТИЧЕСКАЯ ОШИБКА\n\n';
        let hasIssues = false;
        
        if (kktData.KktStatus === 'OFFLINE') {
            content += '• ККМ не отвечает по сети (OFFLINE)\n';
            hasIssues = true;
        } else if (kktData.KktStatus === 'ERROR') {
            content += '• Ошибка при опросе ККМ: ' + (kktData.Error || 'Неизвестная ошибка') + '\n';
            hasIssues = true;
        } else if (kktData.KktStatus === 'TIMEOUT') {
            content += '• Превышен таймаут опроса ККМ\n';
            hasIssues = true;
        }
        
        if (kktData.SdCardStatus && kktData.SdCardStatus !== 0) {
            content += '• Ошибка SD карты: код ' + kktData.SdCardStatus + '\n';
            hasIssues = true;
        }
        
        if (kktData.FnExpiryDate) {
            const expiryDate = new Date(kktData.FnExpiryDate);
            const now = new Date();
            const daysLeft = Math.floor((expiryDate - now) / (1000 * 60 * 60 * 24));
            if (daysLeft <= 10 && daysLeft >= 0) {
                content += '• СРОК ДЕЙСТВИЯ ФН ИСТЕКАЕТ через ' + daysLeft + ' дней!\n';
                hasIssues = true;
            }
        }
        
        if (kktData.FnDocsLeft && kktData.FnDocsLeft < 50000) {
            content += '• Остаточная ёмкость ФН КРИТИЧЕСКИ МАЛА: ' + kktData.FnDocsLeft.toLocaleString() + '\n';
            hasIssues = true;
        }
        
        if (kktData.FnStatus === 'ARCHIVE') {
            content += '• Фискальный накопитель закрыт (архив)\n';
            hasIssues = true;
        }
        
        if (!hasIssues) {
            content += 'Причина не определена.';
        }
    } else {
        content = 'Состояние ККМ не определено.';
    }
    
    showModal('Статус ККМ - подробная информация', content);
}

function renderDetails() {
    // IP адрес
    const ipAddress = kktData.ip || '—';
    document.getElementById('ipAddress').textContent = ipAddress;
    
    // ЗН
    document.getElementById('serialNumber').textContent = kktData.SerialNumber || '—';
    
    // ИНН
    let inn = kktData.INN || '—';
    if (inn !== '—' && inn.startsWith('0')) {
        inn = inn.replace(/^0+/, '') || '0';
    }
    document.getElementById('inn').textContent = inn;
    
    // Юридическое лицо
    document.getElementById('legalName').textContent = kktData.LegalName || '—';
    
    // Версия прошивки
    let firmwareValue = '—';
    if (kktData.SoftwareVersion) {
        firmwareValue = kktData.SoftwareVersion;
        if (kktData.SoftwareBuild) firmwareValue += ' (build ' + kktData.SoftwareBuild + ')';
    }
    document.getElementById('firmwareVersion').textContent = firmwareValue;
    
    // Тип ФФД
    document.getElementById('ffdVersion').textContent = kktData.FfdVersion || '—';
    
    // URL ОФД
    document.getElementById('ofdUrl').textContent = kktData.OfdUrl || '—';
    
    // Подрежим
    let advancedModeText = '—';
    if (kktData.EcrAdvancedModeDescription) {
        advancedModeText = kktData.EcrAdvancedModeDescription;
        if (kktData.EcrAdvancedMode !== null && kktData.EcrAdvancedMode !== undefined) {
            advancedModeText += ' (' + kktData.EcrAdvancedMode + ')';
        }
    } else if (kktData.EcrAdvancedMode !== null && kktData.EcrAdvancedMode !== undefined) {
        advancedModeText = '' + kktData.EcrAdvancedMode;
    }
    document.getElementById('advancedMode').textContent = advancedModeText;
    
    // Статус режима
    let modeStatusText = '—';
    if (kktData.EcrModeStatusDescription) {
        modeStatusText = kktData.EcrModeStatusDescription;
        if (kktData.EcrModeStatus !== null && kktData.EcrModeStatus !== undefined) {
            modeStatusText += ' (' + kktData.EcrModeStatus + ')';
        }
    } else if (kktData.EcrModeStatus !== null && kktData.EcrModeStatus !== undefined) {
        modeStatusText = '' + kktData.EcrModeStatus;
    }
    document.getElementById('modeStatus').textContent = modeStatusText;
    
    // Напряжение батареи
    const batteryVoltage = kktData.BatteryVoltage;
    document.getElementById('batteryVoltage').textContent = batteryVoltage ? batteryVoltage + ' В' : '—';
    
    // Напряжение источника
    const powerVoltage = kktData.PowerSourceVoltage;
    document.getElementById('powerVoltage').textContent = powerVoltage ? powerVoltage + ' В' : '—';
    
    // Состояние SD карты
    const sdStatusValue = kktData.SdCardStatus;
    let sdStatusHtml = '—';
    if (sdStatusValue !== null && sdStatusValue !== undefined) {
        let statusText = '';
        let statusClass = '';
        if (sdStatusValue === 0) {
            statusText = 'OK';
            statusClass = 'sd-ok';
        } else {
            statusText = 'Ошибка (' + sdStatusValue + ')';
            statusClass = 'sd-error';
        }
        sdStatusHtml = '<span class="sd-status ' + statusClass + '">' + statusText + '</span>';
        sdStatusHtml += ' <span class="clickable" onclick="showSDCardDetails()">[подробно]</span>';
    } else {
        sdStatusHtml = '<span class="sd-status sd-error">Нет данных</span>';
        sdStatusHtml += ' <span class="clickable" onclick="showSDCardDetails()">[подробно]</span>';
    }
    document.getElementById('sdCardStatus').innerHTML = sdStatusHtml;
    
    // ФН данные
    const firstReceiptDate = kktData.FirstReceiptDate;
    document.getElementById('firstReceiptDate').textContent = firstReceiptDate ? formatDate(firstReceiptDate) : '—';
    
    const lastReceiptDate = kktData.LastReceiptDate;
    document.getElementById('lastReceiptDate').textContent = lastReceiptDate ? formatDateTime(lastReceiptDate) : '—';
    
    document.getElementById('lastReceiptNumber').textContent = kktData.LastReceiptNumber || '—';
    
    // Фаза жизни ФН
    let fnLifeStateText = '—';
    if (kktData.FnLifeState !== null && kktData.FnLifeState !== undefined) {
        fnLifeStateText = kktData.FnLifeStateDescription || 'Состояние ' + kktData.FnLifeState;
    }
    document.getElementById('fnLifeState').textContent = fnLifeStateText;
    
    // Дата замены ФН
    let fnExpiryHtml = '—';
    if (kktData.FnExpiryDate) {
        try {
            const expiryDate = new Date(kktData.FnExpiryDate);
            if (!isNaN(expiryDate.getTime()) && expiryDate.getFullYear() > 1970) {
                fnExpiryHtml = formatDate(kktData.FnExpiryDate);
            }
        } catch (e) {
            fnExpiryHtml = kktData.FnExpiryDate;
        }
        fnExpiryHtml += ' <span class="clickable" onclick="showFNExpiryDetails()">[подробно]</span>';
    } else {
        fnExpiryHtml = '— <span class="clickable" onclick="showFNExpiryDetails()">[подробно]</span>';
    }
    document.getElementById('fnExpiryDate').innerHTML = fnExpiryHtml;
    
    // Остаточная ёмкость ФН
    const capacity = kktData.FnDocsLeft;
    document.getElementById('remainingCapacity').textContent = capacity ? capacity.toLocaleString() : '—';
    
    // Последний полный опрос
    const lastFullPoll = kktData.LastFullPoll;
    document.getElementById('lastFullPoll').textContent = lastFullPoll ? formatDateTime(lastFullPoll) : '—';
    
    // Статус ККМ с кнопкой подробно
    let kktStatusText = '—';
    let kktStatusClass = '';
    if (kktData.State === 'OK') {
        kktStatusText = 'OK';
        kktStatusClass = 'status-ok';
    } else if (kktData.State === 'WARNING') {
        kktStatusText = 'Предупреждение';
        kktStatusClass = 'status-warning';
    } else if (kktData.State === 'DANGER' || kktData.Error) {
        kktStatusText = 'Ошибка';
        kktStatusClass = 'status-error';
    }
    document.getElementById('kktStatus').innerHTML = '<span class="' + kktStatusClass + '">' + kktStatusText + '</span> <span class="clickable" onclick="showKktStatusDetails()">[подробно]</span>';
}

window.onclick = function(event) {
    const modal = document.getElementById('infoModal');
    if (event.target === modal) closeModal();
}