import { useState } from 'react';
import { Box, Button, Dialog, DialogContent, DialogTitle, DialogActions, List, ListItem, ListItemText, Stack, Typography, Alert } from '@mui/material';
import UploadIcon from '@mui/icons-material/Upload';
import { api, fmtDateTime, qs } from '../api';
import { useApp, useLoad } from '../context';
import { NoOrg, PageHeader } from '../components/Common';
import EntityTable from '../components/EntityTable';
import { UploadEvidenceDialog } from '../components/EvidencePanel';

function Detail({ ev, onClose, onChanged }: { ev: any; onClose: () => void; onChanged: () => void }) {
  const { orgId, can, toast } = useApp();
  const [tick, setTick] = useState(0);
  const [vers] = useLoad<any>(() => api.get(`/api/e/evidenceVersions?${qs({ orgId, parentId: ev.id, pageSize: 100 })}`), [ev.id, tick], { rows: [] });
  const [check, setCheck] = useState<any[] | null>(null);
  const [up, setUp] = useState(false);
  return (
    <Dialog open onClose={onClose} maxWidth="md" fullWidth>
      <DialogTitle>{ev.title}</DialogTitle>
      <DialogContent dividers>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>{ev.category} - linked to {ev.linkedEntity || 'repository'} {ev.linkedId ? `#${ev.linkedId}` : ''}</Typography>
        {check && <Alert severity={check.every(c => c.intact) ? 'success' : 'error'} sx={{ mb: 1 }}>{check.every(c => c.intact) ? `All ${check.length} version(s) match their recorded SHA-256 fingerprints.` : 'One or more versions do not match - investigate possible tampering.'}</Alert>}
        <List dense>
          {vers.rows.map((v: any) => (
            <ListItem key={v.id} divider secondaryAction={<Button size="small" onClick={() => api.download(`/api/files/evidence/${v.id}`)}>Download</Button>}>
              <ListItemText primary={`Version ${v.version} - ${v.fileName} (${Math.round(v.sizeBytes / 1024)} KB)`} secondary={<><span>{fmtDateTime(v.createdAt)} by {v.createdBy}{v.comment ? ` - ${v.comment}` : ''}</span><br /><span style={{ fontFamily: 'monospace', fontSize: 11 }}>SHA-256 {v.sha256}</span></>} />
            </ListItem>
          ))}
        </List>
      </DialogContent>
      <DialogActions>
        <Button onClick={async () => { try { setCheck(await api.get(`/api/evidence/${ev.id}/verify`)); } catch (e: any) { toast(e.message, 'error'); } }}>Verify integrity</Button>
        {can('evidence.edit') && <Button startIcon={<UploadIcon />} onClick={() => setUp(true)}>New version</Button>}
        <Button onClick={onClose}>Close</Button>
      </DialogActions>
      <UploadEvidenceDialog open={up} evidenceId={ev.id} onClose={() => setUp(false)} onDone={() => { setTick(t => t + 1); onChanged(); }} />
    </Dialog>
  );
}

export default function Evidence() {
  const { orgId, can } = useApp();
  const [sel, setSel] = useState<any>(null);
  const [up, setUp] = useState(false);
  const [tick, setTick] = useState(0);
  if (!orgId) return <><PageHeader title="Evidence Repository" /><NoOrg /></>;
  return (
    <Box>
      <PageHeader title="Evidence Repository" subtitle="Policies, screenshots, configuration exports, contracts, drill reports and logs. Every upload is versioned, timestamped and fingerprinted (SHA-256); every download is logged."
        actions={can('evidence.edit') && <Button variant="contained" startIcon={<UploadIcon />} onClick={() => setUp(true)}>Upload evidence</Button>} />
      <Stack spacing={2}>
        <EntityTable entityKey="evidence" reloadKey={tick} allowCreate={false} onRowClick={r => setSel(r)} />
        <Typography variant="caption" color="text.secondary">Tip: attach evidence directly from an assessment requirement, vendor, incident or control so it is linked and appears in reports.</Typography>
      </Stack>
      {sel && <Detail ev={sel} onClose={() => setSel(null)} onChanged={() => setTick(t => t + 1)} />}
      <UploadEvidenceDialog open={up} onClose={() => setUp(false)} onDone={() => setTick(t => t + 1)} />
    </Box>
  );
}
