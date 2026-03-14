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
            { "ShowOnly", "Show only" },
            { "Locked", "locked" },
            { "Unlocked", "unlocked" },
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
            { "ProtectedAchievement", "Sorry, but this is a protected achievement and cannot be managed with Steam Achievement Manager." },
            { "StatProtected", "Stat is protected! -- you can't modify it" },
            { "InvalidValue", "Invalid value" },
            { "StatsEditingAgreement", "I understand by modifying the values of stats, I may screw things up and can't blame anyone but myself." },
            { "GenericError", "generic error -- this usually means you don't own the game" },

            // MessageBox titles
            { "Error", "Error" },
            { "Warning", "Warning" },
            { "Question", "Question" },
            { "Information", "Information" },
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
            { "ShowOnly", "Показать только" },
            { "Locked", "закрытые" },
            { "Unlocked", "открытые" },
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
            { "StatProtected", "Статистика защищена — изменение невозможно" },
            { "InvalidValue", "Недопустимое значение" },
            { "StatsEditingAgreement", "Я понимаю, что изменяя значения статистик, я могу всё испортить и виноват буду только сам." },
            { "GenericError", "Общая ошибка — обычно означает, что игра не куплена" },

            // MessageBox titles
            { "Error", "Ошибка" },
            { "Warning", "Предупреждение" },
            { "Question", "Вопрос" },
            { "Information", "Информация" },
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
