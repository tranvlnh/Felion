import { DiscordAPIError } from 'discord.js';

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

// Only messages we wrote or Discord's own API errors are shown; anything else may leak internals.
export function getPublicErrorMessage(error: unknown, fallback: string): string {
  if (error instanceof AppError) {
    return error.publicMessage;
  }
  if (error instanceof DiscordAPIError) {
    return `Discord: ${error.message}`;
  }
  return fallback;
}
