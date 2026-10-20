/* Frontend sayfalarında kullanılan ortak domain modellerini tanımlar. */
export type ExamType = 'Vize' | 'Final' | 'Bütünleme';

export interface Course {
  id: string;
  code: string;
  name: string;
  department: string;
  semester: string;
  studentCount: number;
}

export interface ExamSummary {
  type: ExamType;
  date: string;
  status: 'Hazır' | 'Bekliyor';
  scoreCount: number;
  accent: string;
}

export interface BreadcrumbItem { label: string; href?: string; }
