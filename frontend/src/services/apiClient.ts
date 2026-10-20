/* SDS backend çağrıları için Axios istemcisini yapılandırır. */
import axios from 'axios';
import { authStore } from './authStore';

export const apiClient = axios.create({ baseURL: import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5000/api', headers: { 'Content-Type': 'application/json' } });
apiClient.interceptors.request.use((config) => { const token = authStore.getToken(); if (token) config.headers.Authorization = `Bearer ${token}`; return config; });
apiClient.interceptors.response.use((response) => response, (error: unknown) => { if (axios.isAxiosError(error) && error.response?.status === 401) authStore.clear(); return Promise.reject(error); });
