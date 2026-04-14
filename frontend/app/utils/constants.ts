export const AppConstants = {
  CORRELATION_ID_HEADER: 'X-Correlation-ID',
  SLOW_REQUEST_THRESHOLD_MS: 15_000,
  MESSAGES: {
    SLOW_REQUEST: 'Esta requisição está demorando mais que o tempo usual, aguarde um momento...',
    GENERIC_ERROR: 'Ocorreu um erro inesperado. Por favor, tente novamente ou entre em contato com o suporte informando o ID de rastreamento.',
    NOT_FOUND: 'O recurso solicitado não foi encontrado.',
    VALIDATION_ERROR: 'Os dados informados contêm inconsistências. Verifique e tente novamente.'
  }
} as const
