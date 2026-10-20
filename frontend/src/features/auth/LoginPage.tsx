/* Gerçek backend oturum açma ekranını sunar. */
import { useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
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
  const submit = async (event: FormEvent<HTMLFormElement>) => { event.preventDefault(); const response = await signIn(username, password); if (response) navigate(response.mustChangePassword ? '/change-password' : '/dashboard'); };
  return <main className="grid min-h-screen place-items-center bg-surface p-4"><Card className="w-full max-w-[430px] p-8 sm:p-10"><div className="mb-8 text-center"><div className="mx-auto mb-5 grid h-16 w-16 place-items-center rounded-full border border-slate-300 bg-white p-1 shadow-sm"><img src={universityLogo} alt="Selçuk Üniversitesi logosu" className="h-full w-full object-contain" /></div><h1 className="text-xl font-semibold text-ink">Sınav Değerlendirme Sistemi</h1><p className="mt-2 text-sm text-slate-500">Selçuk Üniversitesi akademik personel girişi</p></div><form className="grid gap-4" onSubmit={submit}><Input label="Kullanıcı adı" value={username} onChange={(event) => setUsername(event.target.value)} placeholder="Kullanıcı adınızı giriniz" autoComplete="username" /><Input label="Şifre" type="password" value={password} onChange={(event) => setPassword(event.target.value)} placeholder="Şifrenizi giriniz" autoComplete="current-password" />{error && <p className="rounded-lg border border-red-100 bg-red-50 p-3 text-sm text-red-700">{error}</p>}<Button type="submit" className="mt-2 w-full" disabled={loading}>{loading ? 'Giriş yapılıyor...' : 'Giriş Yap'}</Button></form><p className="mt-6 text-center text-xs text-slate-400">SDS akademik personel portalı</p></Card></main>;
}
