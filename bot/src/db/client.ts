import postgres from 'postgres';
import { drizzle } from 'drizzle-orm/postgres-js';
import type { Config } from '../config.js';
import * as schema from './schema.js';

export function createDatabase(config: Config) {
  const client = postgres(config.DATABASE_URL, { max: 5 });
  return {
    client,
    db: drizzle(client, { schema }),
  };
}

export type Database = ReturnType<typeof createDatabase>;
