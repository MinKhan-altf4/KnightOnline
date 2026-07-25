namespace KnightOnline.Client.Data.Models
{
    public sealed class ClientAuthenticationSettings
    {
        public bool DevelopmentBypassEnabled { get; }
        public float InitialSessionCheckSeconds { get; }
        public float SessionConflictRetrySeconds { get; }
        public string RegistrationUrl { get; }

        public ClientAuthenticationSettings(
            bool developmentBypassEnabled,
            float initialSessionCheckSeconds,
            float sessionConflictRetrySeconds,
            string registrationUrl)
        {
            DevelopmentBypassEnabled = developmentBypassEnabled;
            InitialSessionCheckSeconds = initialSessionCheckSeconds;
            SessionConflictRetrySeconds = sessionConflictRetrySeconds;
            RegistrationUrl = registrationUrl;
        }
    }
}
