import { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { Alert, Box, Button, Card, CardContent, Dialog, DialogActions, DialogContent, DialogTitle, Grid, MenuItem, Stack, Switch, Table, TableBody, TableCell, TableHead, TableRow, TextField, Typography, Chip } from '@mui/material';
import TuneIcon from '@mui/icons-material/Tune';
import { api, fmtDateTime, fromLocalInput, qs, toLocalInput } from '../api';
import { useApp, useLoad } from '../context';
import { DueChip, NoOrg, PageHeader, StatusChip } from '../components/Common';
import EntityTable from '../components/EntityTable';
import RecordDetail from '../components/RecordDetail';

function Notifications({ incidentId, tick, onChange }: { incidentId: number; tick: number; onChange: () => void }) {
  const { orgId, can, toast } = useApp();
  const [d] = useLoad<any>(() => api.get(`/api/e/incidentNotifications?${qs({ orgId, parentId: incidentId, pageSize: 50, sort: 'dueAt', desc: false })}`), [incidentId, tick], { rows: [] });
  const [send, setSend] = useState<any>(null);
  const open = async (n: any) => {
    const t = await api.get(`/api/incidents/${incidentId}/template/${n.ruleKey || 'OTHER'}`);
    setSend({ ...n, content: n.content || t.text, sentAt: n.sentAt || new Date().toISOString(), method: n.method || 'Email' });
  };
  const save = async () => {
    try { await api.put(`/api/e/incidentNotifications/${send.id}`, { content: send.content, sentAt: send.sentAt, method: send.method, submissionReference: send.submissionReference, notes: send.notes }); toast('Notification recorded'); setSend(null); onChange(); }
    catch (e: any) { toast(e.message, 'error'); }
  };
  return (
    <Card><CardContent>
      <Typography variant="subtitle1" fontWeight={700}>Notification obligations</Typography>
      <Typography variant="caption" color="text.secondary">Deadlines run from the time the organisation became aware. DPDP, CERT-In and sectoral reporting are tracked separately.</Typography>
      <Table size="small" sx={{ mt: 1 }}>
        <TableHead><TableRow>{['Recipient', 'Basis', 'Due', 'Status', ''].map(h => <TableCell key={h} sx={{ fontWeight: 700 }}>{h}</TableCell>)}</TableRow></TableHead>
        <TableBody>
          {d.rows.map((n: any) => (
            <TableRow key={n.id}>
              <TableCell sx={{ fontWeight: 600 }}>{n.recipientName}</TableCell>
              <TableCell sx={{ maxWidth: 320 }}><Typography variant="caption">{n.basis}</Typography></TableCell>
              <TableCell sx={{ whiteSpace: 'nowrap' }}>{fmtDateTime(n.dueAt)}<br />{n.status === 'Pending' && <DueChip due={n.dueAt} />}{n.sentAt && <Typography variant="caption" color={new Date(n.sentAt) <= new Date(n.dueAt) ? 'success.main' : 'error.main'}>Sent {fmtDateTime(n.sentAt)}</Typography>}</TableCell>
              <TableCell><StatusChip value={n.status} /></TableCell>
              <TableCell>{can('incident.edit') && n.status !== 'Not required' && <Button size="small" onClick={() => open(n)}>{n.sentAt ? 'View' : 'Prepare & record'}</Button>}</TableCell>
            </TableRow>
          ))}
          {d.rows.length === 0 && <TableRow><TableCell colSpan={5}><Typography variant="body2" color="text.secondary">Mark the incident as a personal data breach and/or CERT-In reportable to create obligations.</Typography></TableCell></TableRow>}
        </TableBody>
      </Table>
      <Dialog open={!!send} onClose={() => setSend(null)} maxWidth="md" fullWidth>
        <DialogTitle>{send?.recipientName}</DialogTitle>
        <DialogContent dividers>
          {send && <Stack spacing={2}>
            <Alert severity="info" variant="outlined">{send.basis}</Alert>
            <TextField label="Notification content (pre-filled template - edit before sending)" multiline minRows={12} value={send.content} onChange={e => setSend({ ...send, content: e.target.value })} InputProps={{ sx: { fontFamily: 'Consolas, monospace', fontSize: 13 } }} />
            <Stack direction="row" spacing={2}>
              <TextField type="datetime-local" label="Sent at" InputLabelProps={{ shrink: true }} value={toLocalInput(send.sentAt)} onChange={e => setSend({ ...send, sentAt: fromLocalInput(e.target.value) })} />
              <TextField select label="Method" value={send.method} onChange={e => setSend({ ...send, method: e.target.value })} sx={{ minWidth: 180 }}>{['Email', 'Portal', 'User account / app', 'SMS', 'Letter', 'Phone'].map(m => <MenuItem key={m} value={m}>{m}</MenuItem>)}</TextField>
              <TextField label="Submission / acknowledgement ref" value={send.submissionReference ?? ''} onChange={e => setSend({ ...send, submissionReference: e.target.value })} sx={{ flex: 1 }} />
            </Stack>
            <Button size="small" sx={{ alignSelf: 'flex-start' }} onClick={() => { navigator.clipboard.writeText(send.content); toast('Copied to clipboard'); }}>Copy text</Button>
          </Stack>}
        </DialogContent>
        <DialogActions><Button onClick={() => setSend(null)}>Cancel</Button><Button variant="contained" onClick={save}>Record as sent</Button></DialogActions>
      </Dialog>
    </CardContent></Card>
  );
}

function RulesDialog({ open, onClose }: { open: boolean; onClose: () => void }) {
  const { orgId, can, toast } = useApp();
  const [rules, , , setRules] = useLoad<any[]>(() => (open ? api.get(`/api/breach/rules?orgId=${orgId}`) : Promise.resolve([])), [open, orgId], []);
  const upd = (i: number, k: string, v: any) => setRules(rules.map((r, j) => (j === i ? { ...r, [k]: v } : r)));
  return (
    <Dialog open={open} onClose={onClose} maxWidth="lg" fullWidth>
      <DialogTitle>Notification deadline engine</DialogTitle>
      <DialogContent dividers>
        <Alert severity="info" variant="outlined" sx={{ mb: 2 }}>DPDP Rule 7 requires intimation to the Board and affected Data Principals "without delay" (internal targets below), and a detailed report to the Board within 72 hours of becoming aware. CERT-In and sectoral obligations are separate. Changes apply to new incidents and pending notifications.</Alert>
        <Table size="small">
          <TableHead><TableRow>{['On', 'Recipient', 'Legal basis', 'Hours from awareness', 'Statutory?', 'Applies when'].map(h => <TableCell key={h} sx={{ fontWeight: 700 }}>{h}</TableCell>)}</TableRow></TableHead>
          <TableBody>{rules.map((r, i) => (
            <TableRow key={r.key}>
              <TableCell><Switch checked={r.enabled} onChange={e => upd(i, 'enabled', e.target.checked)} disabled={!can('incident.approve')} /></TableCell>
              <TableCell><TextField size="small" value={r.recipient} onChange={e => upd(i, 'recipient', e.target.value)} fullWidth /></TableCell>
              <TableCell sx={{ maxWidth: 320 }}><TextField size="small" value={r.basis} onChange={e => upd(i, 'basis', e.target.value)} fullWidth multiline /></TableCell>
              <TableCell width={120}><TextField size="small" type="number" value={r.hours} onChange={e => upd(i, 'hours', Number(e.target.value))} /></TableCell>
              <TableCell>{r.statutoryDeadline ? <Chip size="small" color="error" label="Statutory" /> : <Chip size="small" label="Internal target" />}</TableCell>
              <TableCell><Typography variant="caption">{r.appliesWhen}</Typography></TableCell>
            </TableRow>
          ))}</TableBody>
        </Table>
      </DialogContent>
      <DialogActions><Button onClick={onClose}>Close</Button>{can('incident.approve') && <Button variant="contained" onClick={async () => { try { await api.put(`/api/breach/rules?orgId=${orgId}`, rules); toast('Deadline rules saved'); onClose(); } catch (e: any) { toast(e.message, 'error'); } }}>Save</Button>}</DialogActions>
    </Dialog>
  );
}

function Timeline({ id, tick }: { id: number; tick: number }) {
  const { orgId, can, toast } = useApp();
  const [t, setT] = useState(0);
  const [d] = useLoad<any>(() => api.get(`/api/e/incidentEvents?${qs({ orgId, parentId: id, pageSize: 200, sort: 'eventAt', desc: false })}`), [id, tick, t], { rows: [] });
  const [text, setText] = useState('');
  return (
    <Card><CardContent>
      <Typography variant="subtitle1" fontWeight={700}>Evidence timeline</Typography>
      {d.rows.map((e: any) => (
        <Stack key={e.id} direction="row" spacing={1.5} sx={{ py: 0.8, borderLeft: '2px solid #C9D6EE', pl: 1.5, ml: 0.5 }}>
          <Box><Typography variant="caption" color="text.secondary">{fmtDateTime(e.eventAt)} - {e.createdBy}</Typography><Typography variant="body2">{e.eventText}</Typography></Box>
        </Stack>
      ))}
      {can('incident.edit') && <Stack direction="row" spacing={1} sx={{ mt: 1 }}>
        <TextField size="small" fullWidth placeholder="Add timeline entry (what happened / decided)" value={text} onChange={e => setText(e.target.value)} />
        <Button disabled={!text} onClick={async () => { try { await api.post(`/api/e/incidentEvents?orgId=${orgId}`, { incidentId: id, eventAt: new Date().toISOString(), eventText: text }); setText(''); setT(x => x + 1); } catch (e: any) { toast(e.message, 'error'); } }}>Add</Button>
      </Stack>}
    </CardContent></Card>
  );
}

export default function Incidents() {
  const { id } = useParams();
  const { orgId, can, toast } = useApp();
  const nav = useNavigate();
  const [tick, setTick] = useState(0);
  const [rules, setRules] = useState(false);
  if (!orgId) return <><PageHeader title="Breaches & Incidents" /><NoOrg /></>;
  if (id) {
    const approve = async (kind: string, decision: string) => { try { await api.post(`/api/incidents/${id}/approve`, { kind, decision }); toast(`${kind} approval: ${decision}`); setTick(t => t + 1); } catch (e: any) { toast(e.message, 'error'); } };
    return (
      <RecordDetail entityKey="incidents" id={Number(id)} backTo="/incidents" reloadKey={tick} title={r => `${r.reference} - ${r.title}`}
        subtitle={r => <span><StatusChip value={r.severity} /> <StatusChip value={r.status} /> {r.isPersonalDataBreach && <Chip size="small" color="error" label="Personal data breach" />} {r.isDrill && <Chip size="small" label="Drill" />}</span>}
        actions={r => can('incident.approve') && <>
          {r.legalApproval !== 'Approved' && <Button onClick={() => approve('legal', 'Approved')}>Legal approval</Button>}
          {r.managementApproval !== 'Approved' && <Button onClick={() => approve('management', 'Approved')}>Management approval</Button>}
        </>}
        side={r => <Card><CardContent><Stack spacing={1}>
          <Typography variant="subtitle2">Approvals</Typography>
          <Stack direction="row" spacing={1}><Typography variant="body2" sx={{ width: 110 }}>Legal</Typography><StatusChip value={r.legalApproval} /></Stack>
          <Stack direction="row" spacing={1}><Typography variant="body2" sx={{ width: 110 }}>Management</Typography><StatusChip value={r.managementApproval} /></Stack>
        </Stack></CardContent></Card>}>
        {r => <Stack spacing={2}>
          <Notifications incidentId={Number(id)} tick={tick + (r.updatedAt ? 1 : 0)} onChange={() => setTick(t => t + 1)} />
          <Timeline id={Number(id)} tick={tick} />
        </Stack>}
      </RecordDetail>
    );
  }
  return (
    <Box>
      <PageHeader title="Breaches & Incidents" subtitle="One incident record drives DPDP intimations to Data Principals and the Board, CERT-In and sectoral reporting - each with its own deadline, template and evidence."
        actions={<Button startIcon={<TuneIcon />} onClick={() => setRules(true)}>Deadline rules</Button>} />
      <Grid container spacing={2} sx={{ mb: 2 }}>
        <Grid size={{ xs: 12, md: 4 }}><Alert severity="error" variant="outlined">Board: intimation without delay + detailed report within <b>72 hours</b> of awareness (Rule 7(2)).</Alert></Grid>
        <Grid size={{ xs: 12, md: 4 }}><Alert severity="warning" variant="outlined">Affected Data Principals: without delay, via user account or registered contact (Rule 7(1)).</Alert></Grid>
        <Grid size={{ xs: 12, md: 4 }}><Alert severity="info" variant="outlined">CERT-In: reportable cyber incidents within 6 hours of noticing - separate from DPDP.</Alert></Grid>
      </Grid>
      <EntityTable entityKey="incidents" onRowClick={r => nav(`/incidents/${r.id}`)} createLabel="Record incident" />
      <RulesDialog open={rules} onClose={() => setRules(false)} />
    </Box>
  );
}
