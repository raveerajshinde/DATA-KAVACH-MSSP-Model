import { ReactNode, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Alert, Box, Button, Card, CardContent, Grid, Stack } from '@mui/material';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import { api } from '../api';
import { useApp } from '../context';
import { PageHeader } from './Common';
import { EntityFields } from './EntityForm';
import EvidencePanel from './EvidencePanel';

/** Standard detail page: editable record form, side panel (evidence etc.) and child sections. */
export default function RecordDetail({ entityKey, id, backTo, title, subtitle, actions, children, side, exclude, onLoaded, reloadKey, evidence = true }: {
  entityKey: string; id: number; backTo: string; title: (r: any) => ReactNode; subtitle?: (r: any) => ReactNode; actions?: (r: any, reload: () => void) => ReactNode;
  children?: (r: any, reload: () => void) => ReactNode; side?: (r: any, reload: () => void) => ReactNode; exclude?: string[]; onLoaded?: (r: any) => void; reloadKey?: number; evidence?: boolean;
}) {
  const { entity, can, toast } = useApp();
  const nav = useNavigate();
  const meta = entity(entityKey);
  const [row, setRow] = useState<any>(null);
  const [values, setValues] = useState<any>({});
  const [err, setErr] = useState<string | null>(null);
  const [tick, setTick] = useState(0);
  const reload = () => setTick(t => t + 1);
  useEffect(() => {
    api.get(`/api/e/${entityKey}/${id}`).then(r => { setRow(r); setValues(r); onLoaded?.(r); }).catch(e => setErr(e.message));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [entityKey, id, tick, reloadKey]);
  const dirty = row && JSON.stringify(values) !== JSON.stringify(row);
  const save = async () => {
    try { const r = await api.put(`/api/e/${entityKey}/${id}`, values); setRow(r); setValues(r); toast('Saved'); }
    catch (e: any) { toast(e.message, 'error'); }
  };
  if (err) return <Alert severity="error">{err}</Alert>;
  if (!row) return null;
  return (
    <Box>
      <Button startIcon={<ArrowBackIcon />} onClick={() => nav(backTo)} sx={{ mb: 1 }}>Back</Button>
      <PageHeader title={title(row) as string} subtitle={subtitle?.(row)} actions={<>{actions?.(row, reload)}{can(`${meta.module}.edit`) && <Button variant="contained" disabled={!dirty} onClick={save}>Save changes</Button>}</>} />
      <Grid container spacing={2}>
        <Grid size={{ xs: 12, lg: side || evidence ? 8 : 12 }}>
          <Card><CardContent><EntityFields meta={meta} values={values} setValues={setValues} exclude={exclude} disabled={!can(`${meta.module}.edit`)} /></CardContent></Card>
          {children && <Box sx={{ mt: 2 }}>{children(row, reload)}</Box>}
        </Grid>
        {(side || evidence) && (
          <Grid size={{ xs: 12, lg: 4 }}>
            <Stack spacing={2}>
              {side?.(row, reload)}
              {evidence && <Card><CardContent><EvidencePanel linkedEntity={entityKey} linkedId={id} /></CardContent></Card>}
            </Stack>
          </Grid>
        )}
      </Grid>
    </Box>
  );
}
