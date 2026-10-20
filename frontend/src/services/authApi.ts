/* Kimlik doğrulama API çağrılarını sunar. */
import { apiClient } from './apiClient';
import type { ApiResponse, AuthUser, LoginResponse } from '../types/api';

export async function login(username: string, password: string): Promise<LoginResponse> { const response = await apiClient.post<ApiResponse<LoginResponse>>('/auth/login', { username, password }); if (!response.data.data) throw new Error(response.data.message ?? 'Giriş yapılamadı.'); return response.data.data; }
export async function getCurrentUser(): Promise<AuthUser> { const response = await apiClient.get<ApiResponse<AuthUser>>('/auth/me'); if (!response.data.data) throw new Error(response.data.message ?? 'Kullanıcı bilgisi alınamadı.'); return response.data.data; }
export async function changePassword(currentPassword: string, newPassword: string): Promise<void> { const response = await apiClient.post<ApiResponse<object>>('/auth/change-password', { currentPassword, newPassword }); if (!response.data.success) throw new Error(response.data.message ?? 'Şifre değiştirilemedi.'); }
