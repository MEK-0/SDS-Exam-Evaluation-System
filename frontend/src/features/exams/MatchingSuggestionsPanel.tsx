/* Veri Eşleştirme adımında özet sayaçlarını, önerileri ve geçersiz optik satırlarını gösterir. */
import { CircleAlert } from 'lucide-react';
import { useState } from 'react';
import { Button } from '../../components/ui/Button';
import type { MatchSuggestion, MatchingRecord, MatchingSummary } from '../../types/api';

interface MatchingExtrasProps {
  summary: MatchingSummary;
  suggestions: MatchSuggestion[];
  records: MatchingRecord[];
  isBusy: boolean;
  onAccept: (importedStudentId: string, opticalExamRecordId: string) => void;
  onAcceptAbsent: () => void;
}

function suggestionKey(suggestion: MatchSuggestion) {
  return `${suggestion.importedStudentId}:${suggestion.opticalExamRecordId ?? ''}`;
}

export function MatchingExtras({ summary, suggestions, records, isBusy, onAccept, onAcceptAbsent }: MatchingExtrasProps) {
  const [rejectedKeys, setRejectedKeys] = useState<string[]>([]);
  const visible = (suggestions ?? []).filter(item => !rejectedKeys.includes(suggestionKey(item)));
  const similar = visible.filter(item => item.kind === 'SimilarNumber' && item.opticalExamRecordId);
  const absent = visible.filter(item => item.kind === 'Absent');
  const invalidRecords = records.filter(record => record.isInvalidOptical);
  const counters = [
    { label: 'Önerilen eşleşme', value: summary.suggestedMatchCount },
    { label: 'Girmedi önerisi', value: summary.absentSuggestionCount },
    { label: 'Geçersiz optik', value: summary.invalidOpticalCount },
  ];

  return <div className="mt-6 grid gap-5">
    <div className="grid grid-cols-3 gap-3">
      {counters.map(counter => <div key={counter.label} className="rounded-xl border border-slate-200 p-3 text-center">
        <p className="text-xl font-semibold text-ink">{counter.value}</p>
        <p className="text-xs text-slate-500">{counter.label}</p>
      </div>)}
    </div>
    {similar.length > 0 && <section className="rounded-xl border border-blue-200 bg-blue-50 p-5">
      <h2 className="text-lg font-medium text-ink">Önerilen eşleşmeler <span className="text-sm text-slate-500">({similar.length})</span></h2>
      <div className="mt-4 grid gap-3">
        {similar.map(item => <div key={suggestionKey(item)} className="flex flex-wrap items-center justify-between gap-4 rounded-lg border border-blue-100 bg-white p-4">
          <div className="grid gap-2 text-sm sm:grid-cols-[1fr_auto_1fr] sm:items-center sm:gap-5">
            <div><p className="text-xs text-slate-500">Sınıf Listesi</p><p className="font-medium text-ink">{item.studentNumber}</p><p className="text-slate-600">{item.studentName}</p></div>
            <span className="text-blue-600">↔</span>
            <div><p className="text-xs text-slate-500">Optik</p><p className="font-medium text-ink">{item.opticalStudentNumber}</p><p className="text-slate-600">{item.opticalStudentName || 'İsimsiz'}</p></div>
            <p className="text-xs text-slate-500 sm:col-span-3">{item.reason}</p>
          </div>
          <div className="flex gap-2">
            <Button disabled={isBusy} onClick={() => item.opticalExamRecordId && onAccept(item.importedStudentId, item.opticalExamRecordId)}>Onayla</Button>
            <Button variant="secondary" onClick={() => setRejectedKeys(current => [...current, suggestionKey(item)])}>Reddet</Button>
          </div>
        </div>)}
      </div>
    </section>}
    {absent.length > 0 && <section className="rounded-xl border border-slate-200 p-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 className="text-lg font-medium text-ink">Sınava girmedi önerileri <span className="text-sm text-slate-500">({absent.length})</span></h2>
        <Button disabled={isBusy} onClick={onAcceptAbsent}>Tümünü Onayla</Button>
      </div>
      <ul className="mt-3 grid gap-1 text-sm text-slate-600">
        {absent.map(item => <li key={suggestionKey(item)}>{item.studentNumber} - {item.studentName}</li>)}
      </ul>
    </section>}
    {invalidRecords.length > 0 && <section className="rounded-xl border border-amber-200 bg-amber-50 p-5">
      <h2 className="flex items-center gap-2 text-lg font-medium text-ink"><CircleAlert size={18} className="text-amber-600" />Geçersiz optik satırları <span className="text-sm text-slate-500">({invalidRecords.length})</span></h2>
      <p className="mt-1 text-sm text-amber-800">Bu satırlar eşleştirmeye alınamadı; onay adımını engellemez.</p>
      <ul className="mt-3 grid gap-1 text-sm text-slate-700">
        {invalidRecords.map(record => <li key={record.opticalExamRecordId ?? record.opticalStudentNumber}>{record.opticalStudentNumber || '(numarasız)'} {record.opticalStudentName} {record.issue ? `- ${record.issue}` : ''}</li>)}
      </ul>
    </section>}
  </div>;
}
