import { createContext, ReactNode, useCallback, useContext, useEffect, useRef, useState } from 'react';
import { api, setToken, setUnauthorizedHandler, getToken } from './api';

// ---------------------------------------------------------------- types
export interface ColMeta { name: string; type: string; label: string; len: number; required: boolean; options?: string[] | null; hidden: boolean; readOnly: boolean; sensitive: boolean; ref?: string | null; group?: string | null; help?: string | null; multi: boolean; }
export interface EntityMeta { key: string; label: string; module: string; orgScoped: boolean; parentKey?: string | null; parentCol?: string | null; listCols: string[]; titleCol: string; genericWrite: boolean; cols: ColMeta[]; }
export interface Meta { entities: EntityMeta[]; roles: string[]; permissions: string[]; reportTypes: { key: string; title: string }[]; policyTemplates: { key: string; title: string }[]; securityTools: string[]; commencement: { core: string; consentManager: string }; }
export interface User { id: number; email: string; name: string; role: string; mfaEnabled: boolean; mustChangePassword: boolean; userType?: 'Provider' | 'Customer'; organizationId?: number; }
export interface ProviderBranding { name: string; tagline: string; portalName: string; primaryColor: string; secondaryColor: string; supportEmail: string; supportPhone: string; website: string; loginMessage: string; address: string; useOnReports: boolean; hasLogo: boolean; }
const defaultBranding: ProviderBranding = { name: 'Manlitics', tagline: 'Managed DPDP Compliance Services', portalName: 'DPDP Compliance Portal', primaryColor: '#0B2545', secondaryColor: '#C9A227', supportEmail: '', supportPhone: '', website: '', loginMessage: '', address: '', useOnReports: true, hasLogo: false };
export interface Org { id: number; name: string; industry?: string; [k: string]: any; }

interface Session { user: User | null; permissions: string[]; }
interface Ctx {
  user: User | null;
  can: (perm: string) => boolean;
  login: (s: any) => void;
  logout: () => void;
  meta: Meta | null;
  entity: (key: string) => EntityMeta;
  orgs: Org[];
  org: Org | null;
  orgId: number | null;
  selectOrg: (id: number) => void;
  reloadOrgs: () => Promise<void>;
  toast: (msg: string, severity?: 'success' | 'error' | 'info' | 'warning') => void;
  toastState: { msg: string; severity: any; key: number } | null;
  clearToast: () => void;
  isProvider: boolean;
  isCustomer: boolean;
  branding: ProviderBranding;
  logoUrl: string | null;
  reloadBranding: () => Promise<void>;
}

const AppCtx = createContext<Ctx>(null as any);
export const useApp = () => useContext(AppCtx);

const IDLE_MINUTES = 30;

export function AppProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<Session>(() => {
    try { return JSON.parse(sessionStorage.getItem('dk_session') || '') as Session; } catch { return { user: null, permissions: [] }; }
  });
  const [meta, setMeta] = useState<Meta | null>(null);
  const [orgs, setOrgs] = useState<Org[]>([]);
  const [orgId, setOrgId] = useState<number | null>(() => Number(localStorage.getItem('dk_org')) || null);
  const [toastState, setToast] = useState<Ctx['toastState']>(null);
  const [branding, setBranding] = useState<ProviderBranding>(defaultBranding);
  const [logoVer, setLogoVer] = useState(Date.now());
  const reloadBranding = useCallback(async () => {
    try { const b = await api.get('/api/branding'); setBranding({ ...defaultBranding, ...b }); setLogoVer(Date.now()); document.title = `${b.portalName || 'DPDP Compliance Portal'} | ${b.name}`; } catch { /* keep defaults */ }
  }, []);
  useEffect(() => { reloadBranding(); }, [reloadBranding]);
  const isCustomer = session.user?.userType === 'Customer';
  const lastActivity = useRef(Date.now());

  const logout = useCallback(() => {
    if (getToken()) api.post('/api/auth/logout').catch(() => undefined);
    setToken(null);
    sessionStorage.removeItem('dk_session');
    setSession({ user: null, permissions: [] });
    setMeta(null);
  }, []);

  const login = useCallback((s: any) => {
    setToken(s.token);
    const sess = { user: s.user, permissions: s.permissions };
    sessionStorage.setItem('dk_session', JSON.stringify(sess));
    setSession(sess);
  }, []);

  useEffect(() => { setUnauthorizedHandler(() => logout()); }, [logout]);

  const reloadOrgs = useCallback(async () => {
    const r = await api.get('/api/e/organizations?pageSize=500&sort=name');
    setOrgs(r.rows);
    const own = (JSON.parse(sessionStorage.getItem('dk_session') || '{}').user || {}) as User;
    if (own.userType === 'Customer') { setOrgId(own.organizationId ?? r.rows[0]?.id ?? null); return; }
    setOrgId(cur => (cur && r.rows.some((o: Org) => o.id === cur) ? cur : r.rows[0]?.id ?? null));
  }, []);

  useEffect(() => {
    if (!session.user) return;
    api.get('/api/meta').then(setMeta).catch(() => undefined);
    reloadOrgs().catch(() => undefined);
  }, [session.user, reloadOrgs]);

  useEffect(() => { if (orgId) localStorage.setItem('dk_org', String(orgId)); }, [orgId]);

  // session keep-alive + idle expiry
  useEffect(() => {
    if (!session.user) return;
    const mark = () => { lastActivity.current = Date.now(); };
    ['mousemove', 'keydown', 'click', 'scroll'].forEach(e => window.addEventListener(e, mark, { passive: true }));
    const t = setInterval(async () => {
      const idle = (Date.now() - lastActivity.current) / 60000;
      if (idle > IDLE_MINUTES) { logout(); return; }
      try { const s = await api.post('/api/auth/refresh'); login(s); } catch { /* handled by 401 hook */ }
    }, 10 * 60 * 1000);
    return () => { clearInterval(t); ['mousemove', 'keydown', 'click', 'scroll'].forEach(e => window.removeEventListener(e, mark)); };
  }, [session.user, login, logout]);

  const value: Ctx = {
    user: session.user,
    can: (p: string) => session.permissions.includes(p),
    login, logout, meta,
    entity: (key: string) => meta!.entities.find(e => e.key === key)!,
    orgs, org: orgs.find(o => o.id === orgId) ?? null, orgId,
    selectOrg: (id: number) => { if (!isCustomer) setOrgId(id); }, reloadOrgs,
    isProvider: !!session.user && !isCustomer, isCustomer, branding, reloadBranding,
    logoUrl: branding.hasLogo ? `/api/branding/logo?v=${logoVer}` : null,
    toast: (msg, severity = 'success') => setToast({ msg, severity, key: Date.now() }),
    toastState, clearToast: () => setToast(null),
  };
  return <AppCtx.Provider value={value}>{children}</AppCtx.Provider>;
}

/** Small hook for async loading with error toast. */
export function useLoad<T>(fn: () => Promise<T>, deps: any[], initial: T): [T, boolean, () => void, (v: T) => void] {
  const [data, setData] = useState<T>(initial);
  const [loading, setLoading] = useState(true);
  const [tick, setTick] = useState(0);
  const { toast } = useApp();
  useEffect(() => {
    let alive = true;
    setLoading(true);
    fn().then(d => { if (alive) setData(d); }).catch(e => toast(e.message, 'error')).finally(() => alive && setLoading(false));
    return () => { alive = false; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [...deps, tick]);
  return [data, loading, () => setTick(t => t + 1), setData];
}
