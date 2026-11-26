import React, { lazy, Suspense, useMemo } from 'react';
import ReactDOM from 'react-dom/client';
import { BrowserRouter, Route, Routes, Navigate } from 'react-router-dom';
import { Alert, CircularProgress, CssBaseline, Snackbar, ThemeProvider, createTheme, Box } from '@mui/material';
import { AppProvider, useApp } from './context';
import Layout from './components/Layout';
import Login from './pages/Login';

const Dashboard = lazy(() => import('./pages/Dashboard'));
const Organizations = lazy(() => import('./pages/Organizations'));
const Assessments = lazy(() => import('./pages/Assessments'));
const AssessmentDetail = lazy(() => import('./pages/AssessmentDetail'));
const Inventory = lazy(() => import('./pages/Inventory'));
const Dpia = lazy(() => import('./pages/Dpia'));
const Consent = lazy(() => import('./pages/Consent'));
const Policies = lazy(() => import('./pages/Policies'));
const Vendors = lazy(() => import('./pages/Vendors'));
const Security = lazy(() => import('./pages/Security'));
const Rights = lazy(() => import('./pages/Rights'));
const Incidents = lazy(() => import('./pages/Incidents'));
const Roadmap = lazy(() => import('./pages/Roadmap'));
const Reports = lazy(() => import('./pages/Reports'));
const ReportEditor = lazy(() => import('./pages/ReportEditor'));
const Templates = lazy(() => import('./pages/Templates'));
const Evidence = lazy(() => import('./pages/Evidence'));
const Users = lazy(() => import('./pages/Users'));
const Audit = lazy(() => import('./pages/Audit'));
const Settings = lazy(() => import('./pages/Settings'));
const Requirements = lazy(() => import('./pages/Requirements'));
const Portfolio = lazy(() => import('./pages/Portfolio'));

function makeTheme(primary: string, secondary: string) {
  return createTheme({
    palette: {
      primary: { main: primary },
      secondary: { main: secondary },
      background: { default: '#F4F6FB' },
      text: { primary: '#1B2437' },
    },
    shape: { borderRadius: 10 },
    typography: { fontFamily: '"Segoe UI", "Inter", Roboto, Arial, sans-serif', button: { textTransform: 'none', fontWeight: 600 } },
    components: {
      MuiCard: { defaultProps: { variant: 'outlined' }, styleOverrides: { root: { borderColor: '#E3E8F2' } } },
      MuiTableCell: { styleOverrides: { root: { borderColor: '#EDF0F6' }, head: { fontSize: 12.5, color: '#4B5563' } } },
      MuiButton: { defaultProps: { disableElevation: true } },
    },
  });
}

function Themed() {
  const { branding } = useApp();
  const theme = useMemo(() => makeTheme(branding.primaryColor || '#0B2545', branding.secondaryColor || '#C9A227'), [branding.primaryColor, branding.secondaryColor]);
  return <ThemeProvider theme={theme}><CssBaseline /><Shell /></ThemeProvider>;
}

function Shell() {
  const { user, meta, toastState, clearToast, isProvider } = useApp();
  const toast = toastState && (
    <Snackbar key={toastState.key} open autoHideDuration={5000} onClose={clearToast} anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}>
      <Alert severity={toastState.severity} onClose={clearToast} variant="filled" sx={{ maxWidth: 520 }}>{toastState.msg}</Alert>
    </Snackbar>
  );
  if (!user || user.mustChangePassword) return <>{<Login />}{toast}</>;
  if (!meta) return <Box sx={{ display: 'grid', placeItems: 'center', height: '100vh' }}><CircularProgress /></Box>;
  return (
    <Layout>
      <Suspense fallback={<Box sx={{ p: 6, textAlign: 'center' }}><CircularProgress /></Box>}>
        <Routes>
          <Route path="/" element={isProvider ? <Portfolio /> : <Dashboard />} />
          <Route path="/dashboard" element={<Dashboard />} />
          <Route path="/organizations" element={<Organizations />} />
          <Route path="/assessments" element={<Assessments />} />
          <Route path="/assessments/:id" element={<AssessmentDetail />} />
          <Route path="/inventory" element={<Inventory />} />
          <Route path="/dpia" element={<Dpia />} />
          <Route path="/dpia/:id" element={<Dpia />} />
          <Route path="/consent" element={<Consent />} />
          <Route path="/policies" element={<Policies />} />
          <Route path="/policies/:id" element={<Policies />} />
          <Route path="/vendors" element={<Vendors />} />
          <Route path="/vendors/:id" element={<Vendors />} />
          <Route path="/security" element={<Security />} />
          <Route path="/rights" element={<Rights />} />
          <Route path="/rights/:id" element={<Rights />} />
          <Route path="/incidents" element={<Incidents />} />
          <Route path="/incidents/:id" element={<Incidents />} />
          <Route path="/roadmap" element={<Roadmap />} />
          <Route path="/reports" element={<Reports />} />
          <Route path="/reports/:id" element={<ReportEditor />} />
          <Route path="/templates" element={<Templates />} />
          <Route path="/evidence" element={<Evidence />} />
          <Route path="/requirements" element={<Requirements />} />
          <Route path="/users" element={<Users />} />
          <Route path="/audit" element={<Audit />} />
          <Route path="/settings" element={<Settings />} />
          <Route path="*" element={<Navigate to="/" />} />
        </Routes>
      </Suspense>
      {toast}
    </Layout>
  );
}

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <BrowserRouter>
      <AppProvider><Themed /></AppProvider>
    </BrowserRouter>
  </React.StrictMode>
);
