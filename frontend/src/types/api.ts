/* Backend API yanıtlarında kullanılan ortak frontend tiplerini tanımlar. */
export interface ApiResponse<T> { success: boolean; data: T | null; message: string | null; errors: string[] | null; }

export interface AuthUser { id: string; username: string; displayName: string; role: string; }
export interface LoginResponse { accessToken: string; expiresAt: string; mustChangePassword: boolean; user: AuthUser; }
export interface Course { id: string; code: string; name: string; academicYearId: string; academicYear: string; instructorCount: number; examCount: number; }
export interface AcademicYear { id: string; name: string; isActive: boolean; }
export type ExamType = 'Midterm' | 'Final' | 'MakeUp';
export type EvaluationType = 'MultipleChoice' | 'OpenEnded';
export interface Exam { id: string; courseId: string; examType: ExamType; evaluationType: EvaluationType; status: 'Draft' | 'Completed'; createdAt: string; }
export interface OpticalFormConfiguration { studentNumberStart: number; studentNumberLength: number; studentNameStart: number | null; studentNameLength: number | null; bookletTypeStart: number; bookletTypeLength: number; examTypeStart: number | null; examTypeLength: number | null; answersStart: number; answersLength: number; encodingName: string; }
export interface OpticalRecordPreview { lineNumber: number; studentNumber: string; studentName: string; bookletType: string; answers: string; isValid: boolean; errorMessage: string | null; isAnswerKey: boolean; }
export interface StudentPreview { rowNumber: number; studentNumber: string; firstName: string; lastName: string; sourceFileName: string; }
export interface ImportIssue { source: string; rowNumber: number | null; message: string; }
export interface ImportPreview { importId: string; opticalFileName: string; classListFileName: string; classListFileNames: string[]; totalOpticalRecordCount: number; validOpticalRecordCount: number; invalidOpticalRecordCount: number; studentCount: number; opticalRecords: OpticalRecordPreview[]; students: StudentPreview[]; issues: ImportIssue[]; createdAt: string; }
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
export interface OpticalRecord { id: string; lineNumber: number; studentNumber: string; studentName: string; bookletType: string; answers: string; isValid: boolean; errorMessage: string | null; isAnswerKey: boolean; }
export type StudentMatchType = 'Exact' | 'Normalized' | 'Manual';
export type StudentMatchStatus = 'Matched' | 'Unmatched' | 'Conflict' | 'Absent';
export interface MatchingSummary { totalClassListCount: number; totalOpticalRecordCount: number; exactMatchCount: number; normalizedMatchCount: number; manualMatchCount: number; unmatchedStudentCount: number; unmatchedOpticalRecordCount: number; conflictCount: number; isCalculated: boolean; isConfirmed: boolean; canConfirm: boolean; }
export interface MatchingRecord { matchId: string | null; importedStudentId: string | null; opticalExamRecordId: string | null; studentNumber: string; studentName: string; opticalStudentNumber: string; opticalStudentName: string; bookletType: string; matchType: StudentMatchType; status: StudentMatchStatus; isAbsent: boolean; issue: string | null; }
export interface MatchingResponse { summary: MatchingSummary; records: MatchingRecord[]; updatedAt: string | null; }
export type GradeStatus = 'Graded' | 'Absent' | 'NotEntered';
export interface GradeRow { resultId: string; importedStudentId: string; studentNumber: string; studentName: string; rawScore: number | null; adjustedScore: number | null; adjustmentAmount: number; projectScore: number | null; status: GradeStatus; updatedAt: string; letterGrade: string | null; }
export interface GradeSummary { totalStudentCount: number; gradedStudentCount: number; absentStudentCount: number; averageScore: number | null; medianScore: number | null; standardDeviation: number | null; highestScore: number | null; lowestScore: number | null; }
export interface GradesResponse { rows: GradeRow[]; summary: GradeSummary; calculatedAt: string | null; }
export interface OpenEndedGradeImportResponse { grades: GradesResponse; importedCount: number; issues: string[]; }
export interface ProjectGradeImportResponse { grades: GradesResponse; importedCount: number; issues: string[]; }
export interface LearningOutcome { id: string; courseId: string; description: string; displayOrder: number; updatedAt: string; }
export interface BookletQuestionMappingItem { referenceQuestionNumber: number; bookletQuestionNumber: number; }
export interface BookletQuestionMapping { referenceBookletCode: string; bookletCode: string; items: BookletQuestionMappingItem[]; }
export interface LearningOutcomeQuestionMapping { learningOutcomeId: string; questionNumbers: number[]; }
export interface LearningOutcomePage { outcomes: LearningOutcome[]; bookletMappings: BookletQuestionMapping[]; questionMappings: LearningOutcomeQuestionMapping[]; questionCount: number; isMultipleChoice: boolean; isComplete: boolean; }
export interface AnalysisSummary { totalStudents: number; participatingStudents: number; average: number | null; median: number | null; standardDeviation: number | null; minimum: number | null; maximum: number | null; successRate: number | null; successThreshold: number | null; successMessage: string | null; }
export interface AnalysisGeneral { courseCode: string; courseName: string; examType: string; examFormat: string; questionCount: number; maximumScore: number; }
export interface AnalysisStudent { studentId: string; studentNumber: string; studentName: string; status: string; score: number | null; }
export interface StudentQuestion { questionNumber: number; studentAnswer: string; acceptedAnswer: string; isCorrect: boolean; isBlank: boolean; }
export interface AnalysisStudentDetail { studentId: string; studentNumber: string; studentName: string; status: string; bookletCode: string | null; rawScore: number | null; adjustedScore: number | null; correctCount: number; wrongCount: number; blankCount: number; questions: StudentQuestion[]; }
export interface ScoreDistribution { label: string; count: number; }
export interface QuestionAnalysis { questionNumber: number; successRate: number; correctCount: number; participatingCount: number; }
export interface Choice { option: string; count: number; percentage: number; isCorrect: boolean; }
export interface ChoiceDistribution { questionNumber: number; bookletCode: string; choices: Choice[]; blankCount: number; participatingCount: number; }
export interface LearningOutcomeAnalysis { id: string; description: string; questionNumbers: number[]; successRate: number | null; }
export interface AnalysisResponse { summary: AnalysisSummary; general: AnalysisGeneral; students: AnalysisStudent[]; selectedStudent: AnalysisStudentDetail | null; scoreDistribution: ScoreDistribution[]; questions: QuestionAnalysis[]; choices: ChoiceDistribution[]; learningOutcomes: LearningOutcomeAnalysis[]; }
export interface OpenEndedSettings { examId: string; questionCount: number; maximumScore: number; updatedAt: string; }
