import { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { Alert, Box, Button, Card, CardContent, Dialog, DialogActions, DialogContent, DialogTitle, Stack, TextField, Typography } from '@mui/material';
import { api, qs } from '../api';
import { useApp, useLoad } from '../context';
import { NoOrg, PageHeader, StatusChip } from '../components/Common';
import EntityTable from '../components/EntityTable';
import RecordDetail from '../components/RecordDetail';

function Heatmap({ dpiaId, tick }: { dpiaId: number; tick: number }) {
  const { orgId } = useApp();
  const [data] = useLoad<any>(() => api.get(`/api/e/dpiaRisks?${qs({ orgId, parentId: dpiaId, pageSize: 200 })}`), [dpiaId, tick], { rows: [] });
  const cell = (l: number, i: number) => data.rows.filter((r: any) => Number(r.likelihood) === l && Number(r.impact) === i).length;
  const color = (s: number) => (s >= 15 ? '#F8C9C9' : s >= 8 ? '#FFE9B0' : '#D4EDDA');
  return (
    <Box>
      <Typography variant="subtitle2" sx={{ mb: 1 }}>Inherent risk heat map</Typography>
      <Box sx={{ display: 'grid', gridTemplateColumns: '60px repeat(5, 1fr)', gap: 0.5 }}>
        {[5, 4, 3, 2, 1].map(i => [<Typography key={'l' + i} variant="caption" sx={{ alignSelf: 'center' }}>Impact {i}</Typography>, ...[1, 2, 3, 4, 5].map(l => (
          <Box key={`${i}-${l}`} sx={{ bgcolor: color(i * l), height: 36, borderRadius: 1, display: 'grid', placeItems: 'center', fontWeight: 700 }}>{cell(l, i) || ''}</Box>
        ))])}
        <Box />{[1, 2, 3, 4, 5].map(l => <Typography key={l} variant="caption" textAlign="center">L{l}</Typography>)}
      </Box>
    </Box>
  );
}

export default function Dpia() {
  const { id } = useParams();
  const { orgId, org, can, toast } = useApp();
  const nav = useNavigate();
  const [tick, setTick] = useState(0);
  const [approve, setApprove] = useState<{ open: boolean; conclusion: string }>({ open: false, conclusion: '' });
  if (!orgId) return <><PageHeader title="Impact Assessments" /><NoOrg /></>;
  if (id) return (
    <RecordDetail entityKey="dpias" id={Number(id)} backTo="/dpia" reloadKey={tick}
      title={r => `DPIA: ${r.processingActivity}`} subtitle={r => <Stack direction="row" spacing={1} component="span"><StatusChip value={r.status || 'Draft'} /><StatusChip value={r.overallResidualRisk} /></Stack>}
      actions={(r, reload) => <>
        {can('dpia.edit') && (r.status ?? 'Draft') === 'Draft' && <Button onClick={async () => { await api.post(`/api/dpias/${id}/submit`); toast('Submitted for review'); reload(); }}>Submit for review</Button>}
        {can('dpia.approve') && r.status !== 'Approved' && <Button color="success" variant="outlined" onClick={() => setApprove({ open: true, conclusion: r.conclusion ?? '' })}>Approve / reject</Button>}
      </>}
      side={() => <Card><CardContent><Heatmap dpiaId={Number(id)} tick={tick} /></CardContent></Card>}>
      {() => <>
        <EntityTable entityKey="dpiaRisks" parentId={Number(id)} title="Risk register" createLabel="Add risk" allowExport={false} onChanged={() => setTick(t => t + 1)} />
        <Dialog open={approve.open} onClose={() => setApprove({ ...approve, open: false })} maxWidth="sm" fullWidth>
          <DialogTitle>DPIA decision</DialogTitle>
          <DialogContent><TextField fullWidth multiline minRows={3} sx={{ mt: 1 }} label="Conclusion" value={approve.conclusion} onChange={e => setApprove({ ...approve, conclusion: e.target.value })} /></DialogContent>
          <DialogActions>
            <Button onClick={() => setApprove({ ...approve, open: false })}>Cancel</Button>
            <Button color="error" onClick={async () => { await api.post(`/api/dpias/${id}/approve`, { decision: 'Rejected', conclusion: approve.conclusion }); setApprove({ open: false, conclusion: '' }); setTick(t => t + 1); toast('Rejected', 'warning'); }}>Reject</Button>
            <Button variant="contained" color="success" onClick={async () => { await api.post(`/api/dpias/${id}/approve`, { decision: 'Approved', conclusion: approve.conclusion }); setApprove({ open: false, conclusion: '' }); setTick(t => t + 1); toast('DPIA approved'); }}>Approve</Button>
          </DialogActions>
        </Dialog>
      </>}
    </RecordDetail>
  );
  return (
    <Box>
      <PageHeader title="Data Protection Impact Assessments" subtitle="Structured risk assessment of processing activities with inherent and residual risk scoring and approval." />
      {org?.isSignificantDataFiduciary
        ? <Alert severity="warning" sx={{ mb: 2 }}>As a notified Significant Data Fiduciary, a DPIA and audit are required once every 12 months, with significant observations furnished to the Board (s.10(2)(c), Rule 13).</Alert>
        : <Alert severity="info" variant="outlined" sx={{ mb: 2 }}>DPIA is mandatory only for notified Significant Data Fiduciaries. For other organisations it is recommended good practice for high-risk processing (children's data, large-scale health or financial data, new technologies).</Alert>}
      <EntityTable entityKey="dpias" onRowClick={r => nav(`/dpia/${r.id}`)} createLabel="New DPIA" />
    </Box>
  );
}
