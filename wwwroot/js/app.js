// Глобальные переменные
let editingKktId = null;
let editingNickname = null;
let showInactiveMode = false;

// API базовый URL
const API_BASE = '/api';

document.addEventListener('DOMContentLoaded', () => {
    console.log('DOM загружен, загружаем данные...');
    refreshTable();
    setInterval(refreshTable, 30000);
});

async function refreshTable() {
    const loading = document.getElementById('loading');
    const errorDiv = document.getElementById('error');
    const tableContainer = document.getElementById('table-container');
    
    if (loading) loading.style.display = 'block';
    if (errorDiv) errorDiv.style.display = 'none';
    if (tableContainer) tableContainer.style.display = 'none';
    
    try {
        const apiUrl = `${API_BASE}/kkt?includeInactive=${showInactiveMode}`;
        const response = await fetch(apiUrl);
        
        if (!response.ok) {
            throw new Error(`HTTP ${response.status}`);
        }
        
        const data = await response.json();
        renderTable(data);
        
        if (loading) loading.style.display = 'none';
        if (tableContainer) tableContainer.style.display = 'block';
    } catch (error) {
        console.error('Ошибка загрузки:', error);
        if (loading) loading.style.display = 'none';
        if (errorDiv) {
            errorDiv.style.display = 'block';
            errorDiv.textContent = 'Ошибка загрузки данных: ' + error.message;
        }
    }
}

function toggleShowInactive() {
    showInactiveMode = !showInactiveMode;
    const btn = document.getElementById('toggleInactiveBtn');
    if (btn) {
        if (showInactiveMode) {
            btn.textContent = '📋 Скрыть неактивные';
            btn.classList.remove('btn-warning');
            btn.classList.add('btn-info');
        } else {
            btn.textContent = '🗑️ Показать неактивные';
            btn.classList.remove('btn-info');
            btn.classList.add('btn-warning');
        }
    }
    refreshTable();
}

function renderTable(kktList) {
    const tbody = document.getElementById('kkt-table-body');
    if (!tbody) return;
    
    tbody.innerHTML = '';
    
    if (!kktList || !Array.isArray(kktList) || kktList.length === 0) {
        tbody.innerHTML = '<tr><td colspan="8" style="text-align: center;">Нет данных о ККМ</td></tr>';
        return;
    }
    
    for (const kkt of kktList) {
        const row = tbody.insertRow();
        
        const isActive = kkt.isActive !== false;
        if (!isActive) {
            row.style.opacity = '0.6';
            row.style.backgroundColor = '#f0f0f0';
        }
        
        row.insertCell(0).textContent = kkt.serialNumber || kkt.SerialNumber || '—';
        row.insertCell(1).textContent = kkt.legalName || kkt.LegalName || '—';
        
        const nicknameCell = row.insertCell(2);
        const nickname = kkt.nickname || kkt.Nickname || '';
        const safeNickname = (nickname || '').replace(/'/g, "\\'").replace(/"/g, '&quot;');
        nicknameCell.innerHTML = `
            <div style="display: flex; align-items: center; gap: 5px;">
                <span id="nickname-${kkt.id}">${escapeHtml(nickname || '—')}</span>
                <button class="btn btn-sm btn-warning" onclick="editNickname(${kkt.id}, '${safeNickname}')" title="Редактировать прозвище">✏️</button>
            </div>
        `;
        
        const lastSeen = kkt.lastSeen || kkt.LastSeen;
        row.insertCell(3).textContent = lastSeen ? formatDateTime(lastSeen) : '—';
        row.insertCell(4).textContent = kkt.ip || '—';
        row.insertCell(5).textContent = kkt.source || '—';
        
        const stateCell = row.insertCell(6);
        const state = kkt.state || kkt.State || 'UNKNOWN';
        let stateClass = '';
        let stateText = '';
        switch(state) {
            case 'OK': stateClass = 'status-ok'; stateText = '✓ OK'; break;
            case 'WARNING': stateClass = 'status-warning'; stateText = '⚠ WARNING'; break;
            case 'DANGER': stateClass = 'status-danger'; stateText = '✗ DANGER'; break;
            default: stateText = state;
        }
        stateCell.innerHTML = `<span class="${stateClass}">${stateText}</span>`;
        
        const actionsCell = row.insertCell(7);
        let buttons = `<button class="btn btn-sm btn-primary" onclick="viewDetails(${kkt.id})">📋 Подробно</button>`;
        if (isActive) {
            buttons += `<button class="btn btn-sm btn-danger" onclick="deactivateKkt(${kkt.id}, '${escapeHtml(kkt.serialNumber || kkt.ip || 'ККМ')}')" style="margin-left: 5px;">🗑️</button>`;
        } else {
            buttons += `<button class="btn btn-sm btn-success" onclick="activateKkt(${kkt.id})" style="margin-left: 5px;">🔄 Активировать</button>`;
        }
        actionsCell.innerHTML = buttons;
    }
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

function escapeHtml(text) {
    if (!text || text === '—') return '—';
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

function editNickname(id, currentNickname) {
    editingKktId = id;
    editingNickname = currentNickname === '—' ? '' : currentNickname;
    const nicknameInput = document.getElementById('nicknameInput');
    if (nicknameInput) nicknameInput.value = editingNickname;
    document.getElementById('nicknameModal').style.display = 'block';
}

async function saveNickname() {
    const nicknameInput = document.getElementById('nicknameInput');
    if (!nicknameInput) return;
    
    const newNickname = nicknameInput.value.trim();
    
    try {
        const response = await fetch(`${API_BASE}/kkt/${editingKktId}/nickname`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(newNickname)
        });
        
        if (response.ok) {
            closeNicknameModal();
            refreshTable();
        } else {
            alert('Ошибка при сохранении прозвища');
        }
    } catch (error) {
        alert('Ошибка: ' + error.message);
    }
}

function closeNicknameModal() {
    document.getElementById('nicknameModal').style.display = 'none';
    editingKktId = null;
}

let deactivateKktId = null;
let deactivateKktSerial = '';

function deactivateKkt(id, serial) {
    deactivateKktId = id;
    deactivateKktSerial = serial;
    document.getElementById('deactivateSerial').textContent = serial;
    document.getElementById('deactivateModal').style.display = 'block';
}

function closeDeactivateModal() {
    document.getElementById('deactivateModal').style.display = 'none';
    deactivateKktId = null;
}

async function confirmDeactivate() {
    if (!deactivateKktId) return;
    
    try {
        const response = await fetch(`${API_BASE}/kkt/${deactivateKktId}`, {
            method: 'DELETE'
        });
        
        if (response.ok) {
            closeDeactivateModal();
            refreshTable();
        } else {
            alert('Ошибка при деактивации');
        }
    } catch (error) {
        alert('Ошибка: ' + error.message);
    }
}

async function activateKkt(id) {
    if (!confirm('Вы уверены, что хотите активировать эту ККМ?')) return;
    
    try {
        const response = await fetch(`${API_BASE}/kkt/${id}/restore`, {
            method: 'POST'
        });
        
        if (response.ok) {
            refreshTable();
        } else {
            alert('Ошибка при активации');
        }
    } catch (error) {
        alert('Ошибка: ' + error.message);
    }
}

function viewDetails(id) {
    window.location.href = `/kkt-details.html?id=${id}`;
}

function goToOrganizations() {
    window.location.href = '/organizations.html';
}

window.onclick = function(event) {
    const nicknameModal = document.getElementById('nicknameModal');
    if (event.target === nicknameModal) closeNicknameModal();
    const deactivateModal = document.getElementById('deactivateModal');
    if (event.target === deactivateModal) closeDeactivateModal();
}
