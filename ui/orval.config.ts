import { defineConfig } from 'orval';

export default defineConfig({
  fitnessclub: {
    input: { target: './openapi/fitnessclub.json' },
    output: {
      mode: 'tags-split',
      target: './src/api/generated/endpoints',
      schemas: './src/api/generated/model',
      client: 'react-query',
      httpClient: 'axios',
      mock: { generators: [{ type: 'msw' }] },
      clean: true,
      override: {
        mutator: { path: './src/api/http.ts', name: 'http' },
      },
    },
  },
  fitnessclubZod: {
    input: { target: './openapi/fitnessclub.json' },
    output: {
      mode: 'tags-split',
      client: 'zod',
      target: './src/api/generated/zod',
      fileExtension: '.zod.ts',
      clean: true,
    },
  },
});
