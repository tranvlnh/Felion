import type { Guild } from 'discord.js';
import { describe, expect, it, vi } from 'vitest';
import type { Database } from '#app/db/client.js';
import { recordDiscordRoleSync, type DiscordRoleSyncTarget } from '#app/db/role-synchronization.js';
import { applyDiscordRoleSync } from '#app/discord/role-synchronization.js';

function createAuditDatabase(): { database: Database; audits: Record<string, unknown>[] } {
  const audits: Record<string, unknown>[] = [];
  const database = {
    db: {
      insert: () => ({
        values: async (value: Record<string, unknown>) => {
          audits.push(value);
        },
      }),
    },
  } as unknown as Database;

  return { database, audits };
}

const target: DiscordRoleSyncTarget = {
  discordUserId: '123',
  subjectType: 'Member',
  subjectId: '550e8400-e29b-41d4-a716-446655440000',
  explicitRoleIds: [],
  desiredRoleIds: ['10'],
  managedRoleIds: ['10', '20'],
};

describe('Discord role synchronization audit', () => {
  it('records successful role mutations', async () => {
    const { database, audits } = createAuditDatabase();

    await recordDiscordRoleSync(database, {
      target,
      actorDiscordUserId: '456',
      succeeded: true,
      addedRoleIds: ['10'],
      removedRoleIds: ['20'],
    });

    expect(audits).toEqual([expect.objectContaining({
      actorDiscordUserId: '456',
      action: 'DiscordRolesSynchronized',
      entityType: 'Member',
      entityId: target.subjectId,
      metadata: expect.objectContaining({ addedRoleIds: ['10'], removedRoleIds: ['20'] }),
    })]);
  });

  it('records failed synchronization outcomes', async () => {
    const { database, audits } = createAuditDatabase();

    await recordDiscordRoleSync(database, {
      target,
      actorDiscordUserId: '456',
      succeeded: false,
      addedRoleIds: [],
      removedRoleIds: [],
      error: 'Missing Permissions',
    });

    expect(audits[0]).toEqual(expect.objectContaining({
      action: 'DiscordRoleSynchronizationFailed',
      metadata: expect.objectContaining({ error: 'Missing Permissions' }),
    }));
  });
});

describe('Discord role synchronization adapter', () => {
  it('applies the planned additions and removals in batches', async () => {
    const add = vi.fn(async () => undefined);
    const remove = vi.fn(async () => undefined);
    const member = {
      roles: {
        cache: new Map([['20', {}], ['777', {}]]),
        add,
        remove,
      },
    };
    const roles = new Map([
      ['10', { id: '10', name: 'Desired', managed: false, editable: true }],
      ['20', { id: '20', name: 'Obsolete', managed: false, editable: true }],
    ]);
    const guild = {
      id: '999',
      members: { fetch: vi.fn(async () => member) },
      roles: { fetch: vi.fn(async () => roles) },
    } as unknown as Guild;

    const result = await applyDiscordRoleSync(guild, target);

    expect(add).toHaveBeenCalledWith(['10'], 'Felion role synchronization');
    expect(remove).toHaveBeenCalledWith(['20'], 'Felion role synchronization');
    expect(result).toEqual({
      rolesToAdd: ['10'],
      rolesToRemove: ['20'],
      addedRoleIds: ['10'],
      removedRoleIds: ['20'],
    });
  });

  it('rejects a configured role that the bot cannot edit before applying changes', async () => {
    const add = vi.fn(async () => undefined);
    const member = {
      roles: { cache: new Map(), add, remove: vi.fn(async () => undefined) },
    };
    const roles = new Map([
      ['10', { id: '10', name: 'Above Felion', managed: false, editable: false }],
    ]);
    const guild = {
      id: '999',
      members: { fetch: vi.fn(async () => member) },
      roles: { fetch: vi.fn(async () => roles) },
    } as unknown as Guild;

    await expect(applyDiscordRoleSync(guild, target)).rejects.toThrow('cannot be managed');
    expect(add).not.toHaveBeenCalled();
  });
});
