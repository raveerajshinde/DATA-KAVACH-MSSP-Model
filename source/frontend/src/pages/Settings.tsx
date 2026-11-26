import { useEffect, useState } from 'react';
import { Alert, Box, Button, Card, CardContent, FormControlLabel, Grid, List, ListItem, ListItemText, Stack, Switch, TextField, Typography } from '@mui/material';
import QRCode from 'qrcode';
import { api, fmtDateTime } from '../api';
import { useApp, useLoad } from '../context';
import { PageHeader, SectionCard } from '../components/Common';

function Mfa() {
  const { user, login, toast } = useApp();
  const [setup, setSetup] = useState<{ secret: string; uri: string; qr: string } | null>(null);
  const [code, setCode] = useState('');
  const [pw, setPw] = useState('');
  const refresh = async () => login(await api.post('/api/auth/refresh'));
  return (
    <SectionCard title="Multi-factor authentication">
      {user?.mfaEnabled ? (
        <Stack spacing={2}>
          <Alert severity="success">MFA is enabled for your account.</Alert>
          <TextField size="small" type="password" label="Password to disable" value={pw} onChange={e => setPw(e.target.value)} />
          <Button color="error" disabled={!pw} onClick={async () => { try { await api.post('/api/auth/mfa/disable', { password: pw }); await refresh(); toast('MFA disabled', 'warning'); } catch (e: any) { toast(e.message, 'error'); } }}>Disable MFA</Button>
        </Stack>
      ) : setup ? (
        <Stack spacing={2} alignItems="flex-start">
          <Typography variant="body2">Scan with Microsoft Authenticator, Google Authenticator or any TOTP app, then enter the 6-digit code.</Typography>
          <img src={setup.qr} alt="MFA QR code" width={180} height={180} />
          <Typography variant="caption" sx={{ fontFamily: 'monospace' }}>Manual key: {setup.secret}</Typography>
          <Stack direction="row" spacing={1}><TextField size="small" label="Code" value={code} onChange={e => setCode(e.target.value)} /><Button variant="contained" onClick={async () => { try { await api.post('/api/auth/mfa/enable', { code }); await refresh(); setSetup(null); toast('MFA enabled'); } catch (e: any) { toast(e.message, 'error'); } }}>Enable</Button></Stack>
        </Stack>
      ) : (
        <Stack spacing={2}><Typography variant="body2">Protect your account with a time-based one-time code.</Typography><Button variant="contained" sx={{ alignSelf: 'flex-start' }} onClick={async () => { const r = await api.post('/api/auth/mfa/setup'); setSetup({ ...r, qr: await QRCode.toDataURL(r.uri, { margin: 1, width: 360 }) }); }}>Set up MFA</Button></Stack>
      )}
    </SectionCard>
  );
}

function Password() {
  const { toast } = useApp();
  const [f, setF] = useState({ currentPassword: '', newPassword: '', confirm: '' });
  return (
    <SectionCard title="Change password">
      <Stack spacing={1.5}>
        <TextField size="small" type="password" label="Current password" value={f.currentPassword} onChange={e => setF({ ...f, currentPassword: e.target.value })} />
        <TextField size="small" type="password" label="New password" value={f.newPassword} onChange={e => setF({ ...f, newPassword: e.target.value })} />
        <TextField size="small" type="password" label="Confirm" value={f.confirm} onChange={e => setF({ ...f, confirm: e.target.value })} />
        <Button variant="contained" sx={{ alignSelf: 'flex-start' }} disabled={!f.newPassword || f.newPassword !== f.confirm} onClick={async () => { try { await api.post('/api/auth/change-password', f); toast('Password changed'); setF({ currentPassword: '', newPassword: '', confirm: '' }); } catch (e: any) { toast(e.message, 'error'); } }}>Update</Button>
      </Stack>
    </SectionCard>
  );
}

function ProviderBrandingCard() {
  const { branding, reloadBranding, logoUrl, toast } = useApp();
  const [b, setB] = useState<any>(branding);
  useEffect(() => setB(branding), [branding]);
  const save = async () => { try { await api.put('/api/branding', b); await reloadBranding(); toast('Company branding saved - applied to login page, portal and reports'); } catch (e: any) { toast(e.message, 'error'); } };
  const t = (k: string, label: string, props: any = {}) => <TextField size="small" fullWidth label={label} value={b[k] ?? ''} onChange={e => setB({ ...b, [k]: e.target.value })} {...props} />;
  return (
    <SectionCard title="Your company branding (service provider)" action={<Button variant="contained" size="small" onClick={save}>Save branding</Button>}>
      <Grid container spacing={2}>
        <Grid size={{ xs: 12, md: 4 }}>
          <Stack spacing={1.5} alignItems="flex-start">
            <Box sx={{ width: 220, height: 80, border: '1px dashed #cbd5e1', borderRadius: 1, display: 'grid', placeItems: 'center', bgcolor: '#fafafa' }}>
              {logoUrl ? <img src={logoUrl} style={{ maxWidth: 200, maxHeight: 70 }} /> : <Typography variant="caption" color="text.secondary">No logo uploaded</Typography>}
            </Box>
            <Stack direction="row" spacing={1}>
              <Button size="small" component="label">Upload logo (PNG/JPG)<input hidden type="file" accept=".png,.jpg,.jpeg" onChange={async e => { const f = e.target.files?.[0]; if (f) { try { await api.upload('/api/branding/logo', f); await reloadBranding(); toast('Logo updated'); } catch (er: any) { toast(er.message, 'error'); } } e.target.value = ''; }} /></Button>
              {logoUrl && <Button size="small" color="inherit" onClick={async () => { await api.del('/api/branding/logo'); await reloadBranding(); }}>Remove</Button>}
            </Stack>
            <Stack direction="row" spacing={2}>
              {(['primaryColor', 'secondaryColor'] as const).map(k => (
                <Stack key={k} direction="row" spacing={1} alignItems="center">
                  <input type="color" value={b[k] || '#0B2545'} onChange={e => setB({ ...b, [k]: e.target.value.toUpperCase() })} style={{ width: 38, height: 32, border: 0, background: 'none' }} />
                  <TextField size="small" label={k === 'primaryColor' ? 'Primary' : 'Accent'} value={b[k] ?? ''} onChange={e => setB({ ...b, [k]: e.target.value })} sx={{ width: 110 }} />
                </Stack>
              ))}
            </Stack>
            <FormControlLabel control={<Switch checked={!!b.useOnReports} onChange={e => setB({ ...b, useOnReports: e.target.checked })} />} label={<Typography variant="body2">Show as "Prepared by" and consultant logo on client reports (unless a client sets its own)</Typography>} />
          </Stack>
        </Grid>
        <Grid size={{ xs: 12, md: 8 }}>
          <Grid container spacing={1.5}>
            <Grid size={{ xs: 12, sm: 6 }}>{t('name', 'Company name *')}</Grid>
            <Grid size={{ xs: 12, sm: 6 }}>{t('tagline', 'Tagline')}</Grid>
            <Grid size={{ xs: 12, sm: 6 }}>{t('portalName', 'Portal name')}</Grid>
            <Grid size={{ xs: 12, sm: 6 }}>{t('website', 'Website')}</Grid>
            <Grid size={{ xs: 12, sm: 6 }}>{t('supportEmail', 'Support email (shown to customers)')}</Grid>
            <Grid size={{ xs: 12, sm: 6 }}>{t('supportPhone', 'Support phone')}</Grid>
            <Grid size={12}>{t('address', 'Address')}</Grid>
            <Grid size={12}>{t('loginMessage', 'Login page message', { multiline: true, minRows: 2 })}</Grid>
          </Grid>
        </Grid>
      </Grid>
    </SectionCard>
  );
}

function Admin() {
  const { orgId, can, toast } = useApp();
  const [tick, setTick] = useState(0);
  const [s] = useLoad<any>(() => (can('admin.settings') ? api.get(`/api/settings?orgId=${orgId ?? 0}`) : Promise.resolve(null)), [orgId], null);
  const [days, setDays] = useState('');
  useEffect(() => { if (s) setDays(s.rightsResponseDays); }, [s]);
  const { isProvider } = useApp();
  const [backups] = useLoad<any>(() => (can('admin.backup') && isProvider ? api.get('/api/admin/backups') : Promise.resolve(null)), [tick], null);
  const [health] = useLoad<any>(() => api.get('/api/health'), [], null);
  const [busy, setBusy] = useState(false);
  return (
    <Grid container spacing={2}>
      {can('admin.settings') && <Grid size={{ xs: 12, md: 6 }}>
        <SectionCard title="Rights request response period">
          <Stack direction="row" spacing={1} alignItems="center">
            <TextField size="small" type="number" label="Target days (non-grievance requests)" value={days} onChange={e => setDays(e.target.value)} />
            <Button variant="contained" onClick={async () => { try { await api.put(`/api/settings?orgId=${orgId ?? 0}`, { rightsResponseDays: Number(days) }); toast('Saved'); } catch (e: any) { toast(e.message, 'error'); } }}>Save</Button>
          </Stack>
          <Typography variant="caption" color="text.secondary">Grievance deadlines use the organisation's published grievance period (max 90 days, Rule 14(3)).</Typography>
        </SectionCard>
      </Grid>}
      {can('admin.backup') && isProvider && <Grid size={{ xs: 12, md: 6 }}>
        <SectionCard title="Backup" action={<Button variant="contained" size="small" disabled={busy} onClick={async () => { setBusy(true); try { await api.post('/api/admin/backup'); toast('Backup completed'); setTick(t => t + 1); } catch (e: any) { toast(e.message, 'error'); } finally { setBusy(false); } }}>{busy ? 'Running...' : 'Back up now'}</Button>}>
          <Typography variant="caption" color="text.secondary">{backups?.directory}</Typography>
          <List dense sx={{ maxHeight: 220, overflow: 'auto' }}>{backups?.files?.map((f: any) => <ListItem key={f.name} divider><ListItemText primary={f.name} secondary={`${fmtDateTime(f.created)} - ${(f.size / 1048576).toFixed(1)} MB`} /></ListItem>)}</List>
          <Typography variant="caption" color="text.secondary">Scheduled backups and restore are handled by backup.ps1 / restore.ps1 on the server (see Administrator manual).</Typography>
        </SectionCard>
      </Grid>}
      <Grid size={{ xs: 12, md: 6 }}>
        <SectionCard title="System">
          {health && <Typography variant="body2">Version {health.version} - schema v{health.schema} - PDF engine: {health.pdf ? 'available (LibreOffice)' : 'not installed'}</Typography>}
        </SectionCard>
      </Grid>
    </Grid>
  );
}

export default function Settings() {
  const { isProvider, can } = useApp();
  return (
    <Box>
      <PageHeader title="Settings & Security" />
      {isProvider && can('admin.settings') && <Box sx={{ mb: 2 }}><ProviderBrandingCard /></Box>}
      <Grid container spacing={2} sx={{ mb: 2 }}>
        <Grid size={{ xs: 12, md: 6 }}><Mfa /></Grid>
        <Grid size={{ xs: 12, md: 6 }}><Password /></Grid>
      </Grid>
      <Admin />
    </Box>
  );
}
