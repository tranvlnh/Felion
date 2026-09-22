import 'dotenv/config';
import { resolve } from 'node:path';
import postgres from 'postgres';
import { drizzle } from 'drizzle-orm/postgres-js';
import { migrate } from 'drizzle-orm/postgres-js/migrator';
import { z } from 'zod';

const databaseUrl = z.string().url().parse(process.env.DATABASE_URL);
const client = postgres(databaseUrl, { max: 1 });

try {
  await migrate(drizzle(client), { migrationsFolder: resolve('drizzle') });
  console.log('Database migrations applied.');
} finally {
  await client.end();
}
