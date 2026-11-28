/* Öğrenme çıktıları adımında ters sıra kısayolunu ve sınav tamamlama çakışma mesajını doğrular. */
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import * as examApi from '../../services/examApi';
import type { LearningOutcomePage as OutcomePage } from '../../types/api';
import { LearningOutcomePlaceholderPage } from './LearningOutcomePlaceholderPage';

vi.mock('../../services/examApi', () => ({
  getLearningOutcomePage: vi.fn(),
  createLearningOutcome: vi.fn(),
  saveBookletMappings: vi.fn(),
  saveOutcomeMappings: vi.fn(),
  completeExam: vi.fn(),
  applyReverseBookletMapping: vi.fn(),
}));

const getPage = vi.mocked(examApi.getLearningOutcomePage);
const reverse = vi.mocked(examApi.applyReverseBookletMapping);
const complete = vi.mocked(examApi.completeExam);

function page(items: number[]): OutcomePage {
  return {
    outcomes: [{ id: 'o1', courseId: 'c1', description: 'Çıktı 1', displayOrder: 1, updatedAt: '2026-01-01T00:00:00Z' }],
    bookletMappings: [
      {
        referenceBookletCode: 'A',
        bookletCode: 'B',
        items: items.map((value, index) => ({ referenceQuestionNumber: index + 1, bookletQuestionNumber: value })),
      },
    ],
    questionMappings: [{ learningOutcomeId: 'o1', questionNumbers: [1] }],
    questionCount: 3,
    isMultipleChoice: true,
    isComplete: false,
  };
}

function renderPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={['/course/c1/exam/final/learning-outcomes?examId=e1']}>
        <Routes>
          <Route path="/course/:courseId/exam/:examType/learning-outcomes" element={<LearningOutcomePlaceholderPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

beforeEach(() => {
  vi.resetAllMocks();
  getPage.mockResolvedValue(page([1, 2, 3]));
});

describe('LearningOutcomePlaceholderPage', () => {
  it('K23_ReverseShortcutCallsEndpoint', async () => {
    const user = userEvent.setup();
    reverse.mockResolvedValue(page([3, 2, 1]));
    renderPage();
    await user.click(await screen.findByRole('button', { name: 'Ters sıra' }));
    await waitFor(() => expect(reverse).toHaveBeenCalledWith('e1', 'B'));
    await waitFor(() => {
      const selects = screen.getAllByRole('combobox');
      expect(selects[0]).toHaveValue('3');
      expect(selects[2]).toHaveValue('1');
    });
  });

  it('K26_CompleteConflictMessageShown', async () => {
    const user = userEvent.setup();
    const message = 'Bekleyen iptal önerileri var: B kitapçığı 2. soru.';
    complete.mockRejectedValue(new Error(message));
    renderPage();
    await user.click(await screen.findByRole('button', { name: 'Kaydet ve Analizi Gör' }));
    expect(await screen.findByText(message)).toBeInTheDocument();
  });
});
