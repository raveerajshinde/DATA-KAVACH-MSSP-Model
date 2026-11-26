import { useState } from 'react';
import { Alert, Box, Button, Card, Chip, Stack, Tab, Tabs, Typography } from '@mui/material';
import AutoFixHighIcon from '@mui/icons-material/AutoFixHigh';
import { api } from '../api';
import { useApp } from '../context';
import { NoOrg, PageHeader } from '../components/Common';
import EntityTable from '../components/EntityTable';
import { EntityDialog } from '../components/EntityForm';

const plans = ['', '30-day', '60-day', '90-day', 'Custom'];

export default function Roadmap() {
  const { orgId, can, toast } = useApp();
  const [tab, setTab] = useState(0);
  const [tick, setTick] = useState(0);
  const [sel, setSel] = useState<any>(null);
  if (!orgId) return <><PageHeader title="Remediation Roadmap" /><NoOrg /></>;
  const generate = async () => {
    try { const r = await api.post(`/api/roadmap/generate?orgId=${orgId}`); toast(`${r.created} new actions created from open gaps`); setTick(t => t + 1); }
    catch (e: any) { toast(e.message, 'error'); }
  };
  const approve = async (decision: string) => {
    try { await api.post(`/api/actions/${sel.id}/approve`, { decision }); toast(`Action ${decision.toLowerCase()}`); setSel(null); setTick(t => t + 1); }
    catch (e: any) { toast(e.message, 'error'); }
  };
  return (
    <Box>
      <PageHeader title="Compliance Remediation Roadmap" subtitle="Actions generated from assessment gaps, failing safeguards, vendor contract gaps and high DPIA risks - prioritised by penalty exposure (security safeguards and breach handling first)."
        actions={can('roadmap.edit') && <Button variant="contained" startIcon={<AutoFixHighIcon />} onClick={generate}>Generate from gaps</Button>} />
      <Alert severity="info" variant="outlined" sx={{ mb: 2 }}>Re-running generation only adds actions for new gaps; existing actions and their progress are kept.</Alert>
      <Card sx={{ mb: 2 }}><Tabs value={tab} onChange={(_, v) => setTab(v)}>{['All actions', '30-day plan', '60-day plan', '90-day plan', 'Custom roadmap'].map(l => <Tab key={l} label={l} />)}</Tabs></Card>
      <EntityTable entityKey="actions" reloadKey={tick} filters={{ plan: plans[tab] }} createLabel="Add action" defaults={{ plan: plans[tab] || 'Custom', status: 'Open' }} onRowClick={r => setSel(r)}
        columns={['gapId', 'requirementRef', 'title', 'riskRating', 'priority', 'department', 'owner', 'targetDate', 'status', 'approval']} />
      {sel && <EntityDialog entityKey="actions" id={sel.id} open onClose={() => setSel(null)} onSaved={() => setTick(t => t + 1)} title={`${sel.gapId} - remediation action`} />}
      {sel && can('roadmap.approve') && (
        <Box sx={{ position: 'fixed', bottom: 24, left: '50%', transform: 'translateX(-50%)', zIndex: 1400 }}>
          <Card sx={{ px: 2, py: 1, boxShadow: 6 }}><Stack direction="row" spacing={1} alignItems="center"><Typography variant="body2">Approval for {sel.gapId}:</Typography><Chip size="small" label={sel.approval} /><Button size="small" color="success" onClick={() => approve('Approved')}>Approve</Button><Button size="small" color="error" onClick={() => approve('Rejected')}>Reject</Button></Stack></Card>
        </Box>
      )}
    </Box>
  );
}
