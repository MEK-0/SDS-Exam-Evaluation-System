/* AnswerKeyPage için kitapçık sıra numaralandırması ve düzenlenebilir maksimum puan davranışlarını doğrular. */
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import * as examApi from '../../services/examApi';
import type { AnswerKey, AnswerKeyBooklet } from '../../types/api';
import { AnswerKeyPage } from './AnswerKeyPage';

vi.mock('../../services/examApi', () => ({
  getAnswerKey: vi.fn(),
  getOpticalRecords: vi.fn(),
  updateAnswerKey: vi.fn(),
}));

const getAnswerKey = vi.mocked(examApi.getAnswerKey);
const getOpticalRecords = vi.mocked(examApi.getOpticalRecords);
const updateAnswerKey = vi.mocked(examApi.updateAnswerKey);

function booklet(code: string): AnswerKeyBooklet {
  return {
    id: `b-${code}`,
    bookletCode: code,
    questionCount: 4,
    questions: [1, 2, 3, 4].map(number => ({
      id: `${code}${number}`,
      examQuestionId: `q${number}`,
      examQuestionNumber: number,
      displayQuestionNumber: number,
      acceptedAnswers: ['A'],
      points: 25,
      status: 'Active',
      cancellationPolicy: 'AwardFullPoints',
    })),
  };
}

const answerKey: AnswerKey = {
  id: 'k1',
  examId: 'e1',
  maximumScore: 100,
  questionCount: 4,
  dynamicScoring: true,
  allowMultipleAnswers: false,
  cancelledQuestionPolicy: 'ExcludeFromTotal',
  status: 'Draft',
  updatedAt: '2026-01-01T00:00:00Z',
  isComplete: false,
  questions: [],
  booklets: [booklet('A'), booklet('B')],
  validationIssues: [],
  cancellationSuggestions: [],
  pendingCancellationSuggestionCount: 0,
};

function renderPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={['/course/c1/exam/final/answer-key?examId=e1']}>
        <Routes>
          <Route path="/course/:courseId/exam/:examType/answer-key" element={<AnswerKeyPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

beforeEach(() => {
  vi.resetAllMocks();
  getAnswerKey.mockResolvedValue(answerKey);
  getOpticalRecords.mockResolvedValue([]);
  updateAnswerKey.mockResolvedValue(answerKey);
});

describe('AnswerKeyPage', () => {
  it('KK4_H15_BookletBNotReversedByDefault', async () => {
    const user = userEvent.setup();
    renderPage();
    await screen.findByText('Kitapçık B');
    await user.click(screen.getByRole('button', { name: 'Kaydet' }));
    await waitFor(() => expect(updateAnswerKey).toHaveBeenCalled());
    const input = updateAnswerKey.mock.calls[0][1];
    const bookletB = input.booklets.find(item => item.bookletCode === 'B');
    expect(bookletB?.questions.map(item => item.examQuestionNumber)).toEqual([1, 2, 3, 4]);
    expect(bookletB?.questions.every(item => item.examQuestionNumber === item.displayQuestionNumber)).toBe(true);
  });

  it('R12_1_MaximumScoreIsEditableAndSaved', async () => {
    const user = userEvent.setup();
    renderPage();
    const field = await screen.findByLabelText('Maksimum Puan');
    expect(field).not.toHaveAttribute('readonly');
    await user.clear(field);
    await user.type(field, '120');
    await user.click(screen.getByRole('button', { name: 'Kaydet' }));
    await waitFor(() => expect(updateAnswerKey).toHaveBeenCalled());
    const input = updateAnswerKey.mock.calls[0][1];
    expect(input.maximumScore).toBe(120);
    expect(input.questions.every(item => item.points === 30)).toBe(true);
  });

  it('R12_1_NonPositiveMaximumScoreIsRejected', async () => {
    const user = userEvent.setup();
    renderPage();
    const field = await screen.findByLabelText('Maksimum Puan');
    await user.clear(field);
    await user.type(field, '0');
    expect(screen.getByText('Maksimum puan sıfırdan büyük bir sayı olmalıdır.')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Kaydet' }));
    expect(updateAnswerKey).not.toHaveBeenCalled();
  });
});
