import type { InteractionHandler } from '#app/discord/interaction-router.js';

export const handleSystemInteraction: InteractionHandler = async (interaction) => {
  if (!interaction.isChatInputCommand() || interaction.commandName !== 'ping') {
    return false;
  }

  await interaction.reply({ content: 'Felion is online.', ephemeral: true });
  return true;
};
