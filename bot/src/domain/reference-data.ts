export type DepartmentReference = {
  name: string;
  slug: string;
};

export type MutableReferenceState = {
  active: boolean;
};

export function normalizeDepartmentReference(input: DepartmentReference): DepartmentReference {
  const name = input.name.trim().replace(/\s+/g, ' ');
  const slug = input.slug.trim().toLowerCase();

  if (name.length < 2 || name.length > 100 || slug.length < 2 || slug.length > 100 || !/^[a-z0-9-]+$/.test(slug)) {
    throw new Error('Department name or slug is invalid.');
  }
  if (slug === 'core') {
    throw new Error('The Core department is reserved.');
  }

  return { name, slug };
}

export function normalizeGenerationName(value: string): string {
  const name = value.trim().replace(/\s+/g, ' ');
  if (name.length < 2 || name.length > 100) {
    throw new Error('Generation name is invalid.');
  }

  return name;
}

export function normalizeReferenceId(value: string): string {
  const id = value.trim().toLowerCase();
  if (!/^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/.test(id)) {
    throw new Error('Reference ID is invalid.');
  }

  return id;
}

export function assertActiveReference(reference: MutableReferenceState): void {
  if (!reference.active) {
    throw new Error('The reference is already inactive.');
  }
}

export function assertMutableDepartment(reference: MutableReferenceState & { slug: string }): void {
  if (reference.slug === 'core') {
    throw new Error('The Core department is reserved.');
  }

  assertActiveReference(reference);
}
