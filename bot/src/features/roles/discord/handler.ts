import { Role } from 'discord.js';
import { requireAdminActor } from '#app/db/authorization.js';
import { mapDiscordRole } from '#app/db/reference-management.js';
import { parseDiscordRoleMappingKind } from '#app/domain/discord-roles.js';
import type { InteractionHandler } from '#app/discord/interaction-router.js';
import { replyWithError } from '#app/discord/responses.js';
import { synchronizeDiscordRolesForUser } from '#app/discord/role-synchronization.js';

export const handleRoleInteraction: InteractionHandler = async (interaction, context) => {
  if (!interaction.isChatInputCommand() || interaction.commandName !== 'role') {
    return false;
  }

  try {
    await requireAdminActor(
      context.database,
      interaction.user.id,
      'Only a linked active Admin may change reference data.',
    );
    const subcommand = interaction.options.getSubcommand();
    if (subcommand === 'map') {
      const role = interaction.options.getRole('role', true);
      if (!(role instanceof Role) || role.managed || !role.editable) {
        throw new Error('This Discord role cannot be managed by Felion.');
      }
      await mapDiscordRole(context.database, {
        actorDiscordUserId: interaction.user.id,
        kind: parseDiscordRoleMappingKind(interaction.options.getString('kind', true)),
        key: interaction.options.getString('key', true),
        discordRoleId: role.id,
      });
      await interaction.reply({ content: 'Operation completed.', ephemeral: true });
      return true;
    }

    const targetUser = interaction.options.getUser('user', true);
    const guild = await context.client.guilds.fetch(context.config.DISCORD_GUILD_ID);
    const result = await synchronizeDiscordRolesForUser(
      context.database,
      guild,
      targetUser.id,
      interaction.user.id,
    );
    await interaction.reply({
      content: `Roles synchronized: ${result.addedRoleIds.length} added, ${result.removedRoleIds.length} removed.`,
      ephemeral: true,
    });
  } catch (error: unknown) {
    await replyWithError(interaction, error, 'Unable to complete the operation.');
  }
  return true;
};
