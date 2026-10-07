import { z } from 'zod';
import { PaymentMethod, type PurchaseMembershipRequest } from '../../api/generated/model';
import { ClientsPurchaseMembershipBody } from '../../api/generated/zod/clients/clients.zod';
import dayjs from '../../lib/dayjs';

export const purchaseFormSchema = ClientsPurchaseMembershipBody.extend({
  planId: z.string().nullable().refine((value) => Boolean(value), 'Choose a plan'),
  startsOn: z.iso
    .date()
    .nullable()
    .refine((value) => value === null || !dayjs(value).isBefore(dayjs(), 'day'), 'A membership cannot start in the past'),
  paymentMethod: z.enum(PaymentMethod, { error: 'Choose how the client paid' }),
});

export type PurchaseFormValues = z.infer<typeof purchaseFormSchema>;

export const toPurchaseRequest = (values: PurchaseFormValues): PurchaseMembershipRequest => ({
  planId: values.planId,
  startsOn: values.startsOn,
  paymentMethod: values.paymentMethod,
});
