/* Uygulama genelinde kullanılan aksiyon butonunu sunar. */
import type { ButtonHTMLAttributes, PropsWithChildren } from 'react';

type ButtonVariant = 'primary' | 'secondary' | 'ghost';
interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> { variant?: ButtonVariant; }

export function Button({ children, variant = 'primary', className = '', ...props }: PropsWithChildren<ButtonProps>) {
  const variants: Record<ButtonVariant, string> = {
    primary: 'bg-brand-500 text-slate-900 shadow-sm hover:bg-brand-600',
    secondary: 'border border-slate-200 bg-white text-slate-700 hover:border-brand-500 hover:text-brand-700',
    ghost: 'text-slate-600 hover:bg-slate-100 hover:text-slate-900',
  };
  return <button className={`inline-flex min-h-10 items-center justify-center gap-2 rounded-lg px-4 py-2 text-sm font-semibold transition-colors disabled:cursor-not-allowed disabled:opacity-50 ${variants[variant]} ${className}`} {...props}>{children}</button>;
}
