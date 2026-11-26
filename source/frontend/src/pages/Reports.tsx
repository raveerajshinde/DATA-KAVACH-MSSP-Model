import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Alert, Box, Button, Card, CardContent, Checkbox, Dialog, DialogActions, DialogContent, DialogTitle, FormControlLabel, FormGroup, Grid, MenuItem, Stack, TextField, Typography, LinearProgress } from '@mui/material';
import DescriptionIcon from '@mui/icons-material/Description';
import Inventory2Icon from '@mui/icons-material/Inventory2';
import { api, qs } from '../api';
import { useApp, useLoad } from '../context';
import { NoOrg, PageHeader } from '../components/Common';
import EntityTable from '../components/EntityTable';

const descriptions: Record<string, string> = {
  'executive-summary': 'Board-level overview: indicators, key risks, remediation status, next steps.',
  'gap-assessment': 'Requirement-wise results with detailed findings, recommendations and evidence references.',
  'data-inventory': 'Personal data inventory and data mapping report.',
  'processing-register': 'Record of processing activities per activity.',
  'data-flow': 'Data flow register: sources, destinations, transfers, encryption.',
  'dpia': 'Impact assessment(s) with risk register and heat map.',
  'consent': 'Notice and consent obligations, notices, records and withdrawals.',
  'privacy-policy': 'Current privacy notice / policy (marked draft unless approved).',
  'data-protection-policy': 'Current Data Protection Policy (marked draft unless approved).',
  'vendor-risk': 'Vendor risk and contractual gap analysis.',
  'security': 'Rule 6 safeguards with tool mapping and validation evidence.',
  'rights': 'Data principal requests, SLA performance and register.',
  'breach-readiness': 'Breach requirements, deadline configuration, playbook and drills.',
  'incident': 'Incident and breach report with notification history and timeline.',
  'roadmap': '30/60/90-day and custom remediation plans.',
  'master': 'Complete DPDP master compliance report combining all areas.',
};

export default function Reports() {
  const { orgId, meta, can, toast } = useApp();
  const nav = useNavigate();
  const [f, setF] = useState<any>({ reportType: 'executive-summary', assessmentId: '', periodFrom: '', periodTo: '', templateId: '', entityId: '' });
  const [busy, setBusy] = useState(false);
  const [pkg, setPkg] = useState<{ open: boolean; types: string[]; docx: boolean; pdf: boolean }>({ open: false, types: meta!.reportTypes.map(t => t.key), docx: true, pdf: true });
  const [status] = useLoad<any>(() => api.get('/api/reports/status'), [], { pdfAvailable: true });
  const [assessments] = useLoad<any>(() => (orgId ? api.get(`/api/e/assessments?orgId=${orgId}&pageSize=100`) : Promise.resolve({ rows: [] })), [orgId], { rows: [] });
  const [templates] = useLoad<any[]>(() => api.get(`/api/templates?orgId=${orgId ?? 0}`), [orgId], []);
  const needsEntity = f.reportType === 'dpia' || f.reportType === 'incident';
  const [entities] = useLoad<any>(() => (orgId && needsEntity ? api.get(`/api/e/${f.reportType === 'dpia' ? 'dpias' : 'incidents'}?orgId=${orgId}&pageSize=200`) : Promise.resolve({ rows: [] })), [orgId, f.reportType], { rows: [] });
  if (!orgId) return <><PageHeader title="Report Generator" /><NoOrg /></>;
  const generate = async () => {
    setBusy(true);
    try { const r = await api.post('/api/reports', { orgId, ...Object.fromEntries(Object.entries(f).map(([k, v]) => [k, v === '' ? null : v])) }); nav(`/reports/${r.id}`); }
    catch (e: any) { toast(e.message, 'error'); } finally { setBusy(false); }
  };
  const buildPackage = async () => {
    setBusy(true);
    try {
      await api.download('/api/reports/package', { method: 'POST', body: JSON.stringify({ orgId, types: pkg.types, formats: [pkg.docx && 'docx', pkg.pdf && 'pdf'].filter(Boolean), assessmentId: f.assessmentId || null, periodFrom: f.periodFrom || null, periodTo: f.periodTo || null, templateId: f.templateId || null }) });
      setPkg({ ...pkg, open: false }); toast('Compliance package downloaded');
    } catch (e: any) { toast(e.message, 'error'); } finally { setBusy(false); }
  };
  return (
    <Box>
      <PageHeader title="Report Generator" subtitle="Generate organisation-specific reports from live data, edit the content, approve, then export editable Word (DOCX) and PDF from the same approved content."
        actions={can('report.export') && <Button startIcon={<Inventory2Icon />} onClick={() => setPkg({ ...pkg, open: true })}>Complete compliance package</Button>} />
      {!status.pdfAvailable && <Alert severity="warning" sx={{ mb: 2 }}>PDF engine (LibreOffice) is not installed on the server. DOCX export works; run <code>repair.ps1 -InstallLibreOffice</code> to enable PDF.</Alert>}
      <Grid container spacing={2}>
        <Grid size={{ xs: 12, lg: 7 }}>
          <Card><CardContent>
            <Typography variant="subtitle1" fontWeight={700} sx={{ mb: 2 }}>Report type</Typography>
            <Grid container spacing={1}>
              {meta!.reportTypes.map((t, i) => (
                <Grid key={t.key} size={{ xs: 12, sm: 6 }}>
                  <Box onClick={() => setF({ ...f, reportType: t.key, entityId: '' })} sx={{ p: 1.5, borderRadius: 1.5, cursor: 'pointer', border: '1.5px solid', borderColor: f.reportType === t.key ? 'primary.main' : '#E3E8F2', bgcolor: f.reportType === t.key ? '#EEF3FC' : '#fff', height: '100%' }}>
                    <Stack direction="row" spacing={1} alignItems="center"><DescriptionIcon fontSize="small" color={f.reportType === t.key ? 'primary' : 'action'} /><Typography variant="body2" fontWeight={700}>{i + 1}. {t.title}</Typography></Stack>
                    <Typography variant="caption" color="text.secondary">{descriptions[t.key]}</Typography>
                  </Box>
                </Grid>
              ))}
            </Grid>
          </CardContent></Card>
        </Grid>
        <Grid size={{ xs: 12, lg: 5 }}>
          <Card><CardContent>
            <Typography variant="subtitle1" fontWeight={700} sx={{ mb: 2 }}>Options</Typography>
            <Stack spacing={2}>
              <TextField select size="small" label="Assessment" value={f.assessmentId} onChange={e => setF({ ...f, assessmentId: e.target.value })}><MenuItem value="">Latest assessment</MenuItem>{assessments.rows.map((a: any) => <MenuItem key={a.id} value={a.id}>{a.name}</MenuItem>)}</TextField>
              {needsEntity && <TextField select size="small" label={f.reportType === 'dpia' ? 'DPIA' : 'Incident'} value={f.entityId} onChange={e => setF({ ...f, entityId: e.target.value })}><MenuItem value="">{f.reportType === 'dpia' ? 'All DPIAs' : 'All incidents in period'}</MenuItem>{entities.rows.map((x: any) => <MenuItem key={x.id} value={x.id}>{x.reference ? `${x.reference} - ${x.title}` : x.processingActivity}</MenuItem>)}</TextField>}
              <Stack direction="row" spacing={2}>
                <TextField fullWidth size="small" type="date" label="Reporting period from" InputLabelProps={{ shrink: true }} value={f.periodFrom} onChange={e => setF({ ...f, periodFrom: e.target.value })} />
                <TextField fullWidth size="small" type="date" label="to" InputLabelProps={{ shrink: true }} value={f.periodTo} onChange={e => setF({ ...f, periodTo: e.target.value })} />
              </Stack>
              <TextField select size="small" label="Template (sections, cover, branding overrides)" value={f.templateId} onChange={e => setF({ ...f, templateId: e.target.value })}><MenuItem value="">Organisation branding (default)</MenuItem>{templates.filter(t => t.reportType === '*' || t.reportType === f.reportType).map(t => <MenuItem key={t.id} value={t.id}>{t.name}{t.isSystem ? ' (default)' : ''}</MenuItem>)}</TextField>
              {can('report.edit') && <Button variant="contained" size="large" disabled={busy} onClick={generate}>Generate draft for review</Button>}
              {busy && <LinearProgress />}
              <Typography variant="caption" color="text.secondary">Drafts carry a DRAFT watermark until approved. Edit any text or table before exporting.</Typography>
            </Stack>
          </CardContent></Card>
        </Grid>
      </Grid>
      <Box sx={{ mt: 2 }}>
        <EntityTable entityKey="reports" title="Generated reports" allowCreate={false} allowExport={false} onRowClick={r => nav(`/reports/${r.id}`)} />
      </Box>
      <Dialog open={pkg.open} onClose={() => setPkg({ ...pkg, open: false })} maxWidth="sm" fullWidth>
        <DialogTitle>Complete compliance package</DialogTitle>
        <DialogContent dividers>
          <Typography variant="body2" sx={{ mb: 1 }}>Generates the selected reports with current data and downloads one ZIP with a SHA-256 manifest. Large packages take a few minutes.</Typography>
          <FormGroup>{meta!.reportTypes.map(t => <FormControlLabel key={t.key} control={<Checkbox size="small" checked={pkg.types.includes(t.key)} onChange={e => setPkg({ ...pkg, types: e.target.checked ? [...pkg.types, t.key] : pkg.types.filter(x => x !== t.key) })} />} label={<Typography variant="body2">{t.title}</Typography>} />)}</FormGroup>
          <Stack direction="row" spacing={2} sx={{ mt: 1 }}><FormControlLabel control={<Checkbox checked={pkg.docx} onChange={e => setPkg({ ...pkg, docx: e.target.checked })} />} label="Word (DOCX)" /><FormControlLabel control={<Checkbox checked={pkg.pdf} disabled={!status.pdfAvailable} onChange={e => setPkg({ ...pkg, pdf: e.target.checked })} />} label="PDF" /></Stack>
          {busy && <LinearProgress sx={{ mt: 1 }} />}
        </DialogContent>
        <DialogActions><Button onClick={() => setPkg({ ...pkg, open: false })}>Cancel</Button><Button variant="contained" disabled={busy || pkg.types.length === 0 || (!pkg.docx && !pkg.pdf)} onClick={buildPackage}>Build package</Button></DialogActions>
      </Dialog>
    </Box>
  );
}
