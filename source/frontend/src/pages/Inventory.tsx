import { useState } from 'react';
import { Alert, Box, Card, Tab, Tabs } from '@mui/material';
import { useApp } from '../context';
import { NoOrg, PageHeader } from '../components/Common';
import EntityTable from '../components/EntityTable';

export default function Inventory() {
  const { orgId } = useApp();
  const [tab, setTab] = useState(0);
  if (!orgId) return <><PageHeader title="Data Inventory" /><NoOrg /></>;
  return (
    <Box>
      <PageHeader title="Personal Data Inventory & Data Flows" subtitle="Know your data: what personal data you hold, why, where, who accesses it and which processors receive it. This register feeds the Record of Processing, Data Mapping and Data Flow reports." />
      <Card sx={{ mb: 2 }}><Tabs value={tab} onChange={(_, v) => setTab(v)}><Tab label="Processing activities" /><Tab label="Data flow register" /></Tabs></Card>
      {tab === 0 && <>
        <Alert severity="info" variant="outlined" sx={{ mb: 2 }}>Record a permitted ground for every activity: consent (s.6) or a specific legitimate use under s.7 - for example s.7(i) for employment purposes. DPDP has no "legitimate interests" ground.</Alert>
        <EntityTable entityKey="inventory" allowImport createLabel="Add activity" />
      </>}
      {tab === 1 && <EntityTable entityKey="dataflows" allowImport createLabel="Add flow" />}
    </Box>
  );
}
