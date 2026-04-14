FROM node:22-alpine AS build
WORKDIR /app
COPY package.json .npmrc .pnpmfile.cjs ./
RUN npm install -g pnpm@7.33.7
RUN pnpm install --ignore-scripts

COPY . .
RUN node_modules/.bin/nuxt build

FROM node:22-alpine AS runtime
WORKDIR /app

COPY --from=build /app/.output ./output

EXPOSE 3000
ENV PORT=3000
ENV NODE_ENV=production
ENV HOST=0.0.0.0

CMD ["node", "output/server/index.mjs"]
