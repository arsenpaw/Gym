import { zodResolver } from '@hookform/resolvers/zod';
import { Button, Group, Modal, Stack } from '@mantine/core';
import { notifications } from '@mantine/notifications';
import { useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { NumberInput, TextInput } from 'react-hook-form-mantine';
import {
  getMembershipPlansGetQueryKey,
  getMembershipPlansListQueryKey,
  useMembershipPlansCreate,
  useMembershipPlansUpdate,
} from '../../api/generated/endpoints/membership-plans/membership-plans';
import type { MembershipPlanResponse } from '../../api/generated/model';
import { CURRENCY } from '../../lib/format';
import { planFormSchema, toPlanRequest, type PlanFormValues } from './planForm';

type Props = { opened: boolean; onClose: () => void; plan?: MembershipPlanResponse };

export const PlanFormModal = ({ opened, onClose, plan }: Props) => (
  <Modal opened={opened} onClose={onClose} title={plan ? 'Edit plan' : 'New plan'} centered>
    {opened && <PlanForm plan={plan} onDone={onClose} />}
  </Modal>
);

const PlanForm = ({ plan, onDone }: { plan?: MembershipPlanResponse; onDone: () => void }) => {
  const queryClient = useQueryClient();
  const { control, handleSubmit, formState } = useForm<PlanFormValues>({
    resolver: zodResolver(planFormSchema),
    defaultValues: {
      name: plan?.name ?? '',
      price: plan?.price,
      validityDays: plan?.validityDays ?? 30,
      visitLimit: plan?.visitLimit ?? null,
    },
  });
  const onSuccess = async () => {
    await queryClient.invalidateQueries({ queryKey: getMembershipPlansListQueryKey() });
    if (plan) await queryClient.invalidateQueries({ queryKey: getMembershipPlansGetQueryKey(plan.id) });
    notifications.show({ color: 'teal', message: plan ? 'Plan updated' : 'Plan created' });
    onDone();
  };
  const create = useMembershipPlansCreate({ mutation: { onSuccess } });
  const update = useMembershipPlansUpdate({ mutation: { onSuccess } });

  const submit = handleSubmit(async (values) => {
    const data = toPlanRequest(values);
    await (plan ? update.mutateAsync({ id: plan.id, data }) : create.mutateAsync({ data })).catch(() => undefined);
  });

  return (
    <form onSubmit={submit} noValidate>
      <Stack>
        <TextInput control={control} name="name" label="Name" placeholder="Monthly unlimited" withAsterisk data-autofocus />
        <NumberInput control={control} name="price" label={`Price, ${CURRENCY}`} withAsterisk min={0} decimalScale={2} thousandSeparator=" " />
        <NumberInput control={control} name="validityDays" label="Valid for, days" withAsterisk min={1} allowDecimal={false} />
        <NumberInput
          control={control}
          name="visitLimit"
          label="Visit limit"
          description="Leave empty for unlimited visits"
          min={1}
          allowDecimal={false}
        />
        <Group justify="flex-end">
          <Button variant="default" onClick={onDone}>Cancel</Button>
          <Button type="submit" loading={formState.isSubmitting}>Save</Button>
        </Group>
      </Stack>
    </form>
  );
};
