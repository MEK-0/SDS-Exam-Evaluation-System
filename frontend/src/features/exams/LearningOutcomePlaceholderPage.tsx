/* Öğrenme çıktısı ve soru ilişkilendirme adımını yönetir. */
import { BookOpen, Check, RotateCcw, Save } from 'lucide-react';
import { useEffect, useState } from 'react';
import { useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { Button } from '../../components/ui/Button';
import { Card } from '../../components/ui/Card';
import { LoadingState } from '../../components/ui/State';
import { StepIndicator } from '../../components/ui/StepIndicator';
import { useCompleteExam, useCreateLearningOutcome, useLearningOutcomePage, useSaveBookletMappings, useSaveOutcomeMappings } from '../../hooks/useExams';
import type { BookletQuestionMapping, LearningOutcomePage, LearningOutcomeQuestionMapping } from '../../types/api';
import { wizardSteps } from './wizardSteps';

export function LearningOutcomePlaceholderPage() {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const { courseId = '', examType = '' } = useParams();
  const examId = searchParams.get('examId') ?? '';
  const pageQuery = useLearningOutcomePage(examId);
  const createOutcomeMutation = useCreateLearningOutcome(courseId);
  const saveBookletMutation = useSaveBookletMappings(examId);
  const saveOutcomeMutation = useSaveOutcomeMappings(examId);
  const completeMutation = useCompleteExam(examId);
  const [description, setDescription] = useState('');
  const [bookletMappings, setBookletMappings] = useState<BookletQuestionMapping[]>([]);
  const [outcomeMappings, setOutcomeMappings] = useState<LearningOutcomeQuestionMapping[]>([]);
  const [initialized, setInitialized] = useState(false);
  const page = pageQuery.data;

  useEffect(() => {
    if (!page || initialized) return;
    setBookletMappings(page.bookletMappings);
    setOutcomeMappings(page.questionMappings);
    setInitialized(true);
  }, [initialized, page]);

  const error = pageQuery.error ?? createOutcomeMutation.error ?? saveBookletMutation.error ?? saveOutcomeMutation.error ?? completeMutation.error;
  if (pageQuery.isLoading) return <><StepIndicator steps={wizardSteps} currentStep={5} /><LoadingState label="Öğrenme çıktıları yükleniyor..." /></>;
  if (pageQuery.isError || !page) return <div className="grid gap-7"><StepIndicator steps={wizardSteps} currentStep={5} /><Card className="p-6"><h1 className="text-xl font-semibold text-ink">Öğrenme Çıktıları</h1><p className="mt-3 text-sm text-red-700">{error instanceof Error ? error.message : 'Öğrenme çıktıları yüklenemedi.'}</p><Button className="mt-5" onClick={() => pageQuery.refetch()}>Tekrar Dene</Button></Card></div>;

  const addOutcome = () => { const value = description.trim(); if (value) createOutcomeMutation.mutate(value, { onSuccess: () => { setDescription(''); setInitialized(false); } }); };
  const complete = () => completeMutation.mutate({ bookletMappings, outcomeMappings }, { onSuccess: () => navigate(`/course/${courseId}/exam/${examType}/analysis/${examId}`) });
  const reset = () => { setDescription(''); setBookletMappings(page.bookletMappings); setOutcomeMappings(page.questionMappings); };
  return <div className="grid gap-7"><StepIndicator steps={wizardSteps} currentStep={5} /><Card className="p-5 sm:p-6"><BookletMappingSection page={page} mappings={bookletMappings} onChange={setBookletMappings} onSave={() => saveBookletMutation.mutate(bookletMappings)} saving={saveBookletMutation.isPending} /><Notice text={bookletNotice(page, bookletMappings)} /></Card><Card className="p-5 sm:p-6"><h2 className="text-lg font-semibold text-ink">Yeni Öğrenme Çıktısı Ekle</h2><label className="mt-6 grid gap-2 text-sm font-semibold text-ink">Çıktı Açıklaması<textarea value={description} onChange={event => setDescription(event.target.value)} placeholder="ör: Programlama temel kavramlarını anlama ve uygulama" className="min-h-20 resize-y rounded-lg border border-slate-200 px-3 py-3 font-normal outline-none focus:border-brand-500" /></label><div className="mt-4 flex flex-wrap gap-3"><Button onClick={addOutcome} disabled={!description.trim() || createOutcomeMutation.isPending}>Öğrenme Çıktısı Ekle</Button><Button variant="secondary" onClick={() => pageQuery.refetch()}>Öğrenme Çıktılarını Getir</Button></div></Card>{page.outcomes.length === 0 ? <Notice text="Analiz için en az bir öğrenme çıktısı eklemelisiniz." /> : <Card className="p-5 sm:p-6"><div className="flex flex-wrap items-center justify-between gap-3"><h2 className="text-lg font-semibold text-ink">Öğrenme Çıktıları - Soru İlişkilendirmesi</h2><Button variant="secondary" onClick={() => saveOutcomeMutation.mutate(outcomeMappings)} disabled={saveOutcomeMutation.isPending}><Save size={16} />Kaydet</Button></div><OutcomeMatrix page={page} mappings={outcomeMappings} onChange={setOutcomeMappings} /></Card>}<div className="flex flex-wrap items-center justify-between gap-3"><div className="flex items-center gap-3"><Button variant="ghost" onClick={() => navigate(`/course/${courseId}/exam/${examType}/grades?examId=${examId}`)}>Geri</Button><button type="button" onClick={reset} className="inline-flex items-center gap-2 px-2 py-2 text-sm text-slate-500 hover:text-ink"><RotateCcw size={16} />Sıfırla</button></div><Button className="min-h-12 flex-1 sm:max-w-sm" onClick={complete} disabled={completeMutation.isPending || page.outcomes.length === 0 || !hasOutcomeMapping(outcomeMappings)}>{completeMutation.isPending ? 'Kaydediliyor...' : 'Kaydet ve Analizi Gör'}</Button></div>{error && <p className="rounded-lg border border-red-100 bg-red-50 p-3 text-sm text-red-700">{error instanceof Error ? error.message : 'Öğrenme çıktıları kaydedilemedi.'}</p>}{completeMutation.isSuccess && <p className="rounded-lg border border-green-100 bg-green-50 p-3 text-sm text-green-700"><Check size={16} className="mr-1 inline" />Sınav tamamlandı.</p>}</div>;
}

function BookletMappingSection({ page, mappings, onChange, onSave, saving }: { page: LearningOutcomePage; mappings: BookletQuestionMapping[]; onChange: (value: BookletQuestionMapping[]) => void; onSave: () => void; saving: boolean }) {
  if (!page.isMultipleChoice || page.questionCount === 0) return null;
  const codes = [...new Set(page.bookletMappings.map(item => item.bookletCode))];
  if (codes.length === 0) return null;
  const referenceCode = page.bookletMappings[0]?.referenceBookletCode ?? 'A';
  return <section><div className="flex flex-wrap items-center justify-between gap-3"><h2 className="flex items-center gap-2 text-lg font-semibold text-ink"><BookOpen size={20} />Kitapçık Soru Eşleştirme <span className="text-xs font-normal text-slate-500">(Zorunlu)</span></h2><div className="flex items-center gap-2"><button type="button" onClick={() => onChange(fillAllMappings(page, referenceCode, codes))} className="text-sm text-slate-500 hover:text-ink">Tüm hücreleri doldurun</button><Button variant="secondary" onClick={onSave} disabled={saving}><Save size={16} />{saving ? 'Kaydediliyor...' : 'Kaydet'}</Button></div></div><div className="mt-5 overflow-x-auto rounded-lg border border-slate-100"><div className="min-w-max"><div className="grid" style={{ gridTemplateColumns: `150px repeat(${page.questionCount}, 72px)` }}><div className="border-b border-slate-200 px-3 py-3 text-sm font-semibold text-brand-700">{referenceCode} Kitapçık</div>{Array.from({ length: page.questionCount }, (_, index) => <div key={index} className="border-b border-slate-200 px-3 py-3 text-center text-sm font-semibold text-ink">{index + 1}</div>)}{codes.map(code => <BookletRow key={code} code={code} questionCount={page.questionCount} referenceCode={referenceCode} mappings={mappings} onChange={onChange} />)}</div></div></div></section>;
}

function BookletRow({ code, questionCount, referenceCode, mappings, onChange }: { code: string; questionCount: number; referenceCode: string; mappings: BookletQuestionMapping[]; onChange: (value: BookletQuestionMapping[]) => void }) {
  const values = mappings.find(item => item.bookletCode === code)?.items ?? [];
  return <><div className="px-3 py-3 text-sm font-semibold text-slate-600">{code} Kitapçık</div>{Array.from({ length: questionCount }, (_, index) => { const referenceQuestionNumber = index + 1; const current = values.find(item => item.referenceQuestionNumber === referenceQuestionNumber)?.bookletQuestionNumber ?? ''; return <div key={referenceQuestionNumber} className="p-2"><select value={current} onChange={event => { const value = Number(event.target.value); const nextItems = values.filter(item => item.referenceQuestionNumber !== referenceQuestionNumber).concat({ referenceQuestionNumber, bookletQuestionNumber: value }).sort((a, b) => a.referenceQuestionNumber - b.referenceQuestionNumber); onChange([...mappings.filter(item => item.bookletCode !== code), { referenceBookletCode: referenceCode, bookletCode: code, items: nextItems }]); }} className="h-9 w-16 rounded-lg border border-slate-200 bg-slate-50 px-1 text-center text-sm"><option value="">–</option>{Array.from({ length: questionCount }, (_, number) => <option key={number + 1} value={number + 1} disabled={values.some(item => item.bookletQuestionNumber === number + 1 && item.referenceQuestionNumber !== referenceQuestionNumber)}>{number + 1}</option>)}</select></div>; })}</>;
}

function OutcomeMatrix({ page, mappings, onChange }: { page: LearningOutcomePage; mappings: LearningOutcomeQuestionMapping[]; onChange: (value: LearningOutcomeQuestionMapping[]) => void }) { return <div className="mt-5 overflow-x-auto rounded-lg border border-slate-100"><table className="min-w-max text-sm"><thead><tr><th className="sticky left-0 z-10 min-w-64 bg-white px-3 py-3 text-left font-semibold text-ink">Öğrenme Çıktısı</th>{Array.from({ length: page.questionCount }, (_, index) => <th key={index} className="px-3 py-3 text-center font-semibold text-ink">{index + 1}</th>)}</tr></thead><tbody>{page.outcomes.map(outcome => { const selected = mappings.find(item => item.learningOutcomeId === outcome.id)?.questionNumbers ?? []; return <tr key={outcome.id} className="border-t border-slate-100"><td className="sticky left-0 bg-white px-3 py-3 text-slate-600">{outcome.description}</td>{Array.from({ length: page.questionCount }, (_, index) => { const number = index + 1; const checked = selected.includes(number); return <td key={number} className="px-3 py-3 text-center"><input type="checkbox" checked={checked} onChange={() => { const numbers = checked ? selected.filter(item => item !== number) : [...selected, number].sort((a, b) => a - b); onChange([...mappings.filter(item => item.learningOutcomeId !== outcome.id), { learningOutcomeId: outcome.id, questionNumbers: numbers }]); }} className="h-4 w-4 accent-brand-500" aria-label={`${outcome.description} soru ${number}`} /></td>; })}</tr>; })}</tbody></table></div>; }

function fillAllMappings(page: LearningOutcomePage, referenceCode: string, codes: string[]) { return codes.map(code => ({ referenceBookletCode: referenceCode, bookletCode: code, items: Array.from({ length: page.questionCount }, (_, index) => ({ referenceQuestionNumber: index + 1, bookletQuestionNumber: index + 1 })) })); }
function hasOutcomeMapping(mappings: LearningOutcomeQuestionMapping[]) { return mappings.some(item => item.questionNumbers.length > 0); }
function bookletNotice(page: LearningOutcomePage, mappings: BookletQuestionMapping[]) { if (!page.isMultipleChoice || page.questionCount === 0 || page.bookletMappings.length === 0) return ''; return mappings.some(item => item.items.length !== page.questionCount) ? 'Kazanç girişi için önce kitapçık soru eşleştirmesini tamamlayıp kaydedin.' : 'Kitapçık soru eşleştirmeleri kaydedildi.'; }
function Notice({ text }: { text: string }) { return text ? <div className="mt-5 flex items-center gap-2 rounded-lg border border-amber-100 bg-amber-50 p-4 text-sm text-amber-800"><RotateCcw size={16} />{text}</div> : null; }
