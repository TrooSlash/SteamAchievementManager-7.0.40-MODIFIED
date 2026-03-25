using System.Collections.Generic;

namespace SAM.Game
{
    internal static class GameLocalization
    {
        public enum Language { English, Russian }

        private static Language _current = Language.English;
        public static Language Current { get => _current; set => _current = value; }

        static GameLocalization()
        {
            string lang = System.Environment.GetEnvironmentVariable("SAM_LANGUAGE");
            if (lang == "Russian") _current = Language.Russian;
        }

        private static readonly Dictionary<string, string> En = new()
        {
            // UI controls
            { "CommitChanges", "Commit Changes" },
            { "Refresh", "Refresh" },
            { "Reset", "Reset" },
            { "Achievements", "Achievements" },
            { "Statistics", "Statistics" },
            { "Name", "Name" },
            { "Description", "Description" },
            { "UnlockTime", "Unlock Time" },
            { "GlobalPercent", "Global %" },
            { "ShowOnly", "Show only" },
            { "Locked", "locked" },
            { "Unlocked", "unlocked" },
            { "HideProtected", "hide protected" },
            { "Filter", "Filter" },
            { "LockAll", "Lock All" },
            { "InvertAll", "Invert All" },
            { "UnlockAll", "Unlock All" },
            { "DownloadStatus", "Download status" },
            { "Value", "Value" },
            { "Extra", "Extra" },

            // Messages
            { "DownloadingIcons", "Downloading {0} icons..." },
            { "ErrorRetrievingStats", "Error while retrieving stats: {0}" },
            { "FailedLoadSchema", "Failed to load schema." },
            { "ErrorAchievementsRetrieval", "Error when handling achievements retrieval." },
            { "ErrorStatsRetrieval", "Error when handling stats retrieval." },
            { "RetrievedStats", "Retrieved {0} achievements and {1} statistics." },
            { "RetrievingStats", "Retrieving stat information..." },
            { "ErrorSettingState", "An error occurred while setting the state for {0}, aborting store." },
            { "ErrorSettingValue", "An error occurred while setting the value for {0}, aborting store." },
            { "ErrorStoring", "An error occurred while storing, aborting." },
            { "StoredStats", "Stored {0} achievements and {1} statistics." },
            { "ConfirmResetStats", "Are you absolutely sure you want to reset stats?" },
            { "ConfirmResetAchievements", "Do you want to reset achievements too?" },
            { "ReallyReallySure", "Really really sure?" },
            { "ProtectedAchievement", "Sorry, but this is a protected achievement and cannot be managed with SAM Evolved." },
            { "ProtectedAchievements", "protected" },
            { "ProtectedTooltip", "\u26A0 Protected — server-validated achievement, cannot be modified locally" },
            { "AllProtectedWarning", "All achievements in this game are server-validated and cannot be modified with SAM.\n\nThis game uses server-side achievement validation — changes will be rejected by Steam servers." },
            { "StatProtected", "Stat is protected! -- you can't modify it" },
            { "InvalidValue", "Invalid value" },
            { "StatsEditingAgreement", "I understand by modifying the values of stats, I may screw things up and can't blame anyone but myself." },
            { "GenericError", "generic error -- this usually means you don't own the game" },

            // MessageBox titles
            { "Error", "Error" },
            { "Warning", "Warning" },
            { "Question", "Question" },
            { "Information", "Information" },

            // VAC protection
            { "VacDetected", "VAC / Anti-Cheat Protected" },
            { "VacWarningText", "⚠ VAC / Anti-Cheat Protected — Editing achievements or statistics for this game may result in a VAC ban or account restrictions." },
            { "VacBlockedEdit", "This game uses VAC / Anti-Cheat protection.\nModifying achievements or statistics is blocked to protect your account.\n\nClick \"I understand the risks\" to override at your own risk." },
            { "VacStoreWarning", "This game uses VAC / Anti-Cheat protection!\n\nSaving changes may result in:\n• VAC ban (permanent)\n• Game ban\n• Online features restriction\n\nAre you absolutely sure you want to continue?" },
            { "VacOverrideConfirm", "WARNING: You are about to enable editing for a VAC-protected game.\n\nThis may lead to:\n• Permanent VAC ban on your account\n• Loss of online multiplayer access\n• Game-specific bans\n\nThe developers are NOT responsible for any consequences.\n\nDo you want to proceed?" },
            { "VacOverrideDone", "Unlocked" },
            { "VacOverrideBtn", "I understand the risks" },
            { "VacWarningOverridden", "⚠ VAC protection override active — You are editing at your own risk. Any consequences are your responsibility." },
            { "VacUnlockAllWarning", "This game uses VAC / Anti-Cheat protection!\n\nUnlocking all achievements may result in:\n• Permanent VAC ban\n• Game ban\n• Loss of online features\n\nYes — Unlock all (at your own risk)\nNo — Cancel\n\nDo you want to unlock all achievements?" },
            { "VacActionWarning", "This game uses VAC / Anti-Cheat protection.\nModifying achievements or statistics may result in a ban.\n\nAre you sure you want to continue?" },
            { "VacClickWarning", "This game uses VAC / Anti-Cheat protection.\n\nChanging achievements may lead to a VAC ban or account restrictions.\nThis warning will only appear once per session.\n\nDo you want to edit achievements for this game?" },
            { "LockAllConfirm", "Are you sure you want to lock all achievements?\nThis will uncheck all achievements in the list." },
            { "Confirm", "Confirm" },
        };

        private static readonly Dictionary<string, string> Ru = new()
        {
            // UI controls
            { "CommitChanges", "Сохранить изменения" },
            { "Refresh", "Обновить" },
            { "Reset", "Сброс" },
            { "Achievements", "Достижения" },
            { "Statistics", "Статистика" },
            { "Name", "Название" },
            { "Description", "Описание" },
            { "UnlockTime", "Время разблокировки" },
            { "GlobalPercent", "Глобальный %" },
            { "ShowOnly", "Показать только" },
            { "Locked", "закрытые" },
            { "Unlocked", "открытые" },
            { "HideProtected", "скрыть защищённые" },
            { "Filter", "Фильтр" },
            { "LockAll", "Закрыть все" },
            { "InvertAll", "Инвертировать все" },
            { "UnlockAll", "Открыть все" },
            { "DownloadStatus", "Статус загрузки" },
            { "Value", "Значение" },
            { "Extra", "Дополнительно" },

            // Messages
            { "DownloadingIcons", "Загрузка {0} иконок..." },
            { "ErrorRetrievingStats", "Ошибка при получении статистики: {0}" },
            { "FailedLoadSchema", "Не удалось загрузить схему." },
            { "ErrorAchievementsRetrieval", "Ошибка при получении достижений." },
            { "ErrorStatsRetrieval", "Ошибка при получении статистики." },
            { "RetrievedStats", "Получено {0} достижений и {1} статистик." },
            { "RetrievingStats", "Получение статистики..." },
            { "ErrorSettingState", "Ошибка при установке состояния для {0}, сохранение прервано." },
            { "ErrorSettingValue", "Ошибка при установке значения для {0}, сохранение прервано." },
            { "ErrorStoring", "Ошибка при сохранении, операция прервана." },
            { "StoredStats", "Сохранено {0} достижений и {1} статистик." },
            { "ConfirmResetStats", "Вы уверены, что хотите сбросить статистику?" },
            { "ConfirmResetAchievements", "Сбросить достижения тоже?" },
            { "ReallyReallySure", "Вы точно уверены?" },
            { "ProtectedAchievement", "Это защищённое достижение, им нельзя управлять через SAM." },
            { "ProtectedAchievements", "защищённых" },
            { "ProtectedTooltip", "\u26A0 Защищённое — серверное достижение, нельзя изменить локально" },
            { "AllProtectedWarning", "Все достижения в этой игре имеют серверную валидацию и не могут быть изменены через SAM.\n\nИгра использует серверную проверку достижений — изменения будут отклонены серверами Steam." },
            { "StatProtected", "Статистика защищена — изменение невозможно" },
            { "InvalidValue", "Недопустимое значение" },
            { "StatsEditingAgreement", "Я понимаю, что изменяя значения статистик, я могу всё испортить и виноват буду только сам." },
            { "GenericError", "Общая ошибка — обычно означает, что игра не куплена" },

            // MessageBox titles
            { "Error", "Ошибка" },
            { "Warning", "Предупреждение" },
            { "Question", "Вопрос" },
            { "Information", "Информация" },

            // VAC protection
            { "VacDetected", "VAC / Античит защита" },
            { "VacWarningText", "⚠ VAC / Античит защита — Изменение достижений или статистики этой игры может привести к VAC-бану или ограничениям аккаунта." },
            { "VacBlockedEdit", "Эта игра использует VAC / античит защиту.\nИзменение достижений и статистики заблокировано для защиты вашего аккаунта.\n\nНажмите «Я принимаю риски» чтобы разблокировать на свой страх и риск." },
            { "VacStoreWarning", "Эта игра использует VAC / античит защиту!\n\nСохранение изменений может привести к:\n• VAC-бану (перманентный)\n• Бану в игре\n• Ограничению онлайн-функций\n\nВы абсолютно уверены, что хотите продолжить?" },
            { "VacOverrideConfirm", "ВНИМАНИЕ: Вы собираетесь включить редактирование для VAC-защищённой игры.\n\nЭто может привести к:\n• Перманентному VAC-бану аккаунта\n• Потере доступа к онлайн-мультиплееру\n• Банам в конкретных играх\n\nРазработчики НЕ несут ответственности за последствия.\n\nПродолжить?" },
            { "VacOverrideDone", "Разблокировано" },
            { "VacOverrideBtn", "Я принимаю риски" },
            { "VacWarningOverridden", "⚠ VAC-защита обойдена — Вы редактируете на свой страх и риск. Вся ответственность лежит на вас." },
            { "VacUnlockAllWarning", "Эта игра использует VAC / античит защиту!\n\nРазблокировка всех достижений может привести к:\n• Перманентному VAC-бану\n• Бану в игре\n• Потере онлайн-функций\n\nДа — Разблокировать все (на свой риск)\nНет — Отмена\n\nРазблокировать все достижения?" },
            { "VacActionWarning", "Эта игра использует VAC / античит защиту.\nИзменение достижений или статистики может привести к бану.\n\nВы уверены, что хотите продолжить?" },
            { "VacClickWarning", "Эта игра использует VAC / античит защиту.\n\nИзменение достижений может привести к VAC-бану или ограничениям аккаунта.\nЭто предупреждение появится только один раз за сессию.\n\nХотите редактировать достижения этой игры?" },
            { "LockAllConfirm", "Вы уверены, что хотите заблокировать все достижения?\nЭто снимет отметку со всех достижений в списке." },
            { "Confirm", "Подтверждение" },
        };

        public static string Get(string key)
        {
            var dict = _current == Language.Russian ? Ru : En;
            return dict.TryGetValue(key, out var val) ? val : (En.TryGetValue(key, out val) ? val : key);
        }

        public static string Get(string key, params object[] args)
        {
            return string.Format(Get(key), args);
        }
    }
}
