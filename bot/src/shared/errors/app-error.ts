export type AppErrorCode =
  | 'FORBIDDEN'
  | 'NOT_FOUND'
  | 'CONFLICT'
  | 'VALIDATION'
  | 'EXTERNAL_SERVICE';

export class AppError extends Error {
  readonly code: AppErrorCode;
  readonly publicMessage: string;

  constructor(code: AppErrorCode, publicMessage: string, options?: ErrorOptions) {
    super(publicMessage, options);
    this.name = 'AppError';
    this.code = code;
    this.publicMessage = publicMessage;
  }
}

export function getPublicErrorMessage(error: unknown, fallback: string): string {
  if (error instanceof AppError) {
    return error.publicMessage;
  }

  // Existing workflows intentionally throw user-facing Error messages. Keep that
  // compatibility while they are migrated incrementally to AppError.
  return error instanceof Error ? error.message : fallback;
}
