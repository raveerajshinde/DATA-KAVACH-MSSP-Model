import { ReactNode, useEffect, useRef, useState } from 'react';
import { Box, Button, Card, IconButton, InputAdornment, Menu, MenuItem, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TablePagination, TableRow, TableSortLabel, TextField, Tooltip, Typography, LinearProgress } from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import SearchIcon from '@mui/icons-material/Search';
import FileDownloadIcon from '@mui/icons-material/FileDownload';
import FileUploadIcon from '@mui/icons-material/FileUpload';
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutline';
import { api, fmtDate, fmtDateTime, qs } from '../api';
import { useApp } from '../context';
import { StatusChip, useConfirm } from './Common';
import { EntityDialog } from './EntityForm';

const chipCols = new Set(['status', 'riskRating', 'defaultRisk', 'approval', 'approvalStatus', 'overallResidualRisk', 'inherentRating', 'residualRating', 'severity', 'legalApproval', 'managementApproval']);

export function renderCell(col: string, v: any, type?: string): ReactNode {
  if (v === null || v === undefined || v === '') return <Typography variant="body2" color="text.disabled">-</Typography>;
  if (chipCols.has(col)) return <StatusChip value={String(v)} />;
  if (type === 'bool' || typeof v === 'boolean') return v ? 'Yes' : 'No';
  if (type === 'date') return fmtDate(v);
  if (type === 'datetime' || col === 'createdAt' || col === 'updatedAt') return fmtDateTime(v);
  const s = String(v);
  return s.length > 120 ? s.slice(0, 117) + '...' : s;
}

export interface EntityTableProps {
  entityKey: string;
  parentId?: number;
  columns?: string[];
  onRowClick?: (row: any) => void;
  allowCreate?: boolean;
  allowImport?: boolean;
  allowExport?: boolean;
  allowDelete?: boolean;
  defaults?: any;
  filters?: Record<string, string>;
  reloadKey?: number;
  title?: ReactNode;
  extraActions?: ReactNode;
  dense?: boolean;
  onChanged?: () => void;
  createLabel?: string;
}

export default function EntityTable(p: EntityTableProps) {
  const { entity, orgId, can, toast } = useApp();
  const meta = entity(p.entityKey);
  const cols = p.columns ?? meta.listCols;
  const [rows, setRows] = useState<any[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(0);
  const [size, setSize] = useState(25);
  const [q, setQ] = useState('');
  const [sort, setSort] = useState<{ col?: string; desc: boolean }>({ desc: true });
  const [loading, setLoading] = useState(false);
  const [tick, setTick] = useState(0);
  const [edit, setEdit] = useState<{ open: boolean; id?: number | null }>({ open: false });
  const [anchor, setAnchor] = useState<null | HTMLElement>(null);
  const fileRef = useRef<HTMLInputElement>(null);
  const { confirm, dialog } = useConfirm();

  useEffect(() => {
    if (meta.orgScoped && !orgId) return;
    setLoading(true);
    const f: Record<string, string> = {};
    Object.entries(p.filters || {}).forEach(([k, v]) => { if (v) f['f.' + k] = v; });
    api.get(`/api/e/${p.entityKey}?${qs({ orgId: meta.orgScoped ? orgId : undefined, parentId: p.parentId, q, page: page + 1, pageSize: size, sort: sort.col, desc: sort.desc, ...f })}`)
      .then(r => { setRows(r.rows); setTotal(r.total); })
      .catch(e => toast(e.message, 'error'))
      .finally(() => setLoading(false));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [p.entityKey, p.parentId, orgId, q, page, size, sort, tick, p.reloadKey, JSON.stringify(p.filters)]);

  const reload = () => { setTick(t => t + 1); p.onChanged?.(); };
  const canEdit = can(`${meta.module}.edit`) && meta.genericWrite && p.allowCreate !== false;
  const colType = (c: string) => meta.cols.find(x => x.name === c)?.type;
  const label = (c: string) => meta.cols.find(x => x.name === c)?.label ?? c.replace(/([A-Z])/g, ' $1').replace(/^./, s => s.toUpperCase());

  const doImport = async (f: File) => {
    try {
      const r = await api.upload(`/api/e/${p.entityKey}/import?orgId=${orgId}`, f);
      toast(`Imported ${r.imported} rows${r.errors.length ? `, ${r.errors.length} errors: ${r.errors.slice(0, 3).join('; ')}` : ''}`, r.errors.length ? 'warning' : 'success');
      reload();
    } catch (e: any) { toast(e.message, 'error'); }
  };

  return (
    <Card variant="outlined">
      <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1} alignItems={{ sm: 'center' }} justifyContent="space-between" sx={{ p: 1.5 }}>
        <Stack direction="row" spacing={1} alignItems="center">
          {p.title && <Typography variant="subtitle1" fontWeight={700} sx={{ mr: 1 }}>{p.title}</Typography>}
          <TextField size="small" placeholder="Search" value={q} onChange={e => { setQ(e.target.value); setPage(0); }} InputProps={{ startAdornment: <InputAdornment position="start"><SearchIcon fontSize="small" /></InputAdornment> }} sx={{ width: 240 }} />
        </Stack>
        <Stack direction="row" spacing={1}>
          {p.extraActions}
          {p.allowExport !== false && can(`${meta.module}.export`) && meta.orgScoped && (
            <>
              <Button size="small" startIcon={<FileDownloadIcon />} onClick={e => setAnchor(e.currentTarget)}>Export</Button>
              <Menu anchorEl={anchor} open={!!anchor} onClose={() => setAnchor(null)}>
                <MenuItem onClick={() => { setAnchor(null); api.download(`/api/e/${p.entityKey}/export?orgId=${orgId}&format=xlsx`); }}>Excel (.xlsx)</MenuItem>
                <MenuItem onClick={() => { setAnchor(null); api.download(`/api/e/${p.entityKey}/export?orgId=${orgId}&format=csv`); }}>CSV</MenuItem>
                {p.allowImport && <MenuItem onClick={() => { setAnchor(null); api.download(`/api/e/${p.entityKey}/import-template`); }}>Import template</MenuItem>}
              </Menu>
            </>
          )}
          {p.allowImport && canEdit && (
            <>
              <input ref={fileRef} type="file" hidden accept=".csv,.xlsx" onChange={e => { const f = e.target.files?.[0]; if (f) doImport(f); e.target.value = ''; }} />
              <Button size="small" startIcon={<FileUploadIcon />} onClick={() => fileRef.current?.click()}>Import</Button>
            </>
          )}
          {canEdit && <Button size="small" variant="contained" startIcon={<AddIcon />} onClick={() => setEdit({ open: true, id: null })}>{p.createLabel ?? 'New'}</Button>}
        </Stack>
      </Stack>
      {loading && <LinearProgress sx={{ height: 2 }} />}
      <TableContainer>
        <Table size={p.dense ? 'small' : 'medium'}>
          <TableHead>
            <TableRow>
              {cols.map(c => (
                <TableCell key={c} sx={{ fontWeight: 700, whiteSpace: 'nowrap', bgcolor: 'grey.50' }}>
                  <TableSortLabel active={sort.col === c} direction={sort.desc ? 'desc' : 'asc'} onClick={() => setSort(s => ({ col: c, desc: s.col === c ? !s.desc : false }))}>{label(c)}</TableSortLabel>
                </TableCell>
              ))}
              {p.allowDelete !== false && can(`${meta.module}.delete`) && meta.genericWrite && <TableCell sx={{ bgcolor: 'grey.50' }} width={48} />}
            </TableRow>
          </TableHead>
          <TableBody>
            {rows.map(r => (
              <TableRow key={r.id} hover sx={{ cursor: 'pointer' }} onClick={() => (p.onRowClick ? p.onRowClick(r) : setEdit({ open: true, id: r.id }))}>
                {cols.map(c => <TableCell key={c} sx={{ maxWidth: 360 }}>{renderCell(c, r[c], colType(c))}</TableCell>)}
                {p.allowDelete !== false && can(`${meta.module}.delete`) && meta.genericWrite && (
                  <TableCell onClick={e => e.stopPropagation()}>
                    <Tooltip title="Delete"><IconButton size="small" onClick={() => confirm('Delete record', 'The record will be removed from active lists. The audit trail keeps a history of the deletion.', async () => { try { await api.del(`/api/e/${p.entityKey}/${r.id}`); toast('Deleted'); reload(); } catch (e: any) { toast(e.message, 'error'); } }, true, 'Delete')}><DeleteOutlineIcon fontSize="small" /></IconButton></Tooltip>
                  </TableCell>
                )}
              </TableRow>
            ))}
            {!loading && rows.length === 0 && <TableRow><TableCell colSpan={cols.length + 1}><Box sx={{ py: 4, textAlign: 'center', color: 'text.secondary' }}>No records yet.</Box></TableCell></TableRow>}
          </TableBody>
        </Table>
      </TableContainer>
      <TablePagination component="div" count={total} page={page} rowsPerPage={size} onPageChange={(_, n) => setPage(n)} onRowsPerPageChange={e => { setSize(Number(e.target.value)); setPage(0); }} rowsPerPageOptions={[10, 25, 50, 100]} />
      <EntityDialog entityKey={p.entityKey} id={edit.id} open={edit.open} onClose={() => setEdit({ open: false })} onSaved={reload} defaults={p.defaults} parentId={p.parentId} />
      {dialog}
    </Card>
  );
}
