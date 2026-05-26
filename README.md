# DTS Audio Monitor — автоматизация для XV272U F3

Когда вы переключаете воспроизведение на **XV272U F3**, Windows не даёт включить **DTS Headphone:X** напрямую. Этот набор скриптов делает обходной цикл автоматически:

1. Временно переключает на **Headphones (HyperX Cloud III)**
2. Запускает **DTS Sound Unbound** (при необходимости нажимает «Try»)
3. Включает **DTS Headphone:X** (пространственный звук)
4. Возвращает вывод на **XV272U F3**

## Установка (один раз)

Откройте PowerShell и выполните:

```powershell
cd C:\Users\dms\Scripts\DTS-AudioMonitor
.\Install-DtsAudioTools.ps1
```

## Ручной запуск

Двойной щелчок по `Run-DtsFix.bat` или:

```powershell
.\Enable-DtsForXV272U.ps1
```

## Автоматизация при переключении на монитор

**Вариант A — фоновый watcher (рекомендуется)**

```powershell
.\Watch-DtsAudioDevice.ps1
```

Оставьте окно открытым или установите автозапуск (нужны права администратора):

```powershell
.\Install-DtsWatcherTask.ps1
```

Watcher следит за сменой устройства по умолчанию. Как только вы переключились **с наушников на XV272U F3**, запускается цикл DTS. Лог: `watch.log`.

**Параметры watcher:**

| Параметр | По умолчанию | Описание |
|----------|--------------|----------|
| `-PollSeconds` | 3 | Интервал опроса |
| `-CooldownSeconds` | 45 | Пауза между автозапусками |

## Параметры основного скрипта

```powershell
.\Enable-DtsForXV272U.ps1 -SkipDtsApp    # только переключение + spatial, без UI DTS
.\Enable-DtsForXV272U.ps1 -Quiet         # меньше вывода
```

## Файлы

| Файл | Назначение |
|------|------------|
| `Enable-DtsForXV272U.ps1` | Основной цикл |
| `Watch-DtsAudioDevice.ps1` | Фоновый мониторинг |
| `Install-DtsAudioTools.ps1` | Загрузка зависимостей |
| `Install-DtsWatcherTask.ps1` | Задача в Планировщике |
| `SoundVolumeView\` | Утилита NirSoft для spatial sound |

## Если не работает

- Убедитесь, что **DTS Sound Unbound** установлен из Microsoft Store.
- Проверьте вручную: ПКМ по значку звука → **Пространственный звук** → **DTS Headphone:X** (на наушниках).
- Если имена устройств изменились, отредактируйте `$hpFriendly` и `$monFriendly` в `Enable-DtsForXV272U.ps1`.
