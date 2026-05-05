📚 KKTMonitor - Документация проекта
1. Назначение проекта
KKTMonitor — это веб-приложение для централизованного мониторинга контрольно-кассовых машин (ККМ) в розничной сети. Система позволяет:

Автоматически обнаруживать ККМ в сети через DHCP лизинг

Выполнять периодический опрос ККМ для получения технического состояния

Отслеживать критически важные параметры (остаток ФН, срок действия, статус смены, ошибки)

Отправлять email-уведомления при изменении статуса на Warning/Danger

Интегрироваться с Zabbix для централизованного мониторинга

Управлять списком ККМ (добавление, редактирование, удаление)

Просматривать детальную информацию по каждой ККМ в реальном времени

2. Функциональные возможности
2.1 Основной функционал
Автоматическое обнаружение ККМ — получение списка IP-адресов через API /api/leases

Планировщик опросов:

Быстрый опрос (ping) — интервал 60 секунд

Полный опрос — по расписанию (настраивается пользователем)

Ручной опрос — принудительное получение данных с ККМ

Остановка/запуск опроса для отдельных ККМ

2.2 Статусы ККМ
Статус	Описание
✅ OK	ККМ работает в штатном режиме
⚠️ WARNING	Предупреждение (низкое напряжение, давно не было чеков, скоро истекает срок ФН)
🔴 DANGER	Критическая ошибка (нет связи, ошибка SD карты, истек срок ФН)
2.3 Мониторинг параметров
Заводской номер (ЗН)

ИНН

Номер фискального накопителя (ФН)

Версия прошивки ККТ

Тип ФФД (1.0, 1.05, 1.1, 1.2)

Дата первого и последнего чека

Номер последнего чека

Остаточная ёмкость ФН

Дата замены ФН (+450 дней от регистрации)

Состояние смены (открыта/закрыта)

Напряжение батареи и источника питания

Состояние SD карты

Подрежим и статус режима ККМ

Фаза жизни ФН

2.4 Уведомления
Email уведомления при изменении статуса на WARNING/DANGER

Повторные уведомления при длительной недоступности (>30 минут)

Тестовая отправка для проверки настроек SMTP

2.5 Интеграция с Zabbix
Отправка статусов ККМ в Zabbix Trapper

Кодирование статуса: 0=OK, 1=WARNING, 2=DANGER

Настраиваемый ключ элемента данных и имя хоста

3. Технические требования
3.1 Аппаратные требования
Компонент	Минимальные требования
Процессор	2 ядра, 2 ГГц
ОЗУ	4 ГБ
Диск	10 ГБ свободного места
Сеть	Доступ к ККМ по порту 7778 (TCP)
3.2 Программное обеспечение
Компонент	Версия	Назначение
ОС Windows Server	2016/2019/2022	Хост для IIS и драйвера ККМ
IIS	10.0	Веб-сервер
.NET Runtime	8.0	Среда выполнения приложения
MySQL Server	8.0+	База данных
DrvFR	5.25+	Драйвер для работы с ККМ (Штрих - проект создавался на базе Торгбаланса)
3.3 Сетевое окружение
ККМ должны быть доступны по TCP порту 7778

SMTP сервер для отправки email (порт 587 или 465)

Zabbix Trapper сервер (порт 10051) — опционально

4. Установка и настройка
4.1 Установка драйвера DrvFR
Скачайте драйвер DrvFR версии 5.25 с сайта производителя

Установите драйвер на сервер

Убедитесь, что COM-объект AddIn.DrvFR зарегистрирован в системе

4.2 Установка MySQL
sql
-- Создание базы данных
CREATE DATABASE kktmonitor CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

-- Создание пользователя
CREATE USER 'kktmonitor'@'localhost' IDENTIFIED BY 'your_password';
GRANT ALL PRIVILEGES ON kktmonitor.* TO 'kktmonitor'@'localhost';
FLUSH PRIVILEGES;
4.3 Скрипт создания таблиц
sql
-- Использовать базу данных
USE kktmonitor;

-- Таблица ККМ
CREATE TABLE IF NOT EXISTS kkt (
    id INT AUTO_INCREMENT PRIMARY KEY,
    serial_number VARCHAR(50) NULL,
    fn_number VARCHAR(50) NULL,
    nickname VARCHAR(255) NULL,
    ip VARCHAR(50) NULL,
    source VARCHAR(50) NULL,
    last_seen DATETIME NULL,
    last_check DATETIME NULL,
    last_full_poll DATETIME NULL,
    fn_status VARCHAR(50) NULL,
    ofd_status VARCHAR(50) NULL,
    kkt_status VARCHAR(50) NULL,
    error TEXT NULL,
    state VARCHAR(20) NULL,
    fn_expiry_date DATETIME NULL,
    shift_state VARCHAR(50) NULL,
    fn_docs_left INT NULL,
    inn VARCHAR(12) NULL,
    legal_entity_id INT NULL,
    last_buy_receipt_number INT NULL,
    software_version VARCHAR(50) NULL,
    software_build INT NULL,
    ofd_server VARCHAR(50) NULL,
    is_active TINYINT(1) DEFAULT 1,
    deleted_at DATETIME NULL,
    last_receipt_number INT NULL,
    first_receipt_date DATE NULL,
    last_receipt_date DATETIME NULL,
    fn_detailed_status VARCHAR(50) NULL,
    ffd_version VARCHAR(10) NULL,
    is_polling_stopped TINYINT(1) DEFAULT 0,
    -- дополнительные поля
    ofd_url VARCHAR(255) NULL,
    ofd_name VARCHAR(255) NULL,
    ofd_inn VARCHAR(12) NULL,
    tax_office_url VARCHAR(255) NULL,
    user_name VARCHAR(255) NULL,
    operator_name VARCHAR(100) NULL,
    address VARCHAR(500) NULL,
    place_of_settlement VARCHAR(255) NULL,
    sender_email VARCHAR(255) NULL,
    rnm VARCHAR(50) NULL,
    tax_system INT NULL,
    ecr_mode INT NULL,
    ecr_mode_description VARCHAR(255) NULL,
    ecr_advanced_mode INT NULL,
    ecr_advanced_mode_description VARCHAR(255) NULL,
    ecr_mode_status INT NULL,
    ecr_mode_status_description VARCHAR(255) NULL,
    battery_voltage DOUBLE NULL,
    power_source_voltage DOUBLE NULL,
    sd_card_status INT NULL,
    sd_card_cluster_size INT NULL,
    sd_card_total_sectors INT NULL,
    sd_card_free_sectors INT NULL,
    sd_card_io_errors INT NULL,
    sd_card_retry_count INT NULL,
    free_registration INT NULL,
    registration_number INT NULL,
    fn_life_state INT NULL,
    fn_life_state_description VARCHAR(100) NULL,
    shift_open_time DATETIME NULL
);

-- Таблица юридических лиц
CREATE TABLE IF NOT EXISTS legal_entities (
    id INT AUTO_INCREMENT PRIMARY KEY,
    name VARCHAR(255) NOT NULL,
    inn VARCHAR(12) NOT NULL
);

-- Таблица настроек
CREATE TABLE IF NOT EXISTS settings (
    id INT PRIMARY KEY,
    notification_email VARCHAR(255) NULL,
    updated_at DATETIME NULL,
    smtp_server VARCHAR(255) NULL,
    smtp_port INT NULL,
    smtp_user VARCHAR(255) NULL,
    smtp_password VARCHAR(255) NULL,
    from_email VARCHAR(255) NULL,
    enable_ssl TINYINT(1) NULL,
    smtp_encryption VARCHAR(20) NULL,
    poll_interval_value INT DEFAULT 1,
    poll_interval_unit VARCHAR(10) DEFAULT 'hours',
    poll_interval_enabled TINYINT(1) DEFAULT 1,
    zabbix_server VARCHAR(255) NULL,
    zabbix_port INT DEFAULT 10051,
    zabbix_host VARCHAR(255) NULL,
    zabbix_enabled TINYINT(1) DEFAULT 0,
    zabbix_key VARCHAR(100) DEFAULT 'kkt.status'
);

-- Таблица расписания опросов
CREATE TABLE IF NOT EXISTS schedule_settings (
    id INT AUTO_INCREMENT PRIMARY KEY,
    schedule_time VARCHAR(5) NOT NULL,
    is_active TINYINT(1) DEFAULT 1,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
);

-- Начальные данные
INSERT INTO settings (id, notification_email, updated_at) VALUES (1, '', NOW())
ON DUPLICATE KEY UPDATE id = id;

INSERT INTO schedule_settings (schedule_time, is_active) VALUES 
('08:00', 1),
('12:00', 1),
('18:00', 1),
('00:30', 1);
4.4 Настройка приложения
Файл appsettings.json:

json
{
  "ConnectionStrings": {
    "Default": "Server=localhost;Database=kktmonitor;User Id=kktmonitor;Password=your_password;"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AppSettings": {
    "LeaseFilter": ""
  }
}
4.5 Публикация и развертывание
bash
# Публикация проекта
dotnet publish -c Release -o C:\inetpub\wwwroot\kktmonitor

# Настройка прав на папку
icacls C:\inetpub\wwwroot\kktmonitor /grant "IIS_IUSRS:(OI)(CI)RX"
4.6 Настройка IIS
Создайте новый сайт или приложение в IIS

Укажите физический путь к опубликованным файлам

Назначьте порт (рекомендуется 5010 или 80)

Установите пул приложений .NET CLR версии "Без управляемого кода"

Включите опцию "Загрузить профиль пользователя" для пула приложений

5. API возможности
5.1 KKT API (/api/kkt)
Метод	Эндпоинт	Описание
GET	/api/kkt	Получить список всех ККМ
GET	/api/kkt/{id}	Получить ККМ по ID
POST	/api/kkt	Создать новую ККМ
PUT	/api/kkt/{id}	Обновить ККМ
DELETE	/api/kkt/{id}	Мягкое удаление
POST	/api/kkt/poll/{id}	Ручной опрос ККМ
POST	/api/kkt/stopPolling/{id}	Остановить опрос
POST	/api/kkt/startPolling/{id}	Запустить опрос
PUT	/api/kkt/{id}/nickname	Изменить прозвище
PUT	/api/kkt/{id}/legalEntity	Привязать юрлицо
5.2 Настройки API (/api/settings)
Метод	Эндпоинт	Описание
GET	/api/settings	Получить настройки
POST	/api/settings/notificationEmail	Сохранить email
GET	/api/settings/smtp	Получить SMTP настройки
POST	/api/settings/smtp	Сохранить SMTP настройки
POST	/api/settings/testEmail	Отправить тестовое письмо
GET	/api/settings/zabbix	Получить Zabbix настройки
POST	/api/settings/zabbix	Сохранить Zabbix настройки
POST	/api/settings/zabbix/test	Тест подключения к Zabbix
5.3 Расписание API (/api/schedule)
Метод	Эндпоинт	Описание
GET	/api/schedule	Получить расписание
POST	/api/schedule	Добавить время опроса
DELETE	/api/schedule/{id}	Удалить время опроса
PATCH	/api/schedule/{id}/toggle	Вкл/выкл время опроса
5.4 Юридические лица API (/api/LegalEntity)
Метод	Эндпоинт	Описание
GET	/api/LegalEntity	Получить все организации
POST	/api/LegalEntity	Создать организацию
PUT	/api/LegalEntity/{id}	Обновить организацию
DELETE	/api/LegalEntity/{id}	Удалить организацию
5.5 Примеры запросов
bash
# Получить список всех ККМ (включая неактивные)
curl http://localhost:5010/api/kkt?includeInactive=true

# Ручной опрос ККМ
curl -X POST http://localhost:5010/api/kkt/poll/1

# Сохранить email для уведомлений
curl -X POST http://localhost:5010/api/settings/notificationEmail \
  -H "Content-Type: application/json" \
  -d '"admin@example.com"'

# Сохранить SMTP настройки
curl -X POST "http://localhost:5010/api/settings/smtp?password=MonitoringKkt" \
  -H "Content-Type: application/json" \
  -d '{"smtpServer":"smtp.mail.ru","smtpPort":587,"smtpUser":"user@mail.ru","smtpPassword":"pass","fromEmail":"user@mail.ru","smtpEncryption":"TLS"}'
6. Пароли доступа
Раздел	Пароль
Настройки SMTP	MonitoringKkt
Настройки Zabbix	MonitoringKkt
Настройка списка ККМ	MonitoringKkt
7. Структура проекта
text
KKTMonitor/
├── Controllers/
│   ├── KktController.cs          # API для ККМ
│   ├── KktDetailsController.cs   # Детальные операции
│   ├── LeasesController.cs       # DHCP лизинг
│   ├── LegalEntityController.cs  # Юридические лица
│   ├── ScheduleController.cs     # Расписание опросов
│   └── SettingsController.cs     # Настройки системы
├── Models/
│   ├── Kkt.cs                    # Модель ККМ
│   ├── LegalEntity.cs            # Модель юрлица
│   ├── ScheduleSettings.cs       # Модель расписания
│   └── Settings.cs               # Модель настроек
├── Services/
│   ├── DbContext.cs              # Подключение к БД
│   ├── EmailService.cs           # Отправка email
│   ├── KktDriverService.cs       # Работа с DrvFR
│   ├── KktPoller.cs              # Фоновый опрос
│   ├── KktService.cs             # Работа с БД ККМ
│   ├── KktStateService.cs        # Расчет статусов
│   ├── LegalEntityService.cs     # Работа с юрлицами
│   ├── ScheduleService.cs        # Работа с расписанием
│   ├── SettingsService.cs        # Работа с настройками
│   └── ZabbixService.cs          # Интеграция с Zabbix
├── wwwroot/
│   ├── css/style.css
│   ├── js/
│   │   ├── app.js
│   │   ├── kkt-details.js
│   │   └── organizations.js
│   ├── index.html
│   ├── kkt-details.html
│   ├── kkt-list.html
│   ├── organizations.html
│   └── settings.html
├── Program.cs
└── appsettings.json
8. Устранение неполадок
8.1 Ошибка "DrvFR не зарегистрирован"
Установите драйвер DrvFR

Зарегистрируйте COM-объект: regsvr32 DrvFR.dll

8.2 Не удается подключиться к ККМ
Проверьте доступность IP и порта 7778

Убедитесь, что ККМ настроена на TCP режим

Проверьте пароль (должен быть 30)

8.3 Не отправляются email
Проверьте настройки SMTP

Убедитесь, что пароль сохранен в базе данных

Проверьте логи в консоли IIS

8.4 Не сохраняются настройки
Проверьте права на запись в базу данных

Убедитесь, что таблица settings существует

Проверьте, что пароль MonitoringKkt введен верно

9. Лицензия
Проект является внутренней разработкой и предназначен для использования в корпоративной среде.