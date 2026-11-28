/* StudentMatchingPage için öneri onayı, reddi, toplu girmedi onayı ve geçersiz optik satırı davranışlarını doğrular. */
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import * as examApi from '../../services/examApi';
import type { ImportPreview, MatchingRecord, MatchingResponse } from '../../types/api';
import { StudentMatchingPage } from './StudentMatchingPage';

vi.mock('../../services/examApi', () => ({
  getMatching: vi.fn(),
  getImportPreview: vi.fn(),
  runMatching: vi.fn(),
  createManualMatch: vi.fn(),
  markStudentAbsent: vi.fn(),
  ignoreOpticalRecord: vi.fn(),
  removeManualMatch: vi.fn(),
  confirmMatching: vi.fn(),
  acceptSuggestion: vi.fn(),
  acceptAbsentSuggestions: vi.fn(),
}));

const getMatching = vi.mocked(examApi.getMatching);
const getImportPreview = vi.mocked(examApi.getImportPreview);
const acceptSuggestion = vi.mocked(examApi.acceptSuggestion);
const acceptAbsentSuggestions = vi.mocked(examApi.acceptAbsentSuggestions);

const preview: ImportPreview = {
  importId: 'i1',
  opticalFileName: 'optik.txt',
  classListFileName: 'liste.xlsx',
  classListFileNames: ['liste.xlsx'],
  totalOpticalRecordCount: 2,
  validOpticalRecordCount: 2,
  invalidOpticalRecordCount: 0,
  studentCount: 2,
  opticalRecords: [],
  students: [],
  issues: [],
  warnings: [],
  createdAt: '2026-01-01T00:00:00Z',
};

const invalidRecord: MatchingRecord = {
  matchId: null,
  importedStudentId: null,
  opticalExamRecordId: 'o9',
  studentNumber: '',
  studentName: '',
  opticalStudentNumber: 'ABC',
  opticalStudentName: 'Bozuk Satır',
  bookletType: 'A',
  matchType: 'Manual',
  status: 'Conflict',
  isAbsent: false,
  issue: 'Numara geçersiz.',
  isInvalidOptical: true,
};

function response(overrides: Partial<MatchingResponse> = {}): MatchingResponse {
  return {
    summary: {
      totalClassListCount: 2,
      totalOpticalRecordCount: 2,
      exactMatchCount: 0,
      normalizedMatchCount: 0,
      manualMatchCount: 0,
      unmatchedStudentCount: 2,
      unmatchedOpticalRecordCount: 1,
      conflictCount: 1,
      isCalculated: true,
      isConfirmed: false,
      canConfirm: true,
      suggestedMatchCount: 1,
      absentSuggestionCount: 1,
      invalidOpticalCount: 1,
    },
    records: [invalidRecord],
    updatedAt: null,
    suggestions: [
      {
        kind: 'SimilarNumber',
        importedStudentId: 's1',
        opticalExamRecordId: 'o1',
        studentNumber: '2021001',
        studentName: 'Ali Veli',
        opticalStudentNumber: '2021010',
        opticalStudentName: 'ALI VELI',
        reason: 'Numara bir hane farklı.',
      },
      {
        kind: 'Absent',
        importedStudentId: 's2',
        opticalExamRecordId: null,
        studentNumber: '2021002',
        studentName: 'Ayşe Kaya',
        opticalStudentNumber: '',
        opticalStudentName: '',
        reason: 'Optik kaydı yok.',
      },
    ],
    ...overrides,
  };
}

function renderPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={['/course/c1/exam/final/matching?examId=e1']}>
        <Routes>
          <Route path="/course/:courseId/exam/:examType/matching" element={<StudentMatchingPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

beforeEach(() => {
  vi.resetAllMocks();
  getImportPreview.mockResolvedValue(preview);
  getMatching.mockResolvedValue(response());
});

describe('StudentMatchingPage öneriler', () => {
  it('KK4_K4_SuggestionCanBeAccepted', async () => {
    acceptSuggestion.mockResolvedValue(response({ suggestions: [] }));
    const user = userEvent.setup();
    renderPage();
    expect(await screen.findByText('Numara bir hane farklı.')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Onayla' }));
    await waitFor(() => expect(acceptSuggestion).toHaveBeenCalledWith('e1', 's1', 'o1'));
  });

  it('KK4_K4_RejectOnlyHidesSuggestion', async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByText('Numara bir hane farklı.');
    await user.click(screen.getByRole('button', { name: 'Reddet' }));
    expect(screen.queryByText('Numara bir hane farklı.')).not.toBeInTheDocument();
    expect(acceptSuggestion).not.toHaveBeenCalled();
  });

  it('KK4_K4_AcceptAllAbsentCallsBulkEndpoint', async () => {
    acceptAbsentSuggestions.mockResolvedValue(response({ suggestions: [] }));
    const user = userEvent.setup();
    renderPage();
    await user.click(await screen.findByRole('button', { name: 'Tümünü Onayla' }));
    await waitFor(() => expect(acceptAbsentSuggestions).toHaveBeenCalledWith('e1'));
  });

  it('KK4_K4_InvalidOpticalRowVisibleAndDoesNotBlockConfirm', async () => {
    renderPage();
    expect(await screen.findByText(/onay adımını engellemez/)).toBeInTheDocument();
    expect(screen.getAllByText(/Bozuk Satır/).length).toBeGreaterThan(0);
    expect(screen.getByRole('button', { name: 'İleri' })).toBeEnabled();
  });

  it('KK4_K4_ConfirmDisabledWhenCanConfirmFalse', async () => {
    const base = response();
    getMatching.mockResolvedValue({ ...base, summary: { ...base.summary, canConfirm: false } });
    renderPage();
    await screen.findByText(/onay adımını engellemez/);
    expect(screen.getByRole('button', { name: 'İleri' })).toBeDisabled();
  });
});
