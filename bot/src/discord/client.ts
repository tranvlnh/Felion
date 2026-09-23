import { Client, Events, GatewayIntentBits } from 'discord.js';
import type { Config } from '#app/config.js';
import type { Database } from '#app/db/client.js';
import { createDrizzleEvaluationPersistence } from '#app/db/evaluations/drizzle-evaluation-persistence.js';
import { createEvaluationService } from '#app/features/evaluations/application/evaluation-service.js';
import { handleEvaluationInteraction } from '#app/features/evaluations/discord/handler.js';
import { handleIdentityInteraction } from '#app/features/identity/discord/handler.js';
import { handleMemberInteraction } from '#app/features/members/discord/handler.js';
import { handleProbationInteraction } from '#app/features/probation/discord/handler.js';
import { handleReferenceDataInteraction } from '#app/features/reference-data/discord/handler.js';
import { handleRoleInteraction } from '#app/features/roles/discord/handler.js';
import { handleSystemInteraction } from '#app/features/system/discord/handler.js';
import { routeInteraction, type InteractionHandler } from './interaction-router.js';

const interactionHandlers: readonly InteractionHandler[] = [
  handleSystemInteraction,
  handleIdentityInteraction,
  handleMemberInteraction,
  handleProbationInteraction,
  handleReferenceDataInteraction,
  handleRoleInteraction,
  handleEvaluationInteraction,
];

export function createDiscordClient(config: Config, database: Database): Client {
  const client = new Client({ intents: [GatewayIntentBits.Guilds] });
  const services = {
    evaluations: createEvaluationService(createDrizzleEvaluationPersistence(database)),
  };

  client.once(Events.ClientReady, (readyClient) => {
    console.log(`Felion bot logged in as ${readyClient.user.tag}`);
  });

  client.on(Events.InteractionCreate, async (interaction) => {
    await routeInteraction(interaction, { client, config, database, services }, interactionHandlers);
  });

  return client;
}
