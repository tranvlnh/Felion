import { randomBytes } from 'node:crypto';

export const addressModes = ['club', 'personal', 'clubThenPersonal'] as const;
export type AddressMode = (typeof addressModes)[number];

export type EmailPerson = {
  fullName: string;
  studentId: string;
  clubEmail: string | null;
  personalEmail: string | null;
  generation: number;
  department: { name: string };
};

export function pickAddress(person: Pick<EmailPerson, 'clubEmail' | 'personalEmail'>, mode: AddressMode): string | null {
  if (mode === 'club') return person.clubEmail;
  if (mode === 'personal') return person.personalEmail;
  return person.clubEmail ?? person.personalEmail;
}

// One email per address: two people sharing a personal email get it once.
export function resolveRecipients<P extends EmailPerson>(people: readonly P[], mode: AddressMode): { person: P; email: string }[] {
  const seen = new Set<string>();
  return people.flatMap((person) => {
    const email = pickAddress(person, mode)?.toLowerCase();
    if (!email || seen.has(email)) return [];
    seen.add(email);
    return [{ person, email }];
  });
}

export const templateKeys = ['name', 'studentId', 'email', 'department', 'generation'] as const;
type TemplateVars = Record<(typeof templateKeys)[number], string>;

export function templateVars(person: EmailPerson, email: string): TemplateVars {
  return {
    name: person.fullName,
    studentId: person.studentId,
    email,
    department: person.department.name,
    generation: String(person.generation),
  };
}

const placeholder = /\{\{\s*(\w+)\s*\}\}/g;

export function unknownPlaceholders(text: string): string[] {
  const keys = [...text.matchAll(placeholder)].map((match) => match[1] ?? '');
  return [...new Set(keys.filter((key) => !(templateKeys as readonly string[]).includes(key)))];
}

export function escapeHtml(text: string): string {
  return text.replace(/[&<>"']/g, (char) => `&#${char.charCodeAt(0)};`);
}

export function renderTemplate(text: string, vars: TemplateVars, html: boolean): string {
  return text.replace(placeholder, (whole, key: string) => {
    const value = (vars as Record<string, string>)[key];
    if (value === undefined) return whole;
    return html ? escapeHtml(value) : value;
  });
}

export type MimePart = { contentType: string; data: Buffer; filename?: string; contentId?: string };

export type MimeMessage = {
  from: { email: string; name?: string | undefined };
  to: string;
  subject: string;
  html: string;
  inline: readonly MimePart[];
  attachments: readonly MimePart[];
};

const crlf = '\r\n';
const isPrintableAscii = (text: string) => /^[\x20-\x7e]*$/.test(text);

// RFC 2047 encoded words of at most 75 chars, split on whole characters so UTF-8 is never cut.
export function encodeHeader(text: string, force = false): string {
  if (!force && isPrintableAscii(text)) return text;
  const words: string[] = [];
  let chunk = '';
  for (const char of text) {
    if (chunk && Buffer.byteLength(chunk + char) > 45) {
      words.push(chunk);
      chunk = '';
    }
    chunk += char;
  }
  words.push(chunk);
  return words.map((word) => `=?UTF-8?B?${Buffer.from(word).toString('base64')}?=`).join(`${crlf} `);
}

const base64Lines = (data: Buffer) => data.toString('base64').replace(/.{76}(?=.)/g, `$&${crlf}`);

// RFC 5987: encodeURIComponent leaves ' ( ) * unescaped, which attr-char does not allow.
const encodeFilename = (name: string) => encodeURIComponent(name)
  .replace(/['()*]/g, (char) => `%${char.charCodeAt(0).toString(16).toUpperCase()}`);

function part(headers: string[], body: Buffer): string {
  return [...headers, 'Content-Transfer-Encoding: base64', '', base64Lines(body)].join(crlf);
}

function multipart(boundary: string, parts: string[]): string {
  return [...parts.map((body) => `--${boundary}${crlf}${body}`), `--${boundary}--`].join(crlf);
}

export function buildMime(message: MimeMessage): string {
  const mixed = `felion_m_${randomBytes(12).toString('hex')}`;
  const related = `felion_r_${randomBytes(12).toString('hex')}`;
  const from = message.from.name ? `${encodeHeader(message.from.name, true)} <${message.from.email}>` : message.from.email;

  const html = part(['Content-Type: text/html; charset=UTF-8'], Buffer.from(message.html));
  const inline = message.inline.map((image) => part([
    `Content-Type: ${image.contentType}`,
    `Content-ID: <${image.contentId}>`,
    'Content-Disposition: inline',
  ], image.data));
  const attachments = message.attachments.map((file) => part([
    `Content-Type: ${file.contentType}`,
    `Content-Disposition: attachment; filename*=UTF-8''${encodeFilename(file.filename ?? 'file')}`,
  ], file.data));

  return [
    `From: ${from}`,
    `To: ${message.to}`,
    `Subject: ${encodeHeader(message.subject)}`,
    'MIME-Version: 1.0',
    `Content-Type: multipart/mixed; boundary="${mixed}"`,
    '',
    multipart(mixed, [
      `Content-Type: multipart/related; boundary="${related}"${crlf}${crlf}${multipart(related, [html, ...inline])}`,
      ...attachments,
    ]),
    '',
  ].join(crlf);
}

// Wraps the editor's fragment so clients render it as UTF-8 HTML.
export function htmlDocument(body: string): string {
  return `<!doctype html><html><head><meta charset="utf-8"></head><body>${body}</body></html>`;
}
