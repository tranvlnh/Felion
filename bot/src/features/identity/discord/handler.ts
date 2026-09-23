import {
  ActionRowBuilder,
  ModalBuilder,
  TextInputBuilder,
  TextInputStyle,
} from 'discord.js';
import { linkDiscordIdentity } from '#app/db/linking.js';
import type { InteractionHandler } from '#app/discord/interaction-router.js';
import { replyWithError } from '#app/discord/responses.js';
import { synchronizeDiscordRolesForUser } from '#app/discord/role-synchronization.js';
import { getPublicErrorMessage } from '#app/shared/errors/app-error.js';
import { createVerificationMessage } from './commands.js';

export const handleIdentityInteraction: InteractionHandler = async (interaction, context) => {
  if (interaction.isChatInputCommand() && interaction.commandName === 'verification') {
    if (!interaction.channel?.isSendable()) {
      await interaction.reply({ content: 'This channel cannot receive messages.', ephemeral: true });
      return true;
    }

    await interaction.channel.send(createVerificationMessage());
    await interaction.reply({ content: 'Verification message published.', ephemeral: true });
    return true;
  }

  if (interaction.isButton() && interaction.customId === 'verification:open') {
    const studentId = new TextInputBuilder()
      .setCustomId('studentId')
      .setLabel('StudentId')
      .setStyle(TextInputStyle.Short)
      .setRequired(true)
      .setMaxLength(32);

    await interaction.showModal(new ModalBuilder()
      .setCustomId('verification:submit')
      .setTitle('Link StudentId')
      .addComponents(new ActionRowBuilder<TextInputBuilder>().addComponents(studentId)));
    return true;
  }

  if (!interaction.isModalSubmit() || interaction.customId !== 'verification:submit') {
    return false;
  }

  const studentId = interaction.fields.getTextInputValue('studentId').trim().toUpperCase();
  try {
    await linkDiscordIdentity(context.database, interaction.user.id, studentId);
  } catch (error: unknown) {
    await replyWithError(interaction, error, 'Unable to link this Discord account.');
    return true;
  }

  try {
    const guild = await context.client.guilds.fetch(context.config.DISCORD_GUILD_ID);
    const result = await synchronizeDiscordRolesForUser(
      context.database,
      guild,
      interaction.user.id,
      interaction.user.id,
    );
    await interaction.reply({
      content: `Discord account linked successfully. Roles synchronized: ${result.addedRoleIds.length} added, ${result.removedRoleIds.length} removed.`,
      ephemeral: true,
    });
  } catch (error: unknown) {
    const message = getPublicErrorMessage(error, 'Unable to synchronize Discord roles.');
    await interaction.reply({
      content: `Discord account linked, but role synchronization failed: ${message} Ask an Admin to run /role sync.`,
      ephemeral: true,
    });
  }

  return true;
};
