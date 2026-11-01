import 'dotenv/config';
import { defineConfig } from 'prisma/config';

export default defineConfig({
  schema: 'prisma/schema.prisma',
  migrations: { path: 'prisma/migrations' },
  // `prisma generate` runs in the Docker build without a database.
  datasource: { url: process.env.DATABASE_URL ?? '' },
});
