/* Optik form şablonlarını ve sınava bağlama işlemini TanStack Query ile sunar. */
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { bindExamTemplate, getOpticalTemplates } from '../services/opticalTemplateApi';

export function useOpticalTemplates() {
  return useQuery({ queryKey: ['optical-templates'], queryFn: getOpticalTemplates, retry: false });
}

export function useBindExamTemplate(examId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (templateId: string) => bindExamTemplate(examId, templateId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['exams', examId] }),
  });
}
