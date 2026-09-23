import type { Client, Interaction } from 'discord.js';
import type { Config } from '#app/config.js';
import type { Database } from '#app/db/client.js';
import type { EvaluationService } from '#app/features/evaluations/application/evaluation-service.js';

export type ApplicationServices = Readonly<{
  evaluations: EvaluationService;
}>;

export type InteractionContext = Readonly<{
  client: Client;
  config: Config;
  database: Database;
  services: ApplicationServices;
}>;

export type InteractionHandler = (
  interaction: Interaction,
  context: InteractionContext,
) => Promise<boolean>;

export async function routeInteraction(
  interaction: Interaction,
  context: InteractionContext,
  handlers: readonly InteractionHandler[],
): Promise<void> {
  for (const handler of handlers) {
    if (await handler(interaction, context)) {
      return;
    }
  }
}
