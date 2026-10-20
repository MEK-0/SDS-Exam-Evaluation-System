/* Uygulama rotalarını ve ortak sayfa düzenini tanımlar. */
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { AppLayout } from '../components/layout/AppLayout';
import { LoginPage } from '../features/auth/LoginPage';
import { ChangePasswordPage } from '../features/auth/ChangePasswordPage';
import { DashboardPage } from '../features/courses/DashboardPage';
import { CoursePage } from '../features/courses/CoursePage';
import { NewExamPage } from '../features/exams/NewExamPage';
import { AnswerKeyPage } from '../features/exams/AnswerKeyPage';
import { WizardPlaceholderPage } from '../features/exams/WizardPlaceholderPage';
import { AnalysisPage } from '../features/analysis/AnalysisPage';
import { AdminPage } from '../features/admin/AdminPage';
import { authStore } from '../services/authStore';

function ProtectedRoute({ children }: { children: JSX.Element }) { return authStore.getToken() ? children : <Navigate to="/login" replace />; }

export function App() { return <BrowserRouter><Routes><Route path="/login" element={<LoginPage />} /><Route path="/change-password" element={<ProtectedRoute><ChangePasswordPage /></ProtectedRoute>} /><Route path="/" element={<Navigate to="/dashboard" replace />} /><Route path="/dashboard" element={<ProtectedRoute><AppLayout><DashboardPage /></AppLayout></ProtectedRoute>} /><Route path="/course/:courseId" element={<ProtectedRoute><AppLayout breadcrumbs={[{ label: 'Ayrık Matematik' }]}><CoursePage /></AppLayout></ProtectedRoute>} /><Route path="/course/:courseId/exam/:examType/new" element={<ProtectedRoute><AppLayout breadcrumbs={[{ label: 'Ayrık Matematik', href: '/course/bm-201' }, { label: 'Vize' }, { label: 'Yeni Sınav Girişi' }]}><NewExamPage /></AppLayout></ProtectedRoute>} /><Route path="/course/:courseId/exam/:examType/answer-key" element={<ProtectedRoute><AppLayout breadcrumbs={[{ label: 'Ayrık Matematik', href: '/course/bm-201' }, { label: 'Vize' }, { label: 'Cevap Anahtarı' }]}><AnswerKeyPage /></AppLayout></ProtectedRoute>} /><Route path="/course/:courseId/exam/:examType/matching" element={<ProtectedRoute><AppLayout breadcrumbs={[{ label: 'Ayrık Matematik' }, { label: 'Veri Eşleştirme' }]}><WizardPlaceholderPage /></AppLayout></ProtectedRoute>} /><Route path="/course/:courseId/exam/:examType/analysis/:examId" element={<ProtectedRoute><AppLayout breadcrumbs={[{ label: 'Ayrık Matematik', href: '/course/bm-201' }, { label: 'Analiz Raporu' }]}><AnalysisPage /></AppLayout></ProtectedRoute>} /><Route path="/admin" element={<ProtectedRoute><AppLayout><AdminPage /></AppLayout></ProtectedRoute>} /><Route path="*" element={<Navigate to="/dashboard" replace />} /></Routes></BrowserRouter>; }
