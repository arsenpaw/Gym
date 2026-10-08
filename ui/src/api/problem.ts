import { isAxiosError } from 'axios';
import { z } from 'zod';
import { isWebPage } from './http';

const ProblemDetails = z.object({
  title: z.string().nullish(),
  detail: z.string().nullish(),
  errors: z.record(z.string(), z.array(z.string())).nullish(),
});

const GENERIC = 'Something went wrong. Please try again.';
const UNEXPECTED = 'The server sent an unexpected response. Please try again later.';

const statusMessages: Record<number, string> = {
  401: 'Your session has expired. Please sign in again.',
  403: 'You do not have permission to do this.',
  404: 'It was not found. It may have been removed.',
  409: 'Someone else changed this record. Reload and try again.',
};

export const problemMessage = (error: unknown): string => {
  if (!isAxiosError(error)) return GENERIC;
  if (!error.response) return 'The server cannot be reached. Check your connection.';

  const { status, data } = error.response;
  const parsed = ProblemDetails.safeParse(data);
  const problem = parsed.success ? parsed.data : undefined;
  const fieldErrors = Object.values(problem?.errors ?? {}).flat();
  if (fieldErrors.length > 0) return fieldErrors.join(' ');
  return problem?.detail ?? statusMessages[status] ?? problem?.title ?? (isWebPage(error.response) ? UNEXPECTED : GENERIC);
};
