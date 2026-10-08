import { zodResolver } from '@hookform/resolvers/zod';
import { Button, Group, Modal, Stack, Text } from '@mantine/core';
import { notifications } from '@mantine/notifications';
import { useQueryClient } from '@tanstack/react-query';
import { useForm, useWatch } from 'react-hook-form';
import { SegmentedControl, Select, TextInput } from 'react-hook-form-mantine';
import { QueryErrorAlert } from '../../api/QueryErrorAlert';
import { useClientsPurchaseMembership } from '../../api/generated/endpoints/clients/clients';
import { useMembershipPlansList } from '../../api/generated/endpoints/membership-plans/membership-plans';
import { PaymentMethod } from '../../api/generated/model';
import { today } from '../../lib/dates';
import { formatMoney } from '../../lib/format';
import { purchaseFormSchema, toPurchaseRequest, type PurchaseFormValues } from './purchaseForm';
import { refreshClient } from './refreshClient';

type Props = { opened: boolean; onClose: () => void; clientId: string };

export const PurchaseMembershipModal = ({ opened, onClose, clientId }: Props) => (
  <Modal opened={opened} onClose={onClose} title="Sell membership" centered>
    {opened && <PurchaseForm clientId={clientId} onDone={onClose} />}
  </Modal>
);

const PurchaseForm = ({ clientId, onDone }: { clientId: string; onDone: () => void }) => {
  const queryClient = useQueryClient();
  const plans = useMembershipPlansList({ includeInactive: false });
  const { control, handleSubmit, formState } = useForm<PurchaseFormValues>({
    resolver: zodResolver(purchaseFormSchema),
    defaultValues: { planId: null, startsOn: today(), paymentMethod: PaymentMethod.Card },
  });
  const planId = useWatch({ control, name: 'planId' });
  const chosen = plans.data?.find((plan) => plan.id === planId);
  const purchase = useClientsPurchaseMembership({
    mutation: {
      meta: { errorTitle: 'Could not sell the membership' },
      onSuccess: async () => {
        await refreshClient(queryClient, clientId);
        notifications.show({ color: 'teal', message: 'Membership sold' });
        onDone();
      },
    },
  });

  const submit = handleSubmit(async (values) => {
    await purchase.mutateAsync({ id: clientId, data: toPurchaseRequest(values) }).catch(() => undefined);
  });

  return (
    <form onSubmit={submit} noValidate>
      <Stack>
        <Select
          control={control}
          name="planId"
          label="Plan"
          placeholder={plans.isPending ? 'Loading plans…' : 'Choose a plan'}
          data={(plans.data ?? []).map((plan) => ({ value: plan.id, label: `${plan.name} — ${formatMoney(plan.price)}` }))}
          withAsterisk
          searchable
        />
        {plans.isError && <QueryErrorAlert title="Could not load plans" error={plans.error} />}
        {chosen && (
          <Text size="sm" c="dimmed">
            Valid for {chosen.validityDays} days · {chosen.visitLimit ? `${chosen.visitLimit} visits` : 'unlimited visits'}
          </Text>
        )}
        <TextInput control={control} name="startsOn" type="date" label="Starts on" min={today()} withAsterisk />
        <SegmentedControl control={control} name="paymentMethod" data={[PaymentMethod.Card, PaymentMethod.Cash]} />
        <Group justify="flex-end">
          <Button variant="default" onClick={onDone}>Cancel</Button>
          <Button type="submit" loading={formState.isSubmitting}>Sell</Button>
        </Group>
      </Stack>
    </form>
  );
};
