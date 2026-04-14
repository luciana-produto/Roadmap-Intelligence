type LogLevel = 'debug' | 'info' | 'warn' | 'error'

interface LogEntry {
  timestamp: string
  level: LogLevel
  context: string
  message: string
  correlationId: string
  data?: unknown
}

const isDev = import.meta.dev

const formatEntry = (entry: LogEntry): string =>
  `[${entry.timestamp}] [${entry.level.toUpperCase()}] [${entry.context}] [cid:${entry.correlationId}] ${entry.message}`

export const useLogger = (context: string) => {
  const { get: getCorrelationId } = useCorrelationId()

  const buildEntry = (level: LogLevel, message: string, data?: unknown): LogEntry => ({
    timestamp: new Date().toISOString(),
    level,
    context,
    message,
    correlationId: getCorrelationId(),
    data
  })

  const log = (level: LogLevel, message: string, data?: unknown) => {
    const entry = buildEntry(level, message, data)
    const formatted = formatEntry(entry)

    switch (level) {
      case 'debug':
        if (isDev) console.debug(formatted, data ?? '')
        break
      case 'info':
        if (isDev) console.info(formatted, data ?? '')
        break
      case 'warn':
        console.warn(formatted, data ?? '')
        break
      case 'error':
        console.error(formatted, data ?? '')
        break
    }
  }

  return {
    debug: (message: string, data?: unknown) => log('debug', message, data),
    info: (message: string, data?: unknown) => log('info', message, data),
    warn: (message: string, data?: unknown) => log('warn', message, data),
    error: (message: string, error?: unknown) => log('error', message, error),
    getCorrelationId
  }
}
