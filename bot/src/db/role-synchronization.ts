import { and, eq } from 'drizzle-orm';
import {
  resolveDiscordRoleState,
  type DiscordRoleState,
  type DiscordRoleSubject,
} from '#app/domain/discord-roles.js';
import type { Database } from './client.js';
import {
  auditLogs,
  discordIdentityLinks,
  discordRoleAssignments,
  discordRoleMappings,
  members,
  probationCandidates,
} from './schema.js';

export type DiscordRoleSyncTarget = DiscordRoleState & {
  discordUserId: string;
  subjectType: 'Member' | 'ProbationCandidate';
  subjectId: string;
  explicitRoleIds: string[];
};

export type DiscordRoleSyncAuditInput = {
  target: DiscordRoleSyncTarget;
  actorDiscordUserId: string;
  succeeded: boolean;
  addedRoleIds: readonly string[];
  removedRoleIds: readonly string[];
  error?: string;
};

export async function loadDiscordRoleSyncTarget(
  database: Database,
  discordUserId: string,
): Promise<DiscordRoleSyncTarget> {
  return database.db.transaction(async (transaction) => {
    const link = (await transaction
      .select({
        subjectType: discordIdentityLinks.subjectType,
        subjectId: discordIdentityLinks.subjectId,
      })
      .from(discordIdentityLinks)
      .where(eq(discordIdentityLinks.discordUserId, discordUserId))
      .limit(1))[0];

    if (!link) {
      throw new Error('This Discord user is not linked to an active Felion identity.');
    }

    let subject: DiscordRoleSubject;
    if (link.subjectType === 'Member') {
      const member = (await transaction
        .select({
          position: members.position,
          departmentId: members.departmentId,
          generationId: members.generationId,
        })
        .from(members)
        .where(and(eq(members.id, link.subjectId), eq(members.status, 'Active')))
        .limit(1))[0];

      if (!member) {
        throw new Error('The linked Member is not active.');
      }
      subject = { subjectType: 'Member', ...member };
    } else {
      const candidate = (await transaction
        .select({
          status: probationCandidates.status,
          departmentId: probationCandidates.departmentId,
          generationId: probationCandidates.generationId,
          teamId: probationCandidates.teamId,
        })
        .from(probationCandidates)
        .where(eq(probationCandidates.id, link.subjectId))
        .limit(1))[0];

      if (!candidate) {
        throw new Error('The linked ProbationCandidate was not found.');
      }
      if (candidate.status === 'Passed' || candidate.status === 'Failed') {
        throw new Error('The linked ProbationCandidate has a final decision.');
      }
      subject = {
        subjectType: 'ProbationCandidate',
        active: candidate.status === 'Active',
        departmentId: candidate.departmentId,
        generationId: candidate.generationId,
        teamId: candidate.teamId,
      };
    }

    const mappings = await transaction
      .select({
        kind: discordRoleMappings.kind,
        key: discordRoleMappings.key,
        discordRoleId: discordRoleMappings.discordRoleId,
      })
      .from(discordRoleMappings);

    const assignments = await transaction
      .select({ discordRoleId: discordRoleAssignments.discordRoleId })
      .from(discordRoleAssignments)
      .where(and(
        eq(discordRoleAssignments.subjectType, link.subjectType),
        eq(discordRoleAssignments.subjectId, link.subjectId),
      ));

    const state = resolveDiscordRoleState(
      subject,
      mappings,
      assignments.map((assignment) => assignment.discordRoleId),
    );

    return {
      discordUserId,
      subjectType: link.subjectType,
      subjectId: link.subjectId,
      explicitRoleIds: assignments.map((assignment) => assignment.discordRoleId),
      ...state,
    };
  });
}

export async function recordDiscordRoleSync(
  database: Database,
  input: DiscordRoleSyncAuditInput,
): Promise<void> {
  await database.db.insert(auditLogs).values({
    actorDiscordUserId: input.actorDiscordUserId,
    action: input.succeeded ? 'DiscordRolesSynchronized' : 'DiscordRoleSynchronizationFailed',
    entityType: input.target.subjectType,
    entityId: input.target.subjectId,
    metadata: {
      discordUserId: input.target.discordUserId,
      desiredRoleIds: input.target.desiredRoleIds,
      addedRoleIds: [...input.addedRoleIds],
      removedRoleIds: [...input.removedRoleIds],
      ...(input.error ? { error: input.error } : {}),
    },
  });
}
