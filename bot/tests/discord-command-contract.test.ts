import { describe, expect, it } from 'vitest';
import { commandDefinitions } from '#app/discord/commands.js';

describe('Discord command contract', () => {
  it('preserves the registered command order and names across feature composition', () => {
    expect(commandDefinitions.map((command) => command.name)).toEqual([
      'ping',
      'verification',
      'bootstrap-admin',
      'member',
      'probation-candidate',
      'department',
      'generation',
      'probation-team',
      'role',
      'evaluation-criteria',
      'evaluation-period',
      'evaluation',
      'evaluation-report',
    ]);
  });

  it('does not register duplicate top-level command names', () => {
    const names = commandDefinitions.map((command) => command.name);
    expect(new Set(names).size).toBe(names.length);
  });

  it('registers the approved raw-report view and export flows', () => {
    const report = commandDefinitions.find((command) => command.name === 'evaluation-report');

    expect(report?.options?.map((option) => option.name)).toEqual(['view', 'export']);
    expect(report?.options?.[0]?.options?.map((option) => option.name))
      .toEqual(['period-id', 'candidate-id', 'kind']);
    expect(report?.options?.[1]?.options?.map((option) => option.name))
      .toEqual(['period-id']);
  });
});
