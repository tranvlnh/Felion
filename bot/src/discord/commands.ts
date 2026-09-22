import {
  ActionRowBuilder,
  ButtonBuilder,
  ButtonStyle,
  EmbedBuilder,
  PermissionFlagsBits,
  SlashCommandBuilder,
} from 'discord.js';

export const commandDefinitions = [
  new SlashCommandBuilder()
    .setName('ping')
    .setDescription('Check whether the Felion bot is online.'),
  new SlashCommandBuilder()
    .setName('verification')
    .setDescription('Manage the StudentId verification message.')
    .setDefaultMemberPermissions(PermissionFlagsBits.Administrator.toString())
    .addSubcommand((command) => command
      .setName('publish')
      .setDescription('Publish the StudentId verification message in this channel.')),
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
  new SlashCommandBuilder()
    .setName('department')
    .setDescription('Manage Departments.')
    .addSubcommand((command) => command
      .setName('create')
      .setDescription('Create a Department.')
      .addStringOption((option) => option.setName('name').setDescription('Department name').setRequired(true))
      .addStringOption((option) => option.setName('slug').setDescription('Department slug').setRequired(true))),
  new SlashCommandBuilder()
    .setName('generation')
    .setDescription('Manage Generations.')
    .addSubcommand((command) => command
      .setName('create')
      .setDescription('Create a Generation.')
      .addStringOption((option) => option.setName('name').setDescription('Generation name').setRequired(true))),
  new SlashCommandBuilder()
    .setName('role')
    .setDescription('Manage Discord role mappings.')
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
      .addRoleOption((option) => option.setName('role').setDescription('Guild role').setRequired(true))),
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
].map((command) => command.toJSON());

export function createVerificationMessage() {
  return {
    embeds: [new EmbedBuilder()
      .setTitle('Felion verification')
      .setDescription('Press the button below and enter your StudentId to link your Discord account.')
      .setColor(0x5865f2)],
    components: [new ActionRowBuilder<ButtonBuilder>().addComponents(
      new ButtonBuilder()
        .setCustomId('verification:open')
        .setLabel('Link StudentId')
        .setStyle(ButtonStyle.Primary),
    )],
  };
}
