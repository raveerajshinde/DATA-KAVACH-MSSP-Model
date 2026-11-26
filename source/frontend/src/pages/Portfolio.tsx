import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Alert, Box, Button, Card, Chip, Dialog, DialogActions, DialogContent, DialogTitle, Grid, LinearProgress, MenuItem, Stack, Table, TableBody, TableCell, TableHead, TableRow, TableSortLabel, TextField, Tooltip, Typography, Divider } from '@mui/material';
import AddBusinessIcon from '@mui/icons-material/AddBusiness';
import ApartmentIcon from '@mui/icons-material/Apartment';
import GppMaybeIcon from '@mui/icons-material/GppMaybe';
import ReportProblemIcon from '@mui/icons-material/ReportProblem';
import PeopleIcon from '@mui/icons-material/People';
import { Bar, BarChart, CartesianGrid, Cell, Pie, PieChart, ResponsiveContainer, Tooltip as RTooltip, XAxis, YAxis, Legend } from 'recharts';
import { api, fmtDateTime } from '../api';
import { useApp, useLoad } from '../context';
import { Kpi, PageHeader, SectionCard } from '../components/Common';

const HEALTH: Record<string, string> = { 'On track': '#2E7D32', 'Needs attention': '#F9A825', 'At risk': '#C62828', 'Not started': '#9E9E9E' };

function HealthChip({ v }: { v: string }) {
  return <Chip size="small" label={v} sx={{ bgcolor: HEALTH[v] + '22', color: HEALTH[v] === '#F9A825' ? '#8A5A00' : HEALTH[v], fontWeight: 700, border: `1px solid ${HEALTH[v]}55` }} />;
}

function Onboard({ open, onClose, onDone }: { open: boolean; onClose: () => void; onDone: (id: number) => void }) {
  const { meta, toast, can } = useApp();
  const [f, setF] = useState({ name: '', industry: '', adminName: '', adminEmail: '', adminPassword: '' });
  const [busy, setBusy] = useState(false);
  const industries = meta!.entities.find(e => e.key === 'organizations')!.cols.find(c => c.name === 'industry')!.options!;
  const go = async () => {
    setBusy(true);
    try {
      const r = await api.post('/api/clients/onboard', f);
      toast(f.adminEmail ? `Client created. Portal login for ${f.adminEmail} is ready (password change required at first sign-in).` : 'Client created');
      setF({ name: '', industry: '', adminName: '', adminEmail: '', adminPassword: '' });
      onDone(r.orgId);
    } catch (e: any) { toast(e.message, 'error'); } finally { setBusy(false); }
  };
  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Onboard a new client</DialogTitle>
      <DialogContent dividers>
        <Stack spacing={2}>
          <TextField label="Client organisation name" value={f.name} onChange={e => setF({ ...f, name: e.target.value })} autoFocus />
          <TextField select label="Industry" value={f.industry} onChange={e => setF({ ...f, industry: e.target.value })}>{industries.map(o => <MenuItem key={o} value={o}>{o}</MenuItem>)}</TextField>
          <Divider textAlign="left"><Typography variant="caption">Customer portal administrator (optional)</Typography></Divider>
          {can('admin.users') ? <>
            <TextField label="Name" value={f.adminName} onChange={e => setF({ ...f, adminName: e.target.value })} />
            <TextField label="Email" value={f.adminEmail} onChange={e => setF({ ...f, adminEmail: e.target.value })} />
            <TextField label="Temporary password" type="password" value={f.adminPassword} onChange={e => setF({ ...f, adminPassword: e.target.value })} helperText="Min 10 characters with upper, lower case and a digit. They change it at first sign-in." />
            <Alert severity="info" variant="outlined">The customer sees only their own organisation. Your team keeps full access from the Client Portfolio.</Alert>
          </> : <Alert severity="info">You can add portal users later under Users & Roles.</Alert>}
        </Stack>
      </DialogContent>
      <DialogActions><Button onClick={onClose}>Cancel</Button><Button variant="contained" disabled={!f.name || busy} onClick={go}>Create client</Button></DialogActions>
    </Dialog>
  );
}

export default function Portfolio() {
  const { selectOrg, reloadOrgs, branding, can } = useApp();
  const nav = useNavigate();
  const [tick, setTick] = useState(0);
  const [data, loading] = useLoad<any>(() => api.get('/api/portfolio'), [tick], null);
  const [open, setOpen] = useState(false);
  const [sort, setSort] = useState<{ k: string; desc: boolean }>({ k: 'highRisk', desc: true });
  const [q, setQ] = useState('');
  const openClient = (id: number, path = '/dashboard') => { selectOrg(id); nav(path); };
  if (!data) return <><PageHeader title="Client Portfolio" />{loading && <LinearProgress />}</>;
  const t = data.totals;
  const clients = [...data.clients]
    .filter((c: any) => !q || `${c.name} ${c.industry ?? ''} ${c.city ?? ''}`.toLowerCase().includes(q.toLowerCase()))
    .sort((a: any, b: any) => { const x = a[sort.k] ?? -1, y = b[sort.k] ?? -1; return (x > y ? 1 : x < y ? -1 : 0) * (sort.desc ? -1 : 1); });
  const healthPie = Object.entries(t.byHealth).map(([name, value]) => ({ name, value })).filter((x: any) => x.value > 0);
  const bars = data.clients.filter((c: any) => c.indicator != null).map((c: any) => ({ name: c.name.length > 18 ? c.name.slice(0, 17) + '…' : c.name, indicator: c.indicator, health: c.health }));
  const H = (k: string, label: string, align: 'left' | 'right' | 'center' = 'right') => (
    <TableCell align={align} sx={{ fontWeight: 700, bgcolor: 'grey.50', whiteSpace: 'nowrap' }}><TableSortLabel active={sort.k === k} direction={sort.desc ? 'desc' : 'asc'} onClick={() => setSort(s => ({ k, desc: s.k === k ? !s.desc : true }))}>{label}</TableSortLabel></TableCell>
  );
  const num = (n: number, bad = false) => <Typography variant="body2" fontWeight={n > 0 && bad ? 700 : 400} color={n > 0 && bad ? 'error.main' : 'text.primary'}>{n}</Typography>;
  return (
    <Box>
      <PageHeader title="Client Portfolio" subtitle={`All clients managed by ${branding.name}. Open a client to work on its assessment, evidence and reports; customers see only their own portal.`}
        actions={can('org.edit') && <Button variant="contained" startIcon={<AddBusinessIcon />} onClick={() => setOpen(true)}>Onboard client</Button>} />
      {data.daysToCore > 0 && <Alert severity="warning" sx={{ mb: 2 }}>Core DPDP obligations commence on <b>13 May 2027</b> - <b>{data.daysToCore} days</b> for your clients to close gaps.</Alert>}
      {t.overdueNotifications > 0 && <Alert severity="error" sx={{ mb: 2 }}><b>{t.overdueNotifications}</b> breach notification(s) are overdue across clients - check the clients marked At risk.</Alert>}
      <Grid container spacing={2} sx={{ mb: 2 }}>
        <Grid size={{ xs: 6, md: 2.4 }}><Kpi label="Clients" value={t.clients} icon={<ApartmentIcon />} hint={`${t.byHealth['At risk']} at risk, ${t.byHealth['Not started']} not started`} /></Grid>
        <Grid size={{ xs: 6, md: 2.4 }}><Kpi label="Avg. indicator" value={t.averageIndicator != null ? `${t.averageIndicator}%` : '-'} hint="across assessed clients" /></Grid>
        <Grid size={{ xs: 6, md: 2.4 }}><Kpi label="High-risk gaps" value={t.highRisk} color="#C62828" icon={<GppMaybeIcon />} hint="all clients" /></Grid>
        <Grid size={{ xs: 6, md: 2.4 }}><Kpi label="Open incidents" value={t.openIncidents} color={t.overdueNotifications ? '#C62828' : undefined} icon={<ReportProblemIcon />} hint={`${t.overdueNotifications} overdue notifications`} /></Grid>
        <Grid size={{ xs: 12, md: 2.4 }}><Kpi label="Open actions" value={t.openActions} hint={<span style={{ color: t.overdueActions ? '#C62828' : undefined }}>{t.overdueActions} overdue - {t.overdueRequests} overdue DSRs</span>} /></Grid>
        <Grid size={{ xs: 12, md: 4 }}>
          <SectionCard title="Client health">
            <ResponsiveContainer width="100%" height={230}>
              <PieChart><Pie data={healthPie} dataKey="value" nameKey="name" innerRadius={55} outerRadius={85} paddingAngle={1}>{healthPie.map((p: any) => <Cell key={p.name} fill={HEALTH[p.name]} />)}</Pie><RTooltip /><Legend iconSize={10} wrapperStyle={{ fontSize: 12 }} /></PieChart>
            </ResponsiveContainer>
          </SectionCard>
        </Grid>
        <Grid size={{ xs: 12, md: 8 }}>
          <SectionCard title="Assessment indicator by client">
            {bars.length === 0 ? <Typography variant="body2" color="text.secondary">No client has started an assessment yet.</Typography> :
              <ResponsiveContainer width="100%" height={230}>
                <BarChart data={bars}><CartesianGrid strokeDasharray="3 3" vertical={false} /><XAxis dataKey="name" tick={{ fontSize: 11 }} /><YAxis domain={[0, 100]} unit="%" /><RTooltip /><Bar dataKey="indicator" radius={[6, 6, 0, 0]}>{bars.map((b: any, i: number) => <Cell key={i} fill={HEALTH[b.health] ?? '#3A64B5'} />)}</Bar></BarChart>
              </ResponsiveContainer>}
          </SectionCard>
        </Grid>
      </Grid>
      <Card>
        <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ p: 1.5 }}>
          <Typography variant="subtitle1" fontWeight={700}>All clients</Typography>
          <TextField size="small" placeholder="Search clients" value={q} onChange={e => setQ(e.target.value)} sx={{ width: 260 }} />
        </Stack>
        <Box sx={{ overflowX: 'auto' }}>
          <Table size="small">
            <TableHead><TableRow>
              {H('name', 'Client', 'left')}{H('health', 'Health', 'left')}{H('indicator', 'Indicator')}{H('completion', 'Assessed')}{H('highRisk', 'High gaps')}{H('overdueActions', 'Overdue actions')}
              {H('openIncidents', 'Incidents')}{H('overdueNotifications', 'Overdue notices')}{H('overdueRequests', 'Overdue DSRs')}{H('processorsWithoutDpa', 'No DPA')}{H('portalUsers', 'Portal users')}{H('lastActivity', 'Last activity', 'left')}
              <TableCell sx={{ bgcolor: 'grey.50' }} />
            </TableRow></TableHead>
            <TableBody>
              {clients.map((c: any) => (
                <TableRow key={c.id} hover sx={{ cursor: 'pointer' }} onClick={() => openClient(c.id)}>
                  <TableCell><Typography variant="body2" fontWeight={700}>{c.name}</Typography><Typography variant="caption" color="text.secondary">{[c.industry, c.city].filter(Boolean).join(' - ')}{c.isSdf ? ' - SDF' : ''}</Typography></TableCell>
                  <TableCell><HealthChip v={c.health} /></TableCell>
                  <TableCell align="right">{c.indicator != null ? <b>{c.indicator}%</b> : '-'}</TableCell>
                  <TableCell align="right" sx={{ minWidth: 110 }}><Tooltip title={`${c.compliant} compliant, ${c.partial} partial, ${c.nonCompliant} non-compliant`}><Box><LinearProgress variant="determinate" value={c.completion} sx={{ height: 6, borderRadius: 3 }} /><Typography variant="caption">{c.completion.toFixed(0)}%</Typography></Box></Tooltip></TableCell>
                  <TableCell align="right">{num(c.highRisk, true)}</TableCell>
                  <TableCell align="right">{num(c.overdueActions, true)}</TableCell>
                  <TableCell align="right">{num(c.openIncidents)}</TableCell>
                  <TableCell align="right">{num(c.overdueNotifications, true)}</TableCell>
                  <TableCell align="right">{num(c.overdueRequests, true)}</TableCell>
                  <TableCell align="right">{num(c.processorsWithoutDpa, true)}</TableCell>
                  <TableCell align="right">{c.portalUsers}</TableCell>
                  <TableCell><Typography variant="caption">{c.lastActivity ? fmtDateTime(c.lastActivity) : '-'}</Typography></TableCell>
                  <TableCell onClick={e => e.stopPropagation()} sx={{ whiteSpace: 'nowrap' }}>
                    <Button size="small" onClick={() => openClient(c.id, c.hasAssessment ? '/dashboard' : '/assessments')}>{c.hasAssessment ? 'Open' : 'Start'}</Button>
                    <Button size="small" onClick={() => openClient(c.id, '/reports')}>Reports</Button>
                  </TableCell>
                </TableRow>
              ))}
              {clients.length === 0 && <TableRow><TableCell colSpan={13}><Box sx={{ py: 4, textAlign: 'center', color: 'text.secondary' }}>No clients yet - use Onboard client.</Box></TableCell></TableRow>}
            </TableBody>
          </Table>
        </Box>
        <Stack direction="row" spacing={1} alignItems="center" sx={{ p: 1.5 }}><PeopleIcon fontSize="small" color="action" /><Typography variant="caption" color="text.secondary">{t.portalUsers} customer portal users across all clients. Health: At risk = overdue breach notice, 5+ high gaps or indicator below 40%; Needs attention = any high gap, overdue action or indicator below 75%.</Typography></Stack>
      </Card>
      <Onboard open={open} onClose={() => setOpen(false)} onDone={async id => { setOpen(false); await reloadOrgs(); setTick(x => x + 1); selectOrg(id); }} />
    </Box>
  );
}
