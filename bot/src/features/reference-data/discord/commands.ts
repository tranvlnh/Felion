import { SlashCommandBuilder } from 'discord.js';

export const referenceDataCommandDefinitions = [
  new SlashCommandBuilder()
    .setName('department')
    .setDescription('Manage Departments.')
    .addSubcommand((command) => command
      .setName('create')
      .setDescription('Create a Department.')
      .addStringOption((option) => option.setName('name').setDescription('Department name').setRequired(true))
      .addStringOption((option) => option.setName('slug').setDescription('Department slug').setRequired(true)))
    .addSubcommand((command) => command
      .setName('edit')
      .setDescription('Edit an active Department.')
      .addStringOption((option) => option.setName('department-id').setDescription('Department UUID').setRequired(true))
      .addStringOption((option) => option.setName('name').setDescription('New Department name').setRequired(true))
      .addStringOption((option) => option.setName('slug').setDescription('New Department slug').setRequired(true)))
    .addSubcommand((command) => command
      .setName('deactivate')
      .setDescription('Deactivate a Department for future assignments.')
      .addStringOption((option) => option.setName('department-id').setDescription('Department UUID').setRequired(true))),
  new SlashCommandBuilder()
    .setName('generation')
    .setDescription('Manage Generations.')
    .addSubcommand((command) => command
      .setName('create')
      .setDescription('Create a Generation.')
      .addStringOption((option) => option.setName('name').setDescription('Generation name').setRequired(true)))
    .addSubcommand((command) => command
      .setName('edit')
      .setDescription('Edit an active Generation.')
      .addStringOption((option) => option.setName('generation-id').setDescription('Generation UUID').setRequired(true))
      .addStringOption((option) => option.setName('name').setDescription('New Generation name').setRequired(true)))
    .addSubcommand((command) => command
      .setName('deactivate')
      .setDescription('Deactivate a Generation for future assignments.')
      .addStringOption((option) => option.setName('generation-id').setDescription('Generation UUID').setRequired(true))),
];
