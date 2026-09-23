import type { AutocompleteInteraction } from 'discord.js';
import type { AutocompleteItem } from '#app/db/discord-autocomplete.js';

export async function respondWithAutocomplete(
  interaction: AutocompleteInteraction,
  items: readonly AutocompleteItem[],
): Promise<void> {
  await interaction.respond(items.map((item) => ({
    name: item.name.slice(0, 100),
    value: item.value,
    ...(item.description ? { description: item.description.slice(0, 100) } : {}),
  })));
}
