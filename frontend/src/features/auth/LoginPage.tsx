/* Gerçek backend oturum açma ekranını sunar. */
import { useState, type FormEvent } from 'react';
import { Eye, EyeOff } from 'lucide-react';
import { Link, useNavigate } from 'react-router-dom';
import { Button } from '../../components/ui/Button';
import { Card } from '../../components/ui/Card';
import { Input } from '../../components/ui/Input';
import { useAuth } from '../../hooks/useAuth';
import universityLogo from '../../assets/selcuk-university-logo.svg';

export function LoginPage() {
  const navigate = useNavigate();
  const { signIn, loading, error } = useAuth();
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const submit = async (event: FormEvent<HTMLFormElement>) => { event.preventDefault(); const response = await signIn(username, password); if (response) navigate(response.mustChangePassword ? '/change-password' : '/dashboard'); };
  return <main className="grid min-h-screen bg-surface lg:grid-cols-2"><section className="hidden bg-gradient-to-br from-brand-500 via-yellow-400 to-slate-900 p-10 text-center lg:flex lg:flex-col lg:items-center lg:justify-center"><img src={universityLogo} alt="Selçuk Üniversitesi logosu" className="mb-7 h-28 w-28 rounded-full bg-white p-3" /><h1 className="text-3xl font-bold text-slate-950">Sınav Değerlendirme Sistemi</h1><p className="mt-3 text-sm font-medium text-slate-900/80">Selçuk Üniversitesi akademik personel girişi</p></section><section className="flex items-center justify-center p-4"><Card className="w-full max-w-[430px] p-8 sm:p-10"><div className="mb-8"><h1 className="text-2xl font-semibold text-ink">Giriş Yap</h1><p className="mt-2 text-sm text-slate-500">Sisteme erişim için bilgilerinizi girin</p></div><form className="grid gap-4" onSubmit={submit}><Input label="E-posta" type="email" value={username} onChange={(event) => setUsername(event.target.value)} placeholder="E-posta adresinizi giriniz" autoComplete="username" /><label className="grid gap-1.5 text-sm font-medium text-slate-700">Şifre<div className="relative"><input className="h-11 w-full rounded-md border border-slate-300 bg-white px-3 pr-11 outline-none transition focus:border-brand-500 focus:ring-2 focus:ring-brand-100" type={showPassword ? 'text' : 'password'} value={password} onChange={(event) => setPassword(event.target.value)} placeholder="Şifrenizi giriniz" autoComplete="current-password" required /><button type="button" className="absolute right-2 top-2 rounded p-1 text-slate-500" onClick={() => setShowPassword(value => !value)} aria-label="Şifreyi göster veya gizle">{showPassword ? <EyeOff size={18} /> : <Eye size={18} />}</button></div></label>{error && <p className="rounded-lg border border-red-100 bg-red-50 p-3 text-sm text-red-700">{error}</p>}<Button type="submit" className="mt-2 w-full" disabled={loading}>{loading ? 'Giriş yapılıyor...' : 'Giriş Yap'}</Button><Link to="/forgot-password" className="text-center text-sm font-semibold text-brand-700 hover:underline">Şifremi Unuttum</Link></form><p className="mt-6 text-center text-xs text-slate-400">© Selçuk Üniversitesi · SDS</p></Card></section></main>;
}
