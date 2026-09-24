import { Role, MessageFlags } from 'discord.js';
import { requireAdminActor } from '#app/db/authorization.js';
import { listRoleMappingKeys } from '#app/db/discord-autocomplete.js';
import { loadDiscordRoleSyncTarget } from '#app/db/role-synchronization.js';
import { assignExplicitDiscordRole, mapDiscordRole } from '#app/db/reference-management.js';
import { parseDiscordRoleMappingKind } from '#app/domain/discord-roles.js';
import type { InteractionHandler } from '#app/discord/interaction-router.js';
import { respondWithAutocomplete } from '#app/discord/autocomplete.js';
import { replyWithError } from '#app/discord/responses.js';
import { synchronizeDiscordRolesForUser } from '#app/discord/role-synchronization.js';
import { planDiscordRoleReconciliation } from '#app/domain/discord-roles.js';

function formatRoleIds(roleIds: readonly string[], roles: Map<string, Role>): string {
  if (roleIds.length === 0) return 'Không có';
  return roleIds.map((roleId) => `${roles.get(roleId)?.name ?? 'Unknown'} (${roleId})`).join(', ');
}

export const handleRoleInteraction: InteractionHandler = async (interaction, context) => {
  if (interaction.isAutocomplete()) {
    if (interaction.commandName !== 'role') {
      return false;
    }
    try {
      await requireAdminActor(
        context.database,
        interaction.user.id,
        'Only a linked active Admin may change reference data.',
      );
      const focused = interaction.options.getFocused(true);
      if (focused.name !== 'key') {
        await respondWithAutocomplete(interaction, []);
        return true;
      }
      await respondWithAutocomplete(
        interaction,
        await listRoleMappingKeys(
          context.database,
          interaction.options.getString('kind'),
          focused.value,
        ),
      );
    } catch {
      await respondWithAutocomplete(interaction, []);
    }
    return true;
  }

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
    if (subcommand === 'inspect') {
      const targetUser = interaction.options.getUser('user', true);
      const target = await loadDiscordRoleSyncTarget(context.database, targetUser.id);
      const guild = await context.client.guilds.fetch(context.config.DISCORD_GUILD_ID);
      const guildMember = await guild.members.fetch(targetUser.id);
      const guildRoles = await guild.roles.fetch();
      const currentRoleIds = [...guildMember.roles.cache.keys()];
      const plan = planDiscordRoleReconciliation(currentRoleIds, target);
      const managedCurrent = currentRoleIds.filter((roleId) => target.managedRoleIds.includes(roleId));
      await interaction.reply({
        content: [
          `Role inspection: <@${targetUser.id}> (${target.subjectType})`,
          `Current Felion-managed: ${formatRoleIds(managedCurrent, guildRoles)}`,
          `Desired Felion-managed: ${formatRoleIds(target.desiredRoleIds, guildRoles)}`,
          `Would add: ${formatRoleIds(plan.rolesToAdd, guildRoles)}`,
          `Would remove: ${formatRoleIds(plan.rolesToRemove, guildRoles)}`,
          `Explicit assignments: ${formatRoleIds(target.explicitRoleIds, guildRoles)}`,
        ].join('\n'),
        flags: MessageFlags.Ephemeral,
      });
      return true;
    }
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
      await interaction.reply({ content: 'Operation completed.',flags: MessageFlags.Ephemeral });
      return true;
    }

    if (subcommand === 'assign') {
      const targetUser = interaction.options.getUser('user', true);
      const role = interaction.options.getRole('role', true);
      if (!(role instanceof Role) || role.managed || !role.editable) {
        throw new Error('This Discord role cannot be managed by Felion.');
      }

      await assignExplicitDiscordRole(context.database, {
        discordUserId: targetUser.id,
        discordRoleId: role.id,
        actorDiscordUserId: interaction.user.id,
      });

      try {
        const guild = await context.client.guilds.fetch(context.config.DISCORD_GUILD_ID);
        const result = await synchronizeDiscordRolesForUser(
          context.database,
          guild,
          targetUser.id,
          interaction.user.id,
        );
        await interaction.reply({
          content: `Role assigned and synchronized: ${result.addedRoleIds.length} added, ${result.removedRoleIds.length} removed.`,
          flags: MessageFlags.Ephemeral,
        });
      } catch (error: unknown) {
        const message = error instanceof Error ? error.message : 'Unable to synchronize Discord roles.';
        await interaction.reply({
          content: `Role assignment was saved, but Discord synchronization failed: ${message}`,
          flags: MessageFlags.Ephemeral,
        });
      }
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
     flags: MessageFlags.Ephemeral,
    });
  } catch (error: unknown) {
    await replyWithError(interaction, error, 'Unable to complete the operation.');
  }
  return true;
};
