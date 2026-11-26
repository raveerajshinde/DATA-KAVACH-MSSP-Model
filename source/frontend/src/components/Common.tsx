import { ReactNode, useState } from 'react';
import { Box, Button, Card, CardContent, Chip, Dialog, DialogActions, DialogContent, DialogTitle, Stack, Tooltip, Typography, Alert, LinearProgress } from '@mui/material';
import { timeLeft } from '../api';

export function PageHeader({ title, subtitle, actions }: { title: string; subtitle?: ReactNode; actions?: ReactNode }) {
  return (
    <Stack direction={{ xs: 'column', md: 'row' }} justifyContent="space-between" alignItems={{ md: 'flex-end' }} spacing={2} sx={{ mb: 3 }}>
      <Box>
        <Typography variant="h5" fontWeight={700} color="primary.dark">{title}</Typography>
        {subtitle && <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5, maxWidth: 900 }}>{subtitle}</Typography>}
      </Box>
      {actions && <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap>{actions}</Stack>}
    </Stack>
  );
}

const statusColors: Record<string, { bg: string; fg: string }> = {
  'Compliant': { bg: '#E6F4EA', fg: '#1E6B34' }, 'Partially Compliant': { bg: '#FFF4D6', fg: '#8A5A00' }, 'Non-Compliant': { bg: '#FDE7E7', fg: '#A11B1B' },
  'Not Assessed': { bg: '#EEF1F5', fg: '#4B5563' }, 'Not Applicable': { bg: '#F3F4F6', fg: '#6B7280' },
  'High': { bg: '#FDE7E7', fg: '#A11B1B' }, 'Medium': { bg: '#FFF4D6', fg: '#8A5A00' }, 'Low': { bg: '#E6F4EA', fg: '#1E6B34' }, 'Critical': { bg: '#F8D0D0', fg: '#7F0000' },
  'Approved': { bg: '#E6F4EA', fg: '#1E6B34' }, 'Approved internally': { bg: '#E6F4EA', fg: '#1E6B34' }, 'Pending': { bg: '#FFF4D6', fg: '#8A5A00' }, 'Rejected': { bg: '#FDE7E7', fg: '#A11B1B' },
  'Completed': { bg: '#E6F4EA', fg: '#1E6B34' }, 'Open': { bg: '#FFF4D6', fg: '#8A5A00' }, 'In Progress': { bg: '#E3EDFB', fg: '#0B3D91' }, 'Overdue': { bg: '#FDE7E7', fg: '#A11B1B' },
  'Sent': { bg: '#E6F4EA', fg: '#1E6B34' }, 'Not required': { bg: '#F3F4F6', fg: '#6B7280' }, 'Draft': { bg: '#EEF1F5', fg: '#4B5563' }, 'Closed': { bg: '#E6F4EA', fg: '#1E6B34' },
  'Given': { bg: '#E6F4EA', fg: '#1E6B34' }, 'Withdrawn': { bg: '#FDE7E7', fg: '#A11B1B' }, 'Draft - not legally approved': { bg: '#FFF4D6', fg: '#8A5A00' }, 'Under Review': { bg: '#E3EDFB', fg: '#0B3D91' },
};

export function StatusChip({ value, size = 'small' }: { value?: string | null; size?: 'small' | 'medium' }) {
  if (!value) return null;
  const c = statusColors[value] ?? { bg: '#EEF1F5', fg: '#374151' };
  return <Chip label={value} size={size} sx={{ bgcolor: c.bg, color: c.fg, fontWeight: 600, borderRadius: 1.5, height: size === 'small' ? 22 : 28 }} />;
}

export function DueChip({ due, done }: { due?: string | null; done?: boolean }) {
  if (!due) return null;
  if (done) return <Chip size="small" label="Done" color="success" variant="outlined" />;
  const t = timeLeft(due);
  return <Tooltip title={new Date(due).toLocaleString('en-IN')}><Chip size="small" label={t.text} color={t.overdue ? 'error' : t.hours < 24 ? 'warning' : 'default'} variant={t.overdue ? 'filled' : 'outlined'} /></Tooltip>;
}

export function Kpi({ label, value, hint, color = 'primary.main', icon }: { label: string; value: ReactNode; hint?: ReactNode; color?: string; icon?: ReactNode }) {
  return (
    <Card variant="outlined" sx={{ height: '100%' }}>
      <CardContent>
        <Stack direction="row" justifyContent="space-between" alignItems="flex-start">
          <Typography variant="overline" color="text.secondary" lineHeight={1.4}>{label}</Typography>
          {icon && <Box sx={{ color, opacity: 0.8 }}>{icon}</Box>}
        </Stack>
        <Typography variant="h4" fontWeight={700} sx={{ color, mt: 0.5 }}>{value}</Typography>
        {hint && <Typography variant="caption" color="text.secondary">{hint}</Typography>}
      </CardContent>
    </Card>
  );
}

export function SectionCard({ title, action, children, sx }: { title?: ReactNode; action?: ReactNode; children: ReactNode; sx?: any }) {
  return (
    <Card variant="outlined" sx={{ height: '100%', ...sx }}>
      <CardContent>
        {(title || action) && (
          <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ mb: 1.5 }}>
            <Typography variant="subtitle1" fontWeight={700}>{title}</Typography>
            {action}
          </Stack>
        )}
        {children}
      </CardContent>
    </Card>
  );
}

export function Confirm({ open, title, text, onClose, onConfirm, confirmLabel = 'Confirm', danger }: { open: boolean; title: string; text: ReactNode; onClose: () => void; onConfirm: () => void; confirmLabel?: string; danger?: boolean }) {
  return (
    <Dialog open={open} onClose={onClose} maxWidth="xs" fullWidth>
      <DialogTitle>{title}</DialogTitle>
      <DialogContent><Typography variant="body2">{text}</Typography></DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Cancel</Button>
        <Button variant="contained" color={danger ? 'error' : 'primary'} onClick={() => { onConfirm(); onClose(); }}>{confirmLabel}</Button>
      </DialogActions>
    </Dialog>
  );
}

export function useConfirm() {
  const [state, setState] = useState<{ title: string; text: ReactNode; action: () => void; danger?: boolean; label?: string } | null>(null);
  const dialog = state ? <Confirm open title={state.title} text={state.text} danger={state.danger} confirmLabel={state.label} onClose={() => setState(null)} onConfirm={state.action} /> : null;
  return { confirm: (title: string, text: ReactNode, action: () => void, danger = false, label?: string) => setState({ title, text, action, danger, label }), dialog };
}

export function Disclaimer() {
  return <Alert severity="info" variant="outlined" sx={{ mb: 2 }}>Indicators show self-assessment status only. They are not a legal opinion, certification or proof of DPDP compliance.</Alert>;
}

export function Progress({ value, label }: { value: number; label?: string }) {
  return (
    <Box>
      <Stack direction="row" justifyContent="space-between"><Typography variant="caption" color="text.secondary">{label}</Typography><Typography variant="caption" fontWeight={600}>{value.toFixed(0)}%</Typography></Stack>
      <LinearProgress variant="determinate" value={Math.min(100, value)} sx={{ height: 8, borderRadius: 4, mt: 0.5 }} />
    </Box>
  );
}

export function Empty({ text, action }: { text: string; action?: ReactNode }) {
  return <Box sx={{ py: 6, textAlign: 'center', color: 'text.secondary' }}><Typography variant="body2" sx={{ mb: 2 }}>{text}</Typography>{action}</Box>;
}

export function NoOrg() {
  return <Alert severity="warning">Create or select an organisation first (Organisations page).</Alert>;
}
