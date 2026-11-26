import { Alert, Box } from '@mui/material';
import { PageHeader } from '../components/Common';
import EntityTable from '../components/EntityTable';

export default function Requirements() {
  return (
    <Box>
      <PageHeader title="DPDP Requirement Library" subtitle="Requirements mapped to sections of the Digital Personal Data Protection Act, 2023 and the DPDP Rules, 2025 (G.S.R. 846(E), 13 November 2025). New assessments are built from the active requirements." />
      <Alert severity="info" variant="outlined" sx={{ mb: 2 }}>Edit wording, guidance, evidence expectations, default risk and commencement dates as notifications and amendments are issued. Changes apply to assessments created afterwards; existing assessments keep their recorded results.</Alert>
      <EntityTable entityKey="requirements" allowImport createLabel="Add requirement" />
    </Box>
  );
}
