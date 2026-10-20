/* Form metin alanını ve etiketini sunar. */
import type { InputHTMLAttributes } from 'react';
export function Input({ label, ...props }: InputHTMLAttributes<HTMLInputElement> & { label: string }) { return <label className="grid gap-1.5 text-sm font-medium text-slate-700">{label}<input className="h-11 rounded-md border border-slate-300 bg-white px-3 outline-none transition focus:border-brand-500 focus:ring-2 focus:ring-brand-100" {...props} /></label>; }
