namespace ProductHub.Shared.Constants;

public static class AppConstants
{
    public static class Http
    {
        public const string CorrelationIdHeader = "X-Correlation-ID";
        public const int SlowRequestThresholdSeconds = 15;
    }

    public static class Database
    {
        public const int RetryCount = 3;
        public const int RetryBaseDelaySeconds = 2;
        public const int CommandTimeoutSeconds = 30;
        public const int ConnectionTimeoutSeconds = 15;
    }

    public static class KeyVault
    {
        public const string SecretNameSeparator = "--";
    }

    public static class Logging
    {
        public const string CorrelationIdProperty = "CorrelationId";
        public const string RequestNameProperty = "RequestName";
        public const string ElapsedMsProperty = "ElapsedMs";
    }

    public static class UserMessages
    {
        public const string SlowRequest = "Esta requisição está demorando mais que o tempo usual, aguarde um momento...";
        public const string GenericError = "Ocorreu um erro inesperado. Por favor, tente novamente ou entre em contato com o suporte informando o ID de rastreamento.";
        public const string NotFound = "O recurso solicitado não foi encontrado.";
        public const string ValidationError = "Os dados informados contêm inconsistências. Verifique e tente novamente.";
        public const string Unauthorized = "Você não tem permissão para acessar este recurso.";
        public const string ServiceUnavailable = "Serviço temporariamente indisponível. Tente novamente em alguns instantes.";
    }
}
