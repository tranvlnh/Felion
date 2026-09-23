import { ComponentType } from 'discord.js';
import { describe, expect, it } from 'vitest';

import {
  createEvaluationDraft,
  createEvaluationScoresModal,
  createEvaluationSelection,
  deleteEvaluationDraft,
  deleteEvaluationSelection,
  renderEvaluationDraft,
  renderEvaluationSelection,
  setEvaluationSelectionCandidate,
  setEvaluationSelectionPeriod,
} from '#app/features/evaluations/discord/evaluation-ui.js';

const context = {
  kind: 'Peer' as const,
  periodId: 'period-1',
  actorDiscordUserId: 'actor-1',
  evaluatorId: 'candidate-1',
  targetCandidateId: 'candidate-2',
  evaluatorName: 'Evaluator',
  targetName: 'Target',
  criteria: [
    {
      id: 'criterion-1',
      kind: 'Peer' as const,
      key: 'quality',
      name: 'Quality',
      minScore: 1,
      maxScore: 5,
      sortOrder: 0,
      active: true,
    },
    {
      id: 'criterion-2',
      kind: 'Peer' as const,
      key: 'communication',
      name: 'Communication',
      minScore: 1,
      maxScore: 5,
      sortOrder: 1,
      active: true,
    },
  ],
};

describe('evaluation Discord UI', () => {
  it('selects period and candidate before opening the evaluation form', () => {
    const selection = createEvaluationSelection({
      kind: 'Peer',
      actorDiscordUserId: 'actor-1',
      periods: [
        { id: 'period-1', name: 'April' },
        { id: 'period-2', name: 'May' },
      ],
      candidates: [
        { id: 'candidate-2', name: 'Target', studentId: 'S2' },
      ],
    });

    expect(selection.selectedPeriodId).toBeNull();
    expect(renderEvaluationSelection(selection).components).toHaveLength(3);

    setEvaluationSelectionPeriod(selection, 'period-1');
    setEvaluationSelectionCandidate(selection, 'candidate-2');

    const rendered = renderEvaluationSelection(selection);
    const button = rendered.components[2]?.components[0]?.data;

    expect(button?.type).toBe(ComponentType.Button);

    if (button?.type !== ComponentType.Button) {
      throw new Error('Expected evaluation action to be a button');
    }

    expect(button.disabled).toBe(false);

    deleteEvaluationSelection(selection.id);
  });

  it('renders score entry as a modal instead of score select menus', () => {
    const draft = createEvaluationDraft(context);
    const form = renderEvaluationDraft(draft);
    const modal = createEvaluationScoresModal(draft).toJSON();

    expect(form.components).toHaveLength(1);
    expect(form.content).toContain('Use the modal');
    expect(modal.custom_id).toBe(`evaluation:scores:${draft.id}`);
    expect(modal.components).toHaveLength(2);

    deleteEvaluationDraft(draft.id);
  });
});
