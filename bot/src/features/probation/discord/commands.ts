import { SlashCommandBuilder } from 'discord.js';

export const probationCandidateCommandDefinition = new SlashCommandBuilder()
    .setName('probation-candidate')
    .setDescription('Manage ProbationCandidates.')
    .addSubcommand((command) => command
      .setName('create')
      .setDescription('Create an active ProbationCandidate without a team.')
      .addStringOption((option) => option.setName('student-id').setDescription('StudentId').setRequired(true))
      .addStringOption((option) => option.setName('full-name').setDescription('Full name').setRequired(true))
      .addStringOption((option) => option.setName('department').setDescription('Department name or slug').setRequired(true))
      .addStringOption((option) => option.setName('generation').setDescription('Generation name').setRequired(true)))
    .addSubcommand((command) => command
      .setName('assign-team')
      .setDescription('Assign or move an active candidate to an active team.')
      .addStringOption((option) => option.setName('candidate-id').setDescription('ProbationCandidate UUID').setRequired(true))
      .addStringOption((option) => option.setName('team-id').setDescription('Probation team UUID').setRequired(true)))
    .addSubcommand((command) => command
      .setName('deactivate')
      .setDescription('Deactivate an active candidate.')
      .addStringOption((option) => option.setName('candidate-id').setDescription('ProbationCandidate UUID').setRequired(true)))
    .addSubcommand((command) => command
      .setName('reactivate')
      .setDescription('Reactivate an inactive candidate.')
      .addStringOption((option) => option.setName('candidate-id').setDescription('ProbationCandidate UUID').setRequired(true)));

export const probationTeamCommandDefinition = new SlashCommandBuilder()
    .setName('probation-team')
    .setDescription('Manage probation teams.')
    .addSubcommand((command) => command
      .setName('create')
      .setDescription('Create a probation team.')
      .addStringOption((option) => option.setName('name').setDescription('Team name').setRequired(true)))
    .addSubcommand((command) => command
      .setName('edit')
      .setDescription('Rename an active probation team.')
      .addStringOption((option) => option.setName('team-id').setDescription('Probation team UUID').setRequired(true))
      .addStringOption((option) => option.setName('name').setDescription('New team name').setRequired(true)))
    .addSubcommand((command) => command
      .setName('deactivate')
      .setDescription('Deactivate a probation team for future assignments.')
      .addStringOption((option) => option.setName('team-id').setDescription('Probation team UUID').setRequired(true)))
    .addSubcommand((command) => command
      .setName('assign-mentor')
      .setDescription('Assign an active Member as a mentor for an active probation team.')
      .addStringOption((option) => option.setName('team-id').setDescription('Probation team UUID').setRequired(true))
      .addStringOption((option) => option.setName('member-id').setDescription('Member UUID').setRequired(true)));
