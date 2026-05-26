# DTS Audio Monitor

Автоматизация **DTS Headphone:X** для связки **Headphones (HyperX Cloud III)** + **XV272U F3**.

## Поведение (фоновая служба)

| Устройство по умолчанию | Действие |
|-------------------------|----------|
| **Headphones (HyperX Cloud III)** | Только проверка/включение пространственного звука. Устройство **не переключается**. |
| Переключение **на XV272U F3** | Полный цикл: Headphones → DTS Sound Unbound → DTS Headphone:X → обратно на монитор. |

> **Почему не классическая Windows Service (services.msc)?**  
> DTS Sound Unbound и переключение звука работают в **сессии пользователя** (нужен рабочий стол).  
> Установка делается через **Планировщик заданий** при входе в Windows: автозапуск, перезапуск при сбое, скрытый процесс — по сути фоновая служба для вашего аккаунта.

## Установка

```powershell
cd C:\Users\dms\Scripts\DTS-AudioMonitor
.\Install-DtsAudioTools.ps1
```

**Служба (автозапуск при входе)** — PowerShell **от администратора**:

```powershell
.\Install-DtsService.ps1
```

## Управление

```powershell
.\Get-DtsServiceStatus.ps1   # статус и последние строки лога
.\Start-DtsService.ps1        # запустить задачу
.\Stop-DtsService.ps1         # остановить
.\Uninstall-DtsService.ps1    # удалить автозапуск (админ)
```

Лог: `service.log`  
Состояние: `service-state.json`  
Настройки: `config.json`

## Ручной запуск цикла для монитора

`Run-DtsFix.bat` или:

```powershell
.\Enable-DtsForXV272U.ps1
```

## config.json

```json
{
  "HeadphonesNameMatch": "Headphones*",
  "MonitorNameMatch": "XV272U*",
  "HeadphonesCheckSeconds": 300,
  "MonitorFixCooldownSeconds": 45,
  "PollSeconds": 3
}
```

Измените имена устройств, если в Windows они называются иначе.

## Файлы

| Файл | Назначение |
|------|------------|
| `DtsAudioMonitor.Service.ps1` | Фоновый worker |
| `DtsAudioMonitor.Common.ps1` | Общая логика |
| `Install-DtsService.ps1` | Установка автозапуска |
| `Enable-DtsForXV272U.ps1` | Ручной полный цикл |

## Зависимости

- [AudioDeviceCmdlets](https://github.com/frgnca/AudioDeviceCmdlets) — переключение устройств
- [SoundVolumeView](https://www.nirsoft.net/utils/sound_volume_view.html) — пространственный звук (скачивается установщиком)
