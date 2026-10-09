import { z } from 'zod';

const emailFormat = z.email();
const phoneCharacters = /^\+?[\d\s\-()]+$/;
const phoneCharactersMessage = 'Use digits, spaces, dashes, parentheses and a leading +';
const phoneDigitsMessage = 'Phone number must have 10 to 15 digits';

const hasPhoneDigits = (value: string) => {
  const digits = value.replace(/\D/g, '').length;
  return digits >= 10 && digits <= 15;
};

export const requiredText = (label: string, max: number) =>
  z.string().trim().min(1, `${label} is required`).max(max, `${label} can be at most ${max} characters`);

export const optionalText = (label: string, max: number) =>
  z.string().trim().max(max, `${label} can be at most ${max} characters`);

export const requiredEmail = (max: number) =>
  z
    .string()
    .trim()
    .min(1, 'Email is required')
    .max(max, `Email can be at most ${max} characters`)
    .refine((value) => value === '' || emailFormat.safeParse(value).success, 'Enter a valid email');

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
    .regex(phoneCharacters, phoneCharactersMessage)
    .refine(hasPhoneDigits, phoneDigitsMessage);

export const optionalPhone = (max: number) =>
  z
    .string()
    .trim()
    .max(max, `Phone can be at most ${max} characters`)
    .refine((value) => value === '' || phoneCharacters.test(value), phoneCharactersMessage)
    .refine((value) => value === '' || hasPhoneDigits(value), phoneDigitsMessage);

export const nullIfEmpty = (value: string) => (value === '' ? null : value);
