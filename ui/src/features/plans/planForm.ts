import { z } from 'zod';
import type { MembershipPlanRequest } from '../../api/generated/model';
import {
  MembershipPlansCreateBody,
  membershipPlansCreateBodyNameMax,
  membershipPlansCreateBodyPriceMax,
  membershipPlansCreateBodyPriceMin,
  membershipPlansCreateBodyValidityDaysMax,
  membershipPlansCreateBodyVisitLimitMax,
} from '../../api/generated/zod/membership-plans/membership-plans.zod';
import { requiredText } from '../../lib/formSchemas';

export const planFormSchema = MembershipPlansCreateBody.extend({
  name: requiredText('Name', membershipPlansCreateBodyNameMax),
  price: z
    .number({ error: 'Price is required' })
    .min(membershipPlansCreateBodyPriceMin, 'Price must be greater than 0')
    .max(membershipPlansCreateBodyPriceMax, `Price can be at most ${membershipPlansCreateBodyPriceMax}`),
  validityDays: z
    .number({ error: 'Validity is required' })
    .int('Validity must be a whole number of days')
    .min(1, 'Validity must be at least 1 day')
    .max(membershipPlansCreateBodyValidityDaysMax, `Validity can be at most ${membershipPlansCreateBodyValidityDaysMax} days`),
  visitLimit: z.union(
    [z.literal(''), z.null(), z.number().int().min(1).max(membershipPlansCreateBodyVisitLimitMax)],
    { error: `Visit limit must be a whole number from 1 to ${membershipPlansCreateBodyVisitLimitMax}` },
  ),
});

export type PlanFormValues = z.infer<typeof planFormSchema>;

export const toPlanRequest = (values: PlanFormValues): MembershipPlanRequest => ({
  name: values.name,
  price: values.price,
  validityDays: values.validityDays,
  visitLimit: values.visitLimit === '' ? null : values.visitLimit,
});
