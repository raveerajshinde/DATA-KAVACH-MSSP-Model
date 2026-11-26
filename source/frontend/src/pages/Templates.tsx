import { useEffect, useState } from 'react';
import { Alert, Box, Button, Card, CardContent, Chip, Grid, IconButton, List, ListItemButton, ListItemText, MenuItem, Stack, Switch, TextField, Typography, Divider } from '@mui/material';
import DragIndicatorIcon from '@mui/icons-material/DragIndicator';
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutline';
import AddIcon from '@mui/icons-material/Add';
import ContentCopyIcon from '@mui/icons-material/ContentCopy';
import RestoreIcon from '@mui/icons-material/Restore';
import { api } from '../api';
import { useApp, useLoad } from '../context';
import { NoOrg, PageHeader, useConfirm } from '../components/Common';
import { COVERS, CoverThumb, useStoredImage } from '../components/BrandPreview';
import { PdfDialog } from './Organizations';

interface Sec { id: string; heading?: string; include: boolean; order: number; custom?: boolean; pageBreakBefore?: boolean; blocks?: any[]; defaultHeading?: string }

export default function Templates() {
  const { orgId, org, meta, can, toast } = useApp();
  const [tick, setTick] = useState(0);
  const [list] = useLoad<any[]>(() => api.get(`/api/templates?orgId=${orgId ?? 0}`), [orgId, tick], []);
  const [selId, setSelId] = useState<number | null>(null);
  const [t, setT] = useState<any>(null);
  const [secs, setSecs] = useState<Sec[]>([]);
  const [drag, setDrag] = useState<number | null>(null);
  const [pdf, setPdf] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const { confirm, dialog } = useConfirm();
  const logo = useStoredImage(org?.logoFileId);
  const editable = t && !t.isSystem && can('template.edit');

  useEffect(() => {
    const cur = list.find(x => x.id === selId) ?? null;
    setT(cur ? { ...cur } : null);
  }, [selId, list]);

  // build section list = outline of chosen report type merged with saved config
  useEffect(() => {
    if (!t || !orgId) { setSecs([]); return; }
    const type = t.reportType && t.reportType !== '*' ? t.reportType : 'master';
    api.get(`/api/reports/outline?orgId=${orgId}&type=${type}`).then((outline: any[]) => {
      const saved: Sec[] = (() => { try { return JSON.parse(t.sectionsJson || '[]'); } catch { return []; } })();
      const byId = Object.fromEntries(saved.map(s => [s.id, s]));
      const merged: Sec[] = outline.map((o, i) => ({ id: o.id, defaultHeading: o.heading, heading: byId[o.id]?.heading, include: byId[o.id]?.include ?? true, order: byId[o.id]?.order ?? (i + 1) * 10, pageBreakBefore: byId[o.id]?.pageBreakBefore }));
      saved.filter(s => s.custom).forEach(s => merged.push(s));
      setSecs(merged.sort((a, b) => a.order - b.order));
    }).catch(e => toast(e.message, 'error'));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [t?.id, t?.reportType, orgId]);

  if (!orgId) return <><PageHeader title="Template Designer" /><NoOrg /></>;

  const payload = () => ({ ...t, sectionsJson: JSON.stringify(secs.map((s, i) => ({ id: s.id, include: s.include, order: (i + 1) * 10, heading: s.heading || undefined, custom: s.custom, pageBreakBefore: s.pageBreakBefore, blocks: s.blocks }))) });
  const save = async () => { try { await api.put(`/api/templates/${t.id}`, payload()); toast('Template saved'); setTick(x => x + 1); } catch (e: any) { toast(e.message, 'error'); } };
  const duplicate = async (id: number) => { const r = await api.post(`/api/templates/${id}/duplicate?orgId=${orgId}`); setTick(x => x + 1); setSelId(r.id); toast('Template duplicated - you can now customise it'); };
  const preview = async () => {
    setBusy(true);
    try { if (editable) await api.put(`/api/templates/${t.id}`, payload()); const { blob } = await api.blob(`/api/reports/branding-preview?orgId=${orgId}&templateId=${t.id}`); setPdf(URL.createObjectURL(blob)); }
    catch (e: any) { toast(e.message, 'error'); } finally { setBusy(false); }
  };
  const onDrop = (to: number) => { if (drag === null || drag === to) return; const a = [...secs]; const [x] = a.splice(drag, 1); a.splice(to, 0, x); setSecs(a); setDrag(null); };
  const setS = (i: number, p: Partial<Sec>) => setSecs(secs.map((s, j) => (j === i ? { ...s, ...p } : s)));

  return (
    <Box>
      <PageHeader title="Report Template Designer" subtitle="Arrange sections by drag-and-drop, include or exclude them, add your own text, tables and page breaks, and choose cover, colours, fonts, logo placement and watermark. Each organisation keeps its own templates."
        actions={can('template.edit') && <Button startIcon={<RestoreIcon />} onClick={() => confirm('Restore default templates', 'Reset the five built-in templates to factory settings? Your own templates are not affected.', async () => { await api.post('/api/templates/restore-defaults'); setTick(x => x + 1); toast('Defaults restored'); })}>Restore defaults</Button>} />
      <Grid container spacing={2}>
        <Grid size={{ xs: 12, md: 3 }}>
          <Card><CardContent sx={{ p: 1 }}>
            <List dense>
              {list.map(x => (
                <ListItemButton key={x.id} selected={x.id === selId} onClick={() => setSelId(x.id)} sx={{ borderRadius: 1 }}>
                  <ListItemText primary={x.name} secondary={`${x.reportType === '*' ? 'Any report' : meta!.reportTypes.find(r => r.key === x.reportType)?.title} - ${x.coverDesign}`} />
                  {x.isSystem && <Chip size="small" label="default" />}
                </ListItemButton>
              ))}
            </List>
          </CardContent></Card>
        </Grid>
        <Grid size={{ xs: 12, md: 9 }}>
          {!t && <Alert severity="info">Select a template. Default templates are read-only - duplicate one to customise it for this organisation.</Alert>}
          {t && (
            <Stack spacing={2}>
              <Card><CardContent>
                <Stack direction="row" spacing={1} justifyContent="space-between" sx={{ mb: 2 }}>
                  <Typography variant="subtitle1" fontWeight={700}>{t.name}</Typography>
                  <Stack direction="row" spacing={1}>
                    {can('template.edit') && <Button startIcon={<ContentCopyIcon />} onClick={() => duplicate(t.id)}>Duplicate</Button>}
                    {editable && can('template.delete') && <Button color="error" onClick={() => confirm('Delete template', `Delete "${t.name}"?`, async () => { await api.del(`/api/templates/${t.id}`); setSelId(null); setTick(x => x + 1); }, true, 'Delete')}>Delete</Button>}
                    <Button disabled={busy} onClick={preview}>{busy ? 'Rendering...' : 'Preview PDF'}</Button>
                    {editable && <Button variant="contained" onClick={save}>Save template</Button>}
                  </Stack>
                </Stack>
                {t.isSystem && <Alert severity="info" variant="outlined" sx={{ mb: 2 }}>Default template - duplicate to customise.</Alert>}
                <Grid container spacing={1.5}>
                  <Grid size={{ xs: 12, md: 4 }}><TextField size="small" fullWidth label="Name" value={t.name} disabled={!editable} onChange={e => setT({ ...t, name: e.target.value })} /></Grid>
                  <Grid size={{ xs: 12, md: 4 }}><TextField size="small" select fullWidth label="Report type" value={t.reportType} disabled={!editable} onChange={e => setT({ ...t, reportType: e.target.value })}><MenuItem value="*">Any report</MenuItem>{meta!.reportTypes.map(r => <MenuItem key={r.key} value={r.key}>{r.title}</MenuItem>)}</TextField></Grid>
                  <Grid size={{ xs: 6, md: 2 }}><TextField size="small" select fullWidth label="Logo placement" value={t.logoPlacement ?? ''} disabled={!editable} onChange={e => setT({ ...t, logoPlacement: e.target.value })}>{['Header left', 'Header right', 'Cover only'].map(o => <MenuItem key={o} value={o}>{o}</MenuItem>)}</TextField></Grid>
                  <Grid size={{ xs: 6, md: 2 }}><TextField size="small" select fullWidth label="Font" value={t.fontFamily ?? ''} disabled={!editable} onChange={e => setT({ ...t, fontFamily: e.target.value })}><MenuItem value="">Organisation font</MenuItem>{['Calibri', 'Arial', 'Segoe UI', 'Cambria', 'Georgia', 'Times New Roman', 'Verdana', 'Tahoma'].map(o => <MenuItem key={o} value={o}>{o}</MenuItem>)}</TextField></Grid>
                  <Grid size={{ xs: 12, md: 4 }}><TextField size="small" fullWidth label="Watermark (blank = DRAFT until approved)" value={t.watermark ?? ''} disabled={!editable} onChange={e => setT({ ...t, watermark: e.target.value })} /></Grid>
                  {(['primaryColor', 'secondaryColor'] as const).map(k => (
                    <Grid key={k} size={{ xs: 6, md: 4 }}><Stack direction="row" spacing={1} alignItems="center"><input type="color" disabled={!editable} value={t[k] || (k === 'primaryColor' ? org?.primaryColor : org?.secondaryColor) || '#0B3D91'} onChange={e => setT({ ...t, [k]: e.target.value.toUpperCase() })} style={{ width: 40, height: 34, border: 0, background: 'none' }} /><TextField size="small" fullWidth label={k === 'primaryColor' ? 'Primary colour (blank = organisation)' : 'Secondary colour'} value={t[k] ?? ''} disabled={!editable} onChange={e => setT({ ...t, [k]: e.target.value })} /></Stack></Grid>
                  ))}
                </Grid>
                <Typography variant="subtitle2" sx={{ mt: 2, mb: 1 }}>Cover page design</Typography>
                <Stack direction="row" spacing={1.5} sx={{ overflowX: 'auto', pb: 1 }}>
                  {COVERS.map(c => (
                    <Box key={c} onClick={() => editable && setT({ ...t, coverDesign: c })} sx={{ cursor: editable ? 'pointer' : 'default', p: 0.75, borderRadius: 1.5, border: '2px solid', borderColor: t.coverDesign === c ? 'secondary.main' : 'transparent' }}>
                      <CoverThumb design={c} scale={0.55} primary={t.primaryColor || org?.primaryColor} secondary={t.secondaryColor || org?.secondaryColor} font={t.fontFamily || org?.fontFamily} org={org?.name} logo={logo} title="DPDP Report" watermark={t.watermark} />
                      <Typography variant="caption" display="block" textAlign="center">{c}</Typography>
                    </Box>
                  ))}
                </Stack>
              </CardContent></Card>
              <Card><CardContent>
                <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ mb: 1 }}>
                  <Typography variant="subtitle1" fontWeight={700}>Sections</Typography>
                  {editable && <Stack direction="row" spacing={1}>
                    <Button size="small" startIcon={<AddIcon />} onClick={() => setSecs([{ id: `custom-${Date.now()}`, custom: true, include: true, order: 0, heading: 'Custom section', blocks: [{ type: 'p', text: '' }] }, ...secs])}>Custom text section</Button>
                    <Button size="small" startIcon={<AddIcon />} onClick={() => setSecs([...secs, { id: `custom-${Date.now()}`, custom: true, include: true, order: 0, heading: 'Additional table', blocks: [{ type: 'table', columns: ['Item', 'Detail'], rows: [['', '']] }] }])}>Custom table section</Button>
                  </Stack>}
                </Stack>
                <Typography variant="caption" color="text.secondary">Drag to reorder. Sections not present in a particular report are ignored. Sign-off always comes last.</Typography>
                <Divider sx={{ my: 1 }} />
                {secs.map((s, i) => (
                  <Box key={s.id} draggable={!!editable} onDragStart={() => setDrag(i)} onDragOver={e => e.preventDefault()} onDrop={() => onDrop(i)}
                    sx={{ display: 'flex', alignItems: 'flex-start', gap: 1, p: 1, mb: 0.5, borderRadius: 1.5, border: '1px solid #E3E8F2', bgcolor: drag === i ? '#EEF3FC' : s.include ? '#fff' : '#F7F7F7' }}>
                    <DragIndicatorIcon sx={{ color: 'text.disabled', cursor: editable ? 'grab' : 'default', mt: 0.8 }} />
                    <Box sx={{ flex: 1 }}>
                      <Stack direction="row" spacing={1} alignItems="center">
                        <TextField size="small" variant="standard" fullWidth value={s.heading ?? ''} placeholder={s.defaultHeading} disabled={!editable} onChange={e => setS(i, { heading: e.target.value })} />
                        {s.custom && <Chip size="small" label="custom" color="secondary" />}
                        <Stack direction="row" alignItems="center"><Typography variant="caption">New page</Typography><Switch size="small" checked={!!s.pageBreakBefore} disabled={!editable} onChange={e => setS(i, { pageBreakBefore: e.target.checked })} /></Stack>
                        <Stack direction="row" alignItems="center"><Typography variant="caption">Include</Typography><Switch size="small" checked={s.include} disabled={!editable} onChange={e => setS(i, { include: e.target.checked })} /></Stack>
                        {s.custom && editable && <IconButton size="small" onClick={() => setSecs(secs.filter((_, j) => j !== i))}><DeleteOutlineIcon fontSize="small" /></IconButton>}
                      </Stack>
                      {s.custom && s.blocks?.[0]?.type === 'p' && <TextField size="small" fullWidth multiline minRows={2} sx={{ mt: 1 }} placeholder="Section text" value={s.blocks[0].text} disabled={!editable} onChange={e => setS(i, { blocks: [{ type: 'p', text: e.target.value }] })} />}
                      {s.custom && s.blocks?.[0]?.type === 'table' && <TextField size="small" fullWidth multiline minRows={2} sx={{ mt: 1 }} label="Table rows: one row per line, cells separated by |  (first line = header)" value={[s.blocks[0].columns.join(' | '), ...s.blocks[0].rows.map((r: string[]) => r.join(' | '))].join('\n')} disabled={!editable}
                        onChange={e => { const lines = e.target.value.split('\n'); const cols = (lines[0] || '').split('|').map(x => x.trim()); setS(i, { blocks: [{ type: 'table', columns: cols, rows: lines.slice(1).map(l => l.split('|').map(x => x.trim())) }] }); }} />}
                    </Box>
                  </Box>
                ))}
              </CardContent></Card>
            </Stack>
          )}
        </Grid>
      </Grid>
      <PdfDialog url={pdf} onClose={() => { if (pdf) URL.revokeObjectURL(pdf); setPdf(null); }} title="Template preview" />
      {dialog}
    </Box>
  );
}
