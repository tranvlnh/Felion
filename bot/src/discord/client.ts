import {
  Client,
  Events,
  GatewayIntentBits,
  InteractionType,
  ModalBuilder,
  Role,
  TextInputBuilder,
  TextInputStyle,
  ActionRowBuilder,
} from 'discord.js';
import type { Config } from '../config.js';
import type { Database } from '../db/client.js';
import { isLinkedAdmin } from '../db/authorization.js';
import { bootstrapAdmin } from '../db/bootstrap.js';
import { linkDiscordIdentity } from '../db/linking.js';
import { createRegularMember } from '../db/member-management.js';
import { createDepartment, createGeneration, mapDiscordRole } from '../db/reference-management.js';
import { addEvaluationCriterion, deactivateEvaluationCriterion, renameEvaluationCriterion } from '../db/evaluation-criteria.js';
import { createVerificationMessage } from './commands.js';

export function createDiscordClient(config: Config, database: NonNullable<Database>): Client {
  const client = new Client({ intents: [GatewayIntentBits.Guilds] });

  client.once(Events.ClientReady, (readyClient) => {
    console.log(`Felion bot logged in as ${readyClient.user.tag}`);
  });

  client.on(Events.InteractionCreate, async (interaction) => {
    if (interaction.isChatInputCommand()) {
      if (interaction.commandName === 'ping') {
        await interaction.reply({ content: 'Felion is online.', ephemeral: true });
        return;
      }

      if (interaction.commandName === 'verification' && interaction.options.getSubcommand() === 'publish') {
        if (!interaction.channel?.isSendable()) {
          await interaction.reply({ content: 'This channel cannot receive messages.', ephemeral: true });
          return;
        }

        await interaction.channel.send(createVerificationMessage());
        await interaction.reply({ content: 'Verification message published.', ephemeral: true });
        return;
      }

      if (interaction.commandName === 'bootstrap-admin') {
        try {
          await bootstrapAdmin(database, {
            actorDiscordUserId: interaction.user.id,
            studentId: interaction.options.getString('student-id', true),
            fullName: interaction.options.getString('full-name', true),
            clubEmail: interaction.options.getString('club-email', true),
            generationName: interaction.options.getString('generation-name', true),
          });
          await interaction.reply({ content: 'Initial Admin created. Publish the verification message to link this account.', ephemeral: true });
        } catch (error: unknown) {
          const message = error instanceof Error ? error.message : 'Unable to bootstrap the initial Admin.';
          await interaction.reply({ content: message, ephemeral: true });
        }
      }

      if (interaction.commandName === 'member' && interaction.options.getSubcommand() === 'create') {
        if (!(await isLinkedAdmin(database, interaction.user.id))) {
          await interaction.reply({ content: 'Only a linked active Admin may create Members.', ephemeral: true });
          return;
        }

        try {
          await createRegularMember(database, {
            actorDiscordUserId: interaction.user.id,
            studentId: interaction.options.getString('student-id', true),
            fullName: interaction.options.getString('full-name', true),
            clubEmail: interaction.options.getString('club-email', true),
            department: interaction.options.getString('department', true),
            generation: interaction.options.getString('generation', true),
          });
          await interaction.reply({ content: 'Member created successfully.', ephemeral: true });
        } catch (error: unknown) {
          const message = error instanceof Error ? error.message : 'Unable to create Member.';
          await interaction.reply({ content: message, ephemeral: true });
        }
      }

      if (['department', 'generation', 'role'].includes(interaction.commandName)) {
        if (!(await isLinkedAdmin(database, interaction.user.id))) {
          await interaction.reply({ content: 'Only a linked active Admin may change reference data.', ephemeral: true });
          return;
        }

        try {
          const subcommand = interaction.options.getSubcommand();
          if (interaction.commandName === 'department' && subcommand === 'create') {
            await createDepartment(database, {
              actorDiscordUserId: interaction.user.id,
              name: interaction.options.getString('name', true),
              slug: interaction.options.getString('slug', true),
            });
          } else if (interaction.commandName === 'generation' && subcommand === 'create') {
            await createGeneration(database, {
              actorDiscordUserId: interaction.user.id,
              name: interaction.options.getString('name', true),
            });
          } else if (interaction.commandName === 'role' && subcommand === 'map') {
            const role = interaction.options.getRole('role', true);
            if (!(role instanceof Role) || role.managed || !role.editable) {
              throw new Error('This Discord role cannot be managed by Felion.');
            }
            await mapDiscordRole(database, {
              actorDiscordUserId: interaction.user.id,
              kind: interaction.options.getString('kind', true) as 'Position' | 'Probation' | 'Department' | 'Generation' | 'ProbationTeam',
              key: interaction.options.getString('key', true),
              discordRoleId: role.id,
            });
          }
          await interaction.reply({ content: 'Operation completed.', ephemeral: true });
        } catch (error: unknown) {
          const message = error instanceof Error ? error.message : 'Unable to complete the operation.';
          await interaction.reply({ content: message, ephemeral: true });
        }
      }

      if (interaction.commandName === 'evaluation-criteria') {
        if (!(await isLinkedAdmin(database, interaction.user.id))) {
          await interaction.reply({ content: 'Only a linked active Admin may change evaluation criteria.', ephemeral: true });
          return;
        }

        try {
          const subcommand = interaction.options.getSubcommand();
          if (subcommand === 'add') {
            await addEvaluationCriterion(database, {
              actorDiscordUserId: interaction.user.id,
              kind: interaction.options.getString('kind', true) as 'Peer' | 'Mentor',
              name: interaction.options.getString('name', true),
            });
          } else if (subcommand === 'rename') {
            await renameEvaluationCriterion(database, {
              actorDiscordUserId: interaction.user.id,
              criterionId: interaction.options.getString('criterion-id', true),
              name: interaction.options.getString('name', true),
            });
          } else {
            await deactivateEvaluationCriterion(database, {
              actorDiscordUserId: interaction.user.id,
              criterionId: interaction.options.getString('criterion-id', true),
            });
          }
          await interaction.reply({ content: 'Evaluation criterion updated.', ephemeral: true });
        } catch (error: unknown) {
          const message = error instanceof Error ? error.message : 'Unable to update evaluation criteria.';
          await interaction.reply({ content: message, ephemeral: true });
        }
      }

      return;
    }

    if (interaction.type === InteractionType.MessageComponent && interaction.isButton() && interaction.customId === 'verification:open') {
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
      return;
    }

    if (interaction.isModalSubmit() && interaction.customId === 'verification:submit') {
      const studentId = interaction.fields.getTextInputValue('studentId').trim().toUpperCase();
      try {
        await linkDiscordIdentity(database, interaction.user.id, studentId);
        await interaction.reply({ content: 'Discord account linked successfully.', ephemeral: true });
      } catch (error: unknown) {
        const message = error instanceof Error ? error.message : 'Unable to link this Discord account.';
        await interaction.reply({ content: message, ephemeral: true });
      }
    }
  });

  void config;
  return client;
}
