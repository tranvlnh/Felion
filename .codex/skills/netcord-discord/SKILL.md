---
name: netcord-discord
description: Use when implementing or changing NetCord Discord commands, buttons, modals, role synchronization, guild operations, or Discord interaction hosting for this repository.
---
# NetCord Discord skill

- Treat official NetCord documentation as the source of truth; APIs may change. Never fabricate method/type names.
- This project uses one configured guild. Validate guild context on every guild-specific interaction.
- Prefer application commands and component interactions. Verification uses button -> modal -> StudentId.
- Keep handlers thin; resolve input/context and call application use cases.
- Ephemeral responses are preferred for identity and administrative feedback containing member information.
- Application authorization comes from the linked DB Member/Position, not from Discord roles alone.
- Role sync is idempotent: calculate desired managed roles, add missing managed roles, remove obsolete managed roles, and leave unrelated/manual roles untouched.
- Before role mutation, verify bot permissions and role hierarchy. Return actionable errors and audit administrative actions.
- Discord API side effects can fail after DB commit. Use retryable sync jobs rather than claiming cross-system atomicity.
- Never log bot tokens or interaction secrets.
