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
| `/probation-candidate create` | Creates an active candidate without requiring a team | Linked active Felion Admin |
| `/probation-candidate assign-team` | Assigns or moves an active candidate to an active team and immediately synchronizes roles when linked | Linked active Felion Admin |
| `/probation-candidate deactivate` | Changes an active candidate to Inactive and immediately removes Felion-managed roles when linked | Linked active Felion Admin |
| `/probation-candidate reactivate` | Changes an inactive candidate to Active and immediately restores desired Felion-managed roles when linked | Linked active Felion Admin |
| `/department create` | Creates a Department; `core` is reserved | Linked active Felion Admin |
| `/department edit` | Renames an active Department by UUID and changes its slug; `core` is reserved | Linked active Felion Admin |
| `/department deactivate` | Soft-deactivates a Department by UUID for future assignments; `core` is reserved | Linked active Felion Admin |
| `/generation create` | Creates a name-only Generation | Linked active Felion Admin |
| `/generation edit` | Renames an active Generation by UUID | Linked active Felion Admin |
| `/generation deactivate` | Soft-deactivates a Generation by UUID for future assignments | Linked active Felion Admin |
| `/probation-team create` | Creates an active probation team | Linked active Felion Admin |
| `/probation-team edit` | Renames an active probation team by UUID | Linked active Felion Admin |
| `/probation-team deactivate` | Soft-deactivates a probation team by UUID | Linked active Felion Admin |
| `/role map` | Creates or updates a configurable role mapping; reference dimensions use UUID keys and Probation uses `Active` | Linked active Felion Admin |
| `/role sync` | Reconciles mapped and explicit Felion roles for one linked Discord user | Linked active Felion Admin |
| `/evaluation-criteria add` | Adds or reactivates a Peer/Mentor criterion | Linked active Felion Admin |
| `/evaluation-criteria rename` | Renames an active criterion while preserving its ID | Linked active Felion Admin |
| `/evaluation-criteria remove` | Deactivates a criterion without deleting history | Linked active Felion Admin |

## Verification interaction

`/verification publish` sends a button. The button opens a modal that accepts only a
StudentId. The bot normalizes the value, resolves it through `identity_registry`,
ensures both sides are not already linked, writes `discord_identity_links`, and audits
the link in one transaction.

## Not registered yet

Mentors, evaluation periods/submissions, evaluation reads, explicit role-assignment
administration, and PASS/FAIL decisions do not currently have Discord commands.
