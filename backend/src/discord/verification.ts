import {
  ActionRowBuilder,
  ButtonBuilder,
  ButtonStyle,
  ChannelType,
  EmbedBuilder,
  MessageFlags,
  ModalBuilder,
  PermissionFlagsBits,
  TextInputBuilder,
  TextInputStyle,
  type Client,
  type Guild,
  type Interaction,
} from 'discord.js';
import type { Config } from '#app/config.js';
import type { Db } from '#app/db/client.js';
import { linkDiscordIdentity } from '#app/db/linking.js';
import type { SettingsValues } from '#app/db/settings.js';
import { AppError, getPublicErrorMessage } from '#app/errors.js';
import { importFromDiscord, syncPerson } from './role-sync.js';

function createVerificationMessage(settings: SettingsValues) {
  return {
    embeds: [new EmbedBuilder()
      .setTitle(settings.verificationTitle)
      .setDescription(settings.verificationDescription)
      .setColor(0x5865f2)],
    components: [new ActionRowBuilder<ButtonBuilder>().addComponents(
      new ButtonBuilder()
        .setCustomId('verification:open')
        .setLabel(settings.verificationButtonLabel)
        .setStyle(ButtonStyle.Primary),
    )],
  };
}

const sendPermissions = [PermissionFlagsBits.ViewChannel, PermissionFlagsBits.SendMessages, PermissionFlagsBits.EmbedLinks];

export async function listSendableChannels(guild: Guild): Promise<{ id: string; name: string }[]> {
  const [me, channels] = await Promise.all([guild.members.fetchMe(), guild.channels.fetch()]);
  return [...channels.values()]
    .filter((channel) => channel !== null)
    .filter((channel) => channel.type === ChannelType.GuildText || channel.type === ChannelType.GuildAnnouncement)
    .filter((channel) => channel.permissionsFor(me).has(sendPermissions))
    .sort((a, b) => a.rawPosition - b.rawPosition)
    .map((channel) => ({ id: channel.id, name: channel.name }));
}

// Edits the published message in place; sends a new one if it was deleted or the channel changed.
export async function publishVerificationMessage(
  guild: Guild,
  settings: SettingsValues,
  channelId: string,
): Promise<{ channelId: string; messageId: string }> {
  const channel = await guild.channels.fetch(channelId).catch(() => null);
  if (!channel?.isSendable()) throw new AppError('VALIDATION', 'Bot không gửi được tin nhắn vào kênh này.');
  const message = createVerificationMessage(settings);
  const { verificationChannelId: oldChannelId, verificationMessageId: oldMessageId } = settings;

  if (oldMessageId && oldChannelId === channelId) {
    const existing = await channel.messages.fetch(oldMessageId).catch(() => null);
    if (existing) return { channelId, messageId: (await existing.edit(message)).id };
  }
  const sent = await channel.send(message);
  if (oldMessageId && oldChannelId && oldChannelId !== channelId) {
    // Best effort: the old message or its channel may already be gone.
    const oldChannel = await guild.channels.fetch(oldChannelId).catch(() => null);
    if (oldChannel?.isTextBased()) await oldChannel.messages.delete(oldMessageId).catch(() => undefined);
  }
  return { channelId, messageId: sent.id };
}

export type VerificationContext = { client: Client; config: Config; db: Db };

export async function handleVerificationInteraction(interaction: Interaction, context: VerificationContext): Promise<boolean> {
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
    await interaction.editReply(`Đã liên kết, nhưng đồng bộ role thất bại: ${message} Hãy liên hệ admin.`);
  }
  return true;
}
