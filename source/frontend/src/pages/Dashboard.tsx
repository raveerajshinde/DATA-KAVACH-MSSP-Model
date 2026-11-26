import { Alert, Box, Button, Chip, Grid, List, ListItemButton, ListItemText, Stack, Typography } from '@mui/material';
import { Bar, BarChart, CartesianGrid, Cell, Legend, Pie, PieChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { useNavigate } from 'react-router-dom';
import WarningAmberIcon from '@mui/icons-material/WarningAmber';
import TaskAltIcon from '@mui/icons-material/TaskAlt';
import GppMaybeIcon from '@mui/icons-material/GppMaybe';
import EventIcon from '@mui/icons-material/Event';
import { api, fmtDate } from '../api';
import { useApp, useLoad } from '../context';
import { DueChip, Kpi, NoOrg, PageHeader, Progress, SectionCard } from '../components/Common';

const C = { Compliant: '#2E7D32', 'Partially Compliant': '#F9A825', 'Non-Compliant': '#C62828', 'Not Assessed': '#9E9E9E', 'Not Applicable': '#CFD8DC', High: '#C62828', Medium: '#F9A825', Low: '#2E7D32' } as Record<string, string>;

export default function Dashboard() {
  const { orgId, org } = useApp();
  const nav = useNavigate();
  const [d, loading] = useLoad<any>(() => (orgId ? api.get(`/api/dashboard?orgId=${orgId}`) : Promise.resolve(null)), [orgId], null);
  if (!orgId) return <><PageHeader title="Dashboard" /><NoOrg /></>;
  if (!d) return <PageHeader title="Dashboard" subtitle={loading ? 'Loading...' : ''} />;
  const s = d.status;
  const pie = [['Compliant', s.compliant], ['Partially Compliant', s.partial], ['Non-Compliant', s.nonCompliant], ['Not Assessed', s.notAssessed], ['Not Applicable', s.notApplicable]].map(([name, value]) => ({ name, value }));
  const risk = ['High', 'Medium', 'Low'].map(r => ({ name: r, value: d.risk[r] ?? 0 }));
  const modules = d.byModule.map((m: any) => ({ name: m.name, Compliant: m.compliant, 'Partially Compliant': m.partial, 'Non-Compliant': m.nonCompliant, 'Not Assessed': m.notAssessed }));
  const depts = d.byDepartment.map((x: any) => ({ name: x.name, indicator: x.indicator ?? 0, assessed: x.assessed, total: x.total }));
  const remediation = Object.entries(d.actions.byStatus).map(([name, value]) => ({ name, value }));
  return (
    <Box>
      <PageHeader title={`${org?.name ?? ''}`} subtitle="DPDP compliance overview - Digital Personal Data Protection Act, 2023 and DPDP Rules, 2025"
        actions={<><Button variant="outlined" onClick={() => nav('/assessments')}>Open assessment</Button><Button variant="contained" onClick={() => nav('/reports')}>Generate report</Button></>} />
      <Alert severity="info" variant="outlined" sx={{ mb: 2 }}>{d.disclaimer}</Alert>
      {d.commencement.daysToCore > 0 && <Alert severity="warning" icon={<EventIcon />} sx={{ mb: 2 }}>Core DPDP obligations commence on <b>13 May 2027</b> - <b>{d.commencement.daysToCore} days</b> remaining (Rules 3, 5-16, 22, 23).</Alert>}
      {!d.assessmentId && <Alert severity="warning" sx={{ mb: 2 }} action={<Button color="inherit" onClick={() => nav('/assessments')}>Start</Button>}>No assessment yet. Start a DPDP assessment to populate the dashboard.</Alert>}
      <Grid container spacing={2}>
        <Grid size={{ xs: 12, sm: 6, lg: 3 }}><Kpi label="Assessment indicator" value={s.indicator != null ? `${s.indicator}%` : '-'} hint={`${s.assessed} of ${s.applicable} applicable requirements assessed`} icon={<TaskAltIcon />} /></Grid>
        <Grid size={{ xs: 12, sm: 6, lg: 3 }}><Kpi label="High-risk gaps" value={d.risk.High ?? 0} color="#C62828" hint={`${d.risk.Medium ?? 0} medium, ${d.risk.Low ?? 0} low`} icon={<GppMaybeIcon />} /></Grid>
        <Grid size={{ xs: 12, sm: 6, lg: 3 }}><Kpi label="Open actions" value={d.actions.open} color="#0B3D91" hint={<span style={{ color: d.actions.overdue ? '#C62828' : undefined }}>{d.actions.overdue} overdue</span>} /></Grid>
        <Grid size={{ xs: 12, sm: 6, lg: 3 }}><Kpi label="Evidence pending" value={d.evidencePending} color="#8A5A00" hint="Compliant/partial items without evidence" icon={<WarningAmberIcon />} /></Grid>

        <Grid size={{ xs: 12, md: 4 }}>
          <SectionCard title="Compliance status distribution">
            <Progress value={s.completion} label="Assessment completion" />
            <ResponsiveContainer width="100%" height={250}>
              <PieChart><Pie data={pie} dataKey="value" nameKey="name" innerRadius={55} outerRadius={90} paddingAngle={1}>{pie.map(p => <Cell key={p.name as string} fill={C[p.name as string]} />)}</Pie><Tooltip /><Legend iconSize={10} wrapperStyle={{ fontSize: 12 }} /></PieChart>
            </ResponsiveContainer>
          </SectionCard>
        </Grid>
        <Grid size={{ xs: 12, md: 4 }}>
          <SectionCard title="Open gaps by risk">
            <ResponsiveContainer width="100%" height={270}>
              <BarChart data={risk}><CartesianGrid strokeDasharray="3 3" vertical={false} /><XAxis dataKey="name" /><YAxis allowDecimals={false} /><Tooltip /><Bar dataKey="value" name="Gaps" radius={[6, 6, 0, 0]}>{risk.map(r => <Cell key={r.name} fill={C[r.name]} />)}</Bar></BarChart>
            </ResponsiveContainer>
          </SectionCard>
        </Grid>
        <Grid size={{ xs: 12, md: 4 }}>
          <SectionCard title="Upcoming deadlines" action={<Chip size="small" label={d.deadlines.length} />}>
            <List dense sx={{ maxHeight: 290, overflow: 'auto' }}>
              {d.deadlines.length === 0 && <Typography variant="body2" color="text.secondary">Nothing due in the next 14 days.</Typography>}
              {d.deadlines.map((x: any, i: number) => (
                <ListItemButton key={i} onClick={() => nav(x.link)} sx={{ borderRadius: 1, borderLeft: x.critical ? '3px solid #C62828' : '3px solid transparent' }}>
                  <ListItemText primary={x.title} secondary={`${x.type} - ${fmtDate(x.due)}`} primaryTypographyProps={{ fontSize: 13, noWrap: true }} />
                  <DueChip due={x.due} />
                </ListItemButton>
              ))}
            </List>
          </SectionCard>
        </Grid>

        <Grid size={{ xs: 12, lg: 8 }}>
          <SectionCard title="Module-wise compliance">
            <ResponsiveContainer width="100%" height={320}>
              <BarChart data={modules} layout="vertical" margin={{ left: 40 }}><CartesianGrid strokeDasharray="3 3" horizontal={false} /><XAxis type="number" allowDecimals={false} /><YAxis type="category" dataKey="name" width={150} tick={{ fontSize: 12 }} /><Tooltip /><Legend wrapperStyle={{ fontSize: 12 }} />
                {['Compliant', 'Partially Compliant', 'Non-Compliant', 'Not Assessed'].map(k => <Bar key={k} dataKey={k} stackId="a" fill={C[k]} />)}
              </BarChart>
            </ResponsiveContainer>
          </SectionCard>
        </Grid>
        <Grid size={{ xs: 12, lg: 4 }}>
          <SectionCard title="Remediation progress">
            <ResponsiveContainer width="100%" height={200}>
              <BarChart data={remediation}><XAxis dataKey="name" tick={{ fontSize: 11 }} /><YAxis allowDecimals={false} /><Tooltip /><Bar dataKey="value" fill="#0B3D91" radius={[6, 6, 0, 0]} /></BarChart>
            </ResponsiveContainer>
            <Stack direction="row" spacing={1} sx={{ mt: 1 }} flexWrap="wrap" useFlexGap>
              {Object.entries(d.actions.byPlan).map(([k, v]) => <Chip key={k} size="small" label={`${k}: ${v}`} variant="outlined" />)}
            </Stack>
          </SectionCard>
        </Grid>

        <Grid size={{ xs: 12, md: 6 }}>
          <SectionCard title="Department-wise compliance">
            <ResponsiveContainer width="100%" height={240}>
              <BarChart data={depts}><CartesianGrid strokeDasharray="3 3" vertical={false} /><XAxis dataKey="name" tick={{ fontSize: 11 }} /><YAxis domain={[0, 100]} unit="%" /><Tooltip formatter={(v: any, n: any, p: any) => [`${v}% (${p.payload.assessed}/${p.payload.total} assessed)`, 'Indicator']} /><Bar dataKey="indicator" fill="#3A64B5" radius={[6, 6, 0, 0]} /></BarChart>
            </ResponsiveContainer>
          </SectionCard>
        </Grid>
        <Grid size={{ xs: 12, md: 6 }}>
          <Grid container spacing={2}>
            <Grid size={6}><Kpi label="Vendor risk" value={`${d.vendors.byRisk.High ?? 0} high`} color="#C62828" hint={`${d.vendors.total} vendors - ${d.vendors.processorsWithoutDpa} processors without DPA`} /></Grid>
            <Grid size={6}><Kpi label="Security safeguards" value={`${d.security.validated}/${d.security.total}`} hint={`validated with evidence - ${d.security.nonCompliant} non-compliant`} /></Grid>
            <Grid size={6}><Kpi label="Rights requests" value={d.rights.open} hint={<span style={{ color: d.rights.overdue ? '#C62828' : undefined }}>{d.rights.overdue} overdue of {d.rights.total}</span>} /></Grid>
            <Grid size={6}><Kpi label="Incidents" value={d.incidents.open} color={d.incidents.overdueNotifications ? '#C62828' : undefined} hint={`${d.incidents.breaches} personal data breaches - ${d.incidents.overdueNotifications} overdue notifications`} /></Grid>
            <Grid size={6}><Kpi label="Consents" value={d.consent.given} hint={`${d.consent.withdrawn} withdrawn`} /></Grid>
            <Grid size={6}><Kpi label="DPIAs" value={`${d.dpia.approved}/${d.dpia.total}`} hint="approved" /></Grid>
          </Grid>
        </Grid>
      </Grid>
    </Box>
  );
}
