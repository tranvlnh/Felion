export type LegacyData = {
  departments: { id: string; name: string; active: boolean; created_at: Date }[];
  generations: { id: string; name: string }[];
  members: {
    id: string;
    student_id: string;
    full_name: string;
    club_email: string;
    department_id: string;
    generation_id: string;
    position: 'Admin' | 'Core' | 'Member';
    status: 'Active' | 'Inactive';
    created_at: Date;
  }[];
  candidates: {
    id: string;
    student_id: string;
    full_name: string;
    department_id: string;
    generation_id: string;
    status: 'Active' | 'Passed' | 'Failed' | 'Inactive';
    created_at: Date;
  }[];
  links: { discord_user_id: string; subject_type: 'Member' | 'ProbationCandidate'; subject_id: string }[];
  mappings: { kind: string; key: string; discord_role_id: string }[];
  assignments: { subject_type: 'Member' | 'ProbationCandidate'; subject_id: string; discord_role_id: string }[];
};

export type NewPerson = {
  id: string;
  kind: 'Candidate' | 'Member';
  status: 'Active' | 'Inactive';
  studentId: string;
  fullName: string;
  clubEmail: string | null;
  discordUserId: string | null;
  isAdmin: boolean;
  departmentId: string;
  generation: number;
  createdAt: Date;
};

export type TransformResult = {
  people: NewPerson[];
  settings: { probationRoleId: string | null; memberRoleId: string | null };
  // Roles the old bot would have synced; used only for people we cannot read from Discord.
  legacyRoles: Map<string, string[]>;
  problems: string[];
  warnings: string[];
};

export function parseGenerationNumber(name: string): number | null {
  const match = /(\d+)\s*$/.exec(name.trim());
  const value = match ? Number(match[1]) : NaN;
  return Number.isInteger(value) && value >= 1 && value <= 999 ? value : null;
}

const mappingKey = (kind: string, key: string) => `${kind}\u0000${key}`;

export function transformLegacy(data: LegacyData): TransformResult {
  const problems: string[] = [];
  const warnings: string[] = [];

  // Department names are unique now that slugs are gone.
  const departmentNames = new Set<string>();
  for (const department of data.departments) {
    if (departmentNames.has(department.name)) problems.push(`Department name "${department.name}" is used more than once.`);
    departmentNames.add(department.name);
  }

  const generationById = new Map<string, number>();
  for (const generation of data.generations) {
    const value = parseGenerationNumber(generation.name);
    if (value === null) {
      problems.push(`Generation "${generation.name}" (${generation.id}) has no trailing number.`);
    } else {
      generationById.set(generation.id, value);
    }
  }

  const linkBySubject = new Map(data.links.map((link) => [`${link.subject_type}:${link.subject_id}`, link.discord_user_id]));

  const memberStudentIds = new Set(data.members.map((member) => member.student_id));
  for (const candidate of data.candidates) {
    if (memberStudentIds.has(candidate.student_id)) {
      problems.push(`StudentId ${candidate.student_id} exists as both a Member and a ProbationCandidate.`);
    }
  }

  const mappingRole = new Map(data.mappings.map((mapping) => [mappingKey(mapping.kind, mapping.key), mapping.discord_role_id]));
  const explicitRoles = new Map<string, string[]>();
  for (const assignment of data.assignments) {
    const list = explicitRoles.get(assignment.subject_id) ?? [];
    list.push(assignment.discord_role_id);
    explicitRoles.set(assignment.subject_id, list);
  }

  const people: NewPerson[] = [];
  const legacyRoles = new Map<string, string[]>();
  const desired = (id: string, keys: [string, string][]) => {
    const roles = new Set(explicitRoles.get(id) ?? []);
    for (const [kind, key] of keys) {
      const roleId = mappingRole.get(mappingKey(kind, key));
      if (roleId) roles.add(roleId);
    }
    legacyRoles.set(id, [...roles].sort());
  };

  for (const member of data.members) {
    people.push({
      id: member.id,
      kind: 'Member',
      status: member.status,
      studentId: member.student_id,
      fullName: member.full_name,
      clubEmail: member.club_email.trim().toLowerCase(),
      discordUserId: linkBySubject.get(`Member:${member.id}`) ?? null,
      isAdmin: member.position === 'Admin',
      departmentId: member.department_id,
      generation: generationById.get(member.generation_id) ?? 0,
      createdAt: member.created_at,
    });
    if (member.status === 'Active') {
      desired(member.id, [
        ['Position', member.position],
        ['Department', member.department_id],
        ['Generation', member.generation_id],
      ]);
    } else {
      legacyRoles.set(member.id, []);
    }
  }

  for (const candidate of data.candidates) {
    const active = candidate.status === 'Active';
    people.push({
      id: candidate.id,
      kind: 'Candidate',
      status: active ? 'Active' : 'Inactive',
      studentId: candidate.student_id,
      fullName: candidate.full_name,
      clubEmail: null,
      discordUserId: linkBySubject.get(`ProbationCandidate:${candidate.id}`) ?? null,
      isAdmin: false,
      departmentId: candidate.department_id,
      generation: generationById.get(candidate.generation_id) ?? 0,
      createdAt: candidate.created_at,
    });
    if (active) {
      desired(candidate.id, [
        ['Probation', 'Active'],
        ['Department', candidate.department_id],
        ['Generation', candidate.generation_id],
      ]);
    } else {
      legacyRoles.set(candidate.id, []);
    }
  }

  return {
    people,
    settings: {
      probationRoleId: mappingRole.get(mappingKey('Probation', 'Active')) ?? null,
      memberRoleId: mappingRole.get(mappingKey('Position', 'Member')) ?? null,
    },
    legacyRoles,
    problems,
    warnings,
  };
}
