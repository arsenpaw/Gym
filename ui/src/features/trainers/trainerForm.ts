import type { z } from 'zod';
import type { TrainerRequest, TrainerResponse } from '../../api/generated/model';
import {
  TrainersHireBody,
  trainersHireBodyEmailMax,
  trainersHireBodyFirstNameMax,
  trainersHireBodyLastNameMax,
  trainersHireBodyMiddleNameMax,
  trainersHireBodyPhoneMax,
  trainersHireBodySpecializationMax,
} from '../../api/generated/zod/trainers/trainers.zod';
import { nullIfEmpty, optionalEmail, optionalText, phoneNumber, requiredText } from '../../lib/formSchemas';

export const trainerFormSchema = TrainersHireBody.extend({
  firstName: requiredText('First name', trainersHireBodyFirstNameMax),
  lastName: requiredText('Last name', trainersHireBodyLastNameMax),
  middleName: optionalText('Middle name', trainersHireBodyMiddleNameMax),
  phone: phoneNumber(trainersHireBodyPhoneMax),
  email: optionalEmail(trainersHireBodyEmailMax),
  specialization: requiredText('Specialization', trainersHireBodySpecializationMax),
});

export type TrainerFormValues = z.infer<typeof trainerFormSchema>;

export const trainerFormDefaults = (trainer?: TrainerResponse): TrainerFormValues => ({
  firstName: trainer?.firstName ?? '',
  lastName: trainer?.lastName ?? '',
  middleName: trainer?.middleName ?? '',
  phone: trainer?.phone ?? '',
  email: trainer?.email ?? '',
  specialization: trainer?.specialization ?? '',
});

export const toTrainerRequest = (values: TrainerFormValues): TrainerRequest => ({
  firstName: values.firstName,
  lastName: values.lastName,
  middleName: nullIfEmpty(values.middleName),
  phone: values.phone,
  email: nullIfEmpty(values.email),
  specialization: values.specialization,
});
