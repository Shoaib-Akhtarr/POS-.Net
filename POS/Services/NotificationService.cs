using System;
using System.Threading;
using System.Windows.Threading;

namespace POS.Services
{
    public enum NotificationType
    {
        Success,
        Warning,
        Error,
        Info
    }

    public class NotificationItem
    {
        public string Message { get; set; } = string.Empty;
        public NotificationType Type { get; set; } = NotificationType.Info;
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public int DurationSeconds { get; set; } = 3;
    }

    public static class NotificationService
    {
        public static event Action<NotificationItem>? NotificationRequested;

        public static void Show(string message, NotificationType type = NotificationType.Info, int durationSeconds = 3)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            var item = new NotificationItem
            {
                Message = message,
                Type = type,
                DurationSeconds = durationSeconds
            };

            NotificationRequested?.Invoke(item);
        }

        public static void ShowSuccess(string message) => Show(message, NotificationType.Success, 3);
        public static void ShowWarning(string message) => Show(message, NotificationType.Warning, 4);
        public static void ShowError(string message) => Show(message, NotificationType.Error, 5);
        public static void ShowInfo(string message) => Show(message, NotificationType.Info, 3);
    }
}
