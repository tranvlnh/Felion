FROM node:22-bookworm-slim AS build

WORKDIR /app

COPY package.json package-lock.json ./
COPY backend/package.json backend/
COPY frontend/package.json frontend/
RUN npm ci

COPY backend backend
COPY frontend frontend
RUN npm run build

FROM node:22-bookworm-slim AS runtime

ENV NODE_ENV=production
WORKDIR /app

COPY package.json package-lock.json ./
COPY backend/package.json backend/
COPY frontend/package.json frontend/
RUN npm ci --omit=dev -w backend && npm cache clean --force

COPY backend/prisma.config.ts backend/
COPY backend/prisma backend/prisma
COPY --from=build /app/backend/dist backend/dist
COPY --from=build /app/frontend/dist frontend/dist

USER node
WORKDIR /app/backend
EXPOSE 3000

CMD ["sh", "-c", "../node_modules/.bin/prisma migrate deploy && node --enable-source-maps dist/index.js"]
