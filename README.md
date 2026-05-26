# DTS Audio Monitor

Фоновая программа для **DTS Headphone:X** с монитором **XV272U F3** и наушниками **HyperX Cloud III**.

## Поведение

| Устройство | Что делает программа |
|------------|----------------------|
| **Headphones** | Только проверяет и включает пространственный звук. Устройство **не переключает**. |
| **Переключение на XV272U F3** | Полный цикл: наушники → DTS Sound Unbound → DTS Headphone:X → снова монитор. |

## Быстрый старт

### 1. Сборка (нужен [.NET 8 SDK](https://dotnet.microsoft.com/download))

```powershell
cd C:\Users\dms\Scripts\DTS-AudioMonitor
.\Install-DtsApp.ps1
```

Создаёт `publish\DtsAudioMonitor.exe`, ярлык в автозагрузке и запускает приложение в трее.

### 2. Или без автозагрузки

```powershell
.\Build-App.ps1
.\publish\DtsAudioMonitor.exe
```

Двойной щелчок по иконке в трее — открыть окно.

## Интерфейс

- **Автоматический режим** — фоновый мониторинг (можно поставить на паузу).
- **Применить DTS для монитора** — ручной запуск полного цикла.
- **Запускать с Windows** — ярлык в папке «Автозагрузка».
- **Журнал** — последние события.

## Настройки

Файл `config.json` (рядом с exe после сборки):

```json
{
  "HeadphonesNameMatch": "Headphones*",
  "MonitorNameMatch": "XV272U*",
  "HeadphonesCheckSeconds": 300,
  "PollSeconds": 3
}
```

## Старая версия (PowerShell)

Скрипты `DtsAudioMonitor.Service.ps1` и `Install-DtsService.ps1` остаются в репозитории. Рекомендуется использовать **GUI-приложение** вместо задачи планировщика.

## Структура

```
app/DtsAudioMonitor/     — исходники WPF
publish/                 — готовый exe (после Build-App.ps1)
SoundVolumeView/         — утилита spatial sound (скачивается установщиком)
```

## GitHub

https://github.com/k2dms/DTS-AudioMonitor
