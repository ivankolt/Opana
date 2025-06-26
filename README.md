Opana 🏭📦

**Opana** — комплексное WPF приложение для управления производственными процессами, складскими операциями и заказами в текстильной индустрии. Система предоставляет полный цикл управления: от приема заказов до отгрузки готовой продукции.

**Автор:** [ivankolt](https://github.com/ivankolt)  
**Язык:** C#  
**Платформа:** .NET Framework/WPF  
**Статус:** Готов к использованию ✅

## Установка и запуск 🚀

1. Скачайте последнюю версию из раздела Releases.
2. Распакуйте архив в удобную папку.
3. Запустите `UchPR.exe`.
4. При первом запуске настройте подключение к базе данных.

## Сборка проекта 🔨

```bash
git clone https://github.com/ivankolt/Opana.git
```
- Откройте `UchPR.sln` в Visual Studio.
- Нажмите F5 для запуска.

## Возможности ✨

- 📦 **Управление складом**  
  - Инвентаризация — учет и контроль складских запасов  
  - Приемка материалов — регистрация поступающих материалов  
  - Управление остатками — отслеживание количества материалов  
  - Настройка пороговых значений — автоматические уведомления о низких остатках  

- 🏭 **Производство**  
  - Создание производственных заданий — планирование производства  
  - Раскрой материалов — оптимизация использования тканей  
  - Учет отходов — контроль производственных потерь  
  - Просмотр раскроев — визуализация планов раскроя  

- 👥 **Управление заказами**  
  - Заказы клиентов — прием и обработка заказов  
  - Детали заказов — подробная информация по каждому заказу  
  - Менеджерские заказы — управление заказами менеджерами  
  - История документов — отслеживание изменений  

- 🎨 **Дизайн продукции**  
  - Конструктор изделий — создание новых моделей  
  - Состав продукции — управление компонентами изделий  
  - Каталог продукции — просмотр и выбор изделий  
  - Детали продукции — подробные характеристики  

- 📊 **Отчетность**  
  - Отчеты по инвентаризации — аналитика складских операций  
  - История операций — журнал всех действий в системе  

## Технические требования 💻

- .NET Framework 4.5+
- Windows 7/8/10/11
- SQL Server или SQLite для базы данных
- Минимум 2 ГБ RAM
- 100 МБ свободного места на диске

## Структура проекта 🗂️

```
Opana/
├── Windows/              # Окна приложения
│   ├── MainWindow.xaml   # Главное окно
│   ├── LoginWindow.xaml  # Окно авторизации
│   └── ...
├── Models/               # Модели данных
├── Services/             # Бизнес-логика
├── DataBase.cs           # Работа с БД
└── App.xaml              # Конфигурация приложения
```

## Основные компоненты 🔧

**Окна системы**
- MainWindow — главное окно приложения
- LoginWindow — авторизация пользователей
- RegistrationWindow — регистрация новых пользователей
- InventoryWindow — управление складом
- ProductListWindow — каталог продукции
- CustomerOrdersWindow — заказы клиентов
- CuttingWindow — раскрой материалов

**Сервисы**
- UserSessionService — управление сессиями пользователей
- DocumentHistoryRepository — история документов
- DataBase — работа с базой данных

## Использование 📖

- 🔐 **Авторизация**  
  - Запустите приложение  
  - Введите логин и пароль  
  - Или зарегистрируйте новую учетную запись  

- 📦 **Работа со складом**  
  - Перейдите в раздел "Склад"  
  - Добавьте материалы через "Приемка материалов"  
  - Проведите инвентаризацию  
  - Настройте пороговые значения для автоуведомлений  

- 📋 **Создание заказа**  
  - Откройте "Заказы клиентов"  
  - Создайте новый заказ  
  - Выберите продукцию из каталога  
  - Укажите количество и сроки  

## Разработка 🛠️

**Требования для разработки**
- Visual Studio 2017+
- .NET Framework SDK
- SQL Server Management Studio (опционально)

## Поддержка 🤝

Если у вас возникли вопросы или проблемы:
- Создайте Issue в этом репозитории
- Опишите проблему максимально подробно
- Приложите скриншоты при необходимости

## Лицензия 📄

Проект распространяется под лицензией **MIT** — одной из самых популярных и простых лицензий, разрешающей использовать, изменять и распространять код с минимальными ограничениями. Просто сохраняйте оригинальный текст лицензии и уведомление об авторских правах при распространении.

```
MIT License

Copyright (c) 2025 ivankolt

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

**Opana — эффективное управление текстильным производством! 🧵✨**
