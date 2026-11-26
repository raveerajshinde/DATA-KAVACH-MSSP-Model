export const APP_NAME = 'Data Kavach';

let token: string | null = sessionStorage.getItem('dk_token');
let onUnauthorized: (() => void) | null = null;

export function setToken(t: string | null) {
  token = t;
  if (t) sessionStorage.setItem('dk_token', t);
  else sessionStorage.removeItem('dk_token');
}
export const getToken = () => token;
export function setUnauthorizedHandler(fn: () => void) { onUnauthorized = fn; }

export class ApiError extends Error {
  constructor(public status: number, message: string) { super(message); }
}

async function handle(res: Response) {
  if (res.status === 401 && onUnauthorized && token) onUnauthorized();
  if (!res.ok) {
    let msg = res.statusText;
    try { const j = await res.json(); msg = j.error || j.title || msg; } catch { /* not json */ }
    throw new ApiError(res.status, msg);
  }
  const ct = res.headers.get('content-type') || '';
  if (ct.includes('application/json')) return res.json();
  return res.text();
}

function headers(json = true): HeadersInit {
  const h: Record<string, string> = {};
  if (json) h['Content-Type'] = 'application/json';
  if (token) h['Authorization'] = `Bearer ${token}`;
  return h;
}

export const api = {
  get: <T = any>(url: string): Promise<T> => fetch(url, { headers: headers(false) }).then(handle),
  post: <T = any>(url: string, body?: unknown): Promise<T> => fetch(url, { method: 'POST', headers: headers(), body: JSON.stringify(body ?? {}) }).then(handle),
  put: <T = any>(url: string, body?: unknown): Promise<T> => fetch(url, { method: 'PUT', headers: headers(), body: JSON.stringify(body ?? {}) }).then(handle),
  del: <T = any>(url: string): Promise<T> => fetch(url, { method: 'DELETE', headers: headers(false) }).then(handle),
  upload: <T = any>(url: string, file: File, fields: Record<string, string | number | undefined | null> = {}): Promise<T> => {
    const fd = new FormData();
    Object.entries(fields).forEach(([k, v]) => { if (v !== undefined && v !== null) fd.append(k, String(v)); });
    fd.append('file', file);
    return fetch(url, { method: 'POST', headers: headers(false), body: fd }).then(handle);
  },
  /** Fetches a file with the auth header and returns a blob + filename. */
  blob: async (url: string, init?: RequestInit): Promise<{ blob: Blob; name: string }> => {
    const res = await fetch(url, { ...init, headers: { ...headers(!!init?.body), ...(init?.headers || {}) } });
    if (!res.ok) { await handle(res); }
    const cd = res.headers.get('content-disposition') || '';
    const m = /filename\*=UTF-8''([^;]+)|filename="?([^";]+)"?/i.exec(cd);
    const name = m ? decodeURIComponent(m[1] || m[2]) : 'download';
    return { blob: await res.blob(), name };
  },
  download: async (url: string, init?: RequestInit) => {
    const { blob, name } = await api.blob(url, init);
    const a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = name;
    document.body.appendChild(a);
    a.click();
    a.remove();
    setTimeout(() => URL.revokeObjectURL(a.href), 5000);
  },
};

export const qs = (o: Record<string, unknown>) =>
  Object.entries(o).filter(([, v]) => v !== undefined && v !== null && v !== '').map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(String(v))}`).join('&');

// ---------------------------------------------------------------- formatting helpers
export const fmtDate = (v?: string | null) => (v ? new Date(v.length === 10 ? v + 'T00:00:00' : v).toLocaleDateString('en-IN', { day: '2-digit', month: 'short', year: 'numeric' }) : '');
export const fmtDateTime = (v?: string | null) => (v ? new Date(v).toLocaleString('en-IN', { day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' }) : '');
/** UTC ISO -> value for <input type="datetime-local"> in local time. */
export const toLocalInput = (v?: string | null) => {
  if (!v) return '';
  const d = new Date(v);
  const p = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}T${p(d.getHours())}:${p(d.getMinutes())}`;
};
export const fromLocalInput = (v: string) => (v ? new Date(v).toISOString() : null);

export function timeLeft(due?: string | null) {
  if (!due) return { text: '', overdue: false, hours: 0 };
  const ms = new Date(due).getTime() - Date.now();
  const h = Math.abs(ms) / 36e5;
  const text = h >= 48 ? `${Math.floor(h / 24)}d ${Math.floor(h % 24)}h` : `${Math.floor(h)}h ${Math.floor((h * 60) % 60)}m`;
  return { text: ms < 0 ? `${text} overdue` : `${text} left`, overdue: ms < 0, hours: ms / 36e5 };
}
