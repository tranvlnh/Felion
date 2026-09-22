import { describe, expect, it } from 'vitest';
import type { Database } from '../src/db/client.js';
import {
  deactivateDepartment,
  editGeneration,
} from '../src/db/reference-management.js';

type RecordedMutation = Record<string, unknown>;

function createWorkflowDatabase(row: Record<string, unknown>) {
  const audits: RecordedMutation[] = [];
  const updates: RecordedMutation[] = [];

  const transaction = {
    select: () => ({
      from: () => ({
        where: () => ({
          limit: async () => [row],
        }),
      }),
    }),
    update: () => ({
      set: (values: RecordedMutation) => {
        updates.push(values);
        return {
          where: () => ({
            returning: async () => [{ id: row.id }],
          }),
        };
      },
    }),
    insert: () => ({
      values: async (values: RecordedMutation) => {
        audits.push(values);
      },
    }),
  };

  const database = {
    db: {
      transaction: async <T>(callback: (value: typeof transaction) => Promise<T>) => callback(transaction),
    },
  } as unknown as NonNullable<Database>;

  return { database, audits, updates };
}

describe('reference management workflows', () => {
  it('deactivates a Department and records the privileged mutation', async () => {
    const departmentId = '550e8400-e29b-41d4-a716-446655440000';
    const { database, audits, updates } = createWorkflowDatabase({
      id: departmentId,
      name: 'Event',
      slug: 'event',
      active: true,
    });

    await deactivateDepartment(database, { departmentId, actorDiscordUserId: '123' });

    expect(updates).toEqual([expect.objectContaining({ active: false })]);
    expect(audits).toEqual([expect.objectContaining({
      actorDiscordUserId: '123',
      action: 'DepartmentDeactivated',
      entityType: 'Department',
      entityId: departmentId,
    })]);
  });

  it('does not mutate or audit the reserved Core Department', async () => {
    const departmentId = '550e8400-e29b-41d4-a716-446655440000';
    const { database, audits, updates } = createWorkflowDatabase({
      id: departmentId,
      name: 'Core',
      slug: 'core',
      active: true,
    });

    await expect(deactivateDepartment(database, { departmentId, actorDiscordUserId: '123' }))
      .rejects.toThrow('reserved');
    expect(updates).toHaveLength(0);
    expect(audits).toHaveLength(0);
  });

  it('edits a Generation and audits its previous and current names', async () => {
    const generationId = '550e8400-e29b-41d4-a716-446655440000';
    const { database, audits, updates } = createWorkflowDatabase({
      id: generationId,
      name: 'Gen 11',
      active: true,
    });

    await editGeneration(database, {
      generationId,
      name: ' Gen 12 ',
      actorDiscordUserId: '456',
    });

    expect(updates).toEqual([{ name: 'Gen 12' }]);
    expect(audits).toEqual([expect.objectContaining({
      action: 'GenerationEdited',
      entityId: generationId,
      metadata: { previous: { name: 'Gen 11' }, current: { name: 'Gen 12' } },
    })]);
  });
});
