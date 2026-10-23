// Yönetici paneli için kullanıcı, politika ve optik ayar API çağrılarını sunar.
import { apiClient } from './apiClient';
import type { ApiResponse, CreateManagedUserResponse, GradingPolicy, ManagedCourse, ManagedUser, OpticalSystemSettings, PasswordResetApprovalResponse, PasswordResetRequest, UserRole } from '../types/api';

async function getData<T>(request: Promise<{ data: ApiResponse<T> }>): Promise<T> { const response = await request; if (!response.data.data) throw new Error(response.data.message ?? 'İşlem tamamlanamadı.'); return response.data.data; }
export const getManagedUsers = (search = '') => getData<ManagedUser[]>(apiClient.get('/admin/users', { params: { search } }));
export const createManagedUser = (input: { firstName: string; lastName: string; username: string; personnelNumber?: string; role: UserRole; isActive: boolean }) => getData<CreateManagedUserResponse>(apiClient.post('/admin/users', input));
export const updateManagedUser = (id: string, input: { firstName: string; lastName: string; username: string; personnelNumber?: string; role: UserRole; isActive: boolean }) => getData<ManagedUser>(apiClient.put(`/admin/users/${id}`, input));
export const getUserCourses = (id: string) => getData<ManagedCourse[]>(apiClient.get(`/admin/users/${id}/courses`));
export const assignCourse = (userId: string, courseId: string) => getData<object>(apiClient.post(`/admin/users/${userId}/courses/${courseId}`));
export const removeCourse = (userId: string, courseId: string) => getData<object>(apiClient.delete(`/admin/users/${userId}/courses/${courseId}`));
export const getResetRequests = (status: string) => getData<PasswordResetRequest[]>(apiClient.get('/admin/password-reset-requests', { params: { status } }));
export const approveResetRequest = (id: string) => getData<PasswordResetApprovalResponse>(apiClient.post(`/admin/password-reset-requests/${id}/approve`));
export const rejectResetRequest = (id: string) => getData<PasswordResetRequest>(apiClient.post(`/admin/password-reset-requests/${id}/reject`));
export const getGradingPolicy = () => getData<GradingPolicy>(apiClient.get('/admin/grading-policy'));
export const updateGradingPolicy = (input: Omit<GradingPolicy, 'id' | 'updatedAt'>) => getData<GradingPolicy>(apiClient.put('/admin/grading-policy', input));
export const getOpticalSettings = () => getData<OpticalSystemSettings>(apiClient.get('/admin/optical-form-settings'));
export const updateOpticalSettings = (input: Omit<OpticalSystemSettings, 'id' | 'updatedAt'>) => getData<OpticalSystemSettings>(apiClient.put('/admin/optical-form-settings', input));
