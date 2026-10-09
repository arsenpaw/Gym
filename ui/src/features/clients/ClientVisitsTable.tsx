import { DataTable } from 'mantine-datatable';
import type { MembershipResponse, VisitPageResponse } from '../../api/generated/model';
import { formatDateTime } from '../../lib/format';

export const visitPageSizes = [10, 25, 50];

type Props = {
  visits: VisitPageResponse | undefined;
  fetching: boolean;
  memberships: MembershipResponse[];
  page: number;
  pageSize: number;
  onPageChange: (page: number) => void;
  onPageSizeChange: (pageSize: number) => void;
};

export const ClientVisitsTable = ({ visits, fetching, memberships, page, pageSize, onPageChange, onPageSizeChange }: Props) => {
  const planNames = new Map(memberships.map((m) => [m.id, m.planName]));
  return (
    <DataTable
      withTableBorder
      borderRadius="md"
      striped
      minHeight={160}
      fetching={fetching}
      records={visits?.items ?? []}
      noRecordsText="No visits yet"
      totalRecords={visits?.totalCount ?? 0}
      page={page}
      onPageChange={onPageChange}
      recordsPerPage={pageSize}
      recordsPerPageOptions={visitPageSizes}
      onRecordsPerPageChange={onPageSizeChange}
      recordsPerPageLabel="Visits per page"
      paginationText={({ from, to, totalRecords }) => `${from}–${to} of ${totalRecords}`}
      columns={[
        { accessor: 'checkedInAt', title: 'Checked in', render: (v) => formatDateTime(v.checkedInAt) },
        { accessor: 'membershipId', title: 'Membership', render: (v) => planNames.get(v.membershipId) ?? '—' },
      ]}
    />
  );
};
