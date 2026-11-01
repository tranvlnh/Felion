// Grants web admin to a person, creating them first on an empty database.
// Usage: npx tsx scripts/bootstrap-admin.ts <club-email>
//        npx tsx scripts/bootstrap-admin.ts <club-email> --create --student-id X --name "Full Name" --department "Ban" --generation 1
import { parseArgs } from 'node:util';
import { configSchema } from '../src/config.js';
import { createPrismaClient } from '../src/db/client.js';
import { normalizeClubEmail, normalizeFullName, normalizeStudentId } from '../src/domain/member.js';

const { DATABASE_URL } = configSchema.pick({ DATABASE_URL: true }).parse(process.env);
const { positionals, values } = parseArgs({
  allowPositionals: true,
  options: {
    create: { type: 'boolean', default: false },
    'student-id': { type: 'string' },
    name: { type: 'string' },
    department: { type: 'string' },
    generation: { type: 'string' },
  },
});

async function main(): Promise<void> {
  const email = normalizeClubEmail(positionals[0] ?? '');
  const prisma = createPrismaClient(DATABASE_URL);

  const existing = await prisma.person.findUnique({ where: { clubEmail: email } });
  if (existing) {
    await prisma.person.update({ where: { id: existing.id }, data: { isAdmin: true, status: 'Active' } });
    console.log(`${existing.fullName} is now an admin.`);
  } else if (values.create) {
    const departmentName = values.department?.trim() || 'General';
    const department = await prisma.department.upsert({ where: { name: departmentName }, create: { name: departmentName }, update: {} });
    const person = await prisma.person.create({
      data: {
        kind: 'Member',
        studentId: normalizeStudentId(values['student-id'] ?? ''),
        fullName: normalizeFullName(values.name ?? ''),
        clubEmail: email,
        isAdmin: true,
        departmentId: department.id,
        generation: Number(values.generation ?? '1'),
      },
    });
    console.log(`Created admin ${person.fullName}.`);
  } else {
    console.error(`No person with club email ${email}. Run the legacy migration first or pass --create with details.`);
    process.exitCode = 1;
  }
  await prisma.$disconnect();
}

main().catch((error: unknown) => {
  console.error(error);
  process.exitCode = 1;
});
