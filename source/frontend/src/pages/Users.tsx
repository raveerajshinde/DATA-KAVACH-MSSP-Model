import { useState } from 'react';
import { Alert, Box, Button, Card, Checkbox, Chip, Dialog, DialogActions, DialogContent, DialogTitle, FormControlLabel, MenuItem, Stack, Switch, Tab, Table, TableBody, TableCell, TableHead, TableRow, Tabs, TextField, Typography } from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import { api, fmtDateTime } from '../api';
import { useApp, useLoad } from '../context';
import { PageHeader, useConfirm } from '../components/Common';

const MODULE_LABELS: Record<string, string> = { org: 'Organisation', assessment: 'Assessment', evidence: 'Evidence', inventory: 'Inventory', dpia: 'DPIA', consent: 'Consent', policy: 'Policies', vendor: 'Vendors', security: 'Security', rights: 'Rights', incident: 'Incidents', roadmap: 'Roadmap', report: 'Reports', template: 'Templates' };

function Permissions() {
  const { toast } = useApp();
  const [data, , reload] = useLoad<any>(() => api.get('/api/roles'), [], null);
  const [role, setRole] = useState('Compliance Manager');
  const [perms, setPerms] = useState<string[] | null>(null);
  if (!data) return null;
  const cur = perms ?? data.roles.find((r: any) => r.role === role)?.permissions ?? [];
  const toggle = (p: string) => setPerms(cur.includes(p) ? cur.filter((x: string) => x !== p) : [...cur, p]);
  const actions = ['view', 'edit', 'delete', 'approve', 'export'];
  return (
    <Card sx={{ p: 2 }}>
      <Stack direction="row" spacing={2} alignItems="center" sx={{ mb: 2 }}>
        <TextField select size="small" label="Role" value={role} onChange={e => { setRole(e.target.value); setPerms(null); }} sx={{ minWidth: 260 }}>{data.roles.map((r: any) => <MenuItem key={r.role} value={r.role}>{r.role}</MenuItem>)}</TextField>
        <Button variant="contained" disabled={!perms || role === 'Super Administrator'} onClick={async () => { try { await api.put(`/api/roles/${encodeURIComponent(role)}`, cur); toast('Permissions saved'); setPerms(null); reload(); } catch (e: any) { toast(e.message, 'error'); } }}>Save permissions</Button>
      </Stack>
      {role === 'Super Administrator' && <Alert severity="info" sx={{ mb: 2 }}>Super Administrator always has every permission.</Alert>}
      <Table size="small">
        <TableHead><TableRow><TableCell sx={{ fontWeight: 700 }}>Module</TableCell>{actions.map(a => <TableCell key={a} align="center" sx={{ fontWeight: 700, textTransform: 'capitalize' }}>{a}</TableCell>)}</TableRow></TableHead>
        <TableBody>
          {Object.entries(MODULE_LABELS).map(([m, l]) => (
            <TableRow key={m}><TableCell>{l}</TableCell>{actions.map(a => <TableCell key={a} align="center"><Checkbox size="small" checked={cur.includes(`${m}.${a}`)} disabled={role === 'Super Administrator'} onChange={() => toggle(`${m}.${a}`)} /></TableCell>)}</TableRow>
          ))}
        </TableBody>
      </Table>
      <Typography variant="subtitle2" sx={{ mt: 2 }}>Special permissions</Typography>
      <Stack direction="row" flexWrap="wrap" useFlexGap>
        {[['pii.view', 'View unmasked personal data'], ['admin.users', 'Manage users & roles'], ['admin.settings', 'Settings'], ['admin.audit', 'View audit trail'], ['admin.backup', 'Run backups'], ['admin.apikeys', 'Manage API keys']].map(([p, l]) => (
          <FormControlLabel key={p} control={<Checkbox size="small" checked={cur.includes(p)} disabled={role === 'Super Administrator'} onChange={() => toggle(p)} />} label={<Typography variant="body2">{l}</Typography>} />
        ))}
      </Stack>
    </Card>
  );
}

export default function Users() {
  const { meta, toast, user: me, isCustomer, orgs, orgId, org, branding } = useApp();
  const customerRoles = ['Customer Administrator', 'Customer User', 'Customer Viewer'];
  const roleOptions = isCustomer ? customerRoles : meta!.roles;
  const [filter, setFilter] = useState<'all' | 'Provider' | 'Customer'>('all');
  const [tab, setTab] = useState(0);
  const [users, , reload] = useLoad<any[]>(() => api.get('/api/users'), [], []);
  const [edit, setEdit] = useState<any>(null);
  const [pw, setPw] = useState<{ id: number; password: string } | null>(null);
  const { confirm, dialog } = useConfirm();
  const save = async () => {
    try {
      if (edit.id) await api.put(`/api/users/${edit.id}`, edit);
      else await api.post('/api/users', edit);
      toast(edit.id ? 'User updated' : 'User created - they must change the password at first login'); setEdit(null); reload();
    } catch (e: any) { toast(e.message, 'error'); }
  };
  return (
    <Box>
      <PageHeader title="Users & Roles" subtitle="Role-based access with granular, editable permissions. Every user action is recorded in the audit trail."
        actions={tab === 0 && <>
          {!isCustomer && <Button startIcon={<AddIcon />} onClick={() => setEdit({ email: '', name: '', role: 'Customer Administrator', organizationId: orgId, password: '', isActive: true })}>Add customer portal user</Button>}
          <Button variant="contained" startIcon={<AddIcon />} onClick={() => setEdit({ email: '', name: '', role: isCustomer ? 'Customer User' : 'Read-only User', password: '', isActive: true })}>{isCustomer ? 'Add user' : `Add ${branding.name} staff`}</Button>
        </>} />
      {isCustomer && <Alert severity="info" variant="outlined" sx={{ mb: 2 }}>Users you add here can access only {org?.name}'s portal.</Alert>}
      {!isCustomer && tab === 0 && <Stack direction="row" spacing={1} sx={{ mb: 2 }}>{(['all', 'Provider', 'Customer'] as const).map(f => <Chip key={f} label={f === 'all' ? 'All users' : f === 'Provider' ? `${branding.name} staff` : 'Customer portal users'} color={filter === f ? 'primary' : 'default'} onClick={() => setFilter(f)} />)}</Stack>}
      <Card sx={{ mb: 2 }}><Tabs value={tab} onChange={(_, v) => setTab(v)}><Tab label="Users" />{!isCustomer && <Tab label="Role permissions" />}</Tabs></Card>
      {tab === 0 && (
        <Card>
          <Table>
            <TableHead><TableRow>{['Name', 'Email', 'Role', 'Access', 'MFA', 'Status', 'Last login', ''].map(h => <TableCell key={h} sx={{ fontWeight: 700, bgcolor: 'grey.50' }}>{h}</TableCell>)}</TableRow></TableHead>
            <TableBody>{users.filter(u => filter === 'all' || u.userType === filter).map(u => (
              <TableRow key={u.id} hover>
                <TableCell>{u.name}</TableCell><TableCell>{u.email}</TableCell><TableCell>{u.role}</TableCell><TableCell>{u.userType === 'Customer' ? <Chip size="small" color="secondary" variant="outlined" label={`Portal: ${u.organizationName ?? '-'}`} /> : <Chip size="small" variant="outlined" label="All clients" />}</TableCell>
                <TableCell>{u.mfaEnabled ? <Chip size="small" color="success" label="On" /> : <Chip size="small" label="Off" />}</TableCell>
                <TableCell>{u.isActive ? <Chip size="small" color="success" variant="outlined" label="Active" /> : <Chip size="small" label="Disabled" />}{u.lockedUntil && new Date(u.lockedUntil) > new Date() && <Chip size="small" color="error" label="Locked" sx={{ ml: 0.5 }} />}</TableCell>
                <TableCell>{fmtDateTime(u.lastLoginAt)}</TableCell>
                <TableCell align="right" sx={{ whiteSpace: 'nowrap' }}>
                  <Button size="small" onClick={() => setEdit({ ...u })}>Edit</Button>
                  <Button size="small" onClick={() => setPw({ id: u.id, password: '' })}>Reset password</Button>
                  {u.mfaEnabled && <Button size="small" onClick={() => confirm('Reset MFA', `Remove MFA for ${u.email}? They can enrol again at next login.`, async () => { await api.post(`/api/users/${u.id}/reset-mfa`); reload(); })}>Reset MFA</Button>}
                  {u.id !== me?.id && <Button size="small" color="error" onClick={() => confirm('Delete user', `Delete ${u.email}? Their audit history is kept.`, async () => { await api.del(`/api/users/${u.id}`); reload(); }, true, 'Delete')}>Delete</Button>}
                </TableCell>
              </TableRow>
            ))}</TableBody>
          </Table>
        </Card>
      )}
      {tab === 1 && <Permissions />}
      <Dialog open={!!edit} onClose={() => setEdit(null)} maxWidth="sm" fullWidth>
        <DialogTitle>{edit?.id ? 'Edit user' : 'Add user'}</DialogTitle>
        <DialogContent>{edit && <Stack spacing={2} sx={{ mt: 1 }}>
          <TextField label="Full name" value={edit.name ?? ''} onChange={e => setEdit({ ...edit, name: e.target.value })} />
          <TextField label="Email" value={edit.email ?? ''} disabled={!!edit.id} onChange={e => setEdit({ ...edit, email: e.target.value })} />
          <TextField select label="Role" value={edit.role} onChange={e => setEdit({ ...edit, role: e.target.value, organizationId: customerRoles.includes(e.target.value) ? (edit.organizationId || orgId) : null })} helperText={customerRoles.includes(edit.role) ? 'Customer portal role - access limited to one client organisation' : `${branding.name} staff role - access to all clients per permissions`}>
            {roleOptions.map(r => <MenuItem key={r} value={r}>{r}</MenuItem>)}
          </TextField>
          {!isCustomer && customerRoles.includes(edit.role) && <TextField select label="Client organisation" value={edit.organizationId ?? ''} onChange={e => setEdit({ ...edit, organizationId: Number(e.target.value) })}>{orgs.map(o => <MenuItem key={o.id} value={o.id}>{o.name}</MenuItem>)}</TextField>}
          <TextField label="Department" value={edit.department ?? ''} onChange={e => setEdit({ ...edit, department: e.target.value })} />
          <TextField label="Phone" value={edit.phone ?? ''} onChange={e => setEdit({ ...edit, phone: e.target.value })} />
          {!edit.id && <TextField label="Temporary password" type="password" value={edit.password} onChange={e => setEdit({ ...edit, password: e.target.value })} helperText="User must change it at first login. Min 10 chars, upper/lower case and a digit." />}
          {edit.id && <FormControlLabel control={<Switch checked={!!edit.isActive} onChange={e => setEdit({ ...edit, isActive: e.target.checked })} />} label="Active" />}
        </Stack>}</DialogContent>
        <DialogActions><Button onClick={() => setEdit(null)}>Cancel</Button><Button variant="contained" onClick={save}>Save</Button></DialogActions>
      </Dialog>
      <Dialog open={!!pw} onClose={() => setPw(null)} maxWidth="xs" fullWidth>
        <DialogTitle>Reset password</DialogTitle>
        <DialogContent><TextField fullWidth sx={{ mt: 1 }} type="password" label="New temporary password" value={pw?.password ?? ''} onChange={e => setPw({ ...pw!, password: e.target.value })} /></DialogContent>
        <DialogActions><Button onClick={() => setPw(null)}>Cancel</Button><Button variant="contained" onClick={async () => { try { await api.post(`/api/users/${pw!.id}/reset-password`, { password: pw!.password }); toast('Password reset; change required at next login'); setPw(null); } catch (e: any) { toast(e.message, 'error'); } }}>Reset</Button></DialogActions>
      </Dialog>
      {dialog}
    </Box>
  );
}
