/* Kısa ömürlü access token ve kullanıcı oturumunu bellekte tutar. */
import type { AuthUser } from '../types/api';

let accessToken: string | null = null;
let currentUser: AuthUser | null = null;

export const authStore = {
  getToken: () => accessToken,
  getUser: () => currentUser,
  setSession: (token: string, user: AuthUser) => { accessToken = token; currentUser = user; },
  setUser: (user: AuthUser) => { currentUser = user; },
  clear: () => { accessToken = null; currentUser = null; },
};
