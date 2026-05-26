# DTS Audio Monitor v1.1.1

## Исправления

- Исправлен краш при запуске из‑за повреждённого `logo.png` (приложение не появлялось в трее)
- Логотип и иконка окна загружаются с диска с запасным вариантом
- Пересозданы корректные `logo.png` и `app.ico`
- Скрипт `Assets/Create-Logo.ps1` для перегенерации иконок

## Установка

1. Скачайте `DtsAudioMonitor-v1.1.1-win-x64.zip` из Assets.
2. Распакуйте в любую папку.
3. Запустите **`DtsAudioMonitor.exe`** или **`Start DTS Audio Monitor.bat`**.
4. (Опционально) **`Install autostart.bat`** — автозагрузка Windows.

## Требования

- Windows 10/11 x64
- [DTS Sound Unbound](https://apps.microsoft.com/detail/9pj0nkl8mcsj)
- .NET **не требуется** (self-contained)
