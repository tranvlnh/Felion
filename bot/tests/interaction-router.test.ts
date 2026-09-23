import type { Interaction } from 'discord.js';
import { describe, expect, it } from 'vitest';
import {
  routeInteraction,
  type InteractionContext,
  type InteractionHandler,
} from '#app/discord/interaction-router.js';

describe('Discord interaction router', () => {
  it('stops after the first handler accepts an interaction', async () => {
    const calls: string[] = [];
    const handlers: InteractionHandler[] = [
      async () => {
        calls.push('ignored');
        return false;
      },
      async () => {
        calls.push('handled');
        return true;
      },
      async () => {
        calls.push('unreachable');
        return true;
      },
    ];

    await routeInteraction(
      {} as Interaction,
      {} as InteractionContext,
      handlers,
    );

    expect(calls).toEqual(['ignored', 'handled']);
  });
});
