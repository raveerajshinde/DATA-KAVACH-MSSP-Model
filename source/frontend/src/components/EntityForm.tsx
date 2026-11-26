import { useEffect, useMemo, useState } from 'react';
import { Autocomplete, Box, Button, Checkbox, Dialog, DialogActions, DialogContent, DialogTitle, FormControlLabel, Grid, MenuItem, TextField, Typography, Divider, Alert } from '@mui/material';
import { api, fromLocalInput, qs, toLocalInput } from '../api';
import { ColMeta, EntityMeta, useApp } from '../context';

export function FieldInput({ col, value, onChange, disabled, deptOptions }: { col: ColMeta; value: any; onChange: (v: any) => void; disabled?: boolean; deptOptions?: string[] }) {
  const common = { fullWidth: true, size: 'small' as const, label: col.label + (col.required ? ' *' : ''), disabled: disabled || col.readOnly, helperText: col.help ?? undefined };
  if (col.type === 'bool')
    return <FormControlLabel control={<Checkbox checked={!!value} onChange={e => onChange(e.target.checked)} disabled={disabled || col.readOnly} />} label={<Typography variant="body2">{col.label}</Typography>} />;
  if (col.multi && col.options)
    return <Autocomplete multiple size="small" options={col.options} value={(value ? String(value).split(';').map(s => s.trim()).filter(Boolean) : [])} onChange={(_, v) => onChange(v.join('; '))} disabled={disabled || col.readOnly} renderInput={p => <TextField {...p} label={col.label} />} freeSolo />;
  const opts = col.options ?? (col.ref === 'departments' ? deptOptions : null);
  if (opts)
    return <TextField select {...common} value={value ?? ''} onChange={e => onChange(e.target.value)}><MenuItem value=""><em>-</em></MenuItem>{opts.map(o => <MenuItem key={o} value={o}>{o}</MenuItem>)}</TextField>;
  switch (col.type) {
    case 'text': return <TextField {...common} multiline minRows={2} maxRows={10} value={value ?? ''} onChange={e => onChange(e.target.value)} />;
    case 'int': case 'long': case 'dec': return <TextField {...common} type="number" value={value ?? ''} onChange={e => onChange(e.target.value === '' ? null : Number(e.target.value))} />;
    case 'date': return <TextField {...common} type="date" InputLabelProps={{ shrink: true }} value={value ?? ''} onChange={e => onChange(e.target.value || null)} />;
    case 'datetime': return <TextField {...common} type="datetime-local" InputLabelProps={{ shrink: true }} value={toLocalInput(value)} onChange={e => onChange(fromLocalInput(e.target.value))} />;
    default: return <TextField {...common} value={value ?? ''} inputProps={{ maxLength: col.len || 200 }} onChange={e => onChange(e.target.value)} />;
  }
}

export function useDepartments() {
  const { orgId } = useApp();
  const [deps, setDeps] = useState<string[]>([]);
  useEffect(() => { if (orgId) api.get(`/api/e/departments?orgId=${orgId}&pageSize=200&sort=name`).then(r => setDeps(r.rows.map((d: any) => d.name))).catch(() => undefined); }, [orgId]);
  return deps;
}

/** Form body for an entity, grouped by column group. */
export function EntityFields({ meta, values, setValues, exclude = [], disabled }: { meta: EntityMeta; values: any; setValues: (v: any) => void; exclude?: string[]; disabled?: boolean }) {
  const deps = useDepartments();
  const cols = meta.cols.filter(c => !c.hidden && !exclude.includes(c.name));
  const groups = useMemo(() => {
    const g: Record<string, ColMeta[]> = {};
    cols.forEach(c => { (g[c.group || ''] ??= []).push(c); });
    return Object.entries(g);
  }, [cols]);
  return (
    <Box>
      {groups.map(([group, gcols], gi) => (
        <Box key={group || gi} sx={{ mb: 2 }}>
          {group && <><Typography variant="overline" color="primary">{group}</Typography><Divider sx={{ mb: 1.5 }} /></>}
          <Grid container spacing={1.5}>
            {gcols.map(c => (
              <Grid key={c.name} size={{ xs: 12, sm: c.type === 'text' || c.multi ? 12 : 6, md: c.type === 'text' || c.multi ? 12 : c.type === 'bool' ? 6 : 4 }}>
                <FieldInput col={c} value={values[c.name]} onChange={v => setValues({ ...values, [c.name]: v })} disabled={disabled} deptOptions={deps} />
              </Grid>
            ))}
          </Grid>
        </Box>
      ))}
    </Box>
  );
}

export function EntityDialog({ entityKey, id, open, onClose, onSaved, defaults, parentId, title, exclude }: { entityKey: string; id?: number | null; open: boolean; onClose: () => void; onSaved?: (row: any) => void; defaults?: any; parentId?: number; title?: string; exclude?: string[] }) {
  const { entity, orgId, toast, can } = useApp();
  const meta = entity(entityKey);
  const [values, setValues] = useState<any>({});
  const [err, setErr] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  useEffect(() => {
    setErr(null);
    if (!open) return;
    if (id) api.get(`/api/e/${entityKey}/${id}`).then(setValues).catch(e => setErr(e.message));
    else setValues({ ...(defaults || {}) });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, id, entityKey]);
  const canEdit = can(`${meta.module}.edit`);
  const save = async () => {
    setBusy(true); setErr(null);
    try {
      const body = { ...values };
      if (meta.parentCol && parentId) body[meta.parentCol] = parentId;
      const row = id ? await api.put(`/api/e/${entityKey}/${id}`, body) : await api.post(`/api/e/${entityKey}?${qs({ orgId: meta.orgScoped ? orgId : undefined })}`, body);
      toast(id ? 'Saved' : 'Created');
      onSaved?.(row);
      onClose();
    } catch (e: any) { setErr(e.message); } finally { setBusy(false); }
  };
  return (
    <Dialog open={open} onClose={onClose} maxWidth="md" fullWidth>
      <DialogTitle>{title ?? `${id ? 'Edit' : 'New'} ${meta.label.replace(/s$/, '')}`}</DialogTitle>
      <DialogContent dividers>
        {err && <Alert severity="error" sx={{ mb: 2 }}>{err}</Alert>}
        <EntityFields meta={meta} values={values} setValues={setValues} exclude={exclude} disabled={!canEdit} />
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancel</Button>
        {canEdit && <Button variant="contained" onClick={save} disabled={busy}>Save</Button>}
      </DialogActions>
    </Dialog>
  );
}
