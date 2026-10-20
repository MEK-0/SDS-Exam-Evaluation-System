/* Seçim ve bilgilendirme akışları için modal yüzeyini sunar. */
import type { PropsWithChildren } from 'react';
import { X } from 'lucide-react';
import { Button } from './Button';
export function Modal({ open, title, onClose, children }: PropsWithChildren<{ open: boolean; title: string; onClose: () => void }>) { if (!open) return null; return <div className="fixed inset-0 z-50 grid place-items-center bg-slate-950/45 p-4" role="dialog" aria-modal="true" aria-label={title}><div className="w-full max-w-xl rounded-xl border border-slate-200 bg-white shadow-xl"><div className="flex items-start justify-between px-7 pb-2 pt-6"><h2 className="pr-4 text-lg font-bold text-ink">{title}</h2><Button variant="ghost" className="-mr-2 -mt-2 min-h-8 px-2" onClick={onClose} aria-label="Pencereyi kapat"><X size={18} /></Button></div><div className="px-7 pb-7">{children}</div></div></div>; }
