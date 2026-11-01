import {
  ActionRowBuilder,
  ButtonBuilder,
  ButtonStyle,
  EmbedBuilder,
  MessageFlags,
  ModalBuilder,
  PermissionFlagsBits,
  SlashCommandBuilder,
  TextInputBuilder,
  TextInputStyle,
  type Client,
  type Interaction,
} from 'discord.js';
import type { Config } from '#app/config.js';
import type { Db } from '#app/db/client.js';
import { linkDiscordIdentity } from '#app/db/linking.js';
import { getPublicErrorMessage } from '#app/errors.js';
import { importFromDiscord, syncPerson } from './role-sync.js';

export const verificationCommands = [
  new SlashCommandBuilder()
    .setName('verification')
    .setDescription('Manage the StudentId verification message.')
    .setDefaultMemberPermissions(PermissionFlagsBits.Administrator.toString())
    .addSubcommand((command) => command
      .setName('publish')
      .setDescription('Publish the StudentId verification message in this channel.')),
];

function createVerificationMessage() {
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

export type VerificationContext = { client: Client; config: Config; db: Db };

export async function handleVerificationInteraction(interaction: Interaction, context: VerificationContext): Promise<boolean> {
  if (interaction.isChatInputCommand() && interaction.commandName === 'verification') {
    if (!interaction.channel?.isSendable()) {
      await interaction.reply({ content: 'This channel cannot receive messages.', flags: MessageFlags.Ephemeral });
      return true;
    }

    await interaction.channel.send(createVerificationMessage());
    await interaction.reply({ content: 'Verification message published.', flags: MessageFlags.Ephemeral });
    return true;
  }

  if (interaction.isButton() && interaction.customId === 'verification:open') {
    const studentId = new TextInputBuilder()
      .setCustomId('studentId')
      .setLabel('Mã Sinh Viên')
      .setStyle(TextInputStyle.Short)
      .setRequired(true)
      .setMaxLength(32);

    await interaction.showModal(
      new ModalBuilder()
        .setCustomId('verification:submit')
        .setTitle('Nhập Mã Sinh Viên')
        .addComponents(new ActionRowBuilder<TextInputBuilder>().addComponents(studentId)),
    );
    return true;
  }

  if (!interaction.isModalSubmit() || interaction.customId !== 'verification:submit') {
    return false;
  }

  // Import + sync can take longer than Discord's 3 second reply window.
  await interaction.deferReply({ flags: MessageFlags.Ephemeral });

  let link: Awaited<ReturnType<typeof linkDiscordIdentity>>;
  try {
    link = await linkDiscordIdentity(context.db, interaction.user.id, interaction.fields.getTextInputValue('studentId'));
  } catch (error) {
    await interaction.editReply(getPublicErrorMessage(error, 'Không thể liên kết tài khoản Discord này.'));
    return true;
  }

  try {
    const guild = await context.client.guilds.fetch(context.config.DISCORD_GUILD_ID);
    if (link.result === 'Linked') {
      // Keep roles the user already had before linking; the database becomes their source from here on.
      await importFromDiscord(context.db, guild, link.personId, link.personId);
    }
    const outcome = await syncPerson(context.db, guild, link.personId, link.personId);
    const added = outcome.plan?.add.length ?? 0;
    const removed = outcome.plan?.remove.length ?? 0;
    await interaction.editReply(
      `${link.result === 'AlreadyLinked' ? 'Tài khoản Discord đã được liên kết trước đó.' : 'Liên kết tài khoản Discord thành công.'} Role: thêm ${added}, gỡ ${removed}.`,
    );
  } catch (error) {
    const message = getPublicErrorMessage(error, 'Unable to synchronize Discord roles.');
    await interaction.editReply(`Đã liên kết, nhưng đồng bộ role thất bại: ${message} Hãy nhờ admin bấm Sync trên web.`);
  }
  return true;
}
