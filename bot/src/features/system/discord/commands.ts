import { SlashCommandBuilder } from 'discord.js';

export const systemCommandDefinitions = [
  new SlashCommandBuilder()
    .setName('ping')
    .setDescription('Check whether the Felion bot is online.'),
];
