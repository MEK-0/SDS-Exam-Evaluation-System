/* Sınav wizard’ının dördüncü adımı için henüz işlevsel olmayan ekran iskeletini sunar. */
import { Card } from '../../components/ui/Card';
import { StepIndicator } from '../../components/ui/StepIndicator';
import { wizardSteps } from './wizardSteps';

export function GradePlaceholderPage() { return <div className="grid gap-7"><StepIndicator steps={wizardSteps} currentStep={4} /><Card className="p-6"><h1 className="text-xl font-semibold text-ink">Not Düzenleme</h1><p className="mt-3 text-sm text-slate-500">Bu adım sonraki phase kapsamında geliştirilecektir.</p></Card></div>; }
