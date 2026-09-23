import { SlashCommandBuilder } from 'discord.js';

export const evaluationCommandDefinitions = [
  new SlashCommandBuilder()
    .setName('evaluation-criteria')
    .setDescription('Manage configurable score criteria. The note field is always fixed.')
    .addSubcommand((command) => command
      .setName('add')
      .setDescription('Add or reactivate a score criterion.')
      .addStringOption((option) => option
        .setName('kind')
        .setDescription('Evaluation kind')
        .setRequired(true)
        .addChoices({ name: 'Peer', value: 'Peer' }, { name: 'Mentor', value: 'Mentor' }))
      .addStringOption((option) => option.setName('name').setDescription('Criterion name').setRequired(true)))
    .addSubcommand((command) => command
      .setName('rename')
      .setDescription('Rename an active criterion without changing its identity.')
      .addStringOption((option) => option.setName('criterion-id').setDescription('Criterion UUID').setRequired(true))
      .addStringOption((option) => option.setName('name').setDescription('New criterion name').setRequired(true)))
    .addSubcommand((command) => command
      .setName('remove')
      .setDescription('Deactivate a score criterion while preserving history.')
      .addStringOption((option) => option.setName('criterion-id').setDescription('Criterion UUID').setRequired(true))),
  new SlashCommandBuilder()
    .setName('evaluation-period')
    .setDescription('Manage evaluation periods.')
    .addSubcommand((command) => command
      .setName('open')
      .setDescription('Open a new evaluation period.')
      .addStringOption((option) => option.setName('name').setDescription('Evaluation period name').setRequired(true)))
    .addSubcommand((command) => command
      .setName('close')
      .setDescription('Close an open evaluation period.')
      .addStringOption((option) => option.setName('period-id').setDescription('Evaluation period UUID').setRequired(true))),
  new SlashCommandBuilder()
    .setName('evaluation')
    .setDescription('Submit a probation evaluation.')
    .addSubcommand((command) => command
      .setName('peer')
      .setDescription('Evaluate another candidate in your team.')
      .addStringOption((option) => option.setName('period-id').setDescription('Open evaluation period UUID').setRequired(true))
      .addStringOption((option) => option.setName('target-candidate-id').setDescription('Target ProbationCandidate UUID').setRequired(true)))
    .addSubcommand((command) => command
      .setName('mentor')
      .setDescription('Evaluate a candidate in a team you mentor.')
      .addStringOption((option) => option.setName('period-id').setDescription('Open evaluation period UUID').setRequired(true))
      .addStringOption((option) => option.setName('target-candidate-id').setDescription('Target ProbationCandidate UUID').setRequired(true))),
  new SlashCommandBuilder()
    .setName('evaluation-report')
    .setDescription('Read raw evaluation results as Core or Admin.')
    .addSubcommand((command) => command
      .setName('view')
      .setDescription('View raw evaluations for one candidate in a period.')
      .addStringOption((option) => option
        .setName('period-id')
        .setDescription('Evaluation period UUID')
        .setRequired(true))
      .addStringOption((option) => option
        .setName('candidate-id')
        .setDescription('Target ProbationCandidate UUID')
        .setRequired(true))
      .addStringOption((option) => option
        .setName('kind')
        .setDescription('Optional evaluation kind filter')
        .addChoices({ name: 'Peer', value: 'Peer' }, { name: 'Mentor', value: 'Mentor' })))
    .addSubcommand((command) => command
      .setName('export')
      .setDescription('Export every raw evaluation in a period as CSV.')
      .addStringOption((option) => option
        .setName('period-id')
        .setDescription('Evaluation period UUID')
        .setRequired(true))),
];
