/* GradeEditingPage için ham, 100'lük ve nihai not sütunlarını ile bekleyen iptal önerisi uyarısını doğrular. */
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, within } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import * as examApi from '../../services/examApi';
import type { GradeRow, GradesResponse } from '../../types/api';
import { GradeEditingPage } from './GradeEditingPage';

vi.mock('../../services/examApi', () => ({
  getGrades: vi.fn(),
  calculateGrades: vi.fn(),
  updateGrade: vi.fn(),
  addBonus: vi.fn(),
  importProjectGrades: vi.fn(),
  saveOpenEndedGrade: vi.fn(),
}));

const getGrades = vi.mocked(examApi.getGrades);

function row(overrides: Partial<GradeRow>): GradeRow {
  return {
    resultId: 'r1',
    importedStudentId: 's1',
    studentNumber: '2001',
    studentName: 'Ali Veli',
    rawScore: 120,
    adjustedScore: 100,
    adjustmentAmount: 0,
    projectScore: null,
    status: 'Graded',
    updatedAt: '2026-01-01T00:00:00Z',
    letterGrade: 'AA',
    scaledScore: 100,
    isManuallyAdjusted: false,
    ...overrides,
  };
}

function response(pending: number, rows: GradeRow[]): GradesResponse {
  return {
    rows,
    summary: { totalStudentCount: rows.length, gradedStudentCount: rows.length, absentStudentCount: 0, averageScore: 90, medianScore: 90, standardDeviation: 1, highestScore: 100, lowestScore: 80 },
    calculatedAt: '2026-01-01T00:00:00Z',
    pendingCancellationSuggestionCount: pending,
    maximumScore: 120,
    manuallyAdjustedStudentIds: rows.filter(item => item.isManuallyAdjusted).map(item => item.importedStudentId),
  };
}

function renderPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={['/course/c1/exam/final/grades?examId=e1']}>
        <Routes>
          <Route path="/course/:courseId/exam/:examType/grades" element={<GradeEditingPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

beforeEach(() => {
  vi.resetAllMocks();
});

describe('GradeEditingPage', () => {
  it('Y3_16_K7_GradeTableShowsRawScaledAndFinal', async () => {
    getGrades.mockResolvedValue(response(0, [
      row({}),
      row({ importedStudentId: 's2', resultId: 'r2', studentNumber: '2002', studentName: 'Ayşe Kaya', rawScore: 60, scaledScore: 50, adjustedScore: 55, isManuallyAdjusted: true }),
    ]));
    renderPage();
    await screen.findByText('Ali Veli');
    expect(screen.getByRole('columnheader', { name: /Ham Puan/ })).toBeInTheDocument();
    expect(screen.getByRole('columnheader', { name: /100'lük Karşılık/ })).toBeInTheDocument();
    expect(screen.getByRole('columnheader', { name: /Nihai Not/ })).toBeInTheDocument();
    const first = within(screen.getByText('Ali Veli').closest('tr') as HTMLElement);
    expect(first.getByText('120')).toBeInTheDocument();
    expect(first.getByText('100')).toBeInTheDocument();
    expect(first.queryByText('Elle')).not.toBeInTheDocument();
    const second = within(screen.getByText('Ayşe Kaya').closest('tr') as HTMLElement);
    expect(second.getByText('60')).toBeInTheDocument();
    expect(second.getByText('50')).toBeInTheDocument();
    expect(second.getByText('Elle')).toBeInTheDocument();
  });

  it('K26_PendingSuggestionWarningShown', async () => {
    getGrades.mockResolvedValue(response(3, [row({})]));
    renderPage();
    expect(await screen.findByText('3 bekleyen soru iptal önerisi var; sınav tamamlanmadan önce karara bağlanmalı.')).toBeInTheDocument();
  });

  it('K26_NoWarningWhenNoPendingSuggestion', async () => {
    getGrades.mockResolvedValue(response(0, [row({})]));
    renderPage();
    await screen.findByText('Ali Veli');
    expect(screen.queryByText(/bekleyen soru iptal önerisi/)).not.toBeInTheDocument();
  });
});
