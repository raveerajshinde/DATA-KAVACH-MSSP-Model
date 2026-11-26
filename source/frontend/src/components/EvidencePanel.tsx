import { useRef, useState } from 'react';
import { Box, Button, Chip, Collapse, IconButton, List, ListItem, ListItemText, MenuItem, Stack, TextField, Tooltip, Typography, Dialog, DialogTitle, DialogContent, DialogActions, Alert } from '@mui/material';
import AttachFileIcon from '@mui/icons-material/AttachFile';
import VerifiedIcon from '@mui/icons-material/Verified';
import HistoryIcon from '@mui/icons-material/History';
import UploadIcon from '@mui/icons-material/Upload';
import { api, fmtDateTime, qs } from '../api';
import { useApp, useLoad } from '../context';

const categories = ['Policy document', 'Procedure', 'System configuration', 'Screenshot', 'Contract', 'Report', 'Log extract', 'Training record', 'Other'];

function Versions({ id }: { id: number }) {
  const { orgId } = useApp();
  const [data] = useLoad(() => api.get(`/api/e/evidenceVersions?${qs({ orgId, parentId: id, pageSize: 50 })}`), [id], { rows: [] } as any);
  return (
    <List dense sx={{ pl: 2 }}>
      {data.rows.map((v: any) => (
        <ListItem key={v.id} secondaryAction={<Button size="small" onClick={() => api.download(`/api/files/evidence/${v.id}`)}>Download</Button>}>
          <ListItemText primary={`v${v.version} - ${v.fileName}`} secondary={<><span>{fmtDateTime(v.createdAt)} by {v.createdBy}{v.comment ? ` - ${v.comment}` : ''}</span><br /><span style={{ fontFamily: 'monospace', fontSize: 11 }}>SHA-256 {v.sha256}</span></>} />
        </ListItem>
      ))}
    </List>
  );
}

export function UploadEvidenceDialog({ open, onClose, linkedEntity, linkedId, evidenceId, onDone }: { open: boolean; onClose: () => void; linkedEntity?: string; linkedId?: number; evidenceId?: number; onDone: () => void }) {
  const { orgId, toast } = useApp();
  const [file, setFile] = useState<File | null>(null);
  const [f, setF] = useState({ title: '', category: 'Other', description: '', comment: '' });
  const [busy, setBusy] = useState(false);
  const submit = async () => {
    if (!file) return;
    setBusy(true);
    try {
      if (evidenceId) await api.upload(`/api/evidence/${evidenceId}/versions`, file, { comment: f.comment });
      else await api.upload(`/api/evidence?orgId=${orgId}`, file, { ...f, linkedEntity, linkedId });
      toast(evidenceId ? 'New version uploaded' : 'Evidence uploaded');
      setFile(null); setF({ title: '', category: 'Other', description: '', comment: '' });
      onDone(); onClose();
    } catch (e: any) { toast(e.message, 'error'); } finally { setBusy(false); }
  };
  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>{evidenceId ? 'Upload new version' : 'Attach evidence'}</DialogTitle>
      <DialogContent dividers>
        <Stack spacing={2}>
          <Button variant="outlined" component="label" startIcon={<UploadIcon />}>{file ? file.name : 'Choose file (PDF, Word, Excel, image, text, ZIP - max 50 MB)'}<input hidden type="file" onChange={e => { const x = e.target.files?.[0] ?? null; setFile(x); if (x && !f.title) setF(s => ({ ...s, title: x.name.replace(/\.[^.]+$/, '') })); }} /></Button>
          {!evidenceId && <>
            <TextField size="small" label="Title" value={f.title} onChange={e => setF({ ...f, title: e.target.value })} />
            <TextField size="small" select label="Category" value={f.category} onChange={e => setF({ ...f, category: e.target.value })}>{categories.map(c => <MenuItem key={c} value={c}>{c}</MenuItem>)}</TextField>
            <TextField size="small" label="Description" multiline minRows={2} value={f.description} onChange={e => setF({ ...f, description: e.target.value })} />
          </>}
          <TextField size="small" label="Version comment" value={f.comment} onChange={e => setF({ ...f, comment: e.target.value })} />
          <Alert severity="info" variant="outlined">Each upload is stored as a new immutable version with a SHA-256 fingerprint and timestamp.</Alert>
        </Stack>
      </DialogContent>
      <DialogActions><Button onClick={onClose}>Cancel</Button><Button variant="contained" disabled={!file || busy} onClick={submit}>Upload</Button></DialogActions>
    </Dialog>
  );
}

/** Evidence linked to a record, with upload, version history and integrity verification. */
export default function EvidencePanel({ linkedEntity, linkedId, compact, onChanged }: { linkedEntity: string; linkedId: number; compact?: boolean; onChanged?: () => void }) {
  const { orgId, can, toast } = useApp();
  const [tick, setTick] = useState(0);
  const [data] = useLoad(() => api.get(`/api/e/evidence?${qs({ orgId, 'f.linkedEntity': linkedEntity, 'f.linkedId': linkedId, pageSize: 100 })}`), [linkedEntity, linkedId, tick], { rows: [] } as any);
  const [open, setOpen] = useState<number | null>(null);
  const [upload, setUpload] = useState<{ open: boolean; id?: number }>({ open: false });
  const verify = async (id: number) => {
    try { const r = await api.get(`/api/evidence/${id}/verify`); toast(r.every((x: any) => x.intact) ? `Integrity verified for ${r.length} version(s)` : 'Integrity check FAILED - file changed or missing', r.every((x: any) => x.intact) ? 'success' : 'error'); }
    catch (e: any) { toast(e.message, 'error'); }
  };
  const done = () => { setTick(t => t + 1); onChanged?.(); };
  return (
    <Box>
      <Stack direction="row" alignItems="center" spacing={1} sx={{ mb: 1 }}>
        <AttachFileIcon fontSize="small" color="action" />
        <Typography variant="subtitle2">Evidence</Typography>
        <Chip size="small" label={data.rows.length} />
        {can('evidence.edit') && <Button size="small" onClick={() => setUpload({ open: true })}>Attach</Button>}
      </Stack>
      {data.rows.length === 0 && !compact && <Typography variant="body2" color="text.secondary">No evidence attached.</Typography>}
      <List dense disablePadding>
        {data.rows.map((e: any) => (
          <Box key={e.id}>
            <ListItem disableGutters secondaryAction={
              <Stack direction="row">
                <Tooltip title="Version history"><IconButton size="small" onClick={() => setOpen(open === e.id ? null : e.id)}><HistoryIcon fontSize="small" /></IconButton></Tooltip>
                <Tooltip title="Verify integrity"><IconButton size="small" onClick={() => verify(e.id)}><VerifiedIcon fontSize="small" /></IconButton></Tooltip>
                {can('evidence.edit') && <Tooltip title="Upload new version"><IconButton size="small" onClick={() => setUpload({ open: true, id: e.id })}><UploadIcon fontSize="small" /></IconButton></Tooltip>}
              </Stack>}>
              <ListItemText primary={e.title} secondary={`${e.category} - v${e.currentVersion} - ${fmtDateTime(e.updatedAt || e.createdAt)}`} />
            </ListItem>
            <Collapse in={open === e.id} unmountOnExit><Versions id={e.id} /></Collapse>
          </Box>
        ))}
      </List>
      <UploadEvidenceDialog open={upload.open} evidenceId={upload.id} onClose={() => setUpload({ open: false })} linkedEntity={linkedEntity} linkedId={linkedId} onDone={done} />
    </Box>
  );
}
