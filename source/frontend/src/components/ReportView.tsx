import { Box, Typography } from '@mui/material';
import { Bar, BarChart, Cell, Legend, Pie, PieChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';

const sem: Record<string, [string, string]> = {
  'Compliant': ['#C6EFCE', '#006100'], 'Partially Compliant': ['#FFEB9C', '#9C5700'], 'Non-Compliant': ['#FFC7CE', '#9C0006'], 'Not Applicable': ['#EDEDED', '#595959'], 'Not Assessed': ['#F2F2F2', '#595959'],
  High: ['#FFC7CE', '#9C0006'], Medium: ['#FFEB9C', '#9C5700'], Low: ['#C6EFCE', '#006100'], Yes: ['#C6EFCE', '#006100'], No: ['#FFC7CE', '#9C0006'], Overdue: ['#FFC7CE', '#9C0006'], 'On time': ['#C6EFCE', '#006100'],
};
const chartSem: Record<string, string> = { Compliant: '#2E7D32', 'Partially Compliant': '#F9A825', 'Non-Compliant': '#C62828', 'Not Assessed': '#9E9E9E', 'Not Applicable': '#CFD8DC', High: '#C62828', Medium: '#F9A825', Low: '#2E7D32' };
const palette = ['#0B3D91', '#F2A900', '#2E7D32', '#C62828', '#6A1B9A', '#00838F'];

function Rich({ text }: { text?: string }) {
  return <>{(text || '').split(/(\*\*[^*]+\*\*)/).map((p, i) => (p.startsWith('**') ? <b key={i}>{p.slice(2, -2)}</b> : <span key={i} style={{ whiteSpace: 'pre-wrap' }}>{p}</span>))}</>;
}

export function BlockView({ b, primary }: { b: any; primary: string }) {
  switch (b.type) {
    case 'p': return <Typography variant="body2" sx={{ my: 1, textAlign: 'justify' }}><Rich text={b.text} /></Typography>;
    case 'h2': return <Typography variant="subtitle1" fontWeight={700} sx={{ color: primary, mt: 2 }}>{b.text}</Typography>;
    case 'bullets': return <Box sx={{ my: 1 }}>{b.title && <Typography variant="body2" fontWeight={700}>{b.title}</Typography>}<ul style={{ margin: '4px 0', paddingLeft: 20 }}>{(b.items || []).map((x: string, i: number) => <li key={i}><Typography variant="body2"><Rich text={x} /></Typography></li>)}</ul></Box>;
    case 'callout': return <Box sx={{ my: 1.5, p: 1.5, borderLeft: '5px solid', borderColor: b.tone === 'danger' ? '#C00000' : b.tone === 'warning' ? '#C68A00' : b.tone === 'success' ? '#2E7D32' : primary, bgcolor: b.tone === 'danger' ? '#FDECEC' : b.tone === 'warning' ? '#FFF6E0' : b.tone === 'success' ? '#EAF6EC' : '#EAF0FA' }}>{b.title && <Typography variant="body2" fontWeight={700}>{b.title}</Typography>}<Typography variant="body2">{b.text}</Typography></Box>;
    case 'pagebreak': return <Box sx={{ my: 2, borderTop: '2px dashed #cbd5e1', textAlign: 'center' }}><Typography variant="caption" color="text.secondary">page break</Typography></Box>;
    case 'signature': return <Box sx={{ my: 1 }}><table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12 }}><thead><tr>{['Role', 'Name', 'Designation', 'Signature', 'Date'].map(h => <th key={h} style={{ background: primary, color: '#fff', padding: 4, textAlign: 'left' }}>{h}</th>)}</tr></thead><tbody>{['Prepared by', 'Reviewed by', 'Approved by'].map(r => <tr key={r}><td style={{ border: '1px solid #BFC9D9', padding: 12, fontWeight: 700 }}>{r}</td>{[1, 2, 3, 4].map(i => <td key={i} style={{ border: '1px solid #BFC9D9' }} />)}</tr>)}</tbody></table></Box>;
    case 'kv': return (
      <Box sx={{ my: 1 }}>{b.title && <Typography variant="body2" fontWeight={700} sx={{ color: primary }}>{b.title}</Typography>}
        <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12 }}><tbody>{(b.rows || []).map((r: string[], i: number) => <tr key={i}><td style={{ width: '32%', border: '1px solid #BFC9D9', padding: '3px 6px', background: '#EAF0FA', color: primary, fontWeight: 700 }}>{r[0]}</td><td style={{ border: '1px solid #BFC9D9', padding: '3px 6px', whiteSpace: 'pre-wrap' }}>{r[1]}</td></tr>)}</tbody></table>
      </Box>);
    case 'table': return (
      <Box sx={{ my: 1, overflowX: 'auto' }}>{b.title && <Typography variant="body2" fontWeight={700} sx={{ color: primary }}>{b.title}</Typography>}
        <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 11.5 }}>
          <thead><tr>{(b.columns || []).map((c: string, i: number) => <th key={i} style={{ background: primary, color: '#fff', padding: '4px 6px', textAlign: 'left', border: '1px solid #BFC9D9' }}>{c}</th>)}</tr></thead>
          <tbody>{(b.rows || []).map((r: string[], i: number) => <tr key={i}>{r.map((c, j) => { const s = sem[c?.trim?.()]; return <td key={j} style={{ border: '1px solid #BFC9D9', padding: '3px 6px', background: s?.[0] ?? (i % 2 ? '#F5F7FB' : undefined), color: s?.[1], whiteSpace: 'pre-wrap', verticalAlign: 'top' }}>{c}</td>; })}</tr>)}
            {(b.rows || []).length === 0 && <tr><td colSpan={(b.columns || []).length} style={{ border: '1px solid #BFC9D9', padding: 6, color: '#888' }}>No records.</td></tr>}</tbody>
        </table>
      </Box>);
    case 'chart': {
      const data = (b.data || []).map((d: any) => ({ name: d.label, value: d.value, ...(b.series ? Object.fromEntries(b.series.map((s: string, i: number) => [s, d.values?.[i] ?? 0])) : {}) }));
      if (!data.length || data.every((d: any) => !d.value && !(b.series && b.series.some((s: string) => d[s])))) return <Typography variant="caption" display="block" textAlign="center" color="text.secondary">{b.title}: no data recorded yet.</Typography>;
      return (
        <Box sx={{ my: 1 }}>
          <Typography variant="body2" fontWeight={700} textAlign="center" sx={{ color: primary }}>{b.title}</Typography>
          <ResponsiveContainer width="100%" height={b.chartType === 'pie' ? 220 : 240}>
            {b.chartType === 'pie'
              ? <PieChart><Pie data={data} dataKey="value" nameKey="name" innerRadius={50} outerRadius={85} label>{data.map((d: any, i: number) => <Cell key={i} fill={chartSem[d.name] ?? palette[i % palette.length]} />)}</Pie><Legend wrapperStyle={{ fontSize: 11 }} /><Tooltip /></PieChart>
              : b.chartType === 'stacked'
                ? <BarChart data={data}><XAxis dataKey="name" tick={{ fontSize: 10 }} /><YAxis allowDecimals={false} /><Tooltip /><Legend wrapperStyle={{ fontSize: 11 }} />{b.series.map((s: string, i: number) => <Bar key={s} dataKey={s} stackId="a" fill={chartSem[s] ?? palette[i]} />)}</BarChart>
                : <BarChart data={data} layout={b.chartType === 'hbar' ? 'vertical' : 'horizontal'}>{b.chartType === 'hbar' ? <><XAxis type="number" allowDecimals={false} /><YAxis type="category" dataKey="name" width={160} tick={{ fontSize: 10 }} /></> : <><XAxis dataKey="name" tick={{ fontSize: 11 }} /><YAxis allowDecimals={false} /></>}<Tooltip /><Bar dataKey="value">{data.map((d: any, i: number) => <Cell key={i} fill={chartSem[d.name] ?? (i === 0 ? primary : palette[i % palette.length])} />)}</Bar></BarChart>}
          </ResponsiveContainer>
        </Box>
      );
    }
    default: return null;
  }
}

/** HTML rendering of report content for on-screen preview. */
export default function ReportView({ content, primary = '#0B3D91', secondary = '#F2A900', font = 'Calibri' }: { content: any; primary?: string; secondary?: string; font?: string }) {
  let n1 = 0, n2 = 0;
  return (
    <Box sx={{ fontFamily: font, '& *': { fontFamily: `${font}, Segoe UI, Arial !important` } }}>
      <Typography variant="h4" fontWeight={700} sx={{ color: primary }}>{content.title}</Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>{content.subtitle}</Typography>
      {content.sections.filter((s: any) => s.include).map((s: any) => {
        const num = s.level <= 1 ? `${++n1}.` : `${n1}.${++n2}`;
        if (s.level <= 1) n2 = 0;
        return (
          <Box key={s.id} id={`sec-${s.id}`} sx={{ mb: 2 }}>
            <Typography variant={s.level <= 1 ? 'h6' : 'subtitle1'} fontWeight={700} sx={{ color: primary, borderBottom: s.level <= 1 ? `2px solid ${secondary}` : 'none', pb: 0.5, mt: 2 }}>{num} {s.heading}</Typography>
            {s.blocks.map((b: any, i: number) => <BlockView key={i} b={b} primary={primary} />)}
          </Box>
        );
      })}
    </Box>
  );
}
