import { useEffect, useMemo, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { Accordion, AccordionDetails, AccordionSummary, Alert, Box, Button, Chip, Grid, MenuItem, Stack, TextField, ToggleButton, ToggleButtonGroup, Typography, Card, CardContent, Divider } from '@mui/material';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import AttachFileIcon from '@mui/icons-material/AttachFile';
import { api, fmtDate } from '../api';
import { useApp, useLoad } from '../context';
import { Disclaimer, PageHeader, Progress, StatusChip } from '../components/Common';
import EvidencePanel from '../components/EvidencePanel';
import { useDepartments } from '../components/EntityForm';

const STATUSES = ['Compliant', 'Partially Compliant', 'Non-Compliant', 'Not Assessed', 'Not Applicable'];

function ItemEditor({ item, onSaved, canEdit, depts }: { item: any; onSaved: (i: any) => void; canEdit: boolean; depts: string[] }) {
  const { toast } = useApp();
  const [v, setV] = useState<any>(item);
  useEffect(() => setV(item), [item]);
  const dirty = ['status', 'riskRating', 'response', 'gapDescription', 'recommendation', 'department', 'owner', 'targetDate', 'applicabilityNote'].some(k => (v[k] ?? '') !== (item[k] ?? ''));
  const save = async () => {
    try {
      const r = await api.put(`/api/e/assessmentItems/${item.id}`, { status: v.status, riskRating: v.riskRating, response: v.response, gapDescription: v.gapDescription, recommendation: v.recommendation, department: v.department, owner: v.owner, targetDate: v.targetDate, applicabilityNote: v.applicabilityNote });
      onSaved({ ...item, ...r }); toast(`${item.requirementCode} saved`);
    } catch (e: any) { toast(e.message, 'error'); }
  };
  const t = (k: string, label: string, rows = 2) => <TextField fullWidth size="small" label={label} multiline minRows={rows} value={v[k] ?? ''} disabled={!canEdit} onChange={e => setV({ ...v, [k]: e.target.value })} />;
  return (
    <Grid container spacing={2}>
      <Grid size={{ xs: 12, md: 5 }}>
        <Typography variant="overline" color="primary">Requirement</Typography>
        <Typography variant="body2" sx={{ mb: 1 }}>{item.description}</Typography>
        <Typography variant="overline" color="primary">Assessment question</Typography>
        <Typography variant="body2" fontWeight={600} sx={{ mb: 1 }}>{item.question}</Typography>
        <Typography variant="overline" color="primary">Implementation guidance</Typography>
        <Typography variant="body2" sx={{ mb: 1 }}>{item.guidance}</Typography>
        <Typography variant="overline" color="primary">Evidence expected</Typography>
        <Typography variant="body2" sx={{ mb: 1 }}>{item.evidenceRequired}</Typography>
        <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap>
          <Chip size="small" label={item.legalReference} />
          <Chip size="small" variant="outlined" label={`In force: ${fmtDate(item.effectiveFrom)}`} />
          <Chip size="small" variant="outlined" color="error" label={item.penaltyReference} />
        </Stack>
      </Grid>
      <Grid size={{ xs: 12, md: 7 }}>
        <Stack spacing={1.5}>
          <ToggleButtonGroup size="small" exclusive value={v.status} onChange={(_, s) => s && canEdit && setV({ ...v, status: s })} sx={{ flexWrap: 'wrap' }}>
            {STATUSES.map(s => <ToggleButton key={s} value={s} sx={{ textTransform: 'none' }}>{s}</ToggleButton>)}
          </ToggleButtonGroup>
          {v.status === 'Not Applicable' && t('applicabilityNote', 'Reason not applicable', 1)}
          <Stack direction="row" spacing={1.5}>
            <TextField select size="small" label="Risk rating" value={v.riskRating ?? ''} disabled={!canEdit} onChange={e => setV({ ...v, riskRating: e.target.value })} sx={{ minWidth: 140 }}>{['High', 'Medium', 'Low'].map(r => <MenuItem key={r} value={r}>{r}</MenuItem>)}</TextField>
            <TextField select size="small" label="Responsible department" value={v.department ?? ''} disabled={!canEdit} onChange={e => setV({ ...v, department: e.target.value })} sx={{ minWidth: 200 }}><MenuItem value="">-</MenuItem>{depts.map(d => <MenuItem key={d} value={d}>{d}</MenuItem>)}</TextField>
            <TextField size="small" label="Assigned owner" value={v.owner ?? ''} disabled={!canEdit} onChange={e => setV({ ...v, owner: e.target.value })} />
            <TextField size="small" type="date" label="Target date" InputLabelProps={{ shrink: true }} value={v.targetDate ?? ''} disabled={!canEdit} onChange={e => setV({ ...v, targetDate: e.target.value || null })} />
          </Stack>
          {t('response', 'Observations / response')}
          {(v.status === 'Partially Compliant' || v.status === 'Non-Compliant') && <>{t('gapDescription', 'Gap description')}{t('recommendation', 'Recommendation')}</>}
          <Stack direction="row" justifyContent="space-between" alignItems="center">
            <Typography variant="caption" color="text.secondary">{item.assessedBy ? `Last assessed by ${item.assessedBy} on ${fmtDate(item.assessedAt)}` : 'Not yet assessed'}</Typography>
            {canEdit && <Button variant="contained" size="small" disabled={!dirty} onClick={save}>Save</Button>}
          </Stack>
          <Divider />
          <EvidencePanel linkedEntity="assessmentItems" linkedId={item.id} onChanged={() => onSaved({ ...item, evidenceCount: (item.evidenceCount ?? 0) + 1 })} />
        </Stack>
      </Grid>
    </Grid>
  );
}

export default function AssessmentDetail() {
  const { id } = useParams();
  const nav = useNavigate();
  const { can } = useApp();
  const depts = useDepartments();
  const [data, loading, reload, setData] = useLoad<any>(() => api.get(`/api/assessments/${id}/items`), [id], null);
  const [filter, setFilter] = useState({ status: '', module: '', q: '' });
  const [expanded, setExpanded] = useState<number | false>(false);
  const items: any[] = data?.items ?? [];
  const modules = useMemo(() => Array.from(new Set(items.map(i => i.module))), [items]);
  const shown = items.filter(i => (!filter.status || i.status === filter.status) && (!filter.module || i.module === filter.module) && (!filter.q || `${i.requirementCode} ${i.title} ${i.legalReference}`.toLowerCase().includes(filter.q.toLowerCase())));
  if (!data) return <PageHeader title="Assessment" subtitle={loading ? 'Loading...' : ''} />;
  const c = data.counts;
  const updateItem = (n: any) => setData({ ...data, items: items.map(i => (i.id === n.id ? { ...i, ...n } : i)) });
  return (
    <Box>
      <PageHeader title={data.assessment.name} subtitle={`${fmtDate(data.assessment.periodFrom)} - ${fmtDate(data.assessment.periodTo)} | Owner: ${data.assessment.owner ?? '-'}`}
        actions={<><Button onClick={reload}>Recalculate</Button><Button variant="outlined" onClick={() => nav('/roadmap')}>Remediation roadmap</Button><Button variant="contained" onClick={() => nav('/reports')}>Generate report</Button></>} />
      <Disclaimer />
      <Card sx={{ mb: 2 }}><CardContent>
        <Grid container spacing={3} alignItems="center">
          <Grid size={{ xs: 12, md: 4 }}><Progress value={c.completion} label="Applicable requirements assessed" /></Grid>
          <Grid size={{ xs: 12, md: 8 }}>
            <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap>
              {[['Compliant', c.compliant], ['Partially Compliant', c.partial], ['Non-Compliant', c.nonCompliant], ['Not Assessed', c.notAssessed], ['Not Applicable', c.notApplicable]].map(([k, n]) => (
                <Chip key={k as string} clickable onClick={() => setFilter({ ...filter, status: filter.status === k ? '' : (k as string) })} variant={filter.status === k ? 'filled' : 'outlined'} label={`${k}: ${n}`} />
              ))}
              <Chip color="primary" label={`Indicator: ${c.indicator ?? '-'}%`} />
            </Stack>
          </Grid>
        </Grid>
      </CardContent></Card>
      <Stack direction="row" spacing={1.5} sx={{ mb: 2 }}>
        <TextField size="small" placeholder="Search ID, title, reference" value={filter.q} onChange={e => setFilter({ ...filter, q: e.target.value })} sx={{ width: 280 }} />
        <TextField size="small" select label="Area" value={filter.module} onChange={e => setFilter({ ...filter, module: e.target.value })} sx={{ minWidth: 220 }}><MenuItem value="">All areas</MenuItem>{modules.map(m => <MenuItem key={m} value={m}>{m}</MenuItem>)}</TextField>
        {(filter.status || filter.module || filter.q) && <Button onClick={() => setFilter({ status: '', module: '', q: '' })}>Clear filters</Button>}
      </Stack>
      {modules.filter(m => shown.some(i => i.module === m)).map(m => (
        <Box key={m} sx={{ mb: 2 }}>
          <Typography variant="subtitle2" color="primary.dark" sx={{ mb: 1, textTransform: 'uppercase', letterSpacing: 0.8 }}>{m}</Typography>
          {shown.filter(i => i.module === m).map(i => (
            <Accordion key={i.id} expanded={expanded === i.id} onChange={(_, e) => setExpanded(e ? i.id : false)} disableGutters variant="outlined" sx={{ mb: 0.5, '&:before': { display: 'none' } }}>
              <AccordionSummary expandIcon={<ExpandMoreIcon />}>
                <Stack direction="row" spacing={2} alignItems="center" sx={{ width: '100%', pr: 2 }}>
                  <Typography variant="body2" fontWeight={700} sx={{ width: 64, color: 'primary.main' }}>{i.requirementCode}</Typography>
                  <Typography variant="body2" sx={{ flex: 1 }}>{i.title}</Typography>
                  <Typography variant="caption" color="text.secondary" sx={{ display: { xs: 'none', lg: 'block' }, width: 220 }} noWrap>{i.legalReference}</Typography>
                  {i.evidenceCount > 0 && <Chip size="small" icon={<AttachFileIcon />} label={i.evidenceCount} variant="outlined" />}
                  {!['Compliant', 'Not Applicable', 'Not Assessed'].includes(i.status) && <StatusChip value={i.riskRating} />}
                  <StatusChip value={i.status} />
                </Stack>
              </AccordionSummary>
              <AccordionDetails sx={{ bgcolor: '#FBFCFE' }}>{expanded === i.id && <ItemEditor item={i} onSaved={updateItem} canEdit={can('assessment.edit')} depts={depts} />}</AccordionDetails>
            </Accordion>
          ))}
        </Box>
      ))}
      {shown.length === 0 && <Alert severity="info">No requirements match the filter.</Alert>}
    </Box>
  );
}
