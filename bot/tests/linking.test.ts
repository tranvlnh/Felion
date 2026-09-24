import { describe, expect, it } from 'vitest';
import type { Database } from '#app/db/client.js';
import { linkDiscordIdentity } from '#app/db/linking.js';
import {
  discordIdentityLinks,
  identityRegistry,
  members,
  probationCandidates,
} from '#app/db/schema.js';

describe('Discord identity linking lifecycle guard', () => {
  it('does not link an inactive ProbationCandidate retained in the identity registry', async () => {
    const inserts: Record<string, unknown>[] = [];
    const transaction = {
      select: () => ({
        from: (table: unknown) => ({
          where: () => ({
            limit: async () => {
              if (table === discordIdentityLinks) {
                return [];
              }
              if (table === identityRegistry) {
                return [{
                  subjectType: 'ProbationCandidate',
                  subjectId: '550e8400-e29b-41d4-a716-446655440000',
                }];
              }
              if (table === probationCandidates) {
                return [];
              }
              throw new Error('Unexpected select target.');
            },
          }),
        }),
      }),
      insert: () => ({
        values: async (value: Record<string, unknown>) => {
          inserts.push(value);
        },
      }),
    };
    const database = {
      db: {
        transaction: async <T>(callback: (value: typeof transaction) => Promise<T>) => callback(transaction),
      },
    } as unknown as Database;

    await expect(linkDiscordIdentity(database, 'discord-user', 'sv-001'))
      .rejects.toThrow('No active Member or ProbationCandidate');
    expect(inserts).toHaveLength(0);
  });

  it('allows the same linked account to submit its StudentId again', async () => {
    const inserts: Record<string, unknown>[] = [];
    const transaction = {
      select: () => ({
        from: (table: unknown) => ({
          where: () => ({
            limit: async () => {
              if (table === identityRegistry) {
                return [{ subjectType: 'Member', subjectId: 'member-id' }];
              }
              if (table === members) {
                return [{ id: 'member-id' }];
              }
              if (table === discordIdentityLinks) {
                return [{ subjectType: 'Member', subjectId: 'member-id' }];
              }
              throw new Error('Unexpected select target.');
            },
          }),
        }),
      }),
      insert: () => ({
        values: async (value: Record<string, unknown>) => {
          inserts.push(value);
        },
      }),
    };
    const database = {
      db: {
        transaction: async <T>(callback: (value: typeof transaction) => Promise<T>) => callback(transaction),
      },
    } as unknown as Database;

    await expect(linkDiscordIdentity(database, 'discord-user', 'sv-001'))
      .resolves.toBe('AlreadyLinked');
    expect(inserts).toHaveLength(0);
  });
});
