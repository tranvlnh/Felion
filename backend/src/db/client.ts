import { PrismaPg } from '@prisma/adapter-pg';
import { Prisma, PrismaClient } from '#app/generated/prisma/client.js';
import { AppError } from '#app/errors.js';

export type Db = PrismaClient;
export type Tx = Prisma.TransactionClient;

export function createPrismaClient(databaseUrl: string): Db {
  return new PrismaClient({ adapter: new PrismaPg({ connectionString: databaseUrl }) });
}

// Prisma inputs reject explicit `undefined` under exactOptionalPropertyTypes; zod produces it for omitted keys.
export function definedOnly<T extends object>(value: T): { [K in keyof T]?: Exclude<T[K], undefined> } {
  return Object.fromEntries(Object.entries(value).filter(([, entry]) => entry !== undefined)) as {
    [K in keyof T]?: Exclude<T[K], undefined>;
  };
}

export function rethrowUniqueViolation(error: unknown, message: string): never {
  if (error instanceof Prisma.PrismaClientKnownRequestError && error.code === 'P2002') {
    throw new AppError('CONFLICT', message, { cause: error });
  }
  throw error;
}
