/* Beyaz içerik kartı yüzeyini sunar. */
import type { PropsWithChildren } from 'react';
export function Card({ children, className = '' }: PropsWithChildren<{ className?: string }>) { return <section className={`rounded-xl border border-slate-200 bg-white shadow-card ${className}`}>{children}</section>; }
