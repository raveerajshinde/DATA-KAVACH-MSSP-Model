import { useEffect, useState } from 'react';
import { Box, Typography } from '@mui/material';
import { api } from '../api';

export function useStoredImage(fileId?: number | null) {
  const [url, setUrl] = useState<string | null>(null);
  useEffect(() => {
    if (!fileId) { setUrl(null); return; }
    let u: string | null = null;
    api.blob(`/api/files/stored/${fileId}`).then(r => { u = URL.createObjectURL(r.blob); setUrl(u); }).catch(() => setUrl(null));
    return () => { if (u) URL.revokeObjectURL(u); };
  }, [fileId]);
  return url;
}

export const COVERS = ['Corporate Band', 'Executive Minimal', 'Bold Block', 'Split Panel', 'Framed Classic'];

/** Miniature, live HTML rendering of a report cover and content page with the given branding. */
export function CoverThumb({ design, primary = '#0B3D91', secondary = '#F2A900', font = 'Calibri', title = 'DPDP Compliance Report', org = 'Organisation', logo, consultantLogo, scale = 1, watermark }: { design: string; primary?: string; secondary?: string; font?: string; title?: string; org?: string; logo?: string | null; consultantLogo?: string | null; scale?: number; watermark?: string | null }) {
  const W = 210 * scale, H = 297 * scale;
  const t = (n: number) => `${n * scale}px`;
  const tn = (n: number) => n * scale;
  const logos = <Box sx={{ display: 'flex', gap: t(4), alignItems: 'center', height: t(14) }}>{logo ? <img src={logo} style={{ maxHeight: t(14), maxWidth: t(60) }} /> : <Box sx={{ width: t(30), height: t(10), bgcolor: '#e5e7eb' }} />}{consultantLogo && <img src={consultantLogo} style={{ maxHeight: t(11), maxWidth: t(45) }} />}</Box>;
  const meta = <Box sx={{ mt: t(8) }}>{[1, 2, 3, 4].map(i => <Box key={i} sx={{ display: 'flex', gap: t(2), mb: t(1.5) }}><Box sx={{ width: '30%', height: t(4), bgcolor: '#EAF0FA' }} /><Box sx={{ flex: 1, height: t(4), bgcolor: '#f3f4f6' }} /></Box>)}</Box>;
  const titleEl = (color: string, size = 15) => <Typography sx={{ fontFamily: font, fontWeight: 700, fontSize: t(size), lineHeight: 1.15, color }}>{title}</Typography>;
  let body;
  switch (design) {
    case 'Executive Minimal':
      body = <Box sx={{ p: t(14) }}>{logos}<Box sx={{ mt: t(60) }}><Typography sx={{ fontFamily: font, fontSize: t(6), color: '#777', letterSpacing: 1 }}>{org.toUpperCase()}</Typography>{titleEl(primary, 17)}<Box sx={{ height: t(2), bgcolor: secondary, my: t(5) }} />{meta}</Box></Box>;
      break;
    case 'Bold Block':
      body = <Box><Box sx={{ bgcolor: primary, height: H * 0.52, p: t(14), display: 'flex', flexDirection: 'column', justifyContent: 'flex-end' }}>{logos}<Box sx={{ mt: t(20) }}>{titleEl('#fff', 18)}</Box><Typography sx={{ fontFamily: font, fontWeight: 700, color: secondary, fontSize: t(8), mt: t(3) }}>{org}</Typography></Box><Box sx={{ height: t(3), bgcolor: secondary }} /><Box sx={{ p: t(14) }}>{meta}</Box></Box>;
      break;
    case 'Split Panel':
      body = <Box sx={{ display: 'flex', height: '100%' }}><Box sx={{ width: '30%', bgcolor: primary, p: t(6), pt: t(40) }}><Typography sx={{ fontFamily: font, color: '#fff', fontWeight: 700, fontSize: t(8) }}>{org}</Typography><Typography sx={{ color: secondary, fontSize: t(5), fontWeight: 700 }}>CONFIDENTIAL</Typography></Box><Box sx={{ flex: 1, p: t(10) }}><Box sx={{ display: 'flex', justifyContent: 'flex-end' }}>{logos}</Box><Box sx={{ mt: t(60) }}>{titleEl(primary, 15)}</Box>{meta}</Box></Box>;
      break;
    case 'Framed Classic':
      body = <Box sx={{ position: 'absolute', inset: t(8), border: `${tn(1.5)}px double ${primary}`, p: t(10), textAlign: 'center', display: 'flex', flexDirection: 'column', alignItems: 'center' }}><Box sx={{ mt: t(18) }}>{logos}</Box><Typography sx={{ fontFamily: font, fontSize: t(8), fontWeight: 700, color: '#444', mt: t(30) }}>{org}</Typography><Box sx={{ mt: t(6) }}>{titleEl(primary, 16)}</Box><Box sx={{ width: '40%', borderBottom: `${tn(1.5)}px double ${secondary}`, my: t(5) }} /><Box sx={{ width: '100%' }}>{meta}</Box></Box>;
      break;
    default:
      body = <Box sx={{ p: t(14), pt: t(12) }}>{logos}<Box sx={{ bgcolor: primary, mt: t(30), mx: t(-14), px: t(14), py: t(14) }}>{titleEl('#fff', 16)}<Typography sx={{ color: '#fff', opacity: 0.85, fontSize: t(5.5), mt: t(2), fontFamily: font }}>Digital Personal Data Protection Act, 2023</Typography></Box><Box sx={{ height: t(2.5), bgcolor: secondary, mx: t(-14) }} /><Typography sx={{ fontFamily: font, fontWeight: 700, fontSize: t(10), color: primary, mt: t(8) }}>{org}</Typography>{meta}</Box>;
  }
  return (
    <Box sx={{ width: W, height: H, bgcolor: '#fff', boxShadow: '0 2px 10px rgba(16,24,40,0.12)', position: 'relative', overflow: 'hidden', flexShrink: 0 }}>
      {body}
      {watermark && <Typography sx={{ position: 'absolute', top: '45%', left: '-10%', right: '-10%', textAlign: 'center', transform: 'rotate(-45deg)', fontSize: t(26), fontWeight: 800, color: 'rgba(0,0,0,0.07)', pointerEvents: 'none', fontFamily: font }}>{watermark}</Typography>}
      <Typography sx={{ position: 'absolute', bottom: t(5), width: '100%', textAlign: 'center', fontSize: t(4.5), color: '#C00000', fontWeight: 700 }}>CONFIDENTIAL</Typography>
    </Box>
  );
}

export function PagePreview({ primary = '#0B3D91', secondary = '#F2A900', font = 'Calibri', org = 'Organisation', header, footer, logo, logoPlacement = 'Header left', scale = 1, watermark }: { primary?: string; secondary?: string; font?: string; org?: string; header?: string | null; footer?: string | null; logo?: string | null; logoPlacement?: string; scale?: number; watermark?: string | null }) {
  const t = (n: number) => `${n * scale}px`;
  const tn = (n: number) => n * scale;
  const right = logoPlacement === 'Header right';
  const showLogo = logoPlacement !== 'Cover only';
  return (
    <Box sx={{ width: 210 * scale, height: 297 * scale, bgcolor: '#fff', boxShadow: '0 2px 10px rgba(16,24,40,0.12)', p: t(12), position: 'relative', overflow: 'hidden', fontFamily: font, flexShrink: 0 }}>
      <Box sx={{ display: 'flex', flexDirection: right ? 'row-reverse' : 'row', justifyContent: 'space-between', alignItems: 'flex-end', borderBottom: `${tn(0.8)}px solid ${primary}`, pb: t(2) }}>
        <Box sx={{ height: t(10) }}>{showLogo && (logo ? <img src={logo} style={{ maxHeight: t(10), maxWidth: t(45) }} /> : <Box sx={{ width: t(25), height: t(8), bgcolor: '#e5e7eb' }} />)}</Box>
        <Box sx={{ textAlign: right ? 'left' : 'right' }}><Typography sx={{ fontSize: t(5), fontWeight: 700, color: primary, fontFamily: font }}>{header || org}</Typography><Typography sx={{ fontSize: t(4.2), color: '#888', fontFamily: font }}>DPDP Compliance Gap Assessment</Typography></Box>
      </Box>
      <Typography sx={{ fontSize: t(9), fontWeight: 700, color: primary, mt: t(8), fontFamily: font, borderBottom: `${tn(1)}px solid ${secondary}`, pb: t(1) }}>1. Summary of assessment results</Typography>
      {[1, 2, 3].map(i => <Box key={i} sx={{ height: t(3), bgcolor: '#eef0f4', mt: t(3), width: `${100 - i * 8}%` }} />)}
      <Box sx={{ mt: t(6), border: '1px solid #BFC9D9' }}>
        <Box sx={{ display: 'flex', bgcolor: primary }}>{['Requirement', 'Status', 'Risk'].map(h => <Typography key={h} sx={{ flex: 1, color: '#fff', fontSize: t(4.2), fontWeight: 700, p: t(1.2), fontFamily: font }}>{h}</Typography>)}</Box>
        {[['Notice before consent', 'Compliant', '#C6EFCE'], ['Breach readiness', 'Partially Compliant', '#FFEB9C'], ['Processor contracts', 'Non-Compliant', '#FFC7CE']].map(([a, b, c]) => (
          <Box key={a} sx={{ display: 'flex', borderTop: '1px solid #BFC9D9' }}><Typography sx={{ flex: 1, fontSize: t(4), p: t(1.2), fontFamily: font }}>{a}</Typography><Typography sx={{ flex: 1, fontSize: t(4), p: t(1.2), bgcolor: c, fontFamily: font }}>{b}</Typography><Typography sx={{ flex: 1, fontSize: t(4), p: t(1.2), fontFamily: font }}>High</Typography></Box>
        ))}
      </Box>
      <Box sx={{ display: 'flex', gap: t(3), mt: t(8), alignItems: 'flex-end', height: t(40), px: t(10) }}>{[60, 35, 80, 20].map((h, i) => <Box key={i} sx={{ flex: 1, height: `${h}%`, bgcolor: i % 2 ? secondary : primary }} />)}</Box>
      {watermark && <Typography sx={{ position: 'absolute', top: '45%', left: '-10%', right: '-10%', textAlign: 'center', transform: 'rotate(-45deg)', fontSize: t(26), fontWeight: 800, color: 'rgba(0,0,0,0.07)', fontFamily: font }}>{watermark}</Typography>}
      <Box sx={{ position: 'absolute', left: t(12), right: t(12), bottom: t(6), borderTop: '1px solid #ccc', pt: t(1), display: 'flex', justifyContent: 'space-between' }}>
        <Typography sx={{ fontSize: t(3.8), color: '#666', fontFamily: font }}>{footer || `${org} | Ref | v1.0`}</Typography>
        <Typography sx={{ fontSize: t(3.8), color: '#C00000', fontWeight: 700 }}>CONFIDENTIAL</Typography>
        <Typography sx={{ fontSize: t(3.8), color: '#666' }}>Page 5 of 24</Typography>
      </Box>
    </Box>
  );
}
