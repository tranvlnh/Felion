import { evaluationCommandDefinitions } from '#app/features/evaluations/discord/commands.js';
import { identityCommandDefinitions } from '#app/features/identity/discord/commands.js';
import { memberCommandDefinitions } from '#app/features/members/discord/commands.js';
import {
  probationCandidateCommandDefinition,
  probationTeamCommandDefinition,
} from '#app/features/probation/discord/commands.js';
import { referenceDataCommandDefinitions } from '#app/features/reference-data/discord/commands.js';
import { roleCommandDefinitions } from '#app/features/roles/discord/commands.js';
import { systemCommandDefinitions } from '#app/features/system/discord/commands.js';

export const commandDefinitions = [
  ...systemCommandDefinitions,
  ...identityCommandDefinitions,
  ...memberCommandDefinitions,
  probationCandidateCommandDefinition,
  ...referenceDataCommandDefinitions,
  probationTeamCommandDefinition,
  ...roleCommandDefinitions,
  ...evaluationCommandDefinitions,
].map((command) => command.toJSON());
