import { useEffect, useState } from 'react';
import { Alert, Box, Button, Card, CardContent, Dialog, DialogActions, DialogContent, DialogTitle, Grid, MenuItem, Stack, Tab, Tabs, TextField, Typography, Chip } from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import UploadIcon from '@mui/icons-material/Upload';
import PictureAsPdfIcon from '@mui/icons-material/PictureAsPdf';
import { api } from '../api';
import { useApp } from '../context';
import { PageHeader, SectionCard } from '../components/Common';
import { EntityFields } from '../components/EntityForm';
import EntityTable from '../components/EntityTable';
import { COVERS, CoverThumb, PagePreview, useStoredImage } from '../components/BrandPreview';

const groupsByTab = [['Profile'], ['Privacy contacts'], ['Applicability'], ['Report details'], ['Branding']];

export function PdfDialog({ url, onClose, title }: { url: string | null; onClose: () => void; title: string }) {
  return (
    <Dialog open={!!url} onClose={onClose} maxWidth="lg" fullWidth>
      <DialogTitle>{title}</DialogTitle>
      <DialogContent sx={{ p: 0, height: '80vh' }}>{url && <iframe title={title} src={url} style={{ width: '100%', height: '100%', border: 0 }} />}</DialogContent>
      <DialogActions><Button onClick={onClose}>Close</Button></DialogActions>
    </Dialog>
  );
}

export default function Organizations() {
  const { entity, orgId, org, orgs, reloadOrgs, selectOrg, toast, can, isProvider } = useApp();
  const meta = entity('organizations');
  const [tab, setTab] = useState(0);
  const [values, setValues] = useState<any>({});
  const [dirty, setDirty] = useState(false);
  const [create, setCreate] = useState<{ open: boolean; name: string; industry: string }>({ open: false, name: '', industry: '' });
  const [pdf, setPdf] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const logo = useStoredImage(values.logoFileId);
  const clogo = useStoredImage(values.consultantLogoFileId);

  const load = () => { if (orgId) api.get(`/api/e/organizations/${orgId}`).then(v => { setValues(v); setDirty(false); }); };
  useEffect(load, [orgId]);

  const save = async () => {
    try { await api.put(`/api/e/organizations/${orgId}`, values); toast('Organisation saved'); setDirty(false); await reloadOrgs(); }
    catch (e: any) { toast(e.message, 'error'); }
  };
  const createOrg = async () => {
    try {
      const r = await api.post('/api/e/organizations', { name: create.name, industry: create.industry || null });
      await reloadOrgs(); selectOrg(r.id); setCreate({ open: false, name: '', industry: '' }); toast('Organisation created with default departments and Rule 6 controls');
    } catch (e: any) { toast(e.message, 'error'); }
  };
  const upload = async (kind: 'logo' | 'consultant', f: File) => {
    try { await api.upload(`/api/organizations/${orgId}/logo?kind=${kind}`, f); toast('Logo uploaded'); load(); }
    catch (e: any) { toast(e.message, 'error'); }
  };
  const previewPdf = async () => {
    setBusy(true);
    try { if (dirty) await save(); const { blob } = await api.blob(`/api/reports/branding-preview?orgId=${orgId}`); setPdf(URL.createObjectURL(blob)); }
    catch (e: any) { toast(e.message, 'error'); } finally { setBusy(false); }
  };
  const set = (v: any) => { setValues(v); setDirty(true); };
  const canEdit = can('org.edit');
  const brandingCols = meta.cols.filter(c => c.group === 'Branding');

  return (
    <Box>
      <PageHeader title={isProvider ? 'Client Profile & Branding' : 'Organisation Profile'} subtitle="Profile details, applicability and branding automatically populate every generated report."
        actions={<>{canEdit && isProvider && <Button startIcon={<AddIcon />} onClick={() => setCreate({ ...create, open: true })}>New organisation</Button>}{canEdit && org && <Button variant="contained" disabled={!dirty} onClick={save}>Save changes</Button>}</>} />
      {orgs.length === 0 && <Alert severity="info" action={<Button onClick={() => setCreate({ ...create, open: true })}>Create</Button>}>Create your first organisation to begin.</Alert>}
      {org && (
        <>
          <Card sx={{ mb: 2 }}>
            <Tabs value={tab} onChange={(_, v) => setTab(v)} variant="scrollable">
              <Tab label="Profile" /><Tab label="Privacy contacts" /><Tab label="Applicability" /><Tab label="Report details" /><Tab label="Branding" /><Tab label="Departments" />
            </Tabs>
          </Card>
          {tab < 4 && (
            <Card><CardContent>
              {tab === 2 && <Alert severity="info" variant="outlined" sx={{ mb: 2 }}>Applicability drives which requirements are auto-marked Not Applicable when an assessment is created. DPIA and independent audit are mandatory only for notified Significant Data Fiduciaries (s.10, Rule 13).</Alert>}
              <EntityFields meta={{ ...meta, cols: meta.cols.filter(c => groupsByTab[tab].includes(c.group || '')) }} values={values} setValues={set} disabled={!canEdit} />
            </CardContent></Card>
          )}
          {tab === 4 && (
            <Grid container spacing={2}>
              <Grid size={{ xs: 12, lg: 5 }}>
                <SectionCard title="Logos">
                  <Stack spacing={2}>
                    {(['logo', 'consultant'] as const).map(k => (
                      <Stack key={k} direction="row" spacing={2} alignItems="center">
                        <Box sx={{ width: 140, height: 56, border: '1px dashed #cbd5e1', borderRadius: 1, display: 'grid', placeItems: 'center', bgcolor: '#fafafa' }}>
                          {(k === 'logo' ? logo : clogo) ? <img src={(k === 'logo' ? logo : clogo)!} style={{ maxWidth: 130, maxHeight: 48 }} /> : <Typography variant="caption" color="text.secondary">No logo</Typography>}
                        </Box>
                        <Box>
                          <Typography variant="body2" fontWeight={600}>{k === 'logo' ? 'Company logo' : 'Consultant logo (optional)'}</Typography>
                          <Stack direction="row" spacing={1}>
                            {canEdit && <Button size="small" component="label" startIcon={<UploadIcon />}>Upload PNG/JPG<input hidden type="file" accept=".png,.jpg,.jpeg" onChange={e => { const f = e.target.files?.[0]; if (f) upload(k, f); e.target.value = ''; }} /></Button>}
                            {canEdit && (k === 'logo' ? values.logoFileId : values.consultantLogoFileId) > 0 && <Button size="small" color="inherit" onClick={async () => { await api.del(`/api/organizations/${orgId}/logo?kind=${k}`); load(); }}>Remove</Button>}
                          </Stack>
                        </Box>
                      </Stack>
                    ))}
                  </Stack>
                </SectionCard>
                <Box sx={{ mt: 2 }}>
                  <SectionCard title="Colours, fonts, header, footer and signature">
                    <Stack direction="row" spacing={2} sx={{ mb: 2 }}>
                      {(['primaryColor', 'secondaryColor'] as const).map(k => (
                        <Stack key={k} direction="row" spacing={1} alignItems="center">
                          <input type="color" value={values[k] || '#0B3D91'} disabled={!canEdit} onChange={e => set({ ...values, [k]: e.target.value.toUpperCase() })} style={{ width: 42, height: 34, border: 0, background: 'none' }} />
                          <TextField size="small" label={k === 'primaryColor' ? 'Primary' : 'Secondary'} value={values[k] || ''} onChange={e => set({ ...values, [k]: e.target.value })} sx={{ width: 120 }} />
                        </Stack>
                      ))}
                    </Stack>
                    <EntityFields meta={{ ...meta, cols: brandingCols.filter(c => !['primaryColor', 'secondaryColor', 'coverDesign'].includes(c.name)) }} values={values} setValues={set} disabled={!canEdit} />
                  </SectionCard>
                </Box>
              </Grid>
              <Grid size={{ xs: 12, lg: 7 }}>
                <SectionCard title="Cover page design" action={<Button size="small" startIcon={<PictureAsPdfIcon />} disabled={busy} onClick={previewPdf}>{busy ? 'Rendering...' : 'Preview actual PDF'}</Button>}>
                  <Stack direction="row" spacing={1.5} sx={{ overflowX: 'auto', pb: 1 }}>
                    {COVERS.map(c => (
                      <Box key={c} onClick={() => canEdit && set({ ...values, coverDesign: c })} sx={{ cursor: 'pointer', p: 0.75, borderRadius: 1.5, border: '2px solid', borderColor: (values.coverDesign || 'Corporate Band') === c ? 'secondary.main' : 'transparent' }}>
                        <CoverThumb design={c} scale={0.5} primary={values.primaryColor} secondary={values.secondaryColor} font={values.fontFamily} org={values.name} logo={logo} title="DPDP Report" />
                        <Typography variant="caption" display="block" textAlign="center" sx={{ mt: 0.5 }}>{c}</Typography>
                      </Box>
                    ))}
                  </Stack>
                  <Typography variant="subtitle2" sx={{ mt: 2, mb: 1 }}>Live preview <Chip size="small" label="updates as you edit" sx={{ ml: 1 }} /></Typography>
                  <Stack direction={{ xs: 'column', md: 'row' }} spacing={2} sx={{ bgcolor: '#EEF1F7', p: 2, borderRadius: 2, justifyContent: 'center' }}>
                    <CoverThumb design={values.coverDesign || 'Corporate Band'} scale={1.25} primary={values.primaryColor} secondary={values.secondaryColor} font={values.fontFamily} org={values.name} logo={values.logoPlacement === 'none' ? null : logo} consultantLogo={clogo} title="DPDP Compliance Gap Assessment" watermark={values.watermark} />
                    <PagePreview scale={1.25} primary={values.primaryColor} secondary={values.secondaryColor} font={values.fontFamily} org={values.name} header={values.headerText} footer={values.footerText} logo={logo} logoPlacement={values.logoPlacement} watermark={values.watermark} />
                  </Stack>
                </SectionCard>
              </Grid>
            </Grid>
          )}
          {tab === 5 && <EntityTable entityKey="departments" title="Departments" allowExport={false} />}
        </>
      )}
      <Dialog open={create.open} onClose={() => setCreate({ ...create, open: false })} maxWidth="xs" fullWidth>
        <DialogTitle>New organisation</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <TextField label="Organisation name" value={create.name} onChange={e => setCreate({ ...create, name: e.target.value })} autoFocus />
            <TextField select label="Industry" value={create.industry} onChange={e => setCreate({ ...create, industry: e.target.value })}>{meta.cols.find(c => c.name === 'industry')!.options!.map(o => <MenuItem key={o} value={o}>{o}</MenuItem>)}</TextField>
          </Stack>
        </DialogContent>
        <DialogActions><Button onClick={() => setCreate({ ...create, open: false })}>Cancel</Button><Button variant="contained" disabled={!create.name} onClick={createOrg}>Create</Button></DialogActions>
      </Dialog>
      <PdfDialog url={pdf} onClose={() => { if (pdf) URL.revokeObjectURL(pdf); setPdf(null); }} title="Branding preview (rendered by the report engine)" />
    </Box>
  );
}
