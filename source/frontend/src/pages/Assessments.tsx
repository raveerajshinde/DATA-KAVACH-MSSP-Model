import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Box, Button, Dialog, DialogActions, DialogContent, DialogTitle, Stack, TextField, Alert } from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import { api } from '../api';
import { useApp } from '../context';
import { NoOrg, PageHeader } from '../components/Common';
import EntityTable from '../components/EntityTable';

export default function Assessments() {
  const { orgId, org, can, toast } = useApp();
  const nav = useNavigate();
  const [open, setOpen] = useState(false);
  const [f, setF] = useState({ name: `DPDP Assessment ${new Date().getFullYear()}`, periodFrom: '', periodTo: '', owner: '', scope: '' });
  if (!orgId) return <><PageHeader title="Compliance Assessment" /><NoOrg /></>;
  const create = async () => {
    try { const a = await api.post(`/api/assessments?orgId=${orgId}`, f); toast('Assessment created from the requirement library'); nav(`/assessments/${a.id}`); }
    catch (e: any) { toast(e.message, 'error'); }
  };
  return (
    <Box>
      <PageHeader title="Compliance Assessment" subtitle="Questionnaire built from the DPDP Act, 2023 and DPDP Rules, 2025 requirement library. Applicability is pre-set from the organisation profile."
        actions={can('assessment.edit') && <Button variant="contained" startIcon={<AddIcon />} onClick={() => setOpen(true)}>New assessment</Button>} />
      <EntityTable entityKey="assessments" allowCreate={false} onRowClick={r => nav(`/assessments/${r.id}`)} />
      <Dialog open={open} onClose={() => setOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle>New assessment for {org?.name}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <Alert severity="info" variant="outlined">Review the Applicability tab of the organisation profile first - it decides which requirements start as Not Applicable.</Alert>
            <TextField label="Name" value={f.name} onChange={e => setF({ ...f, name: e.target.value })} />
            <Stack direction="row" spacing={2}>
              <TextField fullWidth type="date" label="Period from" InputLabelProps={{ shrink: true }} value={f.periodFrom} onChange={e => setF({ ...f, periodFrom: e.target.value })} />
              <TextField fullWidth type="date" label="Period to" InputLabelProps={{ shrink: true }} value={f.periodTo} onChange={e => setF({ ...f, periodTo: e.target.value })} />
            </Stack>
            <TextField label="Assessment owner" value={f.owner} onChange={e => setF({ ...f, owner: e.target.value })} />
            <TextField label="Scope" multiline minRows={2} value={f.scope} onChange={e => setF({ ...f, scope: e.target.value })} />
          </Stack>
        </DialogContent>
        <DialogActions><Button onClick={() => setOpen(false)}>Cancel</Button><Button variant="contained" onClick={create}>Create</Button></DialogActions>
      </Dialog>
    </Box>
  );
}
