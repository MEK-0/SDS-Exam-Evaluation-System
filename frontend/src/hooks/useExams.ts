/* Sınav taslaklarını TanStack Query ile sunar. */
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { createExam, getAnswerKey, getExams, getImportPreview, getOpticalRecords, updateAnswerKey, uploadExamFiles } from '../services/examApi';
import type { EvaluationType, ExamType, OpticalFormConfiguration, UpdateAnswerKeyInput } from '../types/api';

export function useExams(courseId: string) { return useQuery({ queryKey: ['courses', courseId, 'exams'], queryFn: () => getExams(courseId), enabled: Boolean(courseId), retry: false }); }
export function useCreateExam(courseId: string) { const queryClient = useQueryClient(); return useMutation({ mutationFn: (input: { examType: ExamType; evaluationType: EvaluationType }) => createExam(courseId, input), onSuccess: () => queryClient.invalidateQueries({ queryKey: ['courses', courseId, 'exams'] }) }); }
export function useImportPreview(examId: string) { return useQuery({ queryKey: ['exams', examId, 'import-preview'], queryFn: () => getImportPreview(examId), enabled: Boolean(examId), retry: false }); }
export function useUploadExamFiles(examId: string) { const queryClient = useQueryClient(); return useMutation({ mutationFn: (input: { opticalFile: File; classListFile: File; configuration: OpticalFormConfiguration }) => uploadExamFiles(examId, input.opticalFile, input.classListFile, input.configuration), onSuccess: () => queryClient.invalidateQueries({ queryKey: ['exams', examId, 'import-preview'] }) }); }
export function useAnswerKey(examId: string) { return useQuery({ queryKey: ['exams', examId, 'answer-key'], queryFn: () => getAnswerKey(examId), enabled: Boolean(examId), retry: false }); }
export function useOpticalRecords(examId: string) { return useQuery({ queryKey: ['exams', examId, 'optical-records'], queryFn: () => getOpticalRecords(examId), enabled: Boolean(examId), retry: false }); }
export function useUpdateAnswerKey(examId: string) { const queryClient = useQueryClient(); return useMutation({ mutationFn: (input: UpdateAnswerKeyInput) => updateAnswerKey(examId, input), onSuccess: () => queryClient.invalidateQueries({ queryKey: ['exams', examId] }) }); }
