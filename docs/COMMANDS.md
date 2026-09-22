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
| `/department create` | Creates a Department; `core` is reserved | Linked active Felion Admin |
| `/generation create` | Creates a name-only Generation | Linked active Felion Admin |
| `/role map` | Creates or updates a configurable role mapping | Linked active Felion Admin |
| `/evaluation-criteria add` | Adds or reactivates a Peer/Mentor criterion | Linked active Felion Admin |
| `/evaluation-criteria rename` | Renames an active criterion while preserving its ID | Linked active Felion Admin |
| `/evaluation-criteria remove` | Deactivates a criterion without deleting history | Linked active Felion Admin |

## Verification interaction

`/verification publish` sends a button. The button opens a modal that accepts only a
StudentId. The bot normalizes the value, resolves it through `identity_registry`,
ensures both sides are not already linked, writes `discord_identity_links`, and audits
the link in one transaction.

## Not registered yet

Probation teams/candidates/mentors, evaluation periods/submissions, evaluation reads,
role synchronization, and PASS/FAIL decisions do not currently have Discord commands.
