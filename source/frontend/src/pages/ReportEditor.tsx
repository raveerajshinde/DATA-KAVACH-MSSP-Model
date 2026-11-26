import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { Alert, Box, Button, Card, CardContent, Chip, Divider, Grid, IconButton, List, ListItemButton, ListItemText, MenuItem, Stack, Switch, Tab, Tabs, TextField, Tooltip, Typography, LinearProgress } from '@mui/material';
import ArrowUpwardIcon from '@mui/icons-material/ArrowUpward';
import ArrowDownwardIcon from '@mui/icons-material/ArrowDownward';
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutline';
import AddIcon from '@mui/icons-material/Add';
import { api, fmtDateTime, qs } from '../api';
import { useApp, useLoad } from '../context';
import { PageHeader, StatusChip, useConfirm } from '../components/Common';
import ReportView from '../components/ReportView';
import { PdfDialog } from './Organizations';

function BlockEditor({ b, onChange, onDelete }: { b: any; onChange: (b: any) => void; onDelete: () => void }) {
  const head = <Stack direction="row" justifyContent="space-between" alignItems="center"><Chip size="small" label={b.type} /><IconButton size="small" onClick={onDelete}><DeleteOutlineIcon fontSize="small" /></IconButton></Stack>;
  const setCell = (ri: number, ci: number, v: string) => onChange({ ...b, rows: b.rows.map((r: string[], i: number) => (i === ri ? r.map((c, j) => (j === ci ? v : c)) : r)) });
  switch (b.type) {
    case 'p': case 'h2': return <Box>{head}<TextField fullWidth multiline minRows={b.type === 'p' ? 3 : 1} size="small" value={b.text ?? ''} onChange={e => onChange({ ...b, text: e.target.value })} helperText={b.type === 'p' ? 'Use **text** for bold' : undefined} /></Box>;
    case 'callout': return <Box>{head}<Stack spacing={1}><TextField size="small" label="Title" value={b.title ?? ''} onChange={e => onChange({ ...b, title: e.target.value })} /><TextField size="small" multiline minRows={2} label="Text" value={b.text ?? ''} onChange={e => onChange({ ...b, text: e.target.value })} /><TextField select size="small" label="Tone" value={b.tone ?? 'info'} onChange={e => onChange({ ...b, tone: e.target.value })}>{['info', 'warning', 'danger', 'success'].map(t => <MenuItem key={t} value={t}>{t}</MenuItem>)}</TextField></Stack></Box>;
    case 'bullets': return <Box>{head}<TextField fullWidth multiline minRows={3} size="small" label="One item per line" value={(b.items || []).join('\n')} onChange={e => onChange({ ...b, items: e.target.value.split('\n') })} /></Box>;
    case 'kv': return (
      <Box>{head}{b.title !== undefined && <TextField size="small" label="Title" value={b.title ?? ''} onChange={e => onChange({ ...b, title: e.target.value })} sx={{ mb: 1 }} />}
        {(b.rows || []).map((r: string[], i: number) => <Stack key={i} direction="row" spacing={1} sx={{ mb: 0.5 }}><TextField size="small" value={r[0]} onChange={e => setCell(i, 0, e.target.value)} sx={{ width: '35%' }} /><TextField size="small" fullWidth multiline value={r[1]} onChange={e => setCell(i, 1, e.target.value)} /></Stack>)}
      </Box>);
    case 'table': return (
      <Box>{head}
        <TextField size="small" label="Table title" value={b.title ?? ''} onChange={e => onChange({ ...b, title: e.target.value })} sx={{ mb: 1 }} />
        <Box sx={{ overflowX: 'auto' }}>
          <table style={{ borderCollapse: 'collapse', minWidth: '100%' }}>
            <thead><tr>{(b.columns || []).map((c: string, j: number) => <th key={j} style={{ padding: 2 }}><TextField size="small" value={c} onChange={e => onChange({ ...b, columns: b.columns.map((x: string, k: number) => (k === j ? e.target.value : x)) })} InputProps={{ sx: { fontWeight: 700, fontSize: 12 } }} /></th>)}<th /></tr></thead>
            <tbody>{(b.rows || []).map((r: string[], i: number) => <tr key={i}>{r.map((c, j) => <td key={j} style={{ padding: 2, minWidth: 120 }}><TextField size="small" multiline fullWidth value={c} onChange={e => setCell(i, j, e.target.value)} InputProps={{ sx: { fontSize: 12 } }} /></td>)}<td><IconButton size="small" onClick={() => onChange({ ...b, rows: b.rows.filter((_: any, k: number) => k !== i) })}><DeleteOutlineIcon fontSize="small" /></IconButton></td></tr>)}</tbody>
          </table>
        </Box>
        <Button size="small" startIcon={<AddIcon />} onClick={() => onChange({ ...b, rows: [...(b.rows || []), (b.columns || ['']).map(() => '')] })}>Add row</Button>
      </Box>);
    case 'chart': return <Box>{head}<TextField size="small" fullWidth label="Chart title" value={b.title ?? ''} onChange={e => onChange({ ...b, title: e.target.value })} /><Typography variant="caption" color="text.secondary">Chart data comes from the assessment; regenerate the report to refresh it.</Typography></Box>;
    default: return <Box>{head}<Typography variant="caption" color="text.secondary">{b.type === 'signature' ? 'Signature block (filled from organisation signatory details)' : 'Page break'}</Typography></Box>;
  }
}

export default function ReportEditor() {
  const { id } = useParams();
  const nav = useNavigate();
  const { orgId, org, can, toast } = useApp();
  const [data, , reload] = useLoad<any>(() => api.get(`/api/reports/${id}`), [id], null);
  const [content, setContent] = useState<any>(null);
  const [dirty, setDirty] = useState(false);
  const [sel, setSel] = useState<string | null>(null);
  const [tab, setTab] = useState(0);
  const [busy, setBusy] = useState('');
  const [pdf, setPdf] = useState<string | null>(null);
  const [filesTick, setFilesTick] = useState(0);
  const [files] = useLoad<any>(() => api.get(`/api/e/reportFiles?${qs({ orgId, parentId: id, pageSize: 50 })}`), [id, filesTick], { rows: [] });
  const { confirm, dialog } = useConfirm();
  useEffect(() => { if (data) { setContent(data.content); setDirty(false); setSel(s => s ?? data.content.sections[0]?.id ?? null); } }, [data]);
  if (!data || !content) return <LinearProgress />;
  const r = data.report;
  const canEdit = can('report.edit');
  const update = (c: any) => { setContent(c); setDirty(true); };
  const sections: any[] = content.sections;
  const cur = sections.find(s => s.id === sel);
  const setSection = (s: any) => update({ ...content, sections: sections.map(x => (x.id === s.id ? s : x)) });
  const move = (i: number, d: number) => { const a = [...sections]; const [x] = a.splice(i, 1); a.splice(i + d, 0, x); update({ ...content, sections: a }); };
  const save = async () => { try { await api.put(`/api/reports/${id}/content`, content); toast(r.status === 'Approved' ? 'Saved - approval reset to Draft' : 'Saved'); reload(); } catch (e: any) { toast(e.message, 'error'); } };
  const run = async (label: string, fn: () => Promise<void>) => { setBusy(label); try { if (dirty) await api.put(`/api/reports/${id}/content`, content); await fn(); setFilesTick(t => t + 1); } catch (e: any) { toast(e.message, 'error'); } finally { setBusy(''); } };
  return (
    <Box>
      <Button onClick={() => nav('/reports')} sx={{ mb: 1 }}>Back to reports</Button>
      <PageHeader title={content.title} subtitle={<Stack direction="row" spacing={1} component="span" alignItems="center"><StatusChip value={r.status} /><Chip size="small" label={`v${r.version}`} /><span>Generated {fmtDateTime(r.createdAt)} by {r.createdBy}</span>{r.approvedBy && <span>- approved by {r.approvedBy}</span>}</Stack>}
        actions={<>
          {canEdit && <Button disabled={!dirty} variant="outlined" onClick={save}>Save edits</Button>}
          {can('report.approve') && r.status !== 'Approved' && <Button color="success" variant="outlined" onClick={() => confirm('Approve report', 'Approve this content for release? Further edits will return it to Draft.', async () => { if (dirty) await api.put(`/api/reports/${id}/content`, content); await api.post(`/api/reports/${id}/approve`); toast('Approved'); reload(); })}>Approve</Button>}
          {can('report.export') && <>
            <Button disabled={!!busy} onClick={() => run('pdfprev', async () => { const { blob } = await api.blob(`/api/reports/${id}/preview.pdf`); setPdf(URL.createObjectURL(blob)); })}>Preview PDF</Button>
            <Button disabled={!!busy} variant="contained" onClick={() => run('docx', () => api.download(`/api/reports/${id}/export?format=docx`))}>Word (DOCX)</Button>
            <Button disabled={!!busy} variant="contained" color="secondary" onClick={() => run('pdf', () => api.download(`/api/reports/${id}/export?format=pdf`))}>PDF</Button>
          </>}
        </>} />
      {busy && <LinearProgress sx={{ mb: 1 }} />}
      {r.status !== 'Approved' && <Alert severity="warning" variant="outlined" sx={{ mb: 2 }}>Draft: exports carry a DRAFT watermark until the report is approved. Word and PDF are produced from exactly this content.</Alert>}
      <Grid container spacing={2}>
        <Grid size={{ xs: 12, md: 3 }}>
          <Card><CardContent sx={{ p: 1 }}>
            <Typography variant="overline" sx={{ px: 1 }}>Sections</Typography>
            <List dense sx={{ maxHeight: '70vh', overflow: 'auto' }}>
              {sections.map((s, i) => (
                <ListItemButton key={s.id} selected={s.id === sel} onClick={() => { setSel(s.id); document.getElementById(`sec-${s.id}`)?.scrollIntoView({ behavior: 'smooth' }); }} sx={{ borderRadius: 1, opacity: s.include ? 1 : 0.5, pr: 0 }}>
                  <ListItemText primary={s.heading} primaryTypographyProps={{ fontSize: 13, noWrap: true }} />
                  {canEdit && <Stack direction="row" onClick={e => e.stopPropagation()}>
                    <Tooltip title={s.include ? 'Included' : 'Excluded'}><Switch size="small" checked={s.include} onChange={e => setSection({ ...s, include: e.target.checked })} /></Tooltip>
                    <IconButton size="small" disabled={i === 0} onClick={() => move(i, -1)}><ArrowUpwardIcon sx={{ fontSize: 15 }} /></IconButton>
                    <IconButton size="small" disabled={i === sections.length - 1} onClick={() => move(i, 1)}><ArrowDownwardIcon sx={{ fontSize: 15 }} /></IconButton>
                  </Stack>}
                </ListItemButton>
              ))}
            </List>
            {canEdit && <Button size="small" startIcon={<AddIcon />} onClick={() => { const nid = `custom-${Date.now()}`; update({ ...content, sections: [...sections.slice(0, -1), { id: nid, heading: 'New section', level: 1, include: true, blocks: [{ type: 'p', text: '' }] }, sections[sections.length - 1]] }); setSel(nid); setTab(0); }}>Add section</Button>}
          </CardContent></Card>
          <Card sx={{ mt: 2 }}><CardContent>
            <Typography variant="overline">Export history</Typography>
            {files.rows.map((f: any) => <Box key={f.id} sx={{ py: 0.5 }}><Button size="small" onClick={() => api.download(`/api/files/report/${f.id}`)}>{f.format.toUpperCase()}</Button><Typography variant="caption" color="text.secondary">{fmtDateTime(f.createdAt)} - {f.createdBy}</Typography></Box>)}
            {files.rows.length === 0 && <Typography variant="caption" color="text.secondary">Not exported yet.</Typography>}
          </CardContent></Card>
        </Grid>
        <Grid size={{ xs: 12, md: 9 }}>
          <Card sx={{ mb: 2 }}><Tabs value={tab} onChange={(_, v) => setTab(v)}><Tab label="Edit section" /><Tab label="Preview whole report" /></Tabs></Card>
          {tab === 0 && cur && (
            <Card><CardContent>
              <Stack spacing={2}>
                <Stack direction="row" spacing={2}>
                  <TextField size="small" fullWidth label="Section heading" value={cur.heading} disabled={!canEdit} onChange={e => setSection({ ...cur, heading: e.target.value })} />
                  <TextField size="small" select label="Level" value={cur.level} disabled={!canEdit} onChange={e => setSection({ ...cur, level: Number(e.target.value) })} sx={{ width: 120 }}><MenuItem value={1}>Heading 1</MenuItem><MenuItem value={2}>Heading 2</MenuItem></TextField>
                  <Stack direction="row" alignItems="center"><Switch checked={!!cur.pageBreakBefore} disabled={!canEdit} onChange={e => setSection({ ...cur, pageBreakBefore: e.target.checked })} /><Typography variant="body2" noWrap>New page</Typography></Stack>
                </Stack>
                <Divider />
                {cur.blocks.map((b: any, i: number) => (
                  <Box key={i} sx={{ p: 1.5, border: '1px solid #E3E8F2', borderRadius: 1.5 }}>
                    {canEdit ? <BlockEditor b={b} onChange={nb => setSection({ ...cur, blocks: cur.blocks.map((x: any, j: number) => (j === i ? nb : x)) })} onDelete={() => setSection({ ...cur, blocks: cur.blocks.filter((_: any, j: number) => j !== i) })} /> : <ReportView content={{ title: '', sections: [{ ...cur, blocks: [b], include: true }] }} />}
                  </Box>
                ))}
                {canEdit && <Stack direction="row" spacing={1}>
                  {[['p', 'Paragraph'], ['bullets', 'Bullet list'], ['table', 'Table'], ['callout', 'Callout'], ['pagebreak', 'Page break']].map(([t, l]) => (
                    <Button key={t} size="small" startIcon={<AddIcon />} onClick={() => setSection({ ...cur, blocks: [...cur.blocks, t === 'table' ? { type: 'table', columns: ['Column 1', 'Column 2'], rows: [['', '']] } : t === 'bullets' ? { type: 'bullets', items: [''] } : { type: t, text: '' }] })}>{l}</Button>
                  ))}
                </Stack>}
              </Stack>
            </CardContent></Card>
          )}
          {tab === 1 && <Card><CardContent sx={{ maxHeight: '78vh', overflow: 'auto', bgcolor: '#fff', px: 5 }}><ReportView content={content} primary={org?.primaryColor || '#0B3D91'} secondary={org?.secondaryColor || '#F2A900'} font={org?.fontFamily || 'Calibri'} /></CardContent></Card>}
        </Grid>
      </Grid>
      <PdfDialog url={pdf} onClose={() => { if (pdf) URL.revokeObjectURL(pdf); setPdf(null); }} title="PDF preview" />
      {dialog}
    </Box>
  );
}
