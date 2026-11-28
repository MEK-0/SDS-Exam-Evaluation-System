/* Çoktan seçmeli sınav için optik ve sınıf listesi dosyalarını yükleme adımını sunar. */
import { FileSpreadsheet, FileUp, Info, X } from 'lucide-react';
import { useRef, useState, type RefObject } from 'react';
import { useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { Button } from '../../components/ui/Button';
import { Card } from '../../components/ui/Card';
import { EmptyState, LoadingState } from '../../components/ui/State';
import { StepIndicator } from '../../components/ui/StepIndicator';
import { useBindExamTemplate, useOpticalTemplates } from '../../hooks/useOpticalTemplates';
import { useExam, useImportPreview, useRemoveImportFile, useUploadExamFiles } from '../../hooks/useExams';
import type { ImportPreview, OpticalTemplate } from '../../types/api';
import { wizardSteps } from './wizardSteps';
import { OpenEndedExamPage } from './OpenEndedExamPage';

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
  const examQuery = useExam(examId);
  const templatesQuery = useOpticalTemplates();
  const bindMutation = useBindExamTemplate(examId);
  const templates = templatesQuery.data ?? [];
  const defaultTemplate = templates.find(item => item.isDefault);
  const [pickedTemplateId, setPickedTemplateId] = useState('');
  const selectedTemplateId = pickedTemplateId || examQuery.data?.opticalTemplateId || defaultTemplate?.id || '';
  const selectedTemplate = templates.find(item => item.id === selectedTemplateId);
  const changeTemplate = async (templateId: string) => {
    const previousId = pickedTemplateId;
    setPickedTemplateId(templateId);
    try {
      await bindMutation.mutateAsync(templateId);
    } catch {
      setPickedTemplateId(previousId);
    }
  };
  const errorMessage = uploadMutation.error instanceof Error ? uploadMutation.error.message : removeMutation.error instanceof Error ? removeMutation.error.message : 'Dosyalar işlenemedi.';

  if (searchParams.get('mode') === 'open-ended') return <OpenEndedExamPage />;
  if (previewQuery.isLoading) return <LoadingState label="Yüklenen dosyalar kontrol ediliyor..." />;

  const upload = async () => { if (!opticalFile || classListFiles.length === 0 || classListFiles.length > 2) return; await uploadMutation.mutateAsync({ opticalFile, classListFiles }); setEditingFiles(false); };
  const removeSavedFile = async (fileType: 'optical' | 'classList', fileName?: string) => { await removeMutation.mutateAsync({ fileType, fileName }); uploadMutation.reset(); setOpticalFile(null); setClassListFiles([]); };
  const removeLocalClassList = (index: number) => setClassListFiles(current => current.filter((_, itemIndex) => itemIndex !== index));
  const goNext = () => navigate(`/course/${courseId}/exam/${examType}/answer-key?examId=${examId}`);
  const canUpload = Boolean(opticalFile && classListFiles.length > 0 && classListFiles.length <= 2);

  return <div className="grid gap-7"><StepIndicator steps={wizardSteps} currentStep={1} /><Card className="p-5 sm:p-8"><h1 className="text-xl font-semibold text-ink">Dosya Yükleme</h1><div className="mt-6 grid gap-5 lg:grid-cols-2"><UploadCard title="Optik Okuyucu Dosyası (.txt)" icon={<FileUp size={18} />} fileName={opticalFile?.name ?? (editingFiles ? '' : preview?.opticalFileName ?? '')} fileSize={opticalFile?.size} inputRef={opticalInput} onSelect={setOpticalFile} onRemove={() => opticalFile ? setOpticalFile(null) : removeSavedFile('optical')} accept=".txt" accent /><ClassListCard files={classListFiles} savedFiles={editingFiles ? [] : preview?.classListFileNames ?? []} inputRef={classListInput} onAdd={(files) => setClassListFiles(current => [...current, ...files].slice(0, 2))} onRemoveLocal={removeLocalClassList} onRemoveSaved={(fileName) => removeSavedFile('classList', fileName)} /></div><TemplatePicker templates={templates} isLoading={templatesQuery.isLoading} isError={templatesQuery.isError} onRetry={() => templatesQuery.refetch()} selectedId={selectedTemplateId} selected={selectedTemplate} disabled={bindMutation.isPending || Boolean(!editingFiles && preview?.opticalFileName)} onChange={changeTemplate} bindError={bindMutation.isError ? (bindMutation.error instanceof Error && bindMutation.error.message ? bindMutation.error.message : 'Optik form şablonu sınava bağlanamadı.') : ''} boundTemplateId={examQuery.data?.opticalTemplateId ?? ''} boundVersionNo={examQuery.data?.opticalTemplateVersionNo} /><div className="mt-5 flex items-start gap-2 rounded-xl border border-amber-100 bg-amber-50 p-4 text-sm text-amber-800"><Info size={17} className="mt-0.5 shrink-0" /><span>Optik okuyucu için {selectedTemplate ? `"${selectedTemplate.name}"` : 'sistem yöneticisinin tanımladığı'} formatı kullanılmaktadır. Dosya bu formata uymuyorsa açık bir doğrulama hatası gösterilir.</span></div>{(uploadMutation.isError || removeMutation.isError) && <p className="mt-5 rounded-lg border border-red-100 bg-red-50 p-3 text-sm text-red-700">{errorMessage}</p>}{preview?.warnings?.length ? <div className="mt-5 rounded-lg border border-amber-200 bg-amber-50 p-3 text-sm text-amber-800" role="alert">{preview.warnings.map((warning, index) => <p key={`${warning.source}-${index}`}>{warning.message}</p>)}</div> : null}{preview?.issues.length ? <p className="mt-5 rounded-lg border border-amber-100 bg-amber-50 p-3 text-sm text-amber-800">{preview.issues.length} doğrulama uyarısı bulundu. İleri adıma geçmeden önce kayıtları kontrol edin.</p> : null}{hasSavedImport && <ImportPreviewPanel preview={preview!} />}{!hasSavedImport && <Button className="mt-6" disabled={!canUpload || uploadMutation.isPending} onClick={upload}>{uploadMutation.isPending ? 'Yükleniyor...' : 'Dosyaları Yükle'}</Button>}{hasSavedImport && <div className="mt-6 flex flex-wrap items-center justify-between gap-3"><Button variant="secondary" onClick={() => { uploadMutation.reset(); setOpticalFile(null); setClassListFiles([]); setEditingFiles(true); }}>Dosyaları Değiştir</Button><Button onClick={goNext} disabled={preview!.invalidOpticalRecordCount > 0 || !preview!.opticalFileName || preview!.classListFileNames.length === 0}>İleri</Button></div>}</Card></div>;
}

function TemplatePicker({ templates, isLoading, isError, onRetry, selectedId, selected, disabled, onChange, bindError, boundTemplateId, boundVersionNo }: { templates: OpticalTemplate[]; isLoading: boolean; isError: boolean; onRetry: () => void; selectedId: string; selected?: OpticalTemplate; disabled: boolean; onChange: (templateId: string) => void; bindError: string; boundTemplateId: string; boundVersionNo?: number }) {
  const isBound = Boolean(selected && boundVersionNo !== undefined && selected.id === boundTemplateId);
  const shownVersionNo = isBound ? boundVersionNo : selected?.currentVersion.versionNo;
  const hasNewerVersion = isBound && selected !== undefined && boundVersionNo !== undefined && selected.currentVersion.versionNo > boundVersionNo;
  return (
    <div className="mt-5 rounded-xl border border-slate-200 bg-white p-4">
      <label htmlFor="optical-template" className="text-sm font-semibold text-ink">Optik Form Şablonu</label>
      {isLoading && <LoadingState label="Optik form şablonları yükleniyor..." />}
      {isError && (
        <div className="mt-3 flex flex-wrap items-center gap-3 rounded-lg border border-red-100 bg-red-50 p-3 text-sm text-red-700">
          <span>Optik form şablonları yüklenemedi.</span>
          <Button variant="secondary" onClick={onRetry}>Tekrar Dene</Button>
        </div>
      )}
      {!isLoading && !isError && templates.length === 0 && <EmptyState title="Tanımlı optik form şablonu yok" description="Sistem yöneticisinin bir şablon tanımlaması gerekir." />}
      {!isLoading && !isError && templates.length > 0 && (
        <>
          <select id="optical-template" className="mt-2 w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm text-ink disabled:bg-slate-50" value={selectedId} disabled={disabled} onChange={(event) => onChange(event.target.value)}>
            {templates.map(template => <option key={template.id} value={template.id}>{template.name}{template.isDefault ? ' (Varsayılan)' : ''}</option>)}
          </select>
          {selected && <p className="mt-2 text-xs text-slate-500">{selected.name} - Sürüm {shownVersionNo}</p>}
          {hasNewerVersion && selected && <p className="mt-1 text-xs text-amber-700">Bu sınav Sürüm {boundVersionNo}'e bağlı; güncel sürüm {selected.currentVersion.versionNo}.</p>}
          {disabled && <p className="mt-1 text-xs text-slate-500">Şablonu değiştirmek için önce yüklenen dosyaları kaldırın.</p>}
        </>
      )}
      {bindError && <p className="mt-2 rounded-lg border border-red-100 bg-red-50 p-3 text-sm text-red-700">{bindError}</p>}
    </div>
  );
}

function UploadCard({ title, icon, fileName, fileSize, inputRef, onSelect, onRemove, accept, accent = false }: { title: string; icon: JSX.Element; fileName: string; fileSize?: number; inputRef: RefObject<HTMLInputElement>; onSelect: (file: File | null) => void; onRemove: () => void; accept: string; accent?: boolean }) { return <div className={`rounded-xl border-2 border-dashed p-5 ${accent ? 'border-emerald-300 bg-emerald-50/30' : 'border-amber-300 bg-amber-50/20'}`}><div className="flex items-center gap-3 text-ink"><div className="text-brand-600">{icon}</div><h2 className="font-semibold">{title}</h2></div><input ref={inputRef} type="file" accept={accept} className="hidden" onChange={(event) => onSelect(event.target.files?.[0] ?? null)} />{fileName ? <div className="mt-6 rounded-lg border border-emerald-100 bg-white p-3"><div className="flex items-start justify-between gap-3"><div><p className="break-all text-sm font-semibold text-emerald-700">{fileName}</p><p className="mt-1 text-xs text-slate-500">{fileSize ? formatFileSize(fileSize) : 'Kayıtlı dosya'}</p></div><button type="button" className="text-red-500 hover:text-red-700" aria-label={`${fileName} kaldır`} onClick={onRemove}><X size={16} /></button></div></div> : <Button className="mt-6 w-full" onClick={() => inputRef.current?.click()}><FileUp size={16} /> Dosya Seç</Button>}</div>; }

function ClassListCard({ files, savedFiles, inputRef, onAdd, onRemoveLocal, onRemoveSaved }: { files: File[]; savedFiles: string[]; inputRef: RefObject<HTMLInputElement>; onAdd: (files: File[]) => void; onRemoveLocal: (index: number) => void; onRemoveSaved: (fileName: string) => void }) { const visibleFiles = files.length > 0 ? files.map(file => ({ name: file.name, size: file.size })) : savedFiles.map(name => ({ name, size: undefined })); return <div className="rounded-xl border-2 border-dashed border-amber-300 bg-amber-50/20 p-5"><div className="flex items-center justify-between gap-3"><div className="flex items-center gap-3 text-ink"><FileSpreadsheet size={18} className="text-brand-600" /><h2 className="font-semibold">Sınıf Listesi (.xlsx)</h2></div><span className="text-xs text-slate-500">{visibleFiles.length}/2</span></div><input ref={inputRef} type="file" accept=".xlsx" multiple className="hidden" onChange={(event) => { onAdd(Array.from(event.target.files ?? []).filter(file => file.name.toLowerCase().endsWith('.xlsx'))); event.currentTarget.value = ''; }} />{visibleFiles.map((file, index) => <div key={`${file.name}-${index}`} className="mt-3 flex items-center justify-between gap-3 rounded-lg border border-emerald-100 bg-white p-3"><div><p className="break-all text-sm font-semibold text-emerald-700">{file.name}</p><p className="mt-1 text-xs text-slate-500">{file.size ? formatFileSize(file.size) : 'Kayıtlı dosya'}</p></div><button type="button" className="text-red-500 hover:text-red-700" aria-label={`${file.name} kaldır`} onClick={() => files.length > 0 ? onRemoveLocal(index) : onRemoveSaved(file.name)}><X size={16} /></button></div>)}{visibleFiles.length < 2 && <Button className="mt-3 w-full" onClick={() => inputRef.current?.click()}>+ Dosya Ekle</Button>}</div>; }

function ImportPreviewPanel({ preview }: { preview: ImportPreview }) { return <div className="mt-6 rounded-xl border border-slate-200 bg-white p-4"><div className="grid gap-3 sm:grid-cols-3"><Metric label="Optik kayıt" value={preview.totalOpticalRecordCount} /><Metric label="Geçerli kayıt" value={preview.validOpticalRecordCount} /><Metric label="Öğrenci" value={preview.studentCount} /></div></div>; }
function Metric({ label, value }: { label: string; value: number }) { return <div className="rounded-lg bg-slate-50 p-3"><p className="text-xs text-slate-500">{label}</p><p className="mt-1 text-xl font-semibold text-ink">{value}</p></div>; }
function formatFileSize(bytes: number) { return bytes < 1024 * 1024 ? `${Math.max(1, Math.round(bytes / 1024))} KB` : `${(bytes / (1024 * 1024)).toFixed(1)} MB`; }
