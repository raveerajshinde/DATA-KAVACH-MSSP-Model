import { useNavigate, useParams } from 'react-router-dom';
import { Alert, Box } from '@mui/material';
import { useApp } from '../context';
import { NoOrg, PageHeader, StatusChip } from '../components/Common';
import EntityTable from '../components/EntityTable';
import RecordDetail from '../components/RecordDetail';

export default function Vendors() {
  const { id } = useParams();
  const { orgId } = useApp();
  const nav = useNavigate();
  if (!orgId) return <><PageHeader title="Vendors" /><NoOrg /></>;
  if (id) return (
    <RecordDetail entityKey="vendors" id={Number(id)} backTo="/vendors" title={r => r.name} subtitle={r => <StatusChip value={r.riskRating} />}>
      {() => <EntityTable entityKey="vendorAssessments" parentId={Number(id)} title="Assessment history" createLabel="Add assessment" allowExport={false} />}
    </RecordDetail>
  );
  return (
    <Box>
      <PageHeader title="Vendors & Data Processors" subtitle="Track processors, data processing agreements and contractual safeguards. Processors may be engaged only under a valid contract (s.8(2)) with security safeguards (Rule 6(1)(f))." />
      <Alert severity="info" variant="outlined" sx={{ mb: 2 }}>Risk is suggested automatically when left blank: processors without a DPA are High; missing security or breach clauses are Medium. Set a breach notification window well inside the 72-hour Board deadline.</Alert>
      <EntityTable entityKey="vendors" allowImport onRowClick={r => nav(`/vendors/${r.id}`)} createLabel="Add vendor" />
    </Box>
  );
}
