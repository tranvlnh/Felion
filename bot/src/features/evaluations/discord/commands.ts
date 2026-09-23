import { SlashCommandBuilder } from 'discord.js';

export const evaluationCommandDefinitions = [
  new SlashCommandBuilder()
    .setName('evaluation-criteria')
    .setDescription('Manage configurable score criteria. The note field is always fixed.')
    .addSubcommand((command) => command
      .setName('list')
      .setDescription('List evaluation criteria.'))
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
      .addStringOption((option) => option.setName('criterion').setDescription('Choose a criterion').setRequired(true).setAutocomplete(true))
      .addStringOption((option) => option.setName('name').setDescription('New criterion name').setRequired(true)))
    .addSubcommand((command) => command
      .setName('remove')
      .setDescription('Deactivate a score criterion while preserving history.')
      .addStringOption((option) => option.setName('criterion').setDescription('Choose a criterion').setRequired(true).setAutocomplete(true))),
  new SlashCommandBuilder()
    .setName('evaluation-period')
    .setDescription('Manage evaluation periods.')
    .addSubcommand((command) => command
      .setName('list')
      .setDescription('List evaluation periods.'))
    .addSubcommand((command) => command
      .setName('view')
      .setDescription('Show details and submission counts for a period.')
      .addStringOption((option) => option.setName('period').setDescription('Choose an evaluation period').setRequired(true).setAutocomplete(true)))
    .addSubcommand((command) => command
      .setName('open')
      .setDescription('Open a new evaluation period.')
      .addStringOption((option) => option.setName('name').setDescription('Evaluation period name').setRequired(true)))
    .addSubcommand((command) => command
      .setName('close')
      .setDescription('Close an open evaluation period.')
      .addStringOption((option) => option.setName('period').setDescription('Choose an open period').setRequired(true).setAutocomplete(true))),
  new SlashCommandBuilder()
    .setName('evaluation')
    .setDescription('Submit a probation evaluation.')
    .addSubcommand((command) => command
      .setName('peer')
      .setDescription('Open the Peer evaluation workflow.'))
    .addSubcommand((command) => command
      .setName('mentor')
      .setDescription('Open the Mentor evaluation workflow.')),
  new SlashCommandBuilder()
    .setName('evaluation-report')
    .setDescription('Read raw evaluation results as Core or Admin.')
    .addSubcommand((command) => command
      .setName('view')
      .setDescription('View raw evaluations for one candidate in a period.')
      .addStringOption((option) => option
        .setName('period')
        .setDescription('Choose an evaluation period')
        .setRequired(true)
        .setAutocomplete(true))
      .addStringOption((option) => option
        .setName('candidate')
        .setDescription('Choose a candidate')
        .setRequired(true)
        .setAutocomplete(true))
      .addStringOption((option) => option
        .setName('kind')
        .setDescription('Optional evaluation kind filter')
        .addChoices({ name: 'Peer', value: 'Peer' }, { name: 'Mentor', value: 'Mentor' })))
    .addSubcommand((command) => command
      .setName('export')
      .setDescription('Export a readable Excel workbook for every evaluation in a period.')
      .addStringOption((option) => option
        .setName('period')
        .setDescription('Choose an evaluation period')
        .setRequired(true)
        .setAutocomplete(true))),
];
