import { describe, expect, it } from 'vitest';
import {
  requireAdminActor,
  requireCandidateActor,
  requireCoreOrAdminActor,
  requireMentorActor,
} from '#app/db/authorization.js';
import type { Database } from '#app/db/client.js';
import { AppError } from '#app/shared/errors/app-error.js';

type MemberRow = {
  memberId: string;
  fullName: string;
  position: 'Admin' | 'Core' | 'Member';
};

type CandidateRow = {
  candidateId: string;
  fullName: string;
  teamId: string | null;
};

function createAuthorizationDatabase(input: {
  memberRows?: MemberRow[];
  candidateRows?: CandidateRow[];
  mentorTeamRows?: Array<{ teamId: string }>;
}): Database {
  const db = {
    select: (selection: Record<string, unknown>) => ({
      from: () => {
        let joinedIdentity = false;
        const query = {
          innerJoin: () => {
            joinedIdentity = true;
            return query;
          },
          where: () => {
            if ('teamId' in selection && !('candidateId' in selection)) {
              return Promise.resolve(input.mentorTeamRows ?? []);
            }
            if (!joinedIdentity) {
              return Promise.resolve(input.mentorTeamRows ?? []);
            }

            return {
              limit: async () => ('candidateId' in selection
                ? input.candidateRows ?? []
                : input.memberRows ?? []),
            };
          },
        };
        return query;
      },
    }),
  };

  return { db } as unknown as Database;
}

describe('typed actor authorization', () => {
  it('resolves a linked active Admin actor', async () => {
    const database = createAuthorizationDatabase({
      memberRows: [{ memberId: 'member-id', fullName: 'Admin User', position: 'Admin' }],
    });

    await expect(requireAdminActor(database, '123')).resolves.toEqual({
      discordUserId: '123',
      memberId: 'member-id',
      fullName: 'Admin User',
      position: 'Admin',
    });
  });

  it('allows Core through the Core-or-Admin boundary but not the Admin boundary', async () => {
    const database = createAuthorizationDatabase({
      memberRows: [{ memberId: 'member-id', fullName: 'Core User', position: 'Core' }],
    });

    await expect(requireCoreOrAdminActor(database, '123')).resolves.toMatchObject({
      memberId: 'member-id',
      position: 'Core',
    });
    await expect(requireAdminActor(database, '123')).rejects.toMatchObject({
      code: 'FORBIDDEN',
    });
  });

  it('rejects an unlinked or inactive member result with the requested public message', async () => {
    const promise = requireAdminActor(
      createAuthorizationDatabase({}),
      '123',
      'Custom denial.',
    );

    await expect(promise).rejects.toEqual(expect.objectContaining({
      name: 'AppError',
      code: 'FORBIDDEN',
      publicMessage: 'Custom denial.',
    }));
  });

  it('resolves a linked active candidate actor', async () => {
    const database = createAuthorizationDatabase({
      candidateRows: [{ candidateId: 'candidate-id', fullName: 'Candidate', teamId: 'team-id' }],
    });

    await expect(requireCandidateActor(database, '456')).resolves.toEqual({
      discordUserId: '456',
      candidateId: 'candidate-id',
      fullName: 'Candidate',
      teamId: 'team-id',
    });
  });

  it('resolves all teams for an active mentor', async () => {
    const database = createAuthorizationDatabase({
      memberRows: [{ memberId: 'mentor-id', fullName: 'Mentor', position: 'Member' }],
      mentorTeamRows: [{ teamId: 'team-a' }, { teamId: 'team-b' }],
    });

    await expect(requireMentorActor(database, '789')).resolves.toMatchObject({
      discordUserId: '789',
      memberId: 'mentor-id',
      teamIds: ['team-a', 'team-b'],
    });
  });

  it('rejects an active Member who is not assigned as a mentor', async () => {
    const database = createAuthorizationDatabase({
      memberRows: [{ memberId: 'member-id', fullName: 'Member', position: 'Member' }],
    });

    await expect(requireMentorActor(database, '789')).rejects.toBeInstanceOf(AppError);
  });
});
