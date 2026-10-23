// Şifre sıfırlama talebi oluşturma ekranını sunar.
import { useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { Button } from '../../components/ui/Button';
import { Card } from '../../components/ui/Card';
import { Input } from '../../components/ui/Input';
import { requestPasswordReset } from '../../services/authApi';

export function ForgotPasswordPage() {
  const [username, setUsername] = useState('');
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const submit = async (event: FormEvent<HTMLFormElement>) => { event.preventDefault(); setLoading(true); setError(null); try { await requestPasswordReset(username); setMessage('Talebiniz alındı. Hesabınız varsa yönetici incelemesinden sonra işlem yapılacaktır.'); } catch (reason: unknown) { setError(reason instanceof Error ? reason.message : 'Talep gönderilemedi.'); } finally { setLoading(false); } };
  return <main className="grid min-h-screen place-items-center bg-surface p-4"><Card className="w-full max-w-[430px] p-8"><h1 className="text-2xl font-semibold text-ink">Şifremi Unuttum</h1><p className="mt-2 text-sm text-slate-500">Kurumsal e-posta adresinizi girerek talep oluşturun.</p><form className="mt-6 grid gap-4" onSubmit={submit}><Input label="E-posta" type="email" value={username} onChange={event => setUsername(event.target.value)} required />{message && <p className="rounded-lg border border-emerald-100 bg-emerald-50 p-3 text-sm text-emerald-700">{message}</p>}{error && <p className="rounded-lg border border-red-100 bg-red-50 p-3 text-sm text-red-700">{error}</p>}<Button type="submit" disabled={loading}>{loading ? 'Gönderiliyor...' : 'Talep Gönder'}</Button><Link to="/login" className="text-center text-sm font-semibold text-brand-700 hover:underline">Giriş sayfasına dön</Link></form></Card></main>;
}
