export interface ApiResponse<T> {
  data: T
  success: boolean
  correlationId?: string
  message?: string
  errors?: Record<string, string[]>
}

export interface PaginatedResult<T> {
  items: T[]
  totalCount: number
  pageNumber: number
  pageSize: number
  totalPages: number
}

export interface ProblemDetails {
  title: string
  detail: string
  status: number
  correlationId?: string
  errors?: Record<string, string[]>
}
