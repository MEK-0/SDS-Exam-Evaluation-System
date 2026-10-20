/* Henüz uygulanmayan sınav wizard adımlarının durumunu gösterir. */
import { Card } from '../../components/ui/Card';
import { StepIndicator } from '../../components/ui/StepIndicator';
import { wizardSteps } from './wizardSteps';

export function WizardPlaceholderPage() { return <div className="grid gap-7"><StepIndicator steps={wizardSteps} currentStep={3} /><Card className="p-6"><h1 className="text-xl font-semibold text-ink">Veri Eşleştirme</h1><p className="mt-3 text-sm text-slate-500">Bu adım Phase 3C kapsamında geliştirilecektir.</p></Card></div>; }
