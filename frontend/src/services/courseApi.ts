/* Ders ve akademik yıl API çağrılarını sunar. */
import { apiClient } from './apiClient';
import type { AcademicYear, ApiResponse, Course } from '../types/api';

export async function getCourses(): Promise<Course[]> { const response = await apiClient.get<ApiResponse<Course[]>>('/courses'); return response.data.data ?? []; }
export async function getAcademicYears(): Promise<AcademicYear[]> { const response = await apiClient.get<ApiResponse<AcademicYear[]>>('/academic-years'); return response.data.data ?? []; }
export async function getCourse(courseId: string): Promise<Course> { const response = await apiClient.get<ApiResponse<Course>>(`/courses/${courseId}`); if (!response.data.data) throw new Error(response.data.message ?? 'Ders bulunamadı.'); return response.data.data; }
export async function createCourse(input: { code: string; name: string; academicYearId: string }): Promise<Course> { const response = await apiClient.post<ApiResponse<Course>>('/courses', input); if (!response.data.data) throw new Error(response.data.message ?? 'Ders oluşturulamadı.'); return response.data.data; }
