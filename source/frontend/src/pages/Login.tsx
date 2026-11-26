import { useEffect, useState } from 'react';
import { Alert, Box, Button, Card, CardContent, Stack, TextField, Typography } from '@mui/material';
import { api } from '../api';
import { useApp } from '../context';
import { Logo } from '../components/Layout';

export default function Login() {
  const { login, user, logout, branding } = useApp();
  const [mode, setMode] = useState<'loading' | 'setup' | 'login' | 'mfa' | 'change'>('loading');
  const [f, setF] = useState({ email: '', password: '', name: '', code: '', newPassword: '', confirm: '' });
  const [err, setErr] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (user?.mustChangePassword) { setMode('change'); setF(s => ({ ...s, password: '' })); return; }
    api.get('/api/setup/status').then(r => setMode(r.needsSetup ? 'setup' : 'login')).catch(() => setMode('login'));
  }, [user]);

  const set = (k: string) => (e: any) => setF({ ...f, [k]: e.target.value });
  const run = async (fn: () => Promise<void>) => { setBusy(true); setErr(null); try { await fn(); } catch (e: any) { setErr(e.message); } finally { setBusy(false); } };

  const doLogin = () => run(async () => {
    const r = await api.post('/api/auth/login', { email: f.email, password: f.password, mfaCode: mode === 'mfa' ? f.code : undefined });
    if (r.mfaRequired) { setMode('mfa'); return; }
    login(r);
  });
  const doSetup = () => run(async () => {
    if (f.password !== f.confirm) throw new Error('Passwords do not match');
    await api.post('/api/setup', { email: f.email, name: f.name, password: f.password });
    const r = await api.post('/api/auth/login', { email: f.email, password: f.password });
    login(r);
  });
  const doChange = () => run(async () => {
    if (f.newPassword !== f.confirm) throw new Error('Passwords do not match');
    await api.post('/api/auth/change-password', { currentPassword: f.password, newPassword: f.newPassword });
    const r = await api.post('/api/auth/refresh');
    login(r);
  });

  return (
    <Box sx={{ minHeight: '100vh', display: 'grid', gridTemplateColumns: { md: '1.1fr 1fr' }, bgcolor: '#F4F6FB' }}>
      <Box sx={{ display: { xs: 'none', md: 'flex' }, flexDirection: 'column', justifyContent: 'space-between', p: 6, color: '#fff', bgcolor: branding.primaryColor, backgroundImage: 'linear-gradient(150deg, rgba(0,0,0,0.25) 0%, rgba(0,0,0,0) 60%, rgba(255,255,255,0.06) 100%)' }}>
        <Stack direction="row" spacing={1.5} alignItems="center"><Logo size={44} /><Box><Typography variant="h5" fontWeight={800} lineHeight={1.1}>{branding.name}</Typography><Typography variant="body2" sx={{ opacity: 0.75 }}>{branding.tagline}</Typography></Box></Stack>
        <Box>
          <Typography variant="overline" sx={{ color: branding.secondaryColor, letterSpacing: 2, fontWeight: 700 }}>{branding.portalName}</Typography>
          <Typography variant="h3" fontWeight={800} lineHeight={1.15}>DPDP compliance,<br />managed for you.</Typography>
          <Typography sx={{ mt: 2, opacity: 0.85, maxWidth: 520 }}>{branding.loginMessage || 'Sign in to view your organisation\'s DPDP compliance status, evidence and reports.'}</Typography>
          <Stack direction="row" spacing={4} sx={{ mt: 5 }}>
            {[['57', 'mapped DPDP requirements'], ['16', 'board-ready report types'], ['72h', 'breach clock tracking']].map(([a, b]) => <Box key={b}><Typography variant="h4" fontWeight={800} sx={{ color: branding.secondaryColor }}>{a}</Typography><Typography variant="body2" sx={{ opacity: 0.8 }}>{b}</Typography></Box>)}
          </Stack>
        </Box>
        <Typography variant="caption" sx={{ opacity: 0.7 }}>
          {[branding.supportEmail && `Support: ${branding.supportEmail}`, branding.supportPhone, branding.website].filter(Boolean).join('  |  ') || `Digital Personal Data Protection Act, 2023 and DPDP Rules, 2025`}
        </Typography>
      </Box>
      <Box sx={{ display: 'grid', placeItems: 'center', p: 3 }}>
        <Card sx={{ width: '100%', maxWidth: 420 }} elevation={0}>
          <CardContent sx={{ p: 4 }}>
            {mode === 'setup' && <>
              <Typography variant="h5" fontWeight={700}>Welcome - first-time setup</Typography>
              <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>Create the Super Administrator account.</Typography>
              <Stack spacing={2}>
                <TextField label="Full name" value={f.name} onChange={set('name')} />
                <TextField label="Email" value={f.email} onChange={set('email')} />
                <TextField label="Password" type="password" value={f.password} onChange={set('password')} helperText="Min 10 characters with upper, lower case and a digit" />
                <TextField label="Confirm password" type="password" value={f.confirm} onChange={set('confirm')} />
                {err && <Alert severity="error">{err}</Alert>}
                <Button variant="contained" size="large" disabled={busy} onClick={doSetup}>Create administrator</Button>
              </Stack>
            </>}
            {(mode === 'login' || mode === 'mfa') && <>
              <Box sx={{ display: { md: 'none' }, mb: 2 }}><Stack direction="row" spacing={1} alignItems="center"><Logo size={32} /><Typography fontWeight={800}>{branding.name}</Typography></Stack></Box>
              <Typography variant="h5" fontWeight={700}>Sign in</Typography>
              <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>{mode === 'mfa' ? 'Enter the 6-digit code from your authenticator app.' : `${branding.portalName} - staff and customer accounts`}</Typography>
              <Stack spacing={2} component="form" onSubmit={e => { e.preventDefault(); doLogin(); }}>
                {mode === 'login' && <>
                  <TextField label="Email" autoComplete="username" value={f.email} onChange={set('email')} autoFocus />
                  <TextField label="Password" type="password" autoComplete="current-password" value={f.password} onChange={set('password')} />
                </>}
                {mode === 'mfa' && <TextField label="Authentication code" value={f.code} onChange={set('code')} autoFocus inputProps={{ inputMode: 'numeric', maxLength: 6 }} />}
                {err && <Alert severity="error">{err}</Alert>}
                <Button type="submit" variant="contained" size="large" disabled={busy}>{mode === 'mfa' ? 'Verify' : 'Sign in'}</Button>
                {mode === 'mfa' && <Button onClick={() => { setMode('login'); setF({ ...f, code: '' }); }}>Back</Button>}
              </Stack>
            </>}
            {mode === 'change' && <>
              <Typography variant="h5" fontWeight={700}>Change your password</Typography>
              <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>A new password is required before continuing.</Typography>
              <Stack spacing={2}>
                <TextField label="Current password" type="password" value={f.password} onChange={set('password')} />
                <TextField label="New password" type="password" value={f.newPassword} onChange={set('newPassword')} helperText="Min 10 characters with upper, lower case and a digit" />
                <TextField label="Confirm new password" type="password" value={f.confirm} onChange={set('confirm')} />
                {err && <Alert severity="error">{err}</Alert>}
                <Button variant="contained" size="large" disabled={busy} onClick={doChange}>Update password</Button>
                <Button onClick={logout}>Sign out</Button>
              </Stack>
            </>}
          </CardContent>
        </Card>
      </Box>
    </Box>
  );
}
