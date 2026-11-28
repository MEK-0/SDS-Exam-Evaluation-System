/* NewExamPage için optik şablon seçimi, sürüm gösterimi, kilit ve yükleme davranışlarını doğrular. */
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import * as examApi from '../../services/examApi';
import * as templateApi from '../../services/opticalTemplateApi';
import type { Exam, ImportPreview, OpticalTemplate, OpticalTemplateVersion } from '../../types/api';
import { NewExamPage } from './NewExamPage';

vi.mock('../../services/opticalTemplateApi', () => ({ getOpticalTemplates: vi.fn(), bindExamTemplate: vi.fn() }));
vi.mock('../../services/examApi', () => ({ getExam: vi.fn(), getImportPreview: vi.fn(), uploadExamFiles: vi.fn(), removeImportFile: vi.fn() }));

const getOpticalTemplates = vi.mocked(templateApi.getOpticalTemplates);
const bindExamTemplate = vi.mocked(templateApi.bindExamTemplate);
const getExam = vi.mocked(examApi.getExam);
const getImportPreview = vi.mocked(examApi.getImportPreview);
const uploadExamFiles = vi.mocked(examApi.uploadExamFiles);

function version(versionNo: number): OpticalTemplateVersion {
  return {
    id: `v${versionNo}`,
    versionNo,
    studentNumberStart: 1,
    studentNumberLength: 10,
    studentNameStart: null,
    studentNameLength: null,
    bookletTypeStart: 11,
    bookletTypeLength: 1,
    examTypeStart: null,
    examTypeLength: null,
    answersStart: 12,
    answersLength: 40,
    blankMarkChar: ' ',
    doubleMarkChar: '*',
    encodingMode: 'Auto',
    createdAt: '2026-01-01T00:00:00Z',
  } as OpticalTemplateVersion;
}

function template(id: string, name: string, isDefault: boolean, versionNo: number): OpticalTemplate {
  return { id, name, isDefault, versionCount: versionNo, currentVersion: version(versionNo), createdAt: '2026-01-01T00:00:00Z' };
}

function exam(templateId: string, templateName: string, versionNo: number): Exam {
  return {
    id: 'e1',
    courseId: 'c1',
    examType: 'Final',
    evaluationType: 'MultipleChoice',
    status: 'Draft',
    createdAt: '2026-01-01T00:00:00Z',
    opticalTemplateId: templateId,
    opticalTemplateName: templateName,
    opticalTemplateVersionId: `v${versionNo}`,
    opticalTemplateVersionNo: versionNo,
  } as Exam;
}

function savedPreview(): ImportPreview {
  return {
    importId: 'i1',
    opticalFileName: 'optik.txt',
    classListFileName: 'liste.xlsx',
    classListFileNames: ['liste.xlsx'],
    totalOpticalRecordCount: 3,
    validOpticalRecordCount: 3,
    invalidOpticalRecordCount: 0,
    studentCount: 3,
    opticalRecords: [],
    students: [],
    issues: [],
    createdAt: '2026-01-01T00:00:00Z',
  };
}

function renderPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={['/course/c1/exam/multiple-choice?examId=e1']}>
        <Routes>
          <Route path="/course/:courseId/exam/:examType" element={<NewExamPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

const standard = template('t1', 'Standart Form', true, 1);
const sample = template('t2', 'Örnek Form', false, 1);

async function picker() {
  return (await screen.findByLabelText('Optik Form Şablonu')) as HTMLSelectElement;
}

beforeEach(() => {
  vi.resetAllMocks();
  getOpticalTemplates.mockResolvedValue([standard, sample]);
  getImportPreview.mockRejectedValue(new Error('Import önizlemesi bulunamadı.'));
  getExam.mockResolvedValue(exam('', '', 0));
});

describe('NewExamPage optik şablon seçimi', () => {
  it('KK4_H1_UploadDoesNotSendClientOpticalConfiguration', async () => {
    uploadExamFiles.mockResolvedValue(savedPreview());
    const user = userEvent.setup();
    const { container } = renderPage();
    await picker();
    const inputs = container.querySelectorAll<HTMLInputElement>('input[type="file"]');
    const optical = new File(['x'], 'optik.txt', { type: 'text/plain' });
    const list = new File(['x'], 'liste.xlsx');
    await user.upload(inputs[0], optical);
    await user.upload(inputs[1], list);
    await user.click(await screen.findByRole('button', { name: 'Dosyaları Yükle' }));
    await waitFor(() => expect(uploadExamFiles).toHaveBeenCalledTimes(1));
    const args = uploadExamFiles.mock.calls[0] as unknown[];
    expect(args).toHaveLength(3);
    expect(args[0]).toBe('e1');
    expect(args[1]).toBeInstanceOf(File);
    expect(Array.isArray(args[2])).toBe(true);
    expect((args[2] as unknown[]).every(item => item instanceof File)).toBe(true);
    expect(JSON.stringify(args)).not.toContain('studentNumberStart');
  });

  it('KK4_K11_DefaultTemplatePreselectedWhenExamHasNoBinding', async () => {
    renderPage();
    const select = await picker();
    expect(select.value).toBe('t1');
    expect(screen.getByRole('option', { name: 'Standart Form (Varsayılan)' })).toBeInTheDocument();
  });

  it('KK4_K11_BoundTemplateIsSelected', async () => {
    getExam.mockResolvedValue(exam('t2', 'Örnek Form', 1));
    renderPage();
    const select = await picker();
    await waitFor(() => expect(select.value).toBe('t2'));
  });

  it('KK4_K11_ChangingTemplateBindsExam', async () => {
    bindExamTemplate.mockResolvedValue(exam('t2', 'Örnek Form', 1));
    const user = userEvent.setup();
    renderPage();
    const select = await picker();
    await user.selectOptions(select, 't2');
    await waitFor(() => expect(bindExamTemplate).toHaveBeenCalledWith('e1', 't2'));
  });

  it('KK4_Y6_10_ShowsBoundVersionNotCurrentVersion', async () => {
    getOpticalTemplates.mockResolvedValue([template('t1', 'Standart Form', true, 2), sample]);
    getExam.mockResolvedValue(exam('t1', 'Standart Form', 1));
    renderPage();
    await picker();
    expect(await screen.findByText('Standart Form - Sürüm 1')).toBeInTheDocument();
    expect(screen.getByText("Bu sınav Sürüm 1'e bağlı; güncel sürüm 2.")).toBeInTheDocument();
  });

  it('KK4_K11_FailedBindRevertsSelectionAndShowsError', async () => {
    bindExamTemplate.mockRejectedValue(new Error('Şablon bağlanamadı.'));
    const user = userEvent.setup();
    renderPage();
    const select = await picker();
    expect(select.value).toBe('t1');
    await user.selectOptions(select, 't2');
    expect(await screen.findByText('Şablon bağlanamadı.')).toBeInTheDocument();
    await waitFor(() => expect(select.value).toBe('t1'));
  });

  it('KK4_K11_PickerLockedAfterImport', async () => {
    getImportPreview.mockResolvedValue(savedPreview());
    renderPage();
    const select = await picker();
    await waitFor(() => expect(select).toBeDisabled());
    expect(screen.getByText('Şablonu değiştirmek için önce yüklenen dosyaları kaldırın.')).toBeInTheDocument();
  });

  it('KK4_K11_PickerEnabledBeforeImport', async () => {
    renderPage();
    const select = await picker();
    expect(select).toBeEnabled();
    expect(screen.queryByText('Şablonu değiştirmek için önce yüklenen dosyaları kaldırın.')).not.toBeInTheDocument();
  });

  it('KK4_K11_ShowsLoadingStateWhileTemplatesLoad', async () => {
    getOpticalTemplates.mockReturnValue(new Promise<OpticalTemplate[]>(() => undefined));
    renderPage();
    expect(await screen.findByText('Optik form şablonları yükleniyor...')).toBeInTheDocument();
  });

  it('KK4_K11_ShowsErrorAndRetryWhenTemplatesFail', async () => {
    getOpticalTemplates.mockRejectedValueOnce(new Error('ağ'));
    const user = userEvent.setup();
    renderPage();
    expect(await screen.findByText('Optik form şablonları yüklenemedi.')).toBeInTheDocument();
    getOpticalTemplates.mockResolvedValue([standard, sample]);
    await user.click(screen.getByRole('button', { name: 'Tekrar Dene' }));
    expect((await picker()).value).toBe('t1');
  });

  it('KK4_K11_ShowsEmptyStateWhenNoTemplates', async () => {
    getOpticalTemplates.mockResolvedValue([]);
    renderPage();
    expect(await screen.findByText('Tanımlı optik form şablonu yok')).toBeInTheDocument();
  });
});
