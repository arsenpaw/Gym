import { z } from 'zod';

const emailFormat = z.email();

export const requiredText = (label: string, max: number) =>
  z.string().trim().min(1, `${label} is required`).max(max, `${label} can be at most ${max} characters`);

export const optionalText = (label: string, max: number) =>
  z.string().trim().max(max, `${label} can be at most ${max} characters`);

export const optionalEmail = (max: number) =>
  z
    .string()
    .trim()
    .max(max, `Email can be at most ${max} characters`)
    .refine((value) => value === '' || emailFormat.safeParse(value).success, 'Enter a valid email');

export const phoneNumber = (max: number) =>
  z
    .string()
    .trim()
    .min(1, 'Phone is required')
    .max(max, `Phone can be at most ${max} characters`)
    .regex(/^\+?[\d\s\-()]+$/, 'Use digits, spaces, dashes, parentheses and a leading +')
    .refine((value) => {
      const digits = value.replace(/\D/g, '').length;
      return digits >= 10 && digits <= 15;
    }, 'Phone number must have 10 to 15 digits');

export const nullIfEmpty = (value: string) => (value === '' ? null : value);
