using KnightOnline.Client.Core.Events;

namespace KnightOnline.Client.Data.Events
{
    public readonly struct AccountReadyEvent : IGameEvent
    {
        public readonly string AccountKey;
        public readonly bool IsGuest;

        public AccountReadyEvent(string accountKey, bool isGuest)
        {
            AccountKey = accountKey;
            IsGuest = isGuest;
        }
    }

    public readonly struct AuthenticationEntryRequiredEvent : IGameEvent
    {
        public readonly string Message;
        public AuthenticationEntryRequiredEvent(string message) =>
            Message = message;
    }

    public readonly struct AuthenticationLoadingEvent : IGameEvent
    {
        public readonly bool IsVisible;
        public readonly float RemainingSeconds;
        public readonly string Message;

        public AuthenticationLoadingEvent(
            bool isVisible,
            float remainingSeconds,
            string message)
        {
            IsVisible = isVisible;
            RemainingSeconds = remainingSeconds;
            Message = message;
        }
    }
}
