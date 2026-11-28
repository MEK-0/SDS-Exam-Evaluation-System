/* Ders sayfasında Dönem Değerlendirme kartının termEvaluationAvailable alanına bağlı açılmasını doğrular. */
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import * as courseApi from '../../services/courseApi';
import * as examApi from '../../services/examApi';
import type { Course, Exam } from '../../types/api';
import { CoursePage } from './CoursePage';

vi.mock('../../services/courseApi', () => ({ getCourse: vi.fn() }));
vi.mock('../../services/examApi', () => ({ getExams: vi.fn(), createExam: vi.fn() }));

const getCourse = vi.mocked(courseApi.getCourse);
const getExams = vi.mocked(examApi.getExams);

function course(termEvaluationAvailable: boolean): Course {
  return {
    id: 'c1',
    code: 'BM-201',
    name: 'Ayrık Matematik',
    academicYearId: 'y1',
    academicYear: '2025-2026',
    instructorCount: 1,
    examCount: 2,
    termEvaluationAvailable,
  };
}

function exam(id: string, examType: Exam['examType'], status: Exam['status']): Exam {
  return {
    id,
    courseId: 'c1',
    examType,
    evaluationType: 'MultipleChoice',
    status,
    createdAt: '2026-01-01T00:00:00Z',
    opticalTemplateId: 't1',
    opticalTemplateName: 'Şablon',
    opticalTemplateVersionId: 'v1',
    opticalTemplateVersionNo: 1,
  };
}

function renderPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={['/course/c1']}>
        <Routes>
          <Route path="/course/:courseId" element={<CoursePage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('CoursePage term evaluation', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    getExams.mockResolvedValue([exam('e1', 'Midterm', 'Completed'), exam('e2', 'Final', 'Draft')]);
  });

  it('R18_1_TermEvaluationEnabledWhenAvailable', async () => {
    getCourse.mockResolvedValue(course(true));
    renderPage();
    const button = (await screen.findByText('Dönem Değerlendirme')).closest('button');
    expect(button).not.toBeDisabled();
  });

  it('R7_3_TermEvaluationDisabledWhenNotAvailable', async () => {
    getCourse.mockResolvedValue(course(false));
    renderPage();
    const button = (await screen.findByText('Dönem Değerlendirme')).closest('button');
    expect(button).toBeDisabled();
    expect(screen.getByText('Vize ile final ya da bütünleme tamamlandığında açılır.')).toBeInTheDocument();
  });
});
