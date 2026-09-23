import { PermissionFlagsBits, SlashCommandBuilder } from 'discord.js';

export const memberCommandDefinitions = [
  new SlashCommandBuilder()
    .setName('bootstrap-admin')
    .setDescription('Create the first Felion Admin when the database is empty.')
    .setDefaultMemberPermissions(PermissionFlagsBits.Administrator.toString())
    .addStringOption((option) => option.setName('student-id').setDescription('Initial Admin StudentId').setRequired(true))
    .addStringOption((option) => option.setName('full-name').setDescription('Initial Admin full name').setRequired(true))
    .addStringOption((option) => option.setName('club-email').setDescription('Initial Admin Workspace email').setRequired(true))
    .addStringOption((option) => option.setName('generation-name').setDescription('Generation display name').setRequired(true)),
  new SlashCommandBuilder()
    .setName('member')
    .setDescription('Manage Felion Members.')
    .addSubcommand((command) => command
      .setName('create')
      .setDescription('Create an active regular Member.')
      .addStringOption((option) => option.setName('student-id').setDescription('StudentId').setRequired(true))
      .addStringOption((option) => option.setName('full-name').setDescription('Full name').setRequired(true))
      .addStringOption((option) => option.setName('club-email').setDescription('Workspace email').setRequired(true))
      .addStringOption((option) => option.setName('department').setDescription('Department name or slug').setRequired(true))
      .addStringOption((option) => option.setName('generation').setDescription('Generation name').setRequired(true))),
];
