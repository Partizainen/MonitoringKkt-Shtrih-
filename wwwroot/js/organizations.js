let editingOrgId = null;

document.addEventListener('DOMContentLoaded', () => {
    loadOrganizations();
});

async function loadOrganizations() {
    const loading = document.getElementById('loading');
    const errorDiv = document.getElementById('error');
    const tableContainer = document.getElementById('table-container');
    
    loading.style.display = 'block';
    errorDiv.style.display = 'none';
    tableContainer.style.display = 'none';
    
    try {
        const response = await fetch('/api/LegalEntity');
        if (!response.ok) throw new Error('Ошибка загрузки данных');
        
        const data = await response.json();
        renderTable(data);
        
        loading.style.display = 'none';
        tableContainer.style.display = 'block';
    } catch (error) {
        loading.style.display = 'none';
        errorDiv.style.display = 'block';
        errorDiv.textContent = 'Ошибка: ' + error.message;
    }
}

function renderTable(organizations) {
    const tbody = document.getElementById('org-table-body');
    tbody.innerHTML = '';
    
    if (!organizations || organizations.length === 0) {
        tbody.innerHTML = '<tr><td colspan="3">Нет данных об организациях</td></tr>';
        return;
    }
    
    for (const org of organizations) {
        const row = tbody.insertRow();
        row.insertCell(0).textContent = org.name;
        row.insertCell(1).textContent = org.inn;
        
        const actionsCell = row.insertCell(2);
        actionsCell.innerHTML = `
            <button class="btn btn-sm btn-danger" onclick="deleteOrganization(${org.id})">🗑️ Удалить</button>
        `;
    }
}

function openAddModal() {
    editingOrgId = null;
    document.getElementById('modalTitle').textContent = 'Добавление организации';
    document.getElementById('orgName').value = '';
    document.getElementById('orgInn').value = '';
    document.getElementById('orgModal').style.display = 'block';
}

async function saveOrganization() {
    const name = document.getElementById('orgName').value.trim();
    const inn = document.getElementById('orgInn').value.trim();
    
    if (!name || !inn) {
        alert('Заполните все поля');
        return;
    }
    
    if (!/^\d{10,12}$/.test(inn)) {
        alert('ИНН должен содержать 10-12 цифр');
        return;
    }
    
    try {
        let response;
        if (editingOrgId) {
            response = await fetch(`/api/LegalEntity/${editingOrgId}`, {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ id: editingOrgId, name, inn })
            });
        } else {
            response = await fetch('/api/LegalEntity', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ name, inn })
            });
        }
        
        if (response.ok) {
            closeModal();
            loadOrganizations();
            // Запускаем синхронизацию привязки ККМ
            await fetch('/api/kkt/syncLegalEntities', { method: 'POST' });
        } else {
            alert('Ошибка при сохранении');
        }
    } catch (error) {
        alert('Ошибка: ' + error.message);
    }
}

async function deleteOrganization(id) {
    if (!confirm('Вы уверены, что хотите удалить эту организацию?')) return;
    
    try {
        const response = await fetch(`/api/LegalEntity/${id}`, { method: 'DELETE' });
        if (response.ok) {
            loadOrganizations();
            // Запускаем синхронизацию привязки ККМ
            await fetch('/api/kkt/syncLegalEntities', { method: 'POST' });
        } else {
            alert('Ошибка при удалении');
        }
    } catch (error) {
        alert('Ошибка: ' + error.message);
    }
}

function closeModal() {
    document.getElementById('orgModal').style.display = 'none';
    editingOrgId = null;
}