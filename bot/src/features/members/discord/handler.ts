import { MessageFlags } from "discord.js";
import { requireAdminActor } from "#app/db/authorization.js";
import { bootstrapAdmin } from "#app/db/bootstrap.js";
import { listActiveMembers } from "#app/db/discord-autocomplete.js";
import { createRegularMember } from "#app/db/member-management.js";
import {
  getMemberDetail,
  listMemberSummaries,
} from "#app/db/management-read-model.js";
import { respondWithAutocomplete } from "#app/discord/autocomplete.js";
import type { InteractionHandler } from "#app/discord/interaction-router.js";
import { replyWithError } from "#app/discord/responses.js";
import {
  formatMemberDetail,
  formatMemberList,
} from "#app/discord/management-views.js";

export const handleMemberInteraction: InteractionHandler = async (
  interaction,
  context,
) => {
  if (interaction.isAutocomplete()) {
    if (interaction.commandName !== "member") return false;
    try {
      await requireAdminActor(context.database, interaction.user.id);
      const focused = interaction.options.getFocused(true);
      if (focused.name === "member") {
        await respondWithAutocomplete(
          interaction,
          await listActiveMembers(context.database, focused.value),
        );
      } else {
        await respondWithAutocomplete(interaction, []);
      }
    } catch {
      await respondWithAutocomplete(interaction, []);
    }
    return true;
  }

  if (!interaction.isChatInputCommand()) {
    return false;
  }

  if (interaction.commandName === "bootstrap-admin") {
    try {
      await bootstrapAdmin(context.database, {
        actorDiscordUserId: interaction.user.id,
        studentId: interaction.options.getString("student-id", true),
        fullName: interaction.options.getString("full-name", true),
        clubEmail: interaction.options.getString("club-email", true),
        generationName: interaction.options.getString("generation-name", true),
      });
      await interaction.reply({
        content:
          "Initial Admin created. Publish the verification message to link this account.",
        flags: MessageFlags.Ephemeral,
      });
    } catch (error: unknown) {
      await replyWithError(
        interaction,
        error,
        "Unable to bootstrap the initial Admin.",
      );
    }
    return true;
  }

  if (interaction.commandName !== "member") {
    return false;
  }

  try {
    await requireAdminActor(
      context.database,
      interaction.user.id,
      "Only a linked active Admin may create Members.",
    );
    const subcommand = interaction.options.getSubcommand();
    if (subcommand === "list") {
      await interaction.reply({
        content: formatMemberList(await listMemberSummaries(context.database)),
        flags: MessageFlags.Ephemeral,
      });
      return true;
    }
    if (subcommand === "view") {
      const member = await getMemberDetail(
        context.database,
        interaction.options.getString("member", true),
      );
      if (!member) throw new Error("Member was not found.");
      await interaction.reply({
        content: formatMemberDetail(member),
        flags: MessageFlags.Ephemeral,
      });
      return true;
    }

    await createRegularMember(context.database, {
      actorDiscordUserId: interaction.user.id,
      studentId: interaction.options.getString("student-id", true),
      fullName: interaction.options.getString("full-name", true),
      clubEmail: interaction.options.getString("club-email", true),
      department: interaction.options.getString("department", true),
      generation: interaction.options.getString("generation", true),
    });
    await interaction.reply({
      content: "Member created successfully.",
      flags: MessageFlags.Ephemeral,
    });
  } catch (error: unknown) {
    await replyWithError(interaction, error, "Unable to create Member.");
  }
  return true;
};
