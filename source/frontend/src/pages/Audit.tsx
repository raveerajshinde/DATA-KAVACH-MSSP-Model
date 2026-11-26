import { useState } from 'react';
import { Alert, Box, Button, Card, MenuItem, Stack, Table, TableBody, TableCell, TableHead, TablePagination, TableRow, TextField, Typography } from '@mui/material';
import VerifiedUserIcon from '@mui/icons-material/VerifiedUser';
import { api, fmtDateTime, qs } from '../api';
import { useApp, useLoad } from '../context';
import { PageHeader } from '../components/Common';

const ACTIONS = ['', 'CREATE', 'UPDATE', 'DELETE', 'APPROVE', 'EXPORT', 'DOWNLOAD', 'UPLOAD', 'IMPORT', 'GENERATE', 'LOGIN', 'LOGIN_FAILED', 'LOGOUT', 'PASSWORD_CHANGE', 'MFA_ENABLED', 'BACKUP', 'VERIFY', 'ANALYZE'];

export default function Audit() {
  const { orgId, toast } = useApp();
  const [f, setF] = useState({ action: '', entity: '', user: '', scope: 'org' });
  const [page, setPage] = useState(0);
  const [data, loading] = useLoad<any>(() => api.get(`/api/audit?${qs({ ...f, scope: undefined, orgId: f.scope === 'org' ? orgId : undefined, page: page + 1 })}`), [JSON.stringify(f), page, orgId], { rows: [], total: 0 });
  const [chain, setChain] = useState<any>(null);
  return (
    <Box>
      <PageHeader title="Audit Trail" subtitle="Who created, modified, approved, exported, downloaded or deleted records - with tamper-evident hash chaining."
        actions={<Button startIcon={<VerifiedUserIcon />} onClick={async () => { try { setChain(await api.get('/api/audit/verify')); } catch (e: any) { toast(e.message, 'error'); } }}>Verify integrity</Button>} />
      {chain && <Alert severity={chain.intact ? 'success' : 'error'} sx={{ mb: 2 }}>{chain.intact ? `Audit chain intact - ${chain.checkedEntries} entries verified.` : `Audit chain broken at entry #${chain.firstBrokenEntry}. Records may have been altered outside the application.`}</Alert>}
      <Stack direction="row" spacing={1.5} sx={{ mb: 2 }}>
        <TextField select size="small" label="Scope" value={f.scope} onChange={e => setF({ ...f, scope: e.target.value })} sx={{ width: 200 }}><MenuItem value="org">Selected organisation</MenuItem><MenuItem value="all">All</MenuItem></TextField>
        <TextField select size="small" label="Action" value={f.action} onChange={e => { setF({ ...f, action: e.target.value }); setPage(0); }} sx={{ width: 180 }}>{ACTIONS.map(a => <MenuItem key={a} value={a}>{a || 'Any'}</MenuItem>)}</TextField>
        <TextField size="small" label="Record type (e.g. vendors)" value={f.entity} onChange={e => { setF({ ...f, entity: e.target.value }); setPage(0); }} />
        <TextField size="small" label="User email contains" value={f.user} onChange={e => { setF({ ...f, user: e.target.value }); setPage(0); }} />
      </Stack>
      <Card>
        <Table size="small">
          <TableHead><TableRow>{['Time', 'User', 'Action', 'Record', 'Details', 'IP'].map(h => <TableCell key={h} sx={{ fontWeight: 700, bgcolor: 'grey.50' }}>{h}</TableCell>)}</TableRow></TableHead>
          <TableBody>
            {data.rows.map((r: any) => (
              <TableRow key={r.id}>
                <TableCell sx={{ whiteSpace: 'nowrap' }}>{fmtDateTime(r.at)}</TableCell>
                <TableCell>{r.userEmail}</TableCell>
                <TableCell><Typography variant="body2" fontWeight={600}>{r.action}</Typography></TableCell>
                <TableCell>{r.entity}{r.entityId ? ` #${r.entityId}` : ''}</TableCell>
                <TableCell sx={{ maxWidth: 520 }}><Typography variant="caption" sx={{ fontFamily: 'monospace', wordBreak: 'break-all' }}>{r.details?.length > 300 ? r.details.slice(0, 300) + '...' : r.details}</Typography></TableCell>
                <TableCell>{r.ip}</TableCell>
              </TableRow>
            ))}
            {!loading && data.rows.length === 0 && <TableRow><TableCell colSpan={6}>No entries.</TableCell></TableRow>}
          </TableBody>
        </Table>
        <TablePagination component="div" count={data.total} page={page} rowsPerPage={100} rowsPerPageOptions={[100]} onPageChange={(_, n) => setPage(n)} />
      </Card>
    </Box>
  );
}
