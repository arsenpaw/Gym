import { z } from 'zod';
import type { ClientDetailsResponse, ClientRequest } from '../../api/generated/model';
import {
  ClientsRegisterBody,
  clientsRegisterBodyEmailMax,
  clientsRegisterBodyFirstNameMax,
  clientsRegisterBodyLastNameMax,
  clientsRegisterBodyMiddleNameMax,
  clientsRegisterBodyPhoneMax,
} from '../../api/generated/zod/clients/clients.zod';
import dayjs from '../../lib/dayjs';
import { nullIfEmpty, optionalPhone, optionalText, requiredEmail, requiredText } from '../../lib/formSchemas';

export const clientFormSchema = ClientsRegisterBody.extend({
  firstName: requiredText('First name', clientsRegisterBodyFirstNameMax),
  lastName: requiredText('Last name', clientsRegisterBodyLastNameMax),
  middleName: optionalText('Middle name', clientsRegisterBodyMiddleNameMax),
  dateOfBirth: z
    .string()
    .regex(/^\d{4}-\d{2}-\d{2}$/, 'Enter a full date of birth')
    .refine((value) => !dayjs(value).isAfter(dayjs(), 'day'), 'Date of birth cannot be in the future'),
  email: requiredEmail(clientsRegisterBodyEmailMax),
  phone: optionalPhone(clientsRegisterBodyPhoneMax),
});

export type ClientFormValues = z.infer<typeof clientFormSchema>;

export const clientFormDefaults = (client?: ClientDetailsResponse): ClientFormValues => ({
  firstName: client?.firstName ?? '',
  lastName: client?.lastName ?? '',
  middleName: client?.middleName ?? '',
  dateOfBirth: client?.dateOfBirth ?? '',
  email: client?.email ?? '',
  phone: client?.phone ?? '',
});

export const toClientRequest = (values: ClientFormValues): ClientRequest => ({
  firstName: values.firstName,
  lastName: values.lastName,
  middleName: nullIfEmpty(values.middleName),
  dateOfBirth: values.dateOfBirth,
  email: values.email,
  phone: nullIfEmpty(values.phone),
});
