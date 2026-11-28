/* Optik form şablonlarını listeleme ve sınava bağlama API çağrılarını sunar. */
import { apiClient } from './apiClient';
import type { ApiResponse, Exam, OpticalTemplate } from '../types/api';

export async function getOpticalTemplates(): Promise<OpticalTemplate[]> {
  const response = await apiClient.get<ApiResponse<OpticalTemplate[]>>('/optical-templates');
  if (!response.data.data) throw new Error(response.data.message ?? 'Optik form şablonları alınamadı.');
  return response.data.data;
}

export async function bindExamTemplate(examId: string, templateId: string): Promise<Exam> {
  const response = await apiClient.put<ApiResponse<Exam>>(`/exams/${examId}/optical-template`, { templateId });
  if (!response.data.data) throw new Error(response.data.message ?? 'Optik form şablonu sınava bağlanamadı.');
  return response.data.data;
}
