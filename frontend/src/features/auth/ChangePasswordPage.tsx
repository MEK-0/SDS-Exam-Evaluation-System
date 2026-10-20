/* İlk girişte veya kullanıcı isteğinde parola değiştirme ekranını sunar. */
import { useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button } from '../../components/ui/Button';
import { Card } from '../../components/ui/Card';
import { Input } from '../../components/ui/Input';
import { changePassword } from '../../services/authApi';

export function ChangePasswordPage() {
  const navigate = useNavigate();
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const submit = async (event: FormEvent<HTMLFormElement>) => { event.preventDefault(); setError(null); setLoading(true); try { await changePassword(currentPassword, newPassword); navigate('/dashboard'); } catch (reason: unknown) { setError(reason instanceof Error ? reason.message : 'Şifre değiştirilemedi.'); } finally { setLoading(false); } };
  return <main className="grid min-h-screen place-items-center bg-surface p-4"><Card className="w-full max-w-[430px] p-8 sm:p-10"><h1 className="text-xl font-semibold text-ink">Şifrenizi değiştirin</h1><p className="mt-2 text-sm leading-6 text-slate-500">Güvenliğiniz için devam etmeden önce yeni bir şifre belirleyiniz.</p><form className="mt-6 grid gap-4" onSubmit={submit}><Input label="Mevcut şifre" type="password" value={currentPassword} onChange={(event) => setCurrentPassword(event.target.value)} required /><Input label="Yeni şifre" type="password" minLength={8} value={newPassword} onChange={(event) => setNewPassword(event.target.value)} required />{error && <p className="rounded-lg border border-red-100 bg-red-50 p-3 text-sm text-red-700">{error}</p>}<Button type="submit" disabled={loading}>{loading ? 'Güncelleniyor...' : 'Şifreyi güncelle'}</Button></form></Card></main>;
}
