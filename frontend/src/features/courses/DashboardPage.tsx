/* API’den gelen dersleri ve akademik özetleri dashboard üzerinde sunar. */
import { BookOpen, CalendarDays, ChevronRight, Plus } from 'lucide-react';
import { Link } from 'react-router-dom';
import { Badge } from '../../components/ui/Badge';
import { Button } from '../../components/ui/Button';
import { Card } from '../../components/ui/Card';
import { EmptyState, LoadingState } from '../../components/ui/State';
import { useCourses } from '../../hooks/useCourses';

export function DashboardPage() {
  const { data: courses, isLoading, isError, error } = useCourses();
  if (isLoading) return <LoadingState label="Dersler yükleniyor..." />;
  if (isError) return <Card className="p-6"><EmptyState title="Dersler yüklenemedi" description={error instanceof Error ? error.message : 'API bağlantısı kurulamadı.'} /></Card>;
  const courseList = courses ?? [];
  const examCount = courseList.reduce((total, course) => total + course.examCount, 0);
  return <div className="grid gap-7"><div className="flex flex-col justify-between gap-4 sm:flex-row sm:items-end"><div><p className="mb-1 text-sm font-semibold text-brand-700">Hoş geldiniz</p><h1 className="text-[28px] font-semibold tracking-tight text-ink">Derslerim</h1><p className="mt-1 text-sm text-slate-500">Sınav değerlendirme süreçlerinizi buradan yönetebilirsiniz.</p></div><Button disabled><Plus size={17} /> Yeni sınav oluştur</Button></div><div className="grid gap-4 sm:grid-cols-3"><Card className="p-5"><div className="mb-4 flex items-center justify-between"><span className="rounded-xl bg-brand-50 p-2.5 text-brand-700"><BookOpen size={20} /></span><Badge tone="success">Aktif</Badge></div><p className="text-2xl font-bold text-ink">{courseList.length}</p><p className="text-sm text-slate-500">Aktif ders</p></Card><Card className="p-5"><div className="mb-4 rounded-xl bg-blue-50 p-2.5 text-blue-600 w-fit"><CalendarDays size={20} /></div><p className="text-2xl font-bold text-ink">{examCount}</p><p className="text-sm text-slate-500">Sınav taslağı</p></Card><Card className="p-5"><div className="mb-4 rounded-xl bg-emerald-50 p-2.5 text-emerald-600 w-fit"><BookOpen size={20} /></div><p className="text-2xl font-bold text-ink">{courseList.reduce((total, course) => total + course.instructorCount, 0)}</p><p className="text-sm text-slate-500">Ders eğitmeni</p></Card></div><Card><div className="border-b border-slate-200 px-5 py-4"><h2 className="font-bold text-ink">Ders listesi</h2></div>{courseList.length === 0 ? <EmptyState title="Henüz ders bulunmuyor" description="Yetkiniz dahilindeki dersler burada görünecek." /> : <div className="divide-y divide-slate-100">{courseList.map((course) => <Link to={`/course/${course.id}`} key={course.id} className="flex items-center justify-between gap-4 px-5 py-4 transition hover:bg-slate-50"><div className="flex min-w-0 items-center gap-4"><div className="grid h-11 w-11 shrink-0 place-items-center rounded-md bg-brand-50 text-sm font-bold text-brand-700">{course.code.slice(-3)}</div><div className="min-w-0"><p className="truncate font-semibold text-ink">{course.name}</p><p className="mt-1 text-xs text-slate-500">{course.code} · {course.academicYear}</p></div></div><ChevronRight size={18} className="shrink-0 text-slate-400" /></Link>)}</div>}</Card></div>;
}
