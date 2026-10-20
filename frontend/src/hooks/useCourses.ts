/* Ders ve akademik yıl sorgularını TanStack Query ile sunar. */
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { createCourse, getAcademicYears, getCourse, getCourses } from '../services/courseApi';

export function useCourses() { return useQuery({ queryKey: ['courses'], queryFn: getCourses, retry: false }); }
export function useCourse(courseId: string) { return useQuery({ queryKey: ['courses', courseId], queryFn: () => getCourse(courseId), enabled: Boolean(courseId), retry: false }); }
export function useAcademicYears() { return useQuery({ queryKey: ['academic-years'], queryFn: getAcademicYears, retry: false }); }
export function useCreateCourse() { const queryClient = useQueryClient(); return useMutation({ mutationFn: createCourse, onSuccess: () => queryClient.invalidateQueries({ queryKey: ['courses'] }) }); }
