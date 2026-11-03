import { describe, expect, it } from 'vitest';
import {
  buildMime, encodeHeader, pickAddress, renderTemplate, resolveRecipients, templateVars, unknownPlaceholders,
} from '#app/domain/email.js';
import { embedImages, isEmbeddableImageUrl } from '#app/google/gmail.js';

const person = (clubEmail: string | null, personalEmail: string | null, fullName = 'Nguyễn Văn A') => ({
  fullName,
  studentId: 'SE123',
  clubEmail,
  personalEmail,
  generation: 7,
  department: { name: 'Tech' },
});

describe('recipients', () => {
  it('picks the address for each mode', () => {
    const both = person('a@gdg.edu', 'a@gmail.com');
    expect(pickAddress(both, 'club')).toBe('a@gdg.edu');
    expect(pickAddress(both, 'personal')).toBe('a@gmail.com');
    expect(pickAddress(person(null, 'b@gmail.com'), 'clubThenPersonal')).toBe('b@gmail.com');
    expect(pickAddress(person(null, 'b@gmail.com'), 'club')).toBeNull();
  });

  it('skips people without an address and sends a shared address once', () => {
    const people = [person(null, 'x@gmail.com', 'A'), person(null, 'X@gmail.com', 'B'), person(null, null, 'C')];
    expect(resolveRecipients(people, 'personal').map((r) => [r.person.fullName, r.email])).toEqual([['A', 'x@gmail.com']]);
  });
});

describe('templates', () => {
  const vars = templateVars(person('a@gdg.edu', null, 'Tom <b> & "Jerry"'), 'a@gdg.edu');

  it('fills variables, escaping them only in HTML', () => {
    expect(renderTemplate('Hi {{ name }} ({{studentId}}, K{{generation}})', vars, false)).toBe('Hi Tom <b> & "Jerry" (SE123, K7)');
    expect(renderTemplate('<p>{{name}}</p>', vars, true)).toBe('<p>Tom &#60;b&#62; &#38; &#34;Jerry&#34;</p>');
  });

  it('reports unknown variables once', () => {
    expect(unknownPlaceholders('{{name}} {{nmae}} {{ nmae }} {{department}} {{x}}')).toEqual(['nmae', 'x']);
  });
});

describe('mime', () => {
  it('encodes non-ASCII headers in short UTF-8 words and leaves ASCII alone', () => {
    expect(encodeHeader('Hello')).toBe('Hello');
    const subject = 'Thông báo tuyển thành viên khoá mới của câu lạc bộ GDG';
    const encoded = encodeHeader(subject);
    const words = encoded.split('\r\n ');
    expect(words.length).toBeGreaterThan(1);
    for (const word of words) expect(word.length).toBeLessThanOrEqual(75);
    const decoded = words.map((word) => Buffer.from(/^=\?UTF-8\?B\?(.*)\?=$/.exec(word)![1]!, 'base64').toString()).join('');
    expect(decoded).toBe(subject);
  });

  it('cannot inject headers through the subject', () => {
    expect(encodeHeader('Hi\r\nBcc: evil@x.com')).not.toContain('\r\nBcc');
  });

  it('nests html, inline images and attachments', () => {
    const mime = buildMime({
      from: { email: 'club@gdg.edu', name: 'GDG Club' },
      to: 'a@gdg.edu',
      subject: 'Hi',
      html: '<p>Xin chào</p><img src="cid:img0@felion">',
      inline: [{ contentType: 'image/png', data: Buffer.from('png'), contentId: 'img0@felion' }],
      attachments: [{ contentType: 'application/pdf', data: Buffer.alloc(200, 1), filename: "Kế hoạch (v2)'.pdf" }],
    });
    const split = mime.indexOf('\r\n\r\n');
    const head = mime.slice(0, split);
    const body = mime.slice(split + 4);
    expect(head).toContain('To: a@gdg.edu');
    expect(head).toMatch(/^From: =\?UTF-8\?B\?R0RHIENsdWI=\?= <club@gdg.edu>/);
    const mixed = /boundary="([^"]+)"/.exec(head)![1]!;
    expect(body.trimEnd().endsWith(`--${mixed}--`)).toBe(true);
    expect(body).toContain('Content-Type: multipart/related');
    expect(body).toContain('Content-ID: <img0@felion>');
    expect(body).toContain(`filename*=UTF-8''K%E1%BA%BF%20ho%E1%BA%A1ch%20%28v2%29%27.pdf`);
    expect(body).toContain(Buffer.from('<p>Xin chào</p><img src="cid:img0@felion">').toString('base64'));
    for (const line of mime.split('\r\n')) expect(line.length).toBeLessThanOrEqual(998);
    expect(mime.split('\r\n').filter((line) => /^[A-Za-z0-9+/=]+$/.test(line)).every((line) => line.length <= 76)).toBe(true);
  });
});

describe('images', () => {
  it('only fetches Google-hosted https images', () => {
    expect(isEmbeddableImageUrl('https://lh7-rt.googleusercontent.com/docsz/abc')).toBe(true);
    expect(isEmbeddableImageUrl('http://lh7-rt.googleusercontent.com/docsz/abc')).toBe(false);
    expect(isEmbeddableImageUrl('https://googleusercontent.com.evil.com/a')).toBe(false);
    expect(isEmbeddableImageUrl('https://169.254.169.254/latest')).toBe(false);
  });

  it('turns data URIs into cid parts and keeps other links', async () => {
    const png = Buffer.from('fake png').toString('base64');
    const html = `<img src="data:image/png;base64,${png}"><img src='data:image/png;base64,${png}'><img src="https://example.com/a.png">`;
    const result = await embedImages(html);
    expect(result.inline).toHaveLength(1);
    expect(result.inline[0]!.data.toString()).toBe('fake png');
    expect(result.html).toBe(`<img src="cid:img0@felion"><img src='cid:img0@felion'><img src="https://example.com/a.png">`);
  });
});
