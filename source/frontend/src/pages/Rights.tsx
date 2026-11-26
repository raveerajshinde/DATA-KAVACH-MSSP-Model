import { useNavigate, useParams } from 'react-router-dom';
import { Alert, Box, Card, CardContent, List, ListItem, ListItemText, Typography } from '@mui/material';
import { api, fmtDateTime, qs } from '../api';
import { useApp, useLoad } from '../context';
import { DueChip, NoOrg, PageHeader, StatusChip } from '../components/Common';
import EntityTable from '../components/EntityTable';
import RecordDetail from '../components/RecordDetail';

function Trail({ id, tick }: { id: number; tick: number }) {
  const { orgId } = useApp();
  const [d] = useLoad<any>(() => api.get(`/api/e/requestEvents?${qs({ orgId, parentId: id, pageSize: 200, sort: 'id', desc: false })}`), [id, tick], { rows: [] });
  return (
    <Card><CardContent>
      <Typography variant="subtitle2">Audit trail</Typography>
      <List dense>{d.rows.map((e: any) => <ListItem key={e.id} divider><ListItemText primary={e.notes} secondary={`${fmtDateTime(e.createdAt)} - ${e.createdBy}`} /></ListItem>)}</List>
    </CardContent></Card>
  );
}

export default function Rights() {
  const { id } = useParams();
  const { orgId, org } = useApp();
  const nav = useNavigate();
  if (!orgId) return <><PageHeader title="Data Principal Rights" /><NoOrg /></>;
  if (id) return (
    <RecordDetail entityKey="rightsRequests" id={Number(id)} backTo="/rights" title={r => `${r.reference} - ${r.requestType}`}
      subtitle={r => <span><StatusChip value={r.status} /> <DueChip due={r.dueAt} done={!!r.closedAt} /></span>}
      side={(r) => <Trail id={Number(id)} tick={r.updatedAt ? new Date(r.updatedAt).getTime() : 0} />} />
  );
  return (
    <Box>
      <PageHeader title="Data Principal Rights" subtitle="Access (s.11), correction and erasure (s.12), grievance (s.13), nomination (s.14) and consent withdrawal requests with verification, assignment, deadlines and closure evidence." />
      <Alert severity="info" variant="outlined" sx={{ mb: 2 }}>Grievances are due within the published response period ({org?.grievanceResponseDays ?? 30} days for this organisation, max 90 days under Rule 14(3)). Include the DPO / contact details in every response (Rule 9). Data principals must exhaust this mechanism before approaching the Board.</Alert>
      <EntityTable entityKey="rightsRequests" onRowClick={r => nav(`/rights/${r.id}`)} createLabel="Log request" columns={['reference', 'requestType', 'principalName', 'receivedAt', 'dueAt', 'status', 'assignedTo']} />
    </Box>
  );
}
