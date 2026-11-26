import { useState } from 'react';
import { Alert, Box, Button, Card, CardContent, Dialog, DialogActions, DialogContent, DialogTitle, Grid, List, ListItem, ListItemText, Stack, Tab, Tabs, TextField, Typography, IconButton, Tooltip } from '@mui/material';
import ContentCopyIcon from '@mui/icons-material/ContentCopy';
import { api, fmtDateTime, qs } from '../api';
import { useApp, useLoad } from '../context';
import { NoOrg, PageHeader, SectionCard, StatusChip, useConfirm } from '../components/Common';
import EntityTable, { renderCell } from '../components/EntityTable';
import { EntityDialog } from '../components/EntityForm';

function History({ id }: { id: number }) {
  const { orgId } = useApp();
  const [d] = useLoad<any>(() => api.get(`/api/e/consentEvents?${qs({ orgId, parentId: id, pageSize: 100, sort: 'id', desc: false })}`), [id], { rows: [] });
  return <List dense>{d.rows.map((e: any) => <ListItem key={e.id} divider><ListItemText primary={e.eventType} secondary={`${fmtDateTime(e.createdAt)} - ${e.createdBy} - ${e.details ?? ''}`} /></ListItem>)}</List>;
}

export function ApiKeys() {
  const { orgId, can, toast } = useApp();
  const [tick, setTick] = useState(0);
  const [keys] = useLoad<any[]>(() => (can('admin.apikeys') ? api.get(`/api/apikeys?orgId=${orgId}`) : Promise.resolve([])), [orgId, tick], []);
  const [name, setName] = useState('');
  const [created, setCreated] = useState<string | null>(null);
  const { confirm, dialog } = useConfirm();
  if (!can('admin.apikeys')) return <Alert severity="info">API keys are managed by administrators.</Alert>;
  const origin = window.location.origin;
  return (
    <Grid container spacing={2}>
      <Grid size={{ xs: 12, md: 5 }}>
        <SectionCard title="Integration API keys">
          <Stack direction="row" spacing={1} sx={{ mb: 2 }}>
            <TextField size="small" label="Key name (e.g. Website)" value={name} onChange={e => setName(e.target.value)} fullWidth />
            <Button variant="contained" disabled={!name} onClick={async () => { const r = await api.post(`/api/apikeys?orgId=${orgId}`, { name }); setCreated(r.key); setName(''); setTick(t => t + 1); }}>Create</Button>
          </Stack>
          {created && <Alert severity="warning" sx={{ mb: 2 }} action={<IconButton size="small" onClick={() => { navigator.clipboard.writeText(created); toast('Copied'); }}><ContentCopyIcon fontSize="small" /></IconButton>}>Copy now - shown only once:<br /><code style={{ wordBreak: 'break-all' }}>{created}</code></Alert>}
          <List dense>
            {keys.map(k => (
              <ListItem key={k.id} divider secondaryAction={k.isActive && <Button size="small" color="error" onClick={() => confirm('Revoke key', `Revoke "${k.name}"? Integrations using it will stop working.`, async () => { await api.del(`/api/apikeys/${k.id}`); setTick(t => t + 1); }, true, 'Revoke')}>Revoke</Button>}>
                <ListItemText primary={<>{k.name} <StatusChip value={k.isActive ? 'Open' : 'Closed'} /></>} secondary={`${k.prefix}... - last used ${fmtDateTime(k.lastUsedAt) || 'never'}`} />
              </ListItem>
            ))}
          </List>
        </SectionCard>
      </Grid>
      <Grid size={{ xs: 12, md: 7 }}>
        <SectionCard title="Integration API (for websites, apps, CRMs)">
          <Typography variant="body2" sx={{ mb: 1 }}>Send the key in the <code>X-Api-Key</code> header. Records are attributed to the key name in the audit trail.</Typography>
          <Box component="pre" sx={{ bgcolor: '#0F172A', color: '#E2E8F0', p: 2, borderRadius: 1.5, fontSize: 12, overflow: 'auto' }}>{`# Record consent
curl -X POST ${origin}/api/v1/consents \\
  -H "X-Api-Key: dk_..." -H "Content-Type: application/json" \\
  -d '{"principalRef":"CUST-1001","identifierType":"Customer ID",
       "purpose":"Order updates","noticeId":1,"language":"English",
       "evidenceRef":"form-submission-8812"}'

# Withdraw consent (all purposes, or pass "purpose")
curl -X POST ${origin}/api/v1/consents/withdraw -H "X-Api-Key: dk_..." \\
  -H "Content-Type: application/json" -d '{"principalRef":"CUST-1001"}'

# Consent status
curl "${origin}/api/v1/consents?principalRef=CUST-1001" -H "X-Api-Key: dk_..."

# Rights request intake (from your privacy page form)
curl -X POST ${origin}/api/v1/rights-requests -H "X-Api-Key: dk_..." \\
  -H "Content-Type: application/json" \\
  -d '{"requestType":"Access (s.11)","principalName":"A. Kumar",
       "principalContact":"a@example.com","details":"..."}'`}</Box>
        </SectionCard>
      </Grid>
      {dialog}
    </Grid>
  );
}

export default function Consent() {
  const { orgId } = useApp();
  const [tab, setTab] = useState(0);
  const [rec, setRec] = useState<any>(null);
  const [edit, setEdit] = useState(false);
  const [tick, setTick] = useState(0);
  if (!orgId) return <><PageHeader title="Consent Management" /><NoOrg /></>;
  return (
    <Box>
      <PageHeader title="Consent Management" subtitle="Notices, purpose-specific consent records with versioning, withdrawal and full history - so you can prove notice and consent (s.6(10))." />
      <Card sx={{ mb: 2 }}><Tabs value={tab} onChange={(_, v) => setTab(v)}><Tab label="Consent notices" /><Tab label="Consent records" /><Tab label="Integration API" /></Tabs></Card>
      {tab === 0 && <>
        <Alert severity="info" variant="outlined" sx={{ mb: 2 }}>Rule 3: a notice must be standalone, in clear and plain language, itemise the personal data and purposes, and link to withdrawal, rights and the Board complaint route. Offer English or Eighth Schedule languages.</Alert>
        <EntityTable entityKey="consentNotices" createLabel="New notice" />
      </>}
      {tab === 1 && <EntityTable entityKey="consentRecords" createLabel="Record consent" onRowClick={r => setRec(r)} reloadKey={tick} />}
      {tab === 2 && <ApiKeys />}
      <Dialog open={!!rec} onClose={() => setRec(null)} maxWidth="sm" fullWidth>
        <DialogTitle>Consent record - {rec?.principalRef}</DialogTitle>
        <DialogContent dividers>
          {rec && <>
            <Stack spacing={0.5} sx={{ mb: 2 }}>
              {['purpose', 'status', 'noticeVersion', 'channel', 'givenAt', 'withdrawnAt', 'evidenceRef'].map(k => <Stack key={k} direction="row" spacing={2}><Typography variant="body2" color="text.secondary" sx={{ width: 140 }}>{k.replace(/([A-Z])/g, ' $1')}</Typography><Box>{renderCell(k, rec[k], k.endsWith('At') ? 'datetime' : undefined)}</Box></Stack>)}
            </Stack>
            <Typography variant="subtitle2">Consent history</Typography>
            <History id={rec.id} />
          </>}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setEdit(true)}>Edit</Button>
          {rec?.status === 'Given' && <Button color="error" onClick={async () => { await api.put(`/api/e/consentRecords/${rec.id}`, { status: 'Withdrawn' }); setRec(null); setTick(t => t + 1); }}>Record withdrawal</Button>}
          <Button onClick={() => setRec(null)}>Close</Button>
        </DialogActions>
      </Dialog>
      {rec && <EntityDialog entityKey="consentRecords" id={rec.id} open={edit} onClose={() => setEdit(false)} onSaved={() => { setRec(null); setTick(t => t + 1); }} />}
    </Box>
  );
}
