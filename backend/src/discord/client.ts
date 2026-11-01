import { Client, Events, GatewayIntentBits } from 'discord.js';
import type { Config } from '#app/config.js';
import type { Db } from '#app/db/client.js';
import { findPersonByDiscordUserId } from '#app/db/people.js';
import { deleteRoleEverywhere } from '#app/db/roles.js';
import { handleVerificationInteraction } from './verification.js';
import { syncPerson } from './role-sync.js';

export function createDiscordClient(config: Config, db: Db): Client {
  // GuildMembers is a privileged intent: enable "Server Members Intent" in the Developer Portal.
  const client = new Client({ intents: [GatewayIntentBits.Guilds, GatewayIntentBits.GuildMembers] });

  client.once(Events.ClientReady, (readyClient) => {
    console.log(`Felion bot logged in as ${readyClient.user.tag}`);
  });

  client.on(Events.InteractionCreate, async (interaction) => {
    try {
      await handleVerificationInteraction(interaction, { client, config, db });
    } catch (error) {
      console.error('Interaction failed', error);
    }
  });

  client.on(Events.GuildMemberAdd, async (member) => {
    if (member.guild.id !== config.DISCORD_GUILD_ID) return;
    try {
      const person = await findPersonByDiscordUserId(db, member.id);
      if (person?.status === 'Active') {
        await syncPerson(db, member.guild, person.id, null, { member });
      }
    } catch (error) {
      console.error(`Restoring roles for ${member.id} failed`, error);
    }
  });

  client.on(Events.GuildRoleDelete, async (role) => {
    if (role.guild.id !== config.DISCORD_GUILD_ID) return;
    try {
      await deleteRoleEverywhere(db, role.id, null);
    } catch (error) {
      console.error(`Forgetting deleted role ${role.id} failed`, error);
    }
  });

  return client;
}
