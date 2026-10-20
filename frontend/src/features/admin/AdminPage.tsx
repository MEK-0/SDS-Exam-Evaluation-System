/* Admin kullanıcıları için temel ders oluşturma ve yönetim alanını sunar. */
import { useState, type FormEvent } from 'react';
import { Settings, Users } from 'lucide-react';
import { Button } from '../../components/ui/Button';
import { Card } from '../../components/ui/Card';
import { Input } from '../../components/ui/Input';
import { Select } from '../../components/ui/Select';
import { EmptyState, LoadingState } from '../../components/ui/State';
import { useAcademicYears, useCreateCourse } from '../../hooks/useCourses';

export function AdminPage() {
  const { data: years, isLoading } = useAcademicYears();
  const createCourseMutation = useCreateCourse();
  const [code, setCode] = useState('');
  const [name, setName] = useState('');
  const [academicYearId, setAcademicYearId] = useState('');
  const submit = async (event: FormEvent<HTMLFormElement>) => { event.preventDefault(); if (!academicYearId) return; await createCourseMutation.mutateAsync({ code, name, academicYearId }); setCode(''); setName(''); };
  if (isLoading) return <LoadingState label="Akademik yıllar yükleniyor..." />;
  return <div className="grid gap-6"><div><h1 className="text-2xl font-semibold text-ink">Yönetim</h1><p className="mt-1 text-sm text-slate-500">Ders ve sistem yönetimi</p></div><div className="grid gap-4 md:grid-cols-2"><Card className="p-5"><Users className="text-brand-600" /><h2 className="mt-4 font-bold text-ink">Kullanıcılar</h2><p className="mt-1 text-sm text-slate-500">Kullanıcı ve rol yönetimi için backend yetkilendirmesi aktiftir.</p></Card><Card className="p-5"><Settings className="text-slate-600" /><h2 className="mt-4 font-bold text-ink">Akademik yıllar</h2><p className="mt-1 text-sm text-slate-500">{years?.length ?? 0} akademik yıl tanımlı.</p></Card></div><Card className="p-6"><h2 className="text-lg font-bold text-ink">Yeni ders oluştur</h2><form className="mt-5 grid gap-4 md:grid-cols-3" onSubmit={submit}><Input label="Ders kodu" value={code} onChange={(event) => setCode(event.target.value)} placeholder="Örn. BM201" required /><Input label="Ders adı" value={name} onChange={(event) => setName(event.target.value)} placeholder="Ders adını giriniz" required /><Select label="Akademik yıl" value={academicYearId} onChange={(event) => setAcademicYearId(event.target.value)} required><option value="">Seçiniz</option>{years?.map((year) => <option value={year.id} key={year.id}>{year.name}</option>)}</Select><Button type="submit" className="md:col-span-3 md:justify-self-end" disabled={createCourseMutation.isPending}>{createCourseMutation.isPending ? 'Kaydediliyor...' : 'Dersi oluştur'}</Button></form>{createCourseMutation.isError && <p className="mt-4 rounded-lg border border-red-100 bg-red-50 p-3 text-sm text-red-700">{createCourseMutation.error instanceof Error ? createCourseMutation.error.message : 'Ders oluşturulamadı.'}</p>}{createCourseMutation.isSuccess && <p className="mt-4 rounded-lg border border-emerald-100 bg-emerald-50 p-3 text-sm text-emerald-700">Ders başarıyla oluşturuldu.</p>}</Card><Card><EmptyState title="Kapsam dışı yönetim alanları" description="Kullanıcı düzenleme ve tam rol yönetimi sonraki geliştirmelerde genişletilecektir." /></Card></div>;
}
