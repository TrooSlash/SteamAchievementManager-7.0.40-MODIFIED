# Анализ проекта Steam Achievement Manager [MODIFIED]

Глубокий анализ кодовой базы. Выявленные проблемы сгруппированы по степени серьёзности.


## Критические

### 1. Неправильный вызов нативной функции GetISteamApps

**Файл:** `SAM.API\Wrappers\SteamClient018.cs`

У делегата `NativeGetISteamApps` отсутствует атрибут `[UnmanagedFunctionPointer(CallingConvention.ThisCall)]` и первый параметр `IntPtr self`. Все остальные 11 делегатов в этом файле имеют оба элемента.

Без `CallingConvention.ThisCall` маршаллер использует `StdCall`, который не устанавливает регистр ECX. Нативная функция `ISteamClient::GetISteamApps()` читает ECX как указатель `this` и получает мусор. Это влияет на инициализацию `SteamApps001` и `SteamApps008`, через которые приложение получает названия игр и язык.

```csharp
// Как сейчас (неправильно):
private delegate IntPtr NativeGetISteamApps(int user, int pipe, IntPtr version);

// Как должно быть:
[UnmanagedFunctionPointer(CallingConvention.ThisCall)]
private delegate IntPtr NativeGetISteamApps(IntPtr self, int user, int pipe, IntPtr version);
```


## Высокие

### 2. NullReferenceException при отмене загрузки списка игр

**Файл:** `SAM.Picker\GamePicker.cs`, строки 162-165

Если фоновый поток `_ListWorker` отменён (`e.Cancelled == true`), но без ошибки (`e.Error == null`), код входит в условие и вызывает `e.Error.ToString()` на null-ссылке. Приложение падает с `NullReferenceException`.

```csharp
if (e.Error != null || e.Cancelled == true)
{
    this.AddDefaultGames();
    MessageBox.Show(e.Error.ToString(), ...);  // e.Error может быть null
}
```

### 3. Заморозка интерфейса на 2 секунды в режиме Anti-Idle

**Файл:** `SAM.Picker\GamePicker.cs`, строка 1377

В режиме Anti-Idle при рестарте процессов вызывается `Thread.Sleep(2000)` из обработчика `Timer.Tick`, который выполняется в UI-потоке. Интерфейс полностью замораживается на 2 секунды при каждом цикле переподключения.

```csharp
KillIdleProcesses();
System.Threading.Thread.Sleep(2000);  // блокировка UI
StartIdleBatch(0, s.MaxGames);
```

### 4. Обращение к элементам интерфейса из фонового потока

**Файл:** `SAM.Picker\GamePicker.cs`, строки 126, 151

Метод `DoDownloadList` выполняется в потоке `BackgroundWorker` и напрямую записывает текст в `_PickerStatusLabel.Text` — элемент управления, принадлежащий UI-потоку. Это нарушение потокобезопасности WinForms, которое может привести к `InvalidOperationException` или повреждению состояния контрола.

```csharp
private void DoDownloadList(object sender, DoWorkEventArgs e)
{
    this._PickerStatusLabel.Text = "Downloading game list...";   // фоновый поток
    // ...
    this._PickerStatusLabel.Text = "Checking game ownership..."; // фоновый поток
}
```

### 5. Обработка колбэков Steam навсегда блокируется при исключении

**Файл:** `SAM.API\Client.cs`, строки 135-158

Флаг `_RunningCallbacks` устанавливается в `true` перед обработкой и сбрасывается в `false` после, но без конструкции `try/finally`. Если любой обработчик колбэка выбрасывает исключение:

- Флаг навсегда остаётся `true` — все будущие вызовы `RunCallbacks()` сразу возвращаются, обработка колбэков мертва до перезапуска
- `FreeLastCallback` не вызывается — нативное сообщение утекает и может заблокировать очередь

```csharp
this._RunningCallbacks = true;
// ... обработка, которая может выбросить исключение ...
Steam.FreeLastCallback(this._Pipe);     // пропускается при исключении
this._RunningCallbacks = false;         // не выполняется при исключении
```


## Средние

### 6. Утечка хендлов процессов в ActiveGamesForm

**Файл:** `SAM.Picker\ActiveGamesForm.cs`

Объекты `Process` никогда не вызывают `Dispose()`. При остановке игры (`StopGame`) процесс убивается, но хендл не освобождается. При паузе и возобновлении старый объект `Process` перезаписывается новым без освобождения. За длительную сессию с многократными паузами накапливаются утечки нативных хендлов ОС.

```csharp
private void StopGame(GameEntry entry)
{
    KillProcess(entry);
    entry.Process = null;  // хендл утёк, Dispose() не вызван
}
```

### 7. Утечка Bitmap-объектов при загрузке обложек

**Файл:** `SAM.Picker\GamePicker.cs`, строки 477-486

`ImageList.Images.Add()` копирует изображение во внутреннее хранилище. Оригинальные объекты `Bitmap` и миниатюры 32x32 не освобождаются после добавления. При библиотеке из 500+ игр — 1000+ утечек GDI+ объектов (полная обложка + миниатюра на каждую игру).

```csharp
this._LogoImageList.Images.Add(gameInfo.ImageUrl, logoInfo.Bitmap);
// logoInfo.Bitmap не освобождён

Bitmap smallIcon = new(32, 32);
// ... отрисовка миниатюры ...
this._SmallIconImageList.Images.Add(gameInfo.ImageUrl, smallIcon);
// smallIcon не освобождён
```

### 8. Неправильное отображение времени после 24 часов

**Файл:** `SAM.Picker\GamePicker.cs`, строки 1432, 1454

Свойство `elapsed.Hours` возвращает только компонент часов (0-23), а не полное количество. После 25 часов работы таймер показывает "01:00:00" вместо "25:00:00". Проблема актуальна для всех режимов простоя, поддерживающих неограниченную работу.

```csharp
string time = $"{elapsed.Hours:D2}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
// Должно быть: (int)elapsed.TotalHours
```

### 9. Процессы простоя не останавливаются при закрытии главного окна

**Файл:** `SAM.Picker\GamePicker.Designer.cs`

Метод `Dispose()` формы `GamePicker` не вызывает `StopIdling()` и `KillIdleProcesses()`. Обработчик `FormClosing` отсутствует. При закрытии приложения во время активного режима простоя дочерние процессы `SAM.Game.exe` продолжают работать как зомби.

### 10. Ошибка копирования: PageUp отправляет неверный тип прокрутки

**Файл:** `SAM.Picker\MyListView.cs`, строка 100

Клавиша `PageUp` сопоставлена с `ScrollEventType.SmallDecrement` (как `Keys.Up`), хотя должна быть `ScrollEventType.LargeDecrement`. Ошибка копирования из обработчика `Keys.Up`. Асимметрия с `Keys.PageDown`, который правильно использует `LargeIncrement`.

```csharp
case Keys.PageDown:
    type = ScrollEventType.LargeIncrement;   // правильно
    return true;
case Keys.PageUp:
    type = ScrollEventType.SmallDecrement;   // ошибка, должно быть LargeDecrement
    return true;
```


## Низкие

### 11. Утечка хендла процесса при исключении в CleanupExitedProcesses

**Файл:** `SAM.Picker\GamePicker.cs`, строки 1420-1423

Если обращение к `Process.HasExited` выбрасывает исключение (например, отказ в доступе), процесс удаляется из списка без вызова `Dispose()`, что приводит к утечке нативного хендла.

```csharp
catch
{
    this._IdleProcesses.RemoveAt(i);  // Dispose() не вызван
}
```
