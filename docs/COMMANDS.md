# Discord command reference

Commands are registered only in the configured `DISCORD_GUILD_ID`. The list below is
the currently registered command surface; a command can exist in the target product
specification without being implemented yet.

| Command | Current behavior | Access |
| --- | --- | --- |
| `/ping` | Replies with an online check | Any guild user |
| `/verification publish` | Publishes the StudentId-linking message in the current sendable channel | Discord Administrator permission |
| `/bootstrap-admin` | Creates the first Admin when no Member exists | Discord Administrator permission; database guard also applies |
| `/member create` | Creates an active regular Member | Linked active Felion Admin |
| `/member list` | Lists Members with position, status, Department, and Discord-link state | Linked active Felion Admin |
| `/member view` | Shows detailed Member information, including email and mentor teams | Linked active Felion Admin |
| `/probation-candidate create` | Creates an active candidate without requiring a team | Linked active Felion Admin |
| `/probation-candidate list` | Lists candidates with optional status and team filters | Linked active Felion Admin or Core |
| `/probation-candidate view` | Shows candidate details, team mentors, Discord-link state, and evaluation counts; candidates can view only themselves | Linked active Felion Admin/Core or the candidate themselves |
| `/probation-candidate assign-team` | Shows only active candidates without a team in autocomplete, assigns them to an active team, and immediately synchronizes roles when linked | Linked active Felion Admin |
| `/probation-candidate deactivate` | Changes an active candidate to Inactive and immediately removes Felion-managed roles when linked | Linked active Felion Admin |
| `/probation-candidate reactivate` | Changes an inactive candidate to Active and immediately restores desired Felion-managed roles when linked | Linked active Felion Admin |
| `/department create` | Creates a Department; `core` is reserved | Linked active Felion Admin |
| `/department edit` | Renames an active Department selected by name and changes its slug; `core` is reserved | Linked active Felion Admin |
| `/department deactivate` | Soft-deactivates a Department selected by name for future assignments; `core` is reserved | Linked active Felion Admin |
| `/generation create` | Creates a name-only Generation | Linked active Felion Admin |
| `/generation edit` | Renames an active Generation selected by name | Linked active Felion Admin |
| `/generation deactivate` | Soft-deactivates a Generation selected by name for future assignments | Linked active Felion Admin |
| `/probation-team create` | Creates an active probation team | Linked active Felion Admin |
| `/probation-team list` | Lists teams with active state and mentor/candidate counts | Linked active Felion Admin or Core |
| `/probation-team view` | Shows team mentors and candidate roster | Linked active Felion Admin or Core |
| `/probation-team edit` | Renames an active probation team selected by name | Linked active Felion Admin |
| `/probation-team deactivate` | Soft-deactivates an active probation team selected by name | Linked active Felion Admin |
| `/probation-team assign-mentor` | Assigns an active Member to an active probation team | Linked active Felion Admin |
| `/role map` | Creates or updates a configurable role mapping; references and teams are selected by name, while Probation uses `Active` | Linked active Felion Admin |
| `/role assign` | Explicitly assigns an existing Discord role to an active linked Member, preserves it during synchronization, and synchronizes immediately | Linked active Felion Admin |
| `/role sync` | Reconciles mapped and explicit Felion roles for one linked Discord user | Linked active Felion Admin |
| `/role inspect` | Shows current Felion-managed roles, desired roles, explicit assignments, and add/remove plan | Linked active Felion Admin |
| `/evaluation-criteria add` | Adds or reactivates a Peer/Mentor criterion | Linked active Felion Admin |
| `/evaluation-criteria list` | Lists criteria, score bounds, and active state | Linked active Felion Admin or Core |
| `/evaluation-criteria rename` | Renames an active criterion selected by kind/name while preserving its ID | Linked active Felion Admin |
| `/evaluation-criteria remove` | Deactivates an active criterion selected by kind/name without deleting history | Linked active Felion Admin |
| `/evaluation-period open` | Opens a named evaluation period | Linked active Felion Admin |
| `/evaluation-period list` | Lists periods with status and Peer/Mentor submission counts | Linked active Felion Admin or Core |
| `/evaluation-period view` | Shows one period's status, timestamps, and submission counts | Linked active Felion Admin or Core |
| `/evaluation-period close` | Closes an open evaluation period selected by name | Linked active Felion Admin |
| `/evaluation peer` | Opens an ephemeral workflow to select an open period/candidate, then opens score Modals; offers the next eligible candidate after submit | Linked active ProbationCandidate |
| `/evaluation mentor` | Opens an ephemeral workflow to select an open period/candidate, then opens score Modals; offers the next eligible candidate after submit | Linked active Member mentor |
| `/evaluation-report view` | Shows a paged ephemeral raw report after selecting a period and candidate, optionally filtered by Peer/Mentor kind | Linked active Core or Admin |
| `/evaluation-report export` | Exports an ephemeral Excel workbook with a readable summary and raw score sheet; UUIDs are omitted in favor of names | Linked active Core or Admin |
| `/department list` | Lists Departments and active state | Linked active Felion Admin |
| `/generation list` | Lists Generations and active state | Linked active Felion Admin |

## Verification interaction

All command options that refer to a Department, Generation, team, Member,
ProbationCandidate, criterion, evaluation period, or role-mapping target use Discord
autocomplete. Start typing a name and select the suggested item; UUIDs remain internal
database values and are not required as user input.

`/verification publish` sends a button. The button opens a modal that accepts only a
StudentId. The bot normalizes the value, resolves it through `identity_registry`,
ensures both sides are not already linked, writes `discord_identity_links`, and audits
the link in one transaction.

## Not registered yet

PASS/FAIL decisions do not currently have Discord commands.
