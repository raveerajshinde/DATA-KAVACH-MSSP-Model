import { useState } from 'react';
import { Alert, Box, Card, CardContent, Chip, Grid, Stack, Typography } from '@mui/material';
import VerifiedIcon from '@mui/icons-material/Verified';
import { api, qs } from '../api';
import { useApp, useLoad } from '../context';
import { Kpi, NoOrg, PageHeader, StatusChip } from '../components/Common';
import EntityTable from '../components/EntityTable';
import { EntityDialog } from '../components/EntityForm';
import EvidencePanel from '../components/EvidencePanel';

export default function Security() {
  const { orgId } = useApp();
  const [sel, setSel] = useState<any>(null);
  const [ev, setEv] = useState<any>(null);
  const [tick, setTick] = useState(0);
  const [all] = useLoad<any>(() => (orgId ? api.get(`/api/e/securityControls?${qs({ orgId, pageSize: 200 })}`) : Promise.resolve({ rows: [] })), [orgId, tick], { rows: [] });
  if (!orgId) return <><PageHeader title="Security Safeguards" /><NoOrg /></>;
  const rows = all.rows as any[];
  const n = (s: string) => rows.filter(r => r.status === s).length;
  return (
    <Box>
      <PageHeader title="Security Safeguards Assessment" subtitle="Reasonable security safeguards under s.8(5) and Rule 6. Map your tools (Sentinel, Wazuh, Splunk, Elastic, CrowdStrike, Defender, Fortinet, Palo Alto...) and validate each control with configuration evidence." />
      <Alert severity="warning" variant="outlined" sx={{ mb: 2 }}>A control cannot be marked Compliant just because a tool is installed. Record configuration evidence and mark the control validated first. Failure to take reasonable security safeguards can attract a penalty of up to Rs 250 crore.</Alert>
      <Grid container spacing={2} sx={{ mb: 2 }}>
        <Grid size={{ xs: 6, md: 3 }}><Kpi label="Validated controls" value={`${rows.filter(r => r.validated).length}/${rows.length}`} icon={<VerifiedIcon />} /></Grid>
        <Grid size={{ xs: 6, md: 3 }}><Kpi label="Compliant" value={n('Compliant')} color="#2E7D32" /></Grid>
        <Grid size={{ xs: 6, md: 3 }}><Kpi label="Partially compliant" value={n('Partially Compliant')} color="#8A5A00" /></Grid>
        <Grid size={{ xs: 6, md: 3 }}><Kpi label="Non-compliant" value={n('Non-Compliant')} color="#C62828" /></Grid>
      </Grid>
      <EntityTable entityKey="securityControls" allowCreate={false} allowDelete={false} reloadKey={tick} onRowClick={r => setSel(r)} columns={['controlKey', 'domain', 'controlName', 'ruleReference', 'status', 'toolsMapped', 'validated', 'riskRating']} />
      {sel && <EntityDialog entityKey="securityControls" id={sel.id} open={!!sel} title={`${sel.controlKey} ${sel.controlName}`} onClose={() => setSel(null)} onSaved={() => setTick(t => t + 1)} />}
      <Card sx={{ mt: 2 }}><CardContent>
        <Typography variant="subtitle2" sx={{ mb: 1 }}>Configuration evidence per control</Typography>
        <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap>
          {rows.map(r => <Chip key={r.id} label={r.controlKey} onClick={() => setEv(r)} icon={r.validated ? <VerifiedIcon /> : undefined} color={r.validated ? 'success' : 'default'} variant="outlined" />)}
        </Stack>
        {ev && <Box sx={{ mt: 2 }}><Typography variant="body2" fontWeight={600}>{ev.controlKey} {ev.controlName} <StatusChip value={ev.status} /></Typography><EvidencePanel key={ev.id} linkedEntity="securityControls" linkedId={ev.id} /></Box>}
      </CardContent></Card>
    </Box>
  );
}
