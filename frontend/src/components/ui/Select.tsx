/* Form seçim alanını ve etiketini sunar. */
import type { SelectHTMLAttributes } from 'react';
export function Select({ label, children, ...props }: SelectHTMLAttributes<HTMLSelectElement> & { label: string }) { return <label className="grid gap-1.5 text-sm font-medium text-slate-700">{label}<select className="h-11 rounded-md border border-slate-300 bg-white px-3 outline-none focus:border-brand-500 focus:ring-2 focus:ring-brand-100" {...props}>{children}</select></label>; }
