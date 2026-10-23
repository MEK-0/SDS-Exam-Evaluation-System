/* Not Düzenleme adımında puanları, düzeltmeleri ve proje notlarını yönetir. */
import { Download, FileSpreadsheet, Search } from 'lucide-react';
import { useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { Button } from '../../components/ui/Button';
import { Card } from '../../components/ui/Card';
import { Modal } from '../../components/ui/Modal';
import { LoadingState } from '../../components/ui/State';
import { StepIndicator } from '../../components/ui/StepIndicator';
import { useAddBonus, useCalculateGrades, useGrades, useImportProjectGrades, useSaveOpenEndedGrade, useUpdateGrade } from '../../hooks/useExams';
import type { GradeRow } from '../../types/api';
import { wizardSteps as defaultWizardSteps } from './wizardSteps';

const openEndedSteps = ['Sınıf Listesi', 'Soru Sayısı', 'Not Yükleme', 'Not Düzenleme', 'Öğrenme Çıktıları'];

export function GradeEditingPage() {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const { courseId = '', examType = '' } = useParams();
  const examId = searchParams.get('examId') ?? '';
  const openEnded = searchParams.get('mode') === 'open-ended';
  const wizardSteps = openEnded ? openEndedSteps : defaultWizardSteps;
  const gradesQuery = useGrades(examId);
  const calculateMutation = useCalculateGrades(examId);
  const updateMutation = useUpdateGrade(examId);
  const openEndedGradeMutation = useSaveOpenEndedGrade(examId);
  const bonusMutation = useAddBonus(examId);
  const projectMutation = useImportProjectGrades(examId);
  const calculationStarted = useRef(false);
  const [query, setQuery] = useState('');
  const [selectedIds, setSelectedIds] = useState<string[]>([]);
  const [draftScores, setDraftScores] = useState<Record<string, string>>({});
  const [bonusOpen, setBonusOpen] = useState(false);
  const [bonusAmount, setBonusAmount] = useState('');
  const [projectOpen, setProjectOpen] = useState(false);
  const [projectFile, setProjectFile] = useState<File | null>(null);

  const grades = gradesQuery.data;
  useEffect(() => {
    if (openEnded || !grades || grades.rows.length === 0 || !grades.rows.some(row => row.status === 'NotEntered') || calculationStarted.current || calculateMutation.isPending) return;
    calculationStarted.current = true;
    calculateMutation.mutate();
  }, [calculateMutation, grades]);
  useEffect(() => {
    if (!grades) return;
    setDraftScores(Object.fromEntries(grades.rows.filter(row => row.adjustedScore !== null).map(row => [row.importedStudentId, String(row.adjustedScore)])));
  }, [grades]);

  const filteredRows = useMemo(() => grades?.rows.filter(row => `${row.studentNumber} ${row.studentName}`.toLocaleLowerCase('tr-TR').includes(query.toLocaleLowerCase('tr-TR'))) ?? [], [grades, query]);
  const allSelectableSelected = filteredRows.filter(row => row.status === 'Graded').length > 0 && filteredRows.filter(row => row.status === 'Graded').every(row => selectedIds.includes(row.importedStudentId));
  const error = gradesQuery.error ?? calculateMutation.error ?? updateMutation.error ?? openEndedGradeMutation.error ?? bonusMutation.error ?? projectMutation.error;

  if (gradesQuery.isLoading || calculateMutation.isPending) return <><StepIndicator steps={wizardSteps} currentStep={4} /><LoadingState label={calculateMutation.isPending ? 'Notlar hesaplanıyor...' : 'Notlar yükleniyor...'} /></>;
  if (gradesQuery.isError || !grades) return <Card className="p-6"><p className="text-sm text-red-700">Notlar alınamadı: {error instanceof Error ? error.message : 'Sınav verileri bulunamadı.'}</p></Card>;

  const updateScore = (row: GradeRow) => {
    const value = Number(String(draftScores[row.importedStudentId] ?? '').replace(',', '.'));
    if (!Number.isFinite(value) || value < 0) return;
    if (openEnded) { if (row.rawScore !== value) openEndedGradeMutation.mutate({ studentId: row.importedStudentId, rawScore: value, isAbsent: false }); return; }
    if (value > 100) return;
    if (row.adjustedScore !== value) updateMutation.mutate({ studentId: row.importedStudentId, adjustedScore: value });
  };
  const toggleStudent = (id: string) => setSelectedIds(current => current.includes(id) ? current.filter(item => item !== id) : [...current, id]);
  const toggleAll = () => { const selectable = filteredRows.filter(row => row.status === 'Graded').map(row => row.importedStudentId); setSelectedIds(allSelectableSelected ? [] : selectable); };
  const addBonus = () => {
    const amount = Number(bonusAmount.replace(',', '.'));
    if (!Number.isFinite(amount) || amount <= 0 || selectedIds.length === 0) return;
    bonusMutation.mutate({ importedStudentIds: selectedIds, amount, idempotencyKey: crypto.randomUUID() }, { onSuccess: () => { setBonusOpen(false); setBonusAmount(''); setSelectedIds([]); } });
  };
  const saveAndContinue = () => navigate(`/course/${courseId}/exam/${examType}/learning-outcomes?examId=${examId}`);
  const exportCsv = () => { const header = 'Öğrenci No,Ad Soyad,Ham Puan,Eklemeler,Düzeltilmiş Puan'; const lines = grades.rows.map(row => [row.studentNumber, row.studentName, row.rawScore ?? '-1', row.adjustmentAmount || '-', row.adjustedScore ?? '-1'].map(value => `"${String(value).replace(/"/g, '""')}"`).join(',')); const blob = new Blob([`\ufeff${[header, ...lines].join('\n')}`], { type: 'text/csv;charset=utf-8' }); const url = URL.createObjectURL(blob); const anchor = document.createElement('a'); anchor.href = url; anchor.download = 'notlar.csv'; anchor.click(); URL.revokeObjectURL(url); };

  return <div className="grid gap-7"><StepIndicator steps={wizardSteps} currentStep={4} /><Card className="p-5 sm:p-8"><div className="flex flex-wrap items-center justify-between gap-4"><h1 className="text-xl font-semibold text-ink">Not Düzenleme</h1><div className="flex flex-wrap gap-3"><Button variant="secondary" onClick={() => setProjectOpen(true)}><FileSpreadsheet size={16} /> Proje Notları Yükle</Button><Button variant="secondary" onClick={() => setBonusOpen(true)} disabled={selectedIds.length === 0}>Bonus Ekle</Button></div></div><div className="mt-6 flex items-center gap-2 rounded-lg border border-slate-200 bg-slate-50 px-3"><Search size={17} className="text-slate-400" /><input value={query} onChange={event => setQuery(event.target.value)} placeholder="Öğrenci ara (numara veya isim)..." className="min-h-11 w-full bg-transparent text-sm outline-none placeholder:text-slate-400" /></div><div className="mt-3 overflow-x-auto rounded-xl border border-slate-200"><div className="max-h-[510px] min-w-[920px] overflow-y-auto"><table className="w-full text-left text-sm"><thead className="sticky top-0 z-10 bg-amber-50 text-xs text-slate-700"><tr><th className="w-14 px-4 py-3"><input type="checkbox" checked={allSelectableSelected} onChange={toggleAll} aria-label="Notlandırılmış öğrencilerin tümünü seç" /></th><th className="px-4 py-3">Öğrenci No ↕</th><th className="px-4 py-3">Ad Soyad ↕</th><th className="px-4 py-3 text-center">Ham Puan ↕</th><th className="px-4 py-3 text-center">Eklemeler</th><th className="px-4 py-3 text-center">Düzeltilmiş Puan ↕</th><th className="px-4 py-3 text-center">Harf Notu</th></tr></thead><tbody>{filteredRows.map(row => <GradeRowView key={row.importedStudentId} row={row} selected={selectedIds.includes(row.importedStudentId)} draftValue={draftScores[row.importedStudentId] ?? ''} onToggle={() => toggleStudent(row.importedStudentId)} onDraftChange={value => setDraftScores(current => ({ ...current, [row.importedStudentId]: value }))} onSave={() => updateScore(row)} />)}</tbody></table></div></div>{error && <p className="mt-4 rounded-lg border border-red-100 bg-red-50 p-3 text-sm text-red-700">{error instanceof Error ? error.message : 'Not işlemi sırasında hata oluştu.'}</p>}<div className="mt-4 text-xs text-slate-500">{projectMutation.data?.issues.length ? projectMutation.data.issues.join(' ') : projectMutation.data ? `${projectMutation.data.importedCount} proje notu işlendi.` : ''}</div></Card><Card className="p-5 sm:p-6"><h2 className="text-lg font-semibold text-ink">Özet İstatistikler</h2><div className="mt-5 grid gap-5 sm:grid-cols-2 lg:grid-cols-6"><Stat label="Toplam / Giren" value={`${grades.summary.totalStudentCount} / ${grades.summary.gradedStudentCount}`} /><Stat label="Ortalama Puan" value={formatScore(grades.summary.averageScore)} /><Stat label="Medyan" value={formatScore(grades.summary.medianScore)} /><Stat label="Std. Sapma" value={formatScore(grades.summary.standardDeviation)} /><Stat label="En Yüksek Puan" value={formatScore(grades.summary.highestScore)} /><Stat label="En Düşük Puan" value={formatScore(grades.summary.lowestScore)} /></div><Button variant="secondary" className="mt-5" onClick={exportCsv}><Download size={16} /> Excel'e Aktar</Button></Card><div className="flex flex-wrap items-center justify-between gap-3"><Button variant="ghost" onClick={() => navigate(`/course/${courseId}/exam/${examType}/matching?examId=${examId}`)}>Geri</Button><div className="flex gap-3"><Button variant="secondary" onClick={() => filteredRows.forEach(updateScore)}>Kaydet</Button><Button onClick={saveAndContinue} disabled={grades.rows.length === 0}>İleri</Button></div></div><Modal open={bonusOpen} title="Bonus Ekle" onClose={() => setBonusOpen(false)}><p className="text-sm text-slate-500">{selectedIds.length} öğrenciye uygulanacak bonus puanını girin.</p><input type="number" min="0.01" max="100" step="0.01" value={bonusAmount} onChange={event => setBonusAmount(event.target.value)} placeholder="Bonus puanı" className="mt-5 min-h-10 w-full rounded-lg border border-slate-200 px-3 text-sm outline-none focus:border-brand-500" /><Button className="mt-5 w-full" onClick={addBonus} disabled={!bonusAmount || bonusMutation.isPending}>Bonusu Uygula</Button></Modal><Modal open={projectOpen} title="Proje / Ek Sınav Notu Yükleme" onClose={() => { setProjectOpen(false); setProjectFile(null); }}><p className="text-sm text-slate-500">Öğrencilerin proje veya ek sınav notlarını Excel dosyası ile toplu olarak güncelleyebilirsiniz.</p><div className="mt-4 rounded-lg bg-amber-50 p-3 text-xs text-amber-800">Beklenen sütunlar: OGRENCINO, ADSOYAD, SINAVNOTU</div><div className="mt-4 overflow-x-auto rounded-lg border border-slate-200"><table className="min-w-full text-xs"><thead className="bg-slate-50"><tr><th className="px-3 py-2 text-left">OGRENCINO</th><th className="px-3 py-2 text-left">ADSOYAD</th><th className="px-3 py-2 text-left">SINAVNOTU</th></tr></thead><tbody><tr><td className="px-3 py-2">233301101</td><td className="px-3 py-2">Örnek Öğrenci</td><td className="px-3 py-2">78</td></tr></tbody></table></div><input type="file" accept=".xlsx" onChange={event => setProjectFile(event.target.files?.[0] ?? null)} className="mt-5 block w-full rounded-lg border border-slate-200 p-3 text-sm" /><div className="mt-5 flex justify-end gap-3"><Button variant="ghost" onClick={() => { setProjectOpen(false); setProjectFile(null); }}>İptal</Button><Button disabled={!projectFile || projectMutation.isPending} onClick={() => { if (!projectFile) return; projectMutation.mutate(projectFile, { onSuccess: () => { setProjectOpen(false); setProjectFile(null); } }); }}>Notları Yükle</Button></div></Modal></div>;
}

function GradeRowView({ row, selected, draftValue, onToggle, onDraftChange, onSave }: { row: GradeRow; selected: boolean; draftValue: string; onToggle: () => void; onDraftChange: (value: string) => void; onSave: () => void }) {
  const absent = row.status === 'Absent';
  return <tr className="border-t border-slate-100"><td className="px-4 py-3"><input type="checkbox" checked={selected} disabled={absent} onChange={onToggle} /></td><td className="px-4 py-3 text-slate-600">{row.studentNumber}</td><td className="px-4 py-3 font-medium text-ink">{row.studentName}</td><td className="px-4 py-3 text-center"><span className="inline-flex min-w-10 justify-center rounded-lg bg-slate-100 px-3 py-2 text-slate-500">{absent ? '-1' : formatScore(row.rawScore)}</span></td><td className="px-4 py-3 text-center text-slate-500">{row.projectScore !== null ? `Proje: ${formatScore(row.projectScore)}` : row.adjustmentAmount !== 0 ? formatScore(row.adjustmentAmount) : '-'}</td><td className="px-4 py-3 text-center"><input disabled={absent} value={absent ? '-1' : draftValue} onChange={event => onDraftChange(event.target.value)} onBlur={onSave} className="w-24 rounded-lg border border-amber-100 bg-amber-50 px-3 py-2 text-center font-semibold text-amber-700 outline-none focus:border-brand-400 disabled:text-slate-400" /></td><td className="px-4 py-3 text-center">{row.letterGrade ? <span className="inline-flex min-w-12 justify-center rounded-full bg-amber-100 px-3 py-1 font-semibold text-amber-800">{row.letterGrade}</span> : '—'}</td></tr>;
}

function Stat({ label, value }: { label: string; value: string }) { return <div><p className="text-sm text-slate-500">{label}</p><p className="mt-2 text-xl font-medium text-ink">{value}</p></div>; }
function formatScore(value: number | null) { return value === null ? '-' : Number.isInteger(value) ? String(value) : value.toFixed(1); }
