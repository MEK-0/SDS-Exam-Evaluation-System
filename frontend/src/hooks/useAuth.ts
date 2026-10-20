/* Giriş formu ve frontend oturum akışını yönetir. */
import { useState } from 'react';
import { login } from '../services/authApi';
import { authStore } from '../services/authStore';

export function useAuth() { const [loading, setLoading] = useState(false); const [error, setError] = useState<string | null>(null); const signIn = async (username: string, password: string) => { setLoading(true); setError(null); try { const response = await login(username, password); authStore.setSession(response.accessToken, response.user); return response; } catch (reason: unknown) { const message = reason instanceof Error ? reason.message : 'Giriş sırasında beklenmeyen bir hata oluştu.'; setError(message); return null; } finally { setLoading(false); } }; return { signIn, loading, error }; }
