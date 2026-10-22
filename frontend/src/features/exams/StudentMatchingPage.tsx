/* Öğrenci ve optik kayıtlarını referans Veri Eşleştirme akışında sunar. */
import { Check, CircleAlert, Info, RotateCcw } from 'lucide-react';
import { useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { Button } from '../../components/ui/Button';
import { Card } from '../../components/ui/Card';
import { LoadingState } from '../../components/ui/State';
import { StepIndicator } from '../../components/ui/StepIndicator';
import { useConfirmMatching, useCreateManualMatch, useIgnoreOpticalRecord, useImportPreview, useMatching, useMarkStudentAbsent, useRemoveManualMatch, useRunMatching } from '../../hooks/useExams';
import type { MatchingRecord } from '../../types/api';
import { wizardSteps } from './wizardSteps';

export function StudentMatchingPage() {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const { courseId = '', examType = '' } = useParams();
  const examId = searchParams.get('examId') ?? '';
  const matchingQuery = useMatching(examId);
  const importQuery = useImportPreview(examId);
  const runMatchingMutation = useRunMatching(examId);
  const manualMutation = useCreateManualMatch(examId);
  const absentMutation = useMarkStudentAbsent(examId);
  const ignoreOpticalMutation = useIgnoreOpticalRecord(examId);
  const removeMutation = useRemoveManualMatch(examId);
  const confirmMutation = useConfirmMatching(examId);
  const [selectedStudentIds, setSelectedStudentIds] = useState<string[]>([]);
  const [selectedOpticalIds, setSelectedOpticalIds] = useState<string[]>([]);
  const matchingStartedRef = useRef(false);
  const matching = matchingQuery.data;
  const importPreview = importQuery.data;
  const importReady = Boolean(importPreview?.opticalFileName && importPreview.classListFileNames.length > 0);

  useEffect(() => {
    if (!matching || matching.summary.isCalculated || !importReady || runMatchingMutation.isPending || matchingStartedRef.current) return;
    matchingStartedRef.current = true;
    runMatchingMutation.mutate();
  }, [importReady, matching, runMatchingMutation]);

  const students = useMemo(() => matching?.records.filter(record => record.importedStudentId) ?? [], [matching]);
  const opticalRecords = useMemo(() => matching?.records.filter(record => record.opticalExamRecordId) ?? [], [matching]);
  const pendingMatches = useMemo(() => matching?.records.filter(record => record.matchType === 'Manual' && record.status === 'Matched' && record.matchId && record.importedStudentId && record.opticalExamRecordId) ?? [], [matching]);
  const mutationError = [manualMutation, absentMutation, ignoreOpticalMutation, removeMutation, confirmMutation].find(mutation => mutation.isError)?.error;
  const errorMessage = mutationError instanceof Error ? mutationError.message : runMatchingMutation.error instanceof Error ? runMatchingMutation.error.message : null;
  const hasUnresolved = Boolean(matching && (matching.summary.conflictCount > 0 || matching.summary.unmatchedStudentCount > 0 || matching.summary.unmatchedOpticalRecordCount > 0));
  const canCreateManualMatch = selectedStudentIds.length === 1 && selectedOpticalIds.length === 1;

  if (matchingQuery.isLoading || importQuery.isLoading || runMatchingMutation.isPending) return <LoadingState label={runMatchingMutation.isPending ? 'Öğrenci eşleştirmesi başlatılıyor...' : 'Eşleştirme verileri yükleniyor...'} />;
  if (matchingQuery.isError || !matching) return <Card className="p-6"><p className="text-sm text-red-700">Eşleştirme verileri alınamadı. Sınav bulunamadı veya erişim yetkiniz yok.</p></Card>;
  if (importQuery.isError) return <Card className="p-6"><p className="text-sm text-red-700">Dosya import kaydı okunamadı. Dosya Yükleme adımını kontrol edin.</p></Card>;
  if (!importReady) return <Card className="p-6"><p className="text-sm text-amber-800">Optik dosyası veya sınıf listesi yüklenmemiş. Önce Dosya Yükleme adımını tamamlayın.</p></Card>;
  if ((importPreview?.invalidOpticalRecordCount ?? 0) > 0) return <Card className="p-6"><p className="text-sm text-red-700">Optik dosyası ayrıştırılamadı. Dosya Yükleme adımındaki doğrulama uyarılarını giderin.</p></Card>;
  if (runMatchingMutation.isError) return <Card className="p-6"><p className="text-sm text-red-700">Öğrenci eşleştirmesi sırasında hata oluştu: {errorMessage ?? 'Bilinmeyen hata.'}</p><Button className="mt-4" onClick={() => { matchingStartedRef.current = false; runMatchingMutation.reset(); }}>Tekrar Dene</Button></Card>;

  const createManual = () => {
    const importedStudentId = selectedStudentIds[0];
    const opticalExamRecordId = selectedOpticalIds[0];
    if (!importedStudentId || !opticalExamRecordId) return;
    manualMutation.mutate({ importedStudentId, opticalExamRecordId }, { onSuccess: () => { setSelectedStudentIds([]); setSelectedOpticalIds([]); } });
  };
  const confirmAndContinue = () => {
    if (matching.summary.isConfirmed) {
      navigate(`/course/${courseId}/exam/${examType}/grades?examId=${examId}`);
      return;
    }
    confirmMutation.mutate(undefined, { onSuccess: () => navigate(`/course/${courseId}/exam/${examType}/grades?examId=${examId}`) });
  };
  const resetSelection = () => { setSelectedStudentIds([]); setSelectedOpticalIds([]); };
  const navigateBack = () => navigate(`/course/${courseId}/exam/${examType}/answer-key?examId=${examId}`);

  return <div className="grid gap-7">
    <StepIndicator steps={wizardSteps} currentStep={3} />
    <Card className="p-5 sm:p-8">
      <div className="flex items-center justify-between gap-4"><h1 className="text-xl font-semibold text-ink">Veri Eşleştirme</h1><button type="button" className="inline-flex items-center gap-2 text-sm text-slate-600 hover:text-ink"><Info size={16} /> Renk Kodları</button></div>
      {!matching.summary.isCalculated && <p className="mt-4 rounded-lg border border-brand-100 bg-brand-50 p-4 text-sm text-brand-700">Otomatik eşleştirme başlatılıyor.</p>}
      {matching.summary.isCalculated && <>
        <div className="mt-6 grid gap-5 lg:grid-cols-2">
          <RecordPanel title="Optik Verisi" count={opticalRecords.filter(record => record.status !== 'Matched' && record.status !== 'Absent').length} totalCount={matching.summary.totalOpticalRecordCount} records={opticalRecords} kind="optical" selectedIds={selectedOpticalIds} onToggle={id => setSelectedOpticalIds(current => current.includes(id) ? current.filter(item => item !== id) : [...current, id])} onToggleAll={() => setSelectedOpticalIds(selectedOpticalIds.length === opticalRecords.filter(record => record.status !== 'Absent').length ? [] : opticalRecords.filter(record => record.status !== 'Absent').map(record => record.opticalExamRecordId).filter((id): id is string => Boolean(id)))} onIgnore={id => ignoreOpticalMutation.mutate(id)} onIgnoreSelected={() => { selectedOpticalIds.forEach(id => ignoreOpticalMutation.mutate(id)); setSelectedOpticalIds([]); }} emptyTitle="Optik Kaydı Yok" emptyText="Gösterilecek optik kaydı bulunmuyor." />
          <RecordPanel title="Sınıf Listesi" count={students.filter(record => record.status !== 'Matched' && record.status !== 'Absent').length} totalCount={matching.summary.totalClassListCount} records={students} kind="student" selectedIds={selectedStudentIds} onToggle={id => setSelectedStudentIds(current => current.includes(id) ? current.filter(item => item !== id) : [...current, id])} onToggleAll={() => setSelectedStudentIds(selectedStudentIds.length === students.filter(record => record.status !== 'Absent').length ? [] : students.filter(record => record.status !== 'Absent').map(record => record.importedStudentId).filter((id): id is string => Boolean(id)))} onAbsent={id => absentMutation.mutate(id)} />
        </div>
        <div className="mt-6 flex justify-center"><Button onClick={createManual} disabled={!canCreateManualMatch || manualMutation.isPending}>Seçili Kayıtları Eşleştir</Button></div>
        {pendingMatches.length > 0 && <PendingMatches records={pendingMatches} onRemove={id => removeMutation.mutate(id)} />}
        {hasUnresolved && <p className="mt-5 rounded-xl border border-amber-200 bg-amber-50 p-4 text-sm text-amber-800">Bekleyen eşleştirmeler var. İleri adımına geçmeden önce tüm kayıtları çözün veya öğrencileri sınava girmedi olarak işaretleyin.</p>}
      </>}
      {errorMessage && <p className="mt-5 flex items-center gap-2 rounded-lg border border-red-100 bg-red-50 p-3 text-sm text-red-700"><CircleAlert size={16} />{errorMessage}</p>}
    </Card>
    <div className="flex flex-wrap items-center justify-between gap-3"><Button variant="ghost" onClick={resetSelection}><RotateCcw size={16} /> Sıfırla</Button><div className="flex flex-wrap items-center gap-3"><Button variant="secondary" onClick={navigateBack}>Geri</Button><Button onClick={confirmAndContinue} disabled={!matching.summary.canConfirm || confirmMutation.isPending}>İleri</Button></div></div>
  </div>;
}

function RecordPanel({ title, count, totalCount, records, kind, selectedIds, onToggle, onToggleAll, onAbsent, onIgnore, onIgnoreSelected, emptyTitle, emptyText }: { title: string; count: number; totalCount: number; records: MatchingRecord[]; kind: 'student' | 'optical'; selectedIds: string[]; onToggle: (id: string) => void; onToggleAll: () => void; onAbsent?: (id: string) => void; onIgnore?: (id: string) => void; onIgnoreSelected?: () => void; emptyTitle?: string; emptyText?: string }) {
  const [query, setQuery] = useState('');
  const filtered = records.filter(record => `${record.studentNumber} ${record.studentName} ${record.opticalStudentNumber} ${record.opticalStudentName}`.toLocaleLowerCase('tr-TR').includes(query.toLocaleLowerCase('tr-TR')));
  const selectableRecords = records.filter(record => record.status !== 'Absent');
  const allSelected = selectableRecords.length > 0 && selectedIds.length === selectableRecords.length;
  return <section className="rounded-xl border border-slate-200 p-5"><div className="flex items-center justify-between gap-3"><h2 className="text-lg font-medium text-ink">{title} <span className="text-sm text-slate-500">({count} bekleyen / {totalCount} toplam)</span></h2>{kind === 'optical' && selectedIds.length > 0 && onIgnoreSelected && <button type="button" className="rounded-lg bg-red-500 px-3 py-2 text-xs font-semibold text-white hover:bg-red-600" onClick={onIgnoreSelected}>Seçimleri Göz Ardı Et</button>}</div>{records.length > 0 && <input value={query} onChange={event => setQuery(event.target.value)} placeholder={kind === 'student' ? 'Öğrenci ara...' : 'Ad veya numara ile ara...'} className="mt-5 min-h-10 w-full border-0 border-b border-slate-200 bg-transparent px-2 text-sm outline-none placeholder:text-slate-400 focus:border-brand-500" />}{filtered.length === 0 ? <div className="mt-5 rounded-xl border border-emerald-100 bg-emerald-50 p-8 text-center"><Check className="mx-auto text-emerald-600" size={28} /><h3 className="mt-3 font-semibold text-emerald-700">{emptyTitle ?? 'Kayıt bulunamadı'}</h3><p className="mt-1 text-sm text-emerald-700">{emptyText ?? 'Gösterilecek kayıt yok.'}</p></div> : <div className="mt-5 overflow-hidden rounded-xl border border-slate-200"><table className="w-full text-left text-sm"><thead className="bg-slate-50 text-xs text-slate-700"><tr><th className="w-14 px-3 py-3"><input type="checkbox" checked={allSelected} onChange={onToggleAll} aria-label={`${title} kayıtlarının tümünü seç`} /></th><th className="px-3 py-3">No</th><th className="px-3 py-3">Ad Soyad</th><th className="px-3 py-3">Durum</th></tr></thead><tbody>{filtered.map(record => { const id = kind === 'student' ? record.importedStudentId : record.opticalExamRecordId; if (!id) return null; const selectable = record.status !== 'Absent'; const selected = selectedIds.includes(id); const statusLabel = record.status === 'Matched' ? 'Eşleşti' : record.status === 'Absent' ? 'Göz ardı edildi' : record.status === 'Conflict' ? 'Çakışma' : 'Bekliyor'; const displayName = kind === 'student' ? record.studentName : record.status === 'Matched' ? record.studentName : record.opticalStudentName || 'İsimsiz'; return <tr key={id} className={`border-t border-slate-100 ${selected ? 'bg-blue-50' : ''}`}><td className="px-3 py-3"><input type="checkbox" checked={selected} disabled={!selectable} onChange={() => onToggle(id)} /></td><td className="px-3 py-3 font-medium">{kind === 'student' ? record.studentNumber : record.opticalStudentNumber}</td><td className="px-3 py-3">{displayName}</td><td className="px-3 py-3"><span className={`rounded-full border px-2 py-1 text-xs ${record.status === 'Matched' ? 'border-emerald-200 bg-emerald-50 text-emerald-700' : record.status === 'Conflict' ? 'border-red-200 bg-red-50 text-red-700' : 'border-slate-200 text-slate-500'}`}>{statusLabel}</span>{kind === 'optical' && onIgnore && selectable && record.status !== 'Matched' && <button type="button" className="ml-2 rounded-lg bg-red-500 px-3 py-1 text-xs font-semibold text-white hover:bg-red-600" onClick={() => onIgnore(id)}>Göz ardı et</button>}{kind === 'student' && onAbsent && selectable && record.status !== 'Matched' && <button type="button" className="ml-2 text-xs text-slate-500 underline" onClick={() => onAbsent(id)}>Sınava girmedi</button>}</td></tr>; })}</tbody></table></div>}</section>;
}

function PendingMatches({ records, onRemove }: { records: MatchingRecord[]; onRemove: (id: string) => void }) {
  return <section className="mt-5 rounded-xl border border-amber-200 bg-amber-50 p-5"><h2 className="text-lg font-medium text-ink">Bekleyen Eşleştirmeler <span className="rounded-full bg-amber-100 px-2 py-1 text-sm text-amber-800">{records.length}</span></h2><p className="mt-1 text-sm text-slate-500">Bu eşleştirmeler “İleri” butonuna tıkladığınızda kaydedilecek.</p><div className="mt-4 grid gap-3">{records.map(record => <div key={record.matchId} className="flex flex-wrap items-center justify-between gap-4 rounded-lg border border-amber-200 bg-white p-4"><div className="grid gap-1 text-sm sm:grid-cols-[1fr_auto_1fr] sm:items-center sm:gap-5"><div><p className="text-xs text-slate-500">Optik</p><p className="font-medium text-ink">{record.opticalStudentNumber}</p><p className="text-slate-600">{record.opticalStudentName || 'İsimsiz'}</p></div><span className="text-amber-600">→</span><div><p className="text-xs text-slate-500">Sınıf Listesi</p><p className="font-medium text-ink">{record.studentNumber}</p><p className="text-slate-600">{record.studentName}</p></div></div><button type="button" className="text-sm font-medium text-amber-700 hover:text-amber-900" onClick={() => record.matchId && onRemove(record.matchId)}>↶ Geri Al</button></div>)}</div></section>;
}
