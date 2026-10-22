/* Çoktan seçmeli sınav için optik ve sınıf listesi dosyalarını yükleme adımını sunar. */
import { FileSpreadsheet, FileUp, Info, X } from 'lucide-react';
import { useRef, useState, type RefObject } from 'react';
import { useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { Button } from '../../components/ui/Button';
import { Card } from '../../components/ui/Card';
import { LoadingState } from '../../components/ui/State';
import { StepIndicator } from '../../components/ui/StepIndicator';
import { useImportPreview, useRemoveImportFile, useUploadExamFiles } from '../../hooks/useExams';
import type { ImportPreview, OpticalFormConfiguration } from '../../types/api';
import { wizardSteps } from './wizardSteps';
import { OpenEndedExamPage } from './OpenEndedExamPage';

const defaultConfiguration: OpticalFormConfiguration = { studentNumberStart: 0, studentNumberLength: 9, studentNameStart: null, studentNameLength: null, bookletTypeStart: 9, bookletTypeLength: 1, examTypeStart: null, examTypeLength: null, answersStart: 10, answersLength: 25, encodingName: 'utf-8' };

export function NewExamPage() {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const { courseId = '', examType = '' } = useParams();
  const examId = searchParams.get('examId') ?? '';
  const opticalInput = useRef<HTMLInputElement>(null);
  const classListInput = useRef<HTMLInputElement>(null);
  const [opticalFile, setOpticalFile] = useState<File | null>(null);
  const [classListFiles, setClassListFiles] = useState<File[]>([]);
  const [editingFiles, setEditingFiles] = useState(false);
  const uploadMutation = useUploadExamFiles(examId);
  const removeMutation = useRemoveImportFile(examId);
  const previewQuery = useImportPreview(searchParams.get('mode') === 'open-ended' ? '' : examId);
  const preview = uploadMutation.data ?? previewQuery.data;
  const hasSavedImport = Boolean(!editingFiles && preview && (preview.opticalFileName || preview.classListFileNames.length > 0));
  const errorMessage = uploadMutation.error instanceof Error ? uploadMutation.error.message : removeMutation.error instanceof Error ? removeMutation.error.message : 'Dosyalar işlenemedi.';

  if (searchParams.get('mode') === 'open-ended') return <OpenEndedExamPage />;
  if (previewQuery.isLoading) return <LoadingState label="Yüklenen dosyalar kontrol ediliyor..." />;

  const upload = async () => { if (!opticalFile || classListFiles.length === 0 || classListFiles.length > 2) return; await uploadMutation.mutateAsync({ opticalFile, classListFiles, configuration: defaultConfiguration }); setEditingFiles(false); };
  const removeSavedFile = async (fileType: 'optical' | 'classList', fileName?: string) => { await removeMutation.mutateAsync({ fileType, fileName }); uploadMutation.reset(); setOpticalFile(null); setClassListFiles([]); };
  const removeLocalClassList = (index: number) => setClassListFiles(current => current.filter((_, itemIndex) => itemIndex !== index));
  const goNext = () => navigate(`/course/${courseId}/exam/${examType}/answer-key?examId=${examId}`);
  const canUpload = Boolean(opticalFile && classListFiles.length > 0 && classListFiles.length <= 2);

  return <div className="grid gap-7"><StepIndicator steps={wizardSteps} currentStep={1} /><Card className="p-5 sm:p-8"><h1 className="text-xl font-semibold text-ink">Dosya Yükleme</h1><div className="mt-6 grid gap-5 lg:grid-cols-2"><UploadCard title="Optik Okuyucu Dosyası (.txt)" icon={<FileUp size={18} />} fileName={opticalFile?.name ?? (editingFiles ? '' : preview?.opticalFileName ?? '')} fileSize={opticalFile?.size} inputRef={opticalInput} onSelect={setOpticalFile} onRemove={() => opticalFile ? setOpticalFile(null) : removeSavedFile('optical')} accept=".txt" accent /><ClassListCard files={classListFiles} savedFiles={editingFiles ? [] : preview?.classListFileNames ?? []} inputRef={classListInput} onAdd={(files) => setClassListFiles(current => [...current, ...files].slice(0, 2))} onRemoveLocal={removeLocalClassList} onRemoveSaved={(fileName) => removeSavedFile('classList', fileName)} /></div><div className="mt-5 flex items-start gap-2 rounded-xl border border-amber-100 bg-amber-50 p-4 text-sm text-amber-800"><Info size={17} className="mt-0.5 shrink-0" /><span>Optik okuyucu için sistem yöneticisinin tanımladığı kurumsal format kullanılmaktadır. Dosya bu formata uymuyorsa açık bir doğrulama hatası gösterilir.</span></div>{(uploadMutation.isError || removeMutation.isError) && <p className="mt-5 rounded-lg border border-red-100 bg-red-50 p-3 text-sm text-red-700">{errorMessage}</p>}{preview?.issues.length ? <p className="mt-5 rounded-lg border border-amber-100 bg-amber-50 p-3 text-sm text-amber-800">{preview.issues.length} doğrulama uyarısı bulundu. İleri adıma geçmeden önce kayıtları kontrol edin.</p> : null}{hasSavedImport && <ImportPreviewPanel preview={preview!} />}{!hasSavedImport && <Button className="mt-6" disabled={!canUpload || uploadMutation.isPending} onClick={upload}>{uploadMutation.isPending ? 'Yükleniyor...' : 'Dosyaları Yükle'}</Button>}{hasSavedImport && <div className="mt-6 flex flex-wrap items-center justify-between gap-3"><Button variant="secondary" onClick={() => { uploadMutation.reset(); setOpticalFile(null); setClassListFiles([]); setEditingFiles(true); }}>Dosyaları Değiştir</Button><Button onClick={goNext} disabled={preview!.invalidOpticalRecordCount > 0 || !preview!.opticalFileName || preview!.classListFileNames.length === 0}>İleri</Button></div>}</Card></div>;
}

function UploadCard({ title, icon, fileName, fileSize, inputRef, onSelect, onRemove, accept, accent = false }: { title: string; icon: JSX.Element; fileName: string; fileSize?: number; inputRef: RefObject<HTMLInputElement>; onSelect: (file: File | null) => void; onRemove: () => void; accept: string; accent?: boolean }) { return <div className={`rounded-xl border-2 border-dashed p-5 ${accent ? 'border-emerald-300 bg-emerald-50/30' : 'border-amber-300 bg-amber-50/20'}`}><div className="flex items-center gap-3 text-ink"><div className="text-brand-600">{icon}</div><h2 className="font-semibold">{title}</h2></div><input ref={inputRef} type="file" accept={accept} className="hidden" onChange={(event) => onSelect(event.target.files?.[0] ?? null)} />{fileName ? <div className="mt-6 rounded-lg border border-emerald-100 bg-white p-3"><div className="flex items-start justify-between gap-3"><div><p className="break-all text-sm font-semibold text-emerald-700">{fileName}</p><p className="mt-1 text-xs text-slate-500">{fileSize ? formatFileSize(fileSize) : 'Kayıtlı dosya'}</p></div><button type="button" className="text-red-500 hover:text-red-700" aria-label={`${fileName} kaldır`} onClick={onRemove}><X size={16} /></button></div></div> : <Button className="mt-6 w-full" onClick={() => inputRef.current?.click()}><FileUp size={16} /> Dosya Seç</Button>}</div>; }

function ClassListCard({ files, savedFiles, inputRef, onAdd, onRemoveLocal, onRemoveSaved }: { files: File[]; savedFiles: string[]; inputRef: RefObject<HTMLInputElement>; onAdd: (files: File[]) => void; onRemoveLocal: (index: number) => void; onRemoveSaved: (fileName: string) => void }) { const visibleFiles = files.length > 0 ? files.map(file => ({ name: file.name, size: file.size })) : savedFiles.map(name => ({ name, size: undefined })); return <div className="rounded-xl border-2 border-dashed border-amber-300 bg-amber-50/20 p-5"><div className="flex items-center justify-between gap-3"><div className="flex items-center gap-3 text-ink"><FileSpreadsheet size={18} className="text-brand-600" /><h2 className="font-semibold">Sınıf Listesi (.xlsx)</h2></div><span className="text-xs text-slate-500">{visibleFiles.length}/2</span></div><input ref={inputRef} type="file" accept=".xlsx" multiple className="hidden" onChange={(event) => { onAdd(Array.from(event.target.files ?? []).filter(file => file.name.toLowerCase().endsWith('.xlsx'))); event.currentTarget.value = ''; }} />{visibleFiles.map((file, index) => <div key={`${file.name}-${index}`} className="mt-3 flex items-center justify-between gap-3 rounded-lg border border-emerald-100 bg-white p-3"><div><p className="break-all text-sm font-semibold text-emerald-700">{file.name}</p><p className="mt-1 text-xs text-slate-500">{file.size ? formatFileSize(file.size) : 'Kayıtlı dosya'}</p></div><button type="button" className="text-red-500 hover:text-red-700" aria-label={`${file.name} kaldır`} onClick={() => files.length > 0 ? onRemoveLocal(index) : onRemoveSaved(file.name)}><X size={16} /></button></div>)}{visibleFiles.length < 2 && <Button className="mt-3 w-full" onClick={() => inputRef.current?.click()}>+ Dosya Ekle</Button>}</div>; }

function ImportPreviewPanel({ preview }: { preview: ImportPreview }) { return <div className="mt-6 rounded-xl border border-slate-200 bg-white p-4"><div className="grid gap-3 sm:grid-cols-3"><Metric label="Optik kayıt" value={preview.totalOpticalRecordCount} /><Metric label="Geçerli kayıt" value={preview.validOpticalRecordCount} /><Metric label="Öğrenci" value={preview.studentCount} /></div></div>; }
function Metric({ label, value }: { label: string; value: number }) { return <div className="rounded-lg bg-slate-50 p-3"><p className="text-xs text-slate-500">{label}</p><p className="mt-1 text-xl font-semibold text-ink">{value}</p></div>; }
function formatFileSize(bytes: number) { return bytes < 1024 * 1024 ? `${Math.max(1, Math.round(bytes / 1024))} KB` : `${(bytes / (1024 * 1024)).toFixed(1)} MB`; }
