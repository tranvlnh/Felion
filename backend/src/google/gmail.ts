import { createTransport } from 'nodemailer';
import type { Config } from '#app/config.js';
import type { MimePart } from '#app/domain/email.js';
import { AppError } from '#app/errors.js';

const maxImageBytes = 5 * 1024 * 1024;
export const contentTypePattern = /^[\w.+-]+\/[\w.+-]+$/;

export type GmailSender = { address: string; name: string | undefined; send: (mime: string, to: string) => Promise<void> };

// Gmail SMTP with an App Password; the message is built in domain/email.ts and sent as is.
export function createGmailSender(config: Config): GmailSender | null {
  const address = config.EMAIL_SENDER;
  const password = config.EMAIL_APP_PASSWORD;
  if (!address || !password) return null;
  // One pooled connection: sends are sequential, so this reuses the login instead of reconnecting per email.
  const transport = createTransport({
    pool: true,
    maxConnections: 1,
    host: 'smtp.gmail.com',
    port: 465,
    secure: true,
    auth: { user: address, pass: password },
  });

  return {
    address,
    name: config.EMAIL_SENDER_NAME || undefined,
    async send(mime, to) {
      try {
        await transport.sendMail({ envelope: { from: address, to }, raw: mime });
      } catch (error) {
        const message = error instanceof Error ? error.message : 'send failed';
        const hint = (error as { responseCode?: number }).responseCode === 535 ? ' (check EMAIL_SENDER and EMAIL_APP_PASSWORD)' : '';
        throw new AppError('EXTERNAL_SERVICE', `Gmail SMTP: ${message}${hint}`, { cause: error });
      }
    },
  };
}

// Only Google-hosted images (what Docs puts on the clipboard) are fetched, so this is no open proxy.
export function isEmbeddableImageUrl(src: string): boolean {
  try {
    const url = new URL(src);
    return url.protocol === 'https:' && url.hostname.endsWith('.googleusercontent.com');
  } catch {
    return false;
  }
}

async function loadImage(src: string): Promise<{ contentType: string; data: Buffer } | null> {
  const dataUri = /^data:([\w.+-]+\/[\w.+-]+);base64,([A-Za-z0-9+/=\s]+)$/.exec(src);
  if (dataUri) return { contentType: dataUri[1]!, data: Buffer.from(dataUri[2]!, 'base64') };
  if (!isEmbeddableImageUrl(src)) return null;

  const response = await fetch(src, { signal: AbortSignal.timeout(10_000), redirect: 'error' }).catch(() => null);
  const contentType = response?.headers.get('content-type')?.split(';')[0]?.trim() ?? '';
  if (!response?.ok || !contentType.startsWith('image/') || !contentTypePattern.test(contentType)) {
    throw new AppError('VALIDATION', 'Could not download an image pasted from Google Docs. Insert it as a file instead.');
  }
  const data = Buffer.from(await response.arrayBuffer());
  return { contentType, data };
}

// Turns pasted/inserted images into inline cid: parts, since Docs image links expire.
export async function embedImages(html: string): Promise<{ html: string; inline: MimePart[] }> {
  const inline: MimePart[] = [];
  const bySrc = new Map<string, string>();
  const sources = [...html.matchAll(/<img\b[^>]*?\ssrc\s*=\s*(["'])(.*?)\1/gi)].map((match) => match[2]!);

  for (const raw of new Set(sources)) {
    const src = raw.replaceAll('&amp;', '&');
    const image = await loadImage(src);
    if (!image) continue;
    if (image.data.length > maxImageBytes) throw new AppError('VALIDATION', 'Each image must be at most 5 MB.');
    const contentId = `img${inline.length}@felion`;
    inline.push({ ...image, contentId });
    bySrc.set(raw, contentId);
  }

  const replaced = html.replace(/(<img\b[^>]*?\ssrc\s*=\s*)(["'])(.*?)\2/gi, (whole, prefix: string, quote: string, src: string) => {
    const contentId = bySrc.get(src);
    return contentId ? `${prefix}${quote}cid:${contentId}${quote}` : whole;
  });
  return { html: replaced, inline };
}
