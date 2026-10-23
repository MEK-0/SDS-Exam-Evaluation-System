// Uygulamanın header, breadcrumb ve ana içerik düzenini sunar.
import type { PropsWithChildren } from 'react';
import { ChevronDown, ChevronRight, LogOut, Menu } from 'lucide-react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import type { BreadcrumbItem } from '../../types/models';
import { authStore } from '../../services/authStore';
import universityLogo from '../../assets/selcuk-university-logo.svg';

export function AppLayout({ children, breadcrumbs = [] }: PropsWithChildren<{ breadcrumbs?: BreadcrumbItem[] }>) {
  const location = useLocation();
  const navigate = useNavigate();
  const user = authStore.getUser();
  const items = breadcrumbs.length ? breadcrumbs : [{ label: location.pathname === '/dashboard' ? 'Ana Sayfa' : 'SDS' }];
  const resolvedItems = items.map(item => item.label === 'Vize' ? { ...item, label: examTypeLabel(location.pathname) } : item);
  return <div className="min-h-screen"><header className="border-b border-slate-200 bg-white"><div className="mx-auto flex h-[76px] max-w-[1440px] items-center justify-between px-5 sm:px-8"><Link to="/dashboard" className="flex items-center gap-3"><div className="grid h-11 w-11 place-items-center rounded-full border border-slate-300 bg-white p-1 shadow-sm"><img src={universityLogo} alt="Selçuk Üniversitesi logosu" className="h-full w-full object-contain" /></div><div><p className="text-[17px] font-semibold leading-tight text-ink">Sınav Değerlendirme Sistemi</p><p className="mt-0.5 text-sm text-slate-500">Selçuk Üniversitesi</p></div></Link><div className="flex items-center gap-3"><div className="hidden items-center gap-3 sm:flex">{(user?.role === 'Admin' || user?.role === 'AdminInstructor') && <Link to="/admin" className="rounded-lg px-3 py-2 text-sm font-semibold text-slate-600 hover:bg-slate-100">Sistem Yönetimi</Link>}<div className="grid h-9 w-9 place-items-center rounded-full bg-brand-500 text-sm font-bold text-slate-900">{user?.displayName.slice(0, 2).toUpperCase() ?? 'SÜ'}</div><p className="text-sm font-medium text-slate-700">{user?.displayName ?? 'Kullanıcı'}</p><ChevronDown size={16} className="text-slate-700" /><button className="rounded-lg p-2 text-slate-400 hover:bg-slate-100" onClick={() => { authStore.clear(); navigate('/login'); }} aria-label="Oturumu kapat"><LogOut size={16} /></button></div><button className="rounded-lg p-2 text-slate-500 hover:bg-slate-100 sm:hidden" aria-label="Menüyü aç"><Menu size={21} /></button></div></div></header><main className="mx-auto max-w-[1440px] px-5 py-5 sm:px-8 sm:py-6"><nav className="mb-7 flex items-center gap-1 overflow-x-auto whitespace-nowrap text-sm" aria-label="Sayfa yolu"><Link to="/dashboard" className="text-slate-500 hover:text-brand-700">Sınav Değerlendirme Sistemi</Link>{resolvedItems.map(item => <span className="flex items-center gap-1" key={item.label}><ChevronRight size={15} className="text-slate-400" />{item.href ? <Link to={item.href} className="text-slate-500 hover:text-brand-700">{item.label}</Link> : <span className="font-semibold text-ink">{item.label}</span>}</span>)}</nav>{children}</main></div>;
}

function examTypeLabel(pathname: string) { const value = pathname.split('/exam/')[1]?.split('/')[0]; return value === 'Final' ? 'Final' : value === 'MakeUp' ? 'Bütünleme' : 'Vize'; }
