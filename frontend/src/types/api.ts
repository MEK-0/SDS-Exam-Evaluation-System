/* Backend API yanıtlarında kullanılan ortak frontend tiplerini tanımlar. */
export interface ApiResponse<T> { success: boolean; data: T | null; message: string | null; errors: string[] | null; }

export interface AuthUser { id: string; username: string; displayName: string; role: string; }
export interface LoginResponse { accessToken: string; expiresAt: string; mustChangePassword: boolean; user: AuthUser; }
export interface Course { id: string; code: string; name: string; academicYearId: string; academicYear: string; instructorCount: number; examCount: number; }
export interface AcademicYear { id: string; name: string; isActive: boolean; }
export type ExamType = 'Midterm' | 'Final' | 'MakeUp';
export type EvaluationType = 'MultipleChoice' | 'OpenEnded';
export interface Exam { id: string; courseId: string; examType: ExamType; evaluationType: EvaluationType; status: 'Draft'; createdAt: string; }
export interface OpticalFormConfiguration { studentNumberStart: number; studentNumberLength: number; studentNameStart: number | null; studentNameLength: number | null; bookletTypeStart: number; bookletTypeLength: number; examTypeStart: number | null; examTypeLength: number | null; answersStart: number; answersLength: number; encodingName: string; }
export interface OpticalRecordPreview { lineNumber: number; studentNumber: string; studentName: string; bookletType: string; answers: string; isValid: boolean; errorMessage: string | null; }
export interface StudentPreview { rowNumber: number; studentNumber: string; firstName: string; lastName: string; }
export interface ImportIssue { source: string; rowNumber: number | null; message: string; }
export interface ImportPreview { importId: string; opticalFileName: string; classListFileName: string; totalOpticalRecordCount: number; validOpticalRecordCount: number; invalidOpticalRecordCount: number; studentCount: number; opticalRecords: OpticalRecordPreview[]; students: StudentPreview[]; issues: ImportIssue[]; createdAt: string; }
export type AnswerKeyStatus = 'Draft' | 'Validated';
export type ExamQuestionStatus = 'Active' | 'Cancelled';
export type CancelledQuestionPolicy = 'ExcludeFromTotal' | 'RedistributeToActiveQuestions';
export interface AnswerKeyQuestion { id: string; canonicalQuestionNumber: number; points: number; status: ExamQuestionStatus; }
export interface BookletQuestion { id: string; examQuestionId: string; examQuestionNumber: number; displayQuestionNumber: number; acceptedAnswers: string[]; }
export interface AnswerKeyBooklet { id: string; bookletCode: string; questionCount: number; questions: BookletQuestion[]; }
export interface AnswerKey { id: string; examId: string; maximumScore: number; questionCount: number; dynamicScoring: boolean; allowMultipleAnswers: boolean; cancelledQuestionPolicy: CancelledQuestionPolicy; status: AnswerKeyStatus; updatedAt: string; isComplete: boolean; questions: AnswerKeyQuestion[]; booklets: AnswerKeyBooklet[]; validationIssues: string[]; }
export interface AnswerKeyQuestionInput { canonicalQuestionNumber: number; points: number; status: ExamQuestionStatus; }
export interface BookletQuestionInput { examQuestionNumber: number; displayQuestionNumber: number; acceptedAnswers: string[]; }
export interface AnswerKeyBookletInput { bookletCode: string; questions: BookletQuestionInput[]; }
export interface UpdateAnswerKeyInput { maximumScore: number; questionCount: number; dynamicScoring: boolean; allowMultipleAnswers: boolean; cancelledQuestionPolicy: CancelledQuestionPolicy; questions: AnswerKeyQuestionInput[]; booklets: AnswerKeyBookletInput[]; }
export interface OpticalRecord { id: string; lineNumber: number; studentNumber: string; studentName: string; bookletType: string; answers: string; isValid: boolean; errorMessage: string | null; }
