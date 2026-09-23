import {
  ActionRowBuilder,
  ButtonBuilder,
  ButtonStyle,
  EmbedBuilder,
  PermissionFlagsBits,
  SlashCommandBuilder,
} from 'discord.js';

export const identityCommandDefinitions = [
  new SlashCommandBuilder()
    .setName('verification')
    .setDescription('Manage the StudentId verification message.')
    .setDefaultMemberPermissions(PermissionFlagsBits.Administrator.toString())
    .addSubcommand((command) => command
      .setName('publish')
      .setDescription('Publish the StudentId verification message in this channel.')),
];

export function createVerificationMessage(): {
  embeds: EmbedBuilder[];
  components: ActionRowBuilder<ButtonBuilder>[];
} {
  return {
    embeds: [new EmbedBuilder()
      .setTitle('Felion verification')
      .setDescription('Nhấn nút bên dưới và nhập Mã sinh viên của bạn để liên kết tài khoản Discord.')
      .setColor(0x5865f2)],
    components: [new ActionRowBuilder<ButtonBuilder>().addComponents(
      new ButtonBuilder()
        .setCustomId('verification:open')
        .setLabel('Nhấn để mở')
        .setStyle(ButtonStyle.Primary),
    )],
  };
}
