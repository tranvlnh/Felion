import type { RepliableInteraction } from 'discord.js';
import { getPublicErrorMessage } from '#app/shared/errors/app-error.js';

export async function replyWithError(
  interaction: RepliableInteraction,
  error: unknown,
  fallback: string,
): Promise<void> {
  await interaction.reply({
    content: getPublicErrorMessage(error, fallback),
    ephemeral: true,
  });
}
