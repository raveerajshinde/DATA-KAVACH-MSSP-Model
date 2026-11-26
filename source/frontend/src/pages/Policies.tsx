import { useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { Alert, Box, Button, Card, CardContent, Chip, Dialog, DialogActions, DialogContent, DialogTitle, Grid, MenuItem, Stack, Tab, Tabs, TextField, Typography, Table, TableBody, TableCell, TableHead, TableRow } from '@mui/material';
import AutoAwesomeIcon from '@mui/icons-material/AutoAwesome';
import UploadFileIcon from '@mui/icons-material/UploadFile';
import { api, fmtDate, qs } from '../api';
import { useApp, useLoad } from '../context';
import { NoOrg, PageHeader, StatusChip } from '../components/Common';
import EntityTable from '../components/EntityTable';

export function Markup({ text }: { text: string }) {
  const lines = (text || '').split('\n');
  return (
    <Box sx={{ fontFamily: 'Georgia, serif', '& p': { my: 1 } }}>
      {lines.map((l, i) => {
        const t = l.trim();
        if (t.startsWith('# ')) return <Typography key={i} variant="h5" fontWeight={700} color="primary.dark" sx={{ mt: 1 }}>{t.slice(2)}</Typography>;
        if (t.startsWith('## ')) return <Typography key={i} variant="h6" fontWeight={700} color="primary" sx={{ mt: 2 }}>{t.slice(3)}</Typography>;
        if (t.startsWith('- ')) return <Typography key={i} component="li" variant="body2" sx={{ ml: 3 }}>{t.slice(2)}</Typography>;
        if (!t) return <Box key={i} sx={{ height: 6 }} />;
        return <Typography key={i} variant="body2" component="p">{t}</Typography>;
      })}
    </Box>
  );
}

function PolicyDetail({ id }: { id: number }) {
  const { orgId, can, toast } = useApp();
  const nav = useNavigate();
  const [tick, setTick] = useState(0);
  const [p, , reload, setP] = useLoad<any>(() => api.get(`/api/e/policies/${id}`), [id, tick], null);
  const [recs] = useLoad<any>(() => api.get(`/api/e/policyRecommendations?${qs({ orgId, parentId: id, pageSize: 200, sort: 'id', desc: false })}`), [id, tick], { rows: [] });
  const [tab, setTab] = useState(0);
  const [content, setContent] = useState<string | null>(null);
  const [approve, setApprove] = useState(false);
  const [notes, setNotes] = useState('');
  if (!p) return null;
  const text = content ?? p.content ?? '';
  const act = async (fn: () => Promise<any>, msg: string) => { try { const r = await fn(); toast(msg); setTick(t => t + 1); return r; } catch (e: any) { toast(e.message, 'error'); } };
  const approvedCount = recs.rows.filter((r: any) => r.approvalStatus === 'Approved').length;
  return (
    <Box>
      <Button onClick={() => nav('/policies')} sx={{ mb: 1 }}>Back</Button>
      <PageHeader title={p.title} subtitle={<Stack direction="row" spacing={1} component="span" alignItems="center"><StatusChip value={p.status} /><Chip size="small" label={`v${p.version}`} /><Chip size="small" variant="outlined" label={p.source} />{p.basedOnPolicyId > 0 && <Chip size="small" clickable label="View original" onClick={() => nav(`/policies/${p.basedOnPolicyId}`)} />}</Stack>}
        actions={<>
          {can('policy.edit') && <Button startIcon={<AutoAwesomeIcon />} onClick={() => act(() => api.post(`/api/policies/${id}/analyze`), 'DPDP gap analysis refreshed')}>Analyse against DPDP</Button>}
          {can('policy.edit') && approvedCount > 0 && <Button variant="outlined" onClick={async () => { const r = await act(() => api.post(`/api/policies/${id}/apply`), 'Revised version created - original kept unchanged'); if (r?.id) nav(`/policies/${r.id}`); }}>Create revised version ({approvedCount})</Button>}
          {can('policy.approve') && p.status !== 'Approved internally' && <Button variant="contained" color="success" onClick={() => setApprove(true)}>Approve internally</Button>}
        </>} />
      {p.status !== 'Approved internally'
        ? <Alert severity="warning" sx={{ mb: 2 }}>Draft - not a legally approved policy. Generated templates and recommendations must be reviewed by legal counsel before adoption.</Alert>
        : <Alert severity="success" sx={{ mb: 2 }}>Approved internally by {p.approvedBy} on {fmtDate(p.approvedAt)}. Internal approval is not legal certification.</Alert>}
      <Card sx={{ mb: 2 }}><Tabs value={tab} onChange={(_, v) => setTab(v)}><Tab label="Document" /><Tab label={`Recommended amendments (${recs.rows.length})`} /></Tabs></Card>
      {tab === 0 && (
        <Grid container spacing={2}>
          <Grid size={{ xs: 12, md: 6 }}>
            <Card><CardContent>
              <Stack direction="row" justifyContent="space-between" sx={{ mb: 1 }}><Typography variant="subtitle2">Edit (# heading, ## sub-heading, - bullet)</Typography>{content !== null && can('policy.edit') && <Button size="small" variant="contained" onClick={async () => { await act(() => api.put(`/api/e/policies/${id}`, { content }), 'Saved'); setContent(null); }}>Save</Button>}</Stack>
              <TextField fullWidth multiline minRows={24} maxRows={40} value={text} onChange={e => setContent(e.target.value)} disabled={!can('policy.edit')} InputProps={{ sx: { fontFamily: 'Consolas, monospace', fontSize: 13 } }} />
            </CardContent></Card>
          </Grid>
          <Grid size={{ xs: 12, md: 6 }}><Card><CardContent sx={{ maxHeight: 760, overflow: 'auto' }}><Markup text={text} /></CardContent></Card></Grid>
        </Grid>
      )}
      {tab === 1 && (
        <Card>
          {recs.rows.length === 0 && <CardContent><Typography color="text.secondary">No recommendations. Run "Analyse against DPDP" to check this document.</Typography></CardContent>}
          <Table size="small">
            <TableHead><TableRow>{['Topic', 'Existing clause', 'Recommended modification & reason', 'Legal reference', 'Revised clause', 'Approval'].map(h => <TableCell key={h} sx={{ fontWeight: 700, bgcolor: 'grey.50' }}>{h}</TableCell>)}</TableRow></TableHead>
            <TableBody>
              {recs.rows.map((r: any) => (
                <TableRow key={r.id} sx={{ verticalAlign: 'top' }}>
                  <TableCell sx={{ fontWeight: 600, width: 150 }}>{r.topic}</TableCell>
                  <TableCell sx={{ maxWidth: 260, color: r.existingClause?.startsWith('Not found') ? 'text.disabled' : undefined }}>{r.existingClause}</TableCell>
                  <TableCell sx={{ maxWidth: 260 }}>{r.recommendedModification}<Typography variant="caption" display="block" color="text.secondary" sx={{ mt: 0.5 }}>{r.reason}</Typography></TableCell>
                  <TableCell sx={{ width: 130 }}>{r.legalReference}</TableCell>
                  <TableCell sx={{ maxWidth: 300 }}>{r.revisedClause}</TableCell>
                  <TableCell sx={{ width: 170 }}>
                    <StatusChip value={r.approvalStatus} />
                    {can('policy.approve') && <Stack direction="row" spacing={0.5} sx={{ mt: 1 }}>
                      <Button size="small" color="success" disabled={r.approvalStatus === 'Approved'} onClick={() => act(() => api.post(`/api/policyRecommendations/${r.id}/decide`, { status: 'Approved' }), 'Approved')}>Approve</Button>
                      <Button size="small" color="error" disabled={r.approvalStatus === 'Rejected'} onClick={() => act(() => api.post(`/api/policyRecommendations/${r.id}/decide`, { status: 'Rejected' }), 'Rejected')}>Reject</Button>
                    </Stack>}
                    {r.reviewedBy && <Typography variant="caption" color="text.secondary">{r.reviewedBy}</Typography>}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </Card>
      )}
      <Dialog open={approve} onClose={() => setApprove(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Approve policy internally</DialogTitle>
        <DialogContent>
          <Alert severity="info" sx={{ my: 1 }}>Confirm that legal counsel has reviewed this document. Approval is recorded with your name and time in the audit trail.</Alert>
          <TextField fullWidth multiline minRows={2} label="Review notes" value={notes} onChange={e => setNotes(e.target.value)} />
        </DialogContent>
        <DialogActions><Button onClick={() => setApprove(false)}>Cancel</Button><Button variant="contained" color="success" onClick={async () => { await act(() => api.post(`/api/policies/${id}/approve`, { notes }), 'Policy approved'); setApprove(false); }}>Approve</Button></DialogActions>
      </Dialog>
    </Box>
  );
}

export default function Policies() {
  const { id } = useParams();
  const { orgId, meta, can, toast } = useApp();
  const nav = useNavigate();
  const [gen, setGen] = useState(false);
  const [tpl, setTpl] = useState('data-protection-policy');
  const [up, setUp] = useState<{ open: boolean; file: File | null; title: string; type: string }>({ open: false, file: null, title: '', type: 'uploaded' });
  if (!orgId) return <><PageHeader title="Policies" /><NoOrg /></>;
  if (id) return <PolicyDetail id={Number(id)} />;
  return (
    <Box>
      <PageHeader title="Policies & Notices" subtitle="Generate DPDP policy drafts from templates, or upload your existing policies to get clause-level amendment recommendations instead of replacing them."
        actions={can('policy.edit') && <><Button startIcon={<UploadFileIcon />} onClick={() => setUp({ ...up, open: true })}>Upload existing policy</Button><Button variant="contained" startIcon={<AutoAwesomeIcon />} onClick={() => setGen(true)}>Generate from template</Button></>} />
      <Alert severity="info" variant="outlined" sx={{ mb: 2 }}>Prefer one Data Protection Policy and one privacy notice, and add DPDP clauses to existing HR, IT security, vendor and retention policies. Employment-related processing is a legitimate use under s.7(i) and does not automatically need a separate stack of documents.</Alert>
      <EntityTable entityKey="policies" allowCreate={false} onRowClick={r => nav(`/policies/${r.id}`)} />
      <Dialog open={gen} onClose={() => setGen(false)} maxWidth="xs" fullWidth>
        <DialogTitle>Generate policy draft</DialogTitle>
        <DialogContent><TextField select fullWidth sx={{ mt: 1 }} label="Template" value={tpl} onChange={e => setTpl(e.target.value)}>{meta!.policyTemplates.map(t => <MenuItem key={t.key} value={t.key}>{t.title}</MenuItem>)}</TextField>
          <Typography variant="caption" color="text.secondary" display="block" sx={{ mt: 1 }}>Organisation name, DPO and grievance details are filled automatically. The result is a draft that is not legally approved.</Typography></DialogContent>
        <DialogActions><Button onClick={() => setGen(false)}>Cancel</Button><Button variant="contained" onClick={async () => { try { const r = await api.post(`/api/policies/generate?orgId=${orgId}`, { templateKey: tpl }); nav(`/policies/${r.id}`); } catch (e: any) { toast(e.message, 'error'); } }}>Generate</Button></DialogActions>
      </Dialog>
      <Dialog open={up.open} onClose={() => setUp({ ...up, open: false })} maxWidth="sm" fullWidth>
        <DialogTitle>Upload existing policy for DPDP analysis</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <Button variant="outlined" component="label">{up.file ? up.file.name : 'Choose .docx, .txt or .md'}<input hidden type="file" accept=".docx,.txt,.md" onChange={e => setUp({ ...up, file: e.target.files?.[0] ?? null, title: up.title || (e.target.files?.[0]?.name.replace(/\.[^.]+$/, '') ?? '') })} /></Button>
            <TextField label="Title" value={up.title} onChange={e => setUp({ ...up, title: e.target.value })} />
            <TextField select label="Document type (selects applicable checks)" value={up.type} onChange={e => setUp({ ...up, type: e.target.value })}>
              <MenuItem value="uploaded">General / other</MenuItem>{meta!.policyTemplates.map(t => <MenuItem key={t.key} value={t.key}>{t.title}</MenuItem>)}
            </TextField>
            <Alert severity="info" variant="outlined">The original text is kept unchanged. Recommendations show the existing clause, the proposed change, the reason and the legal reference for your approval.</Alert>
          </Stack>
        </DialogContent>
        <DialogActions><Button onClick={() => setUp({ ...up, open: false })}>Cancel</Button><Button variant="contained" disabled={!up.file} onClick={async () => { try { const r = await api.upload(`/api/policies/upload?orgId=${orgId}`, up.file!, { title: up.title, policyType: up.type }); nav(`/policies/${r.id}`); } catch (e: any) { toast(e.message, 'error'); } }}>Upload & analyse</Button></DialogActions>
      </Dialog>
    </Box>
  );
}
