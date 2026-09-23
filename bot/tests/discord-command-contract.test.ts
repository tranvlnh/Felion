import {
  ApplicationCommandOptionType,
  type APIApplicationCommandOption,
} from 'discord.js';
import { describe, expect, it } from 'vitest';

import { commandDefinitions } from '#app/discord/commands.js';

type SubcommandOption = Extract<
  APIApplicationCommandOption,
  { type: ApplicationCommandOptionType.Subcommand }
>;

function isSubcommand(
  option: APIApplicationCommandOption,
): option is SubcommandOption {
  return option.type === ApplicationCommandOptionType.Subcommand;
}

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
    const report = commandDefinitions.find(
      (command) => command.name === 'evaluation-report',
    );

    expect(report).toBeDefined();

    expect(report?.options?.map((option) => option.name)).toEqual([
      'view',
      'export',
    ]);

    const view = report?.options?.[0];
    const exportCommand = report?.options?.[1];

    expect(view?.type).toBe(ApplicationCommandOptionType.Subcommand);
    expect(exportCommand?.type).toBe(
      ApplicationCommandOptionType.Subcommand,
    );

    if (
      !view ||
      !exportCommand ||
      !isSubcommand(view) ||
      !isSubcommand(exportCommand)
    ) {
      throw new Error('Expected view and export to be subcommands');
    }

    expect(view.options?.map((option) => option.name)).toEqual([
      'period',
      'candidate',
      'kind',
    ]);

    expect(exportCommand.options?.map((option) => option.name)).toEqual([
      'period',
    ]);
  });

  it('uses autocomplete for every entity reference that used to require a UUID', () => {
    const autocompleteNames = new Set<string>();

    for (const command of commandDefinitions) {
      for (const option of command.options ?? []) {
        if (!isSubcommand(option)) {
          continue;
        }

        for (const nestedOption of option.options ?? []) {
          if ('autocomplete' in nestedOption && nestedOption.autocomplete) {
            autocompleteNames.add(
              `${command.name}:${option.name}:${nestedOption.name}`,
            );
          }
        }
      }
    }

    expect(autocompleteNames).toEqual(
      new Set([
        'probation-candidate:create:department',
        'probation-candidate:create:generation',
        'probation-candidate:assign-team:candidate',
        'probation-candidate:assign-team:team',
        'probation-candidate:deactivate:candidate',
        'probation-candidate:list:team',
        'probation-candidate:reactivate:candidate',
        'probation-candidate:view:candidate',

        'probation-team:edit:team',
        'probation-team:deactivate:team',
        'probation-team:assign-mentor:team',
        'probation-team:assign-mentor:member',
        'probation-team:view:team',

        'department:edit:department',
        'department:deactivate:department',

        'generation:edit:generation',
        'generation:deactivate:generation',

        'member:view:member',

        'role:map:key',

        'evaluation-criteria:rename:criterion',
        'evaluation-criteria:remove:criterion',

        'evaluation-period:close:period',
        'evaluation-period:view:period',

        'evaluation-report:view:period',
        'evaluation-report:view:candidate',
        'evaluation-report:export:period',
      ]),
    );
  });

  it('registers read-only management and role diagnostics commands', () => {
    const subcommands = (name: string): string[] => {
      const command = commandDefinitions.find(
        (item) => item.name === name,
      );

      return (command?.options ?? [])
        .filter(isSubcommand)
        .map((option) => option.name);
    };

    expect(subcommands('member')).toEqual([
      'list',
      'view',
      'create',
    ]);

    expect(subcommands('probation-candidate')).toContain('list');
    expect(subcommands('probation-candidate')).toContain('view');

    expect(subcommands('probation-team')).toContain('list');
    expect(subcommands('probation-team')).toContain('view');

    expect(subcommands('department')).toContain('list');
    expect(subcommands('generation')).toContain('list');

    expect(subcommands('role')).toEqual([
      'inspect',
      'map',
      'sync',
    ]);

    expect(subcommands('evaluation-criteria')).toContain('list');

    expect(subcommands('evaluation-period')).toContain('list');
    expect(subcommands('evaluation-period')).toContain('view');

    const evaluation = commandDefinitions.find(
      (item) => item.name === 'evaluation',
    );

    expect(evaluation).toBeDefined();

    const evaluationSubcommands = (evaluation?.options ?? []).filter(
      isSubcommand,
    );

    expect(
      evaluationSubcommands.map((option) => ({
        name: option.name,
        options: option.options ?? [],
      })),
    ).toEqual([
      {
        name: 'peer',
        options: [],
      },
      {
        name: 'mentor',
        options: [],
      },
    ]);
  });
});
