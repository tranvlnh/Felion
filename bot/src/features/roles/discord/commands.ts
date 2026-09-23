import { SlashCommandBuilder } from 'discord.js';

export const roleCommandDefinitions = [
  new SlashCommandBuilder()
    .setName('role')
    .setDescription('Manage and synchronize Felion Discord roles.')
    .addSubcommand((command) => command
      .setName('map')
      .setDescription('Map an existing Discord role.')
      .addStringOption((option) => option
        .setName('kind')
        .setDescription('Mapping dimension')
        .setRequired(true)
        .addChoices(
          { name: 'Position', value: 'Position' },
          { name: 'Probation', value: 'Probation' },
          { name: 'Department', value: 'Department' },
          { name: 'Generation', value: 'Generation' },
          { name: 'ProbationTeam', value: 'ProbationTeam' },
        ))
      .addStringOption((option) => option.setName('key').setDescription('Mapping key').setRequired(true))
      .addRoleOption((option) => option.setName('role').setDescription('Guild role').setRequired(true)))
    .addSubcommand((command) => command
      .setName('sync')
      .setDescription('Reconcile Felion-managed roles for a linked Discord user.')
      .addUserOption((option) => option.setName('user').setDescription('Linked Discord user').setRequired(true))),
];
