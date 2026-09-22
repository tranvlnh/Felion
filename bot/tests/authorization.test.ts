import { describe, expect, it } from 'vitest';
import { isLinkedAdmin } from '../src/db/authorization.js';
import type { Database } from '../src/db/client.js';

function createAuthorizationDatabase(rows: Array<{ memberId: string }>): NonNullable<Database> {
  return {
    db: {
      select: () => ({
        from: () => ({
          innerJoin: () => ({
            where: () => ({
              limit: async () => rows,
            }),
          }),
        }),
      }),
    },
  } as unknown as NonNullable<Database>;
}

describe('Admin authorization', () => {
  it('accepts a linked active Admin query result', async () => {
    await expect(isLinkedAdmin(createAuthorizationDatabase([{ memberId: 'member-id' }]), '123'))
      .resolves.toBe(true);
  });

  it('rejects when no linked active Admin is found', async () => {
    await expect(isLinkedAdmin(createAuthorizationDatabase([]), '123')).resolves.toBe(false);
  });
});
