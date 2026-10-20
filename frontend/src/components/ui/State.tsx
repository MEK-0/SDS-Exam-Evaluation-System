/* Yükleniyor ve boş durum ekranlarını sunar. */
import { LoaderCircle, Inbox } from 'lucide-react';
export function LoadingState({ label = 'Yükleniyor...' }: { label?: string }) { return <div className="grid place-items-center gap-3 py-12 text-sm text-slate-500"><LoaderCircle className="animate-spin text-brand-500" /><span>{label}</span></div>; }
export function EmptyState({ title, description }: { title: string; description: string }) { return <div className="grid place-items-center gap-2 py-12 text-center"><Inbox className="text-slate-400" /><h3 className="font-semibold text-slate-700">{title}</h3><p className="text-sm text-slate-500">{description}</p></div>; }
