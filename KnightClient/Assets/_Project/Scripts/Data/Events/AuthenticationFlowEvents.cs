using System;
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

    public readonly struct AuthenticationEntryRequiredEvent : IStickyGameEvent
    {
        public readonly bool IsVisible;
        public readonly string Message;
        public readonly bool CanContinue;
        public readonly string AccountDisplayHint;

        public AuthenticationEntryRequiredEvent(
            string message,
            bool canContinue = false,
            string accountDisplayHint = "",
            bool isVisible = true)
        {
            IsVisible = isVisible;
            Message = message;
            CanContinue = canContinue;
            AccountDisplayHint = accountDisplayHint;
        }
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

    public readonly struct AccountSessionLeaveResultEvent : IGameEvent
    {
        public readonly bool Success;
        public readonly string Message;

        public AccountSessionLeaveResultEvent(bool success, string message)
        {
            Success = success;
            Message = message;
        }
    }

    public readonly struct AuthenticationPopupRequestedEvent : IGameEvent
    {
        public readonly string Message;
        public readonly bool ReconnectOnClose;

        public AuthenticationPopupRequestedEvent(
            string message,
            bool reconnectOnClose = false)
        {
            Message = message;
            ReconnectOnClose = reconnectOnClose;
        }
    }

    public readonly struct AuthenticationPopupDismissedEvent : IGameEvent
    {
        public readonly bool ShouldReconnect;

        public AuthenticationPopupDismissedEvent(bool shouldReconnect) =>
            ShouldReconnect = shouldReconnect;
    }

    public readonly struct RegistrationStartedEvent : IGameEvent
    {
        public readonly bool Success;
        public readonly string Message;
        public readonly Guid TransactionId;
        public readonly string RegistrationUrl;
        public readonly string DevelopmentAuthorizationCode;
        public readonly DateTime ExpiresAtUtc;

        public RegistrationStartedEvent(
            bool success,
            string message,
            Guid transactionId,
            string registrationUrl,
            string developmentAuthorizationCode,
            DateTime expiresAtUtc)
        {
            Success = success;
            Message = message;
            TransactionId = transactionId;
            RegistrationUrl = registrationUrl;
            DevelopmentAuthorizationCode =
                developmentAuthorizationCode;
            ExpiresAtUtc = expiresAtUtc;
        }
    }
}
